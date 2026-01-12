using BestelApp.Shared.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Interface for RabbitMQ publishing to Salesforce queue
    /// </summary>
    public interface IRabbitMQPublisher
    {
        Task<string> PublishToSalesforceAsync(Bestelling order);
    }

    public class RabbitMQPublisher : IRabbitMQPublisher
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQPublisher> _logger;
        private readonly ConnectionFactory _factory;

        public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var rabbitConfig = _configuration.GetSection("RabbitMQ");
            _factory = new ConnectionFactory
            {
                HostName = rabbitConfig["HostName"] ?? "localhost",
                UserName = rabbitConfig["UserName"] ?? "guest",
                Password = rabbitConfig["Password"] ?? "guest"
            };
        }

        public async Task<string> PublishToSalesforceAsync(Bestelling order)
        {
            try
            {
                var queueName = _configuration["RabbitMQ:QueueName"] ?? "salesforce_orders";

                var connection = await _factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();

                // Declare queue (idempotent operation)
                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                // Create message with order data
                var message = new
                {
                    OrderId = order.Id,
                    KlantId = order.KlantId,
                    Datum = order.Datum,
                    Totaal = order.Totaal,
                    Items = order.Items,
                    Timestamp = DateTime.UtcNow
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                var properties = new BasicProperties
                {
                    Persistent = true, // Survive broker restart
                    MessageId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                // Publish to queue
                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                var trackingId = properties.MessageId;
                _logger.LogInformation($"Order {order.Id} published to RabbitMQ. Tracking ID: {trackingId}");

                await channel.CloseAsync();
                await connection.CloseAsync();

                return trackingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to publish order {order.Id} to RabbitMQ");
                throw;
            }
        }
    }
}
