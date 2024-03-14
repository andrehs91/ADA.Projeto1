using ADA.Consumer.Services;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using ADA.Consumer.DTO;
using ADA.Consumer.Entities;

namespace ADA.Consumer
{
    public class Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory
    ) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ConnectionFactory factory = new()
            {
                HostName = _configuration["RabbitMQ:HostName"],
                UserName = _configuration["RabbitMQ:UserName"],
                Password = _configuration["RabbitMQ:Password"]
            };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            channel.ExchangeDeclare(exchange: "ada.transacao", type: ExchangeType.Fanout);
            channel.QueueDeclare(queue: "fraude",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);
            channel.QueueBind(queue: "fraude",
                              exchange: "ada.transacao",
                              routingKey: "transacao");
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var transacaoDTO = JsonSerializer.Deserialize<TransacaoDTO>(message);
                if (transacaoDTO is not null)
                {
                    using IServiceScope scope = _serviceScopeFactory.CreateScope();
                    try
                    {
                        _logger.LogInformation("Avaliando a transação da conta {} efetuada em {}.", transacaoDTO.ContaOrigem, transacaoDTO.DataHora.ToString("dd/MM/yyyy HH':'mm':'ss"));
                        var consumerService = scope.ServiceProvider.GetRequiredService<IConsumerService>();
                        Transacao transacao = await consumerService.ProcessarTransacaoAsync(transacaoDTO.MapearParaEntidade());
                        _logger.LogInformation("Possui indício de fraude: {}.\n", transacao.Fraude ? "sim" : "não");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, message);
                    }
                }
            };
            channel.BasicConsume(queue: "fraude",
                                 autoAck: true,
                                 consumer: consumer);

            _logger.LogInformation("Consumer started at: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
            }

            return;
        }
    }
}
