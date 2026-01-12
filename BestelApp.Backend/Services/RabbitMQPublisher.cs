using BestelApp.Shared.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Queue names for different message types
    /// </summary>
    public static class QueueNames
    {
        public const string Orders = "salesforce_orders";           // Nieuwe bestellingen
        public const string OrderUpdates = "order_updates";         // Order status updates
        public const string OrderDeletes = "order_deletes";         // Verwijderde bestellingen
        public const string CustomerSync = "customer_sync";         // Klant synchronisatie
        public const string InventoryUpdates = "inventory_updates"; // Voorraad updates
        public const string Notifications = "notifications";         // Algemene notificaties
        public const string AuditLog = "audit_log";                  // Audit logging
    }

    /// <summary>
    /// Message types for routing
    /// </summary>
    public enum MessageType
    {
        OrderCreated,
        OrderUpdated,
        OrderDeleted,
        OrderShipped,
        OrderDelivered,
        CustomerCreated,
        CustomerUpdated,
        CustomerDeleted,
        InventoryLow,
        InventoryRestocked,
        NotificationSent,
        AuditEntry
    }

    /// <summary>
    /// Interface for RabbitMQ publishing to multiple queues
    /// </summary>
    public interface IRabbitMQPublisher
    {
        Task<string> PublishToSalesforceAsync(Bestelling order);
        Task<string> PublishOrderUpdateAsync(int orderId, string status, string message);
        Task<string> PublishOrderDeleteAsync(int orderId, string reason);
        Task<string> PublishCustomerSyncAsync(Klant customer, MessageType messageType);
        Task<string> PublishInventoryUpdateAsync(int boekId, string titel, int quantity, bool isLowStock);
        Task<string> PublishNotificationAsync(string title, string message, string recipient);
        Task<string> PublishAuditLogAsync(string action, string entity, int entityId, string userId, object details);
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

        #region Order Messages

        /// <summary>
        /// Publish new order to Salesforce queue
        /// </summary>
        public async Task<string> PublishToSalesforceAsync(Bestelling order)
        {
            var message = new
            {
                MessageType = MessageType.OrderCreated.ToString(),
                OrderId = order.Id,
                KlantId = order.KlantId,
                KlantNaam = order.KlantNaam,
                Datum = order.Datum,
                Totaal = order.Totaal,
                Items = order.Items.Select(item => new
                {
                    BoekId = item.BoekId,
                    BoekTitel = item.BoekTitel,
                    Aantal = item.Aantal,
                    Prijs = item.Prijs
                }),
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.Orders, message, $"Order {order.Id} created");
        }

        /// <summary>
        /// Publish order status update
        /// </summary>
        public async Task<string> PublishOrderUpdateAsync(int orderId, string status, string message)
        {
            var updateMessage = new
            {
                MessageType = MessageType.OrderUpdated.ToString(),
                OrderId = orderId,
                Status = status,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.OrderUpdates, updateMessage, $"Order {orderId} updated to {status}");
        }

        /// <summary>
        /// Publish order deletion
        /// </summary>
        public async Task<string> PublishOrderDeleteAsync(int orderId, string reason)
        {
            var deleteMessage = new
            {
                MessageType = MessageType.OrderDeleted.ToString(),
                OrderId = orderId,
                Reason = reason,
                DeletedAt = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.OrderDeletes, deleteMessage, $"Order {orderId} deleted");
        }

        #endregion

        #region Customer Messages

        /// <summary>
        /// Publish customer synchronization message
        /// </summary>
        public async Task<string> PublishCustomerSyncAsync(Klant customer, MessageType messageType)
        {
            var customerMessage = new
            {
                MessageType = messageType.ToString(),
                CustomerId = customer.Id,
                Naam = customer.Naam,
                Email = customer.Email,
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.CustomerSync, customerMessage, $"Customer {customer.Id} {messageType}");
        }

        #endregion

        #region Inventory Messages

        /// <summary>
        /// Publish inventory update message
        /// </summary>
        public async Task<string> PublishInventoryUpdateAsync(int boekId, string titel, int quantity, bool isLowStock)
        {
            var inventoryMessage = new
            {
                MessageType = isLowStock ? MessageType.InventoryLow.ToString() : MessageType.InventoryRestocked.ToString(),
                BoekId = boekId,
                Titel = titel,
                Quantity = quantity,
                IsLowStock = isLowStock,
                AlertLevel = isLowStock ? "Warning" : "Info",
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.InventoryUpdates, inventoryMessage, $"Inventory update for book {boekId}");
        }

        #endregion

        #region Notification Messages

        /// <summary>
        /// Publish general notification
        /// </summary>
        public async Task<string> PublishNotificationAsync(string title, string message, string recipient)
        {
            var notificationMessage = new
            {
                MessageType = MessageType.NotificationSent.ToString(),
                Title = title,
                Message = message,
                Recipient = recipient,
                SentAt = DateTime.UtcNow,
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.Notifications, notificationMessage, $"Notification sent to {recipient}");
        }

        #endregion

        #region Audit Messages

        /// <summary>
        /// Publish audit log entry
        /// </summary>
        public async Task<string> PublishAuditLogAsync(string action, string entity, int entityId, string userId, object details)
        {
            var auditMessage = new
            {
                MessageType = MessageType.AuditEntry.ToString(),
                Action = action,
                Entity = entity,
                EntityId = entityId,
                UserId = userId,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            return await PublishMessageAsync(QueueNames.AuditLog, auditMessage, $"Audit: {action} on {entity} {entityId}");
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Generic method to publish messages to any queue
        /// </summary>
        private async Task<string> PublishMessageAsync(string queueName, object message, string logDescription)
        {
            try
            {
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

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                }));

                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    ContentType = "application/json"
                };

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: queueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                var trackingId = properties.MessageId;
                _logger.LogInformation($"{logDescription}. Queue: {queueName}, Tracking ID: {trackingId}");

                await channel.CloseAsync();
                await connection.CloseAsync();

                return trackingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to publish message to {queueName}: {logDescription}");
                throw;
            }
        }

        #endregion
    }
}
