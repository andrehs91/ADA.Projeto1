using ADA.Producer.DTO;
using Azure.Storage.Blobs;
using Azure;
using RabbitMQ.Client;
using System.Text.Json;
using StackExchange.Redis;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace ADA.Producer.Services;

public class TransacaoService : ITransacaoService
{
    private readonly ILogger<TransacaoService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDatabase _db;

    public TransacaoService(ILogger<TransacaoService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        string connectionString = _configuration["ConnectionStrings:Redis"]
            ?? throw new Exception("Redis não foi configurado.");
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(connectionString);
        _db = redis.GetDatabase();
    }

    public void EnviarTransacao(TransacaoDTO transacaoDTO)
    {
        ConnectionFactory factory = new()
        {
            HostName = _configuration["RabbitMQ:HostName"],
            UserName = _configuration["RabbitMQ:UserName"],
            Password = _configuration["RabbitMQ:Password"]
        };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var basicProperties = channel.CreateBasicProperties();
        basicProperties.Persistent = true;

        channel.BasicPublish(exchange: "ada.transacao",
                             routingKey: "transacao",
                             basicProperties: basicProperties,
                             body: JsonSerializer.SerializeToUtf8Bytes(transacaoDTO));
    }

    public async Task<string> ConsultarRelatorioAsync(string contaOrigem)
    {
        string chaveTransacaoInvalida = "invalida." + contaOrigem;
        var transacoesInvalidas = await _db.ListRangeAsync(chaveTransacaoInvalida);

        if (transacoesInvalidas.Length == 0)
            return "Conta não possui registro de transações fraudulentas ou todos os registros já foram enviados para o relatório.";

        string content = "[";
        foreach (var transacao in transacoesInvalidas) content += transacao + ",";
        content = content.Remove(content.Length - 1, 1) + "]";
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream);
        streamWriter.Write(content);
        streamWriter.Flush();
        memoryStream.Position = 0;

        BlobContainerClient blobContainerClient = await BlobContainerAsync("ada");
        string blobName = $"{contaOrigem}_{DateTime.Now:yyyyMMddHHmmss}.txt";
        BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(memoryStream, true);

        streamWriter.Dispose();
        memoryStream.Dispose();

        await _db.KeyDeleteAsync(chaveTransacaoInvalida);

        if (blobClient.CanGenerateSasUri)
        {
            BlobSasBuilder sasBuilder = new()
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
            };
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            Uri sasURI = blobClient.GenerateSasUri(sasBuilder);
            string link = sasURI.AbsoluteUri;
            _db.SetAdd("relatorios." + contaOrigem, link);
            return link;
        }
        else
        {
            return "O relatório foi gerado, mas não foi possível gerar um link para download.";
        }
    }

    public async Task<List<string>?> ListarRelatoriosAsync(string contaOrigem)
    {
        var cache = await _db.SetMembersAsync("relatorios." + contaOrigem);
        if (cache.Length == 0) return null;
        List<string> links = cache.Select(c => c.ToString()).ToList();
        return links;
    }

    private async Task<BlobContainerClient> BlobContainerAsync(string containerName)
    {
        string connectionString = _configuration["ConnectionStrings:AzureStorageAccount"]
            ?? throw new Exception("Azure Storage não foi configurado.");
        BlobContainerClient blobContainerClient = new(connectionString, containerName);
        if (!await blobContainerClient.ExistsAsync())
        {
            try
            {
                BlobServiceClient blobServiceClient = new(connectionString);
                blobContainerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);
            }
            catch (RequestFailedException e)
            {
                _logger.LogError(e, "Erro na criação do container.");
            }
        }
        return blobContainerClient;
    }
}
