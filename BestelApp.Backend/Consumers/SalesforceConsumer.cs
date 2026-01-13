using BestelApp.Backend.Services;
using BestelApp.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BestelApp.Backend.Consumers
{
    public class SalesforceConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SalesforceConsumer> _logger;
        private IConnection _connection;
        private IModel _channel;
        private const string QueueName = "bestellingen.salesforce";

        public SalesforceConsumer(IServiceProvider serviceProvider, ILogger<SalesforceConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare queue voor Salesforce
            _channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("SalesforceConsumer geïnitialiseerd voor queue: " + QueueName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogInformation($"Bericht ontvangen voor Salesforce: {message}");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var salesforceService = scope.ServiceProvider.GetRequiredService<ISalesforceService>();
                        var bestelling = JsonConvert.DeserializeObject<Bestelling>(message);

                        if (bestelling != null)
                        {
                            var success = await salesforceService.SendOrderToSalesforceAsync(bestelling);

                            if (success)
                            {
                                _channel.BasicAck(ea.DeliveryTag, false);
                                _logger.LogInformation($"Bestelling {bestelling.Bestelnummer} naar Salesforce gestuurd");

                                // Stuur feedback naar klant queue
                                SendFeedbackToCustomer(bestelling.Bestelnummer, "Salesforce", "success");
                            }
                            else
                            {
                                _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Fout bij verwerken Salesforce bericht: {ex.Message}");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            await Task.CompletedTask;
        }

        private void SendFeedbackToCustomer(string bestelnummer, string system, string status)
        {
            var feedbackMessage = new
            {
                Bestelnummer = bestelnummer,
                System = system,
                Status = status,
                Timestamp = DateTime.UtcNow,
                Message = $"Bestelling {bestelnummer} verwerkt in {system}"
            };

            var feedbackQueue = "bestellingen.feedback";
            _channel.QueueDeclare(queue: feedbackQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

            var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(feedbackMessage));
            _channel.BasicPublish(exchange: "", routingKey: feedbackQueue, basicProperties: null, body: body);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}