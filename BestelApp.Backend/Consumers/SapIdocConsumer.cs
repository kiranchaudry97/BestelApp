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
using System.Xml.Linq;

namespace BestelApp.Backend.Consumers
{
    public class SapIdocConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SapIdocConsumer> _logger;
        private IConnection _connection;
        private IModel _channel;
        private const string QueueName = "bestellingen.sapidoc";

        public SapIdocConsumer(IServiceProvider serviceProvider, ILogger<SapIdocConsumer> logger)
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

            _channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("SapIdocConsumer geïnitialiseerd voor queue: " + QueueName);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                _logger.LogInformation($"Bericht ontvangen voor SAP iDoc: {message}");

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var sapService = scope.ServiceProvider.GetRequiredService<ISapIdocService>();
                        var bestelling = JsonConvert.DeserializeObject<Bestelling>(message);

                        if (bestelling != null)
                        {
                            // 1. Maak iDoc XML
                            XDocument idoc = await sapService.CreateIdocFromOrderAsync(bestelling);
                            _logger.LogInformation($"iDoc XML: {idoc.ToString().Substring(0, Math.Min(200, idoc.ToString().Length))}...");

                            // 2. Stuur naar SAP
                            var success = await sapService.SendIdocToSapAsync(idoc);

                            if (success)
                            {
                                _channel.BasicAck(ea.DeliveryTag, false);
                                _logger.LogInformation($"Bestelling {bestelling.Bestelnummer} als iDoc naar SAP gestuurd");

                                // 3. Stuur feedback naar klant
                                SendFeedbackToCustomer(bestelling.Bestelnummer, "SAP", "success", idoc.ToString());
                            }
                            else
                            {
                                _channel.BasicNack(ea.DeliveryTag, false, true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Fout bij verwerken SAP iDoc: {ex.Message}");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

            await Task.CompletedTask;
        }

        private void SendFeedbackToCustomer(string bestelnummer, string system, string status, string xmlContent = null)
        {
            var feedbackMessage = new
            {
                Bestelnummer = bestelnummer,
                System = system,
                Status = status,
                Timestamp = DateTime.UtcNow,
                Message = $"Bestelling {bestelnummer} verwerkt in {system} als iDoc",
                XmlContent = xmlContent?.Substring(0, Math.Min(500, xmlContent.Length)) // Eerste 500 chars
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