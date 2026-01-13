using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BestelApp.Backend.Consumers
{
    public class SalesforceConsumer : BackgroundService
    {
        private readonly ILogger<SalesforceConsumer> _logger;
        private IConnection _connection;
        private IModel _channel;
        private const string QueueName = "bestellingen.salesforce";

        public SalesforceConsumer(ILogger<SalesforceConsumer> logger)
        {
            _logger = logger;
            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest",
                    Port = 5672
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                _channel.QueueDeclare(QueueName, true, false, false, null);
                _channel.BasicQos(0, 1, false);
                
                _logger.LogInformation($"✅ SalesforceConsumer ready for queue: {QueueName}");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "❌ RabbitMQ initialization failed");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
            {
                _logger.LogError("RabbitMQ channel not initialized");
                return;
            }

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();  // FIXED: .ToArray() voor RabbitMQ 6.x
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogInformation($"📨 Received for Salesforce: {message}");
                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(QueueName, false, consumer);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}
