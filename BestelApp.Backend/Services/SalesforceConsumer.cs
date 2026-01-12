using BestelApp.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Background service that consumes orders from RabbitMQ and creates them in Salesforce
    /// Implements loose coupling: Salesforce and MAUI app don't know each other directly
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
            _logger.LogInformation("Salesforce Consumer started. Waiting for orders...");

            try
            {
                await InitializeRabbitMQAsync();

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        
                        _logger.LogInformation($"Received order from queue: {message}");

                        // Deserialize order
                        var order = JsonSerializer.Deserialize<Bestelling>(message);
                        
                        if (order != null)
                        {
                            // Create order in Salesforce
                            using var scope = _serviceProvider.CreateScope();
                            var salesforceService = scope.ServiceProvider.GetRequiredService<ISalesforceService>();
                            
                            var salesforceId = await salesforceService.CreateOrderAsync(order);
                            
                            if (!string.IsNullOrEmpty(salesforceId))
                            {
                                _logger.LogInformation($"Order {order.Id} created in Salesforce with ID: {salesforceId}");
                                
                                // Acknowledge message
                                await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                            }
                            else
                            {
                                _logger.LogWarning($"Failed to create order {order.Id} in Salesforce. Rejecting message.");
                                // Reject and requeue
                                await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from RabbitMQ");
                        // Reject and requeue on error
                        await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };

                var queueName = _configuration["RabbitMQ:QueueName"] ?? "salesforce_orders";
                await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

                _logger.LogInformation($"Consuming from queue: {queueName}");

                // Keep service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Salesforce Consumer");
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

            var queueName = rabbitConfig["QueueName"] ?? "salesforce_orders";
            
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _logger.LogInformation($"Connected to RabbitMQ. Queue: {queueName}");
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
