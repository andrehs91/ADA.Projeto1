using ADA.Producer.DTO;
using Azure.Storage.Blobs;
using Azure;
using RabbitMQ.Client;
using System.Text.Json;
using StackExchange.Redis;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

namespace ADA.Producer.Services;

public class ProducerService(ILogger<ProducerService> logger, IConfiguration configuration) : IProducerService
{
    private readonly ILogger<ProducerService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

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

    public async Task<string> ConsultarRelatorio(string contaOrigem)
    {
        string chaveTransacaoInvalida = "invalida." + contaOrigem;

        string connectionString = _configuration["ConnectionStrings:Redis"]
            ?? throw new Exception("Redis não foi configurado.");
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(connectionString);
        IDatabase db = redis.GetDatabase();
        var transacoesInvalidas = await db.ListRangeAsync(chaveTransacaoInvalida);

        if (transacoesInvalidas.Length == 0)
        {
            return "Conta não possui registro de transações fraudulentas ou todos os registros já foram enviados para o relatório.";
        }

        //var memoryStream = new MemoryStream();
        //var streamWriter = new StreamWriter(memoryStream);
        //foreach (var transacao in transacoesInvalidas)
        //{
        //    streamWriter.WriteLine(transacao);
        //}
        //memoryStream.Seek(0, SeekOrigin.Begin);

        string content = "";
        foreach (var transacao in transacoesInvalidas)
        {
            content += transacao;
        }
        var memoryStream = new MemoryStream();
        var streamWriter = new StreamWriter(memoryStream);
        streamWriter.Write(content);
        streamWriter.Flush();
        memoryStream.Position = 0;

        BlobContainerClient blobContainerClient = await BlobContainerAsync("ada");
        string blobName = $"{DateTime.Now:yyyyMMddHHmmss}_{contaOrigem}.txt";
        BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(memoryStream, true);

        streamWriter.Dispose();
        memoryStream.Dispose();

        if (blobClient.CanGenerateSasUri)
        {
            BlobSasBuilder sasBuilder = new()
            {
                BlobContainerName = blobClient.GetParentBlobContainerClient().Name,
                BlobName = blobClient.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)
            };
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);
            Uri sasURI = blobClient.GenerateSasUri(sasBuilder);
            return sasURI.AbsoluteUri;
        }
        else
        {
            return "O relatório foi gerado, mas não foi possível gerar um link para download.";
        }
    }

    private async Task<BlobContainerClient> BlobContainerAsync(string containerName)
    {
        string connectionString = _configuration["ConnectionStrings:AzureStorage"]
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
