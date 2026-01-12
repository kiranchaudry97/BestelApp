using BestelApp.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Achtergrond service die bestellingen consumeert van RabbitMQ en aanmaakt in Salesforce
    /// Implementeert loose coupling: Salesforce en MAUI app kennen elkaar niet direct
    /// </summary>
    public class SalesforceConsumer : BackgroundService
    {
        private readonly ILogger<SalesforceConsumer> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private IConnection _connection;
        private IChannel _channel;

        public SalesforceConsumer(
            ILogger<SalesforceConsumer> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Salesforce Consumer gestart. Wachten op bestellingen...");

            try
            {
                await InitializeRabbitMQAsync();

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var bericht = Encoding.UTF8.GetString(body);
                        
                        _logger.LogInformation($"Bestelling ontvangen van queue: {bericht}");

                        // Deserialiseer bestelling
                        var bestelling = JsonSerializer.Deserialize<Bestelling>(bericht, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (bestelling != null)
                        {
                            // Maak bestelling aan in Salesforce
                            using var scope = _serviceProvider.CreateScope();
                            var salesforceService = scope.ServiceProvider.GetRequiredService<ISalesforceService>();
                            
                            var salesforceId = await salesforceService.CreateOrderAsync(bestelling);
                            
                            if (!string.IsNullOrEmpty(salesforceId))
                            {
                                _logger.LogInformation($"Bestelling {bestelling.Id} aangemaakt in Salesforce met ID: {salesforceId}");
                                
                                // Bevestig bericht
                                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                            }
                            else
                            {
                                _logger.LogWarning($"Kon bestelling {bestelling.Id} niet aanmaken in Salesforce. Bericht wordt afgewezen.");
                                // Afwijzen en opnieuw in queue plaatsen
                                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Fout bij verwerken bericht van RabbitMQ");
                        // Afwijzen en opnieuw in queue plaatsen bij fout
                        await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                // Gebruik de Nederlandse queue naam
                await _channel.BasicConsumeAsync(queue: QueueNames.Bestellingen, autoAck: false, consumer: consumer);

                _logger.LogInformation($"Consumeren van queue: {QueueNames.Bestellingen}");

                // Houd service draaiende
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatale fout in Salesforce Consumer");
            }
        }

        private async Task InitializeRabbitMQAsync()
        {
            var rabbitConfig = _configuration.GetSection("RabbitMQ");
            var factory = new ConnectionFactory
            {
                HostName = rabbitConfig["HostName"] ?? "localhost",
                UserName = rabbitConfig["UserName"] ?? "guest",
                Password = rabbitConfig["Password"] ?? "guest"
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            // Declareer de Nederlandse queue naam
            await _channel.QueueDeclareAsync(
                queue: QueueNames.Bestellingen,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _logger.LogInformation($"Verbonden met RabbitMQ. Queue: {QueueNames.Bestellingen}");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null)
                await _channel.CloseAsync();
            if (_connection != null)
                await _connection.CloseAsync();
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
