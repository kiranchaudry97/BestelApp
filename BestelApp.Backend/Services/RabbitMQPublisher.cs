using BestelApp.Shared.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Queue namen voor verschillende berichttypen
    /// </summary>
    public static class QueueNames
    {
        public const string Bestellingen = "bestellingen";              // Nieuwe bestellingen
        public const string BestellingUpdates = "bestelling_updates";   // Bestelling status updates
        public const string BestellingVerwijderd = "bestelling_verwijderd"; // Verwijderde bestellingen
        public const string KlantSynchronisatie = "klant_synchronisatie";   // Klant synchronisatie
        public const string VoorraadUpdates = "voorraad_updates";       // Voorraad updates
        public const string Meldingen = "meldingen";                    // Algemene meldingen
        public const string AuditLog = "audit_log";                     // Audit logging
    }

    /// <summary>
    /// Berichttypen voor routering
    /// </summary>
    public enum BerichtType
    {
        BestellingAangemaakt,
        BestellingBijgewerkt,
        BestellingVerwijderd,
        BestellingVerzonden,
        BestellingAfgeleverd,
        KlantAangemaakt,
        KlantBijgewerkt,
        KlantVerwijderd,
        VoorraadLaag,
        VoorraadAangevuld,
        MeldingVerstuurd,
        AuditLogItem
    }

    /// <summary>
    /// Interface voor RabbitMQ publishing naar meerdere queues
    /// </summary>
    public interface IRabbitMQPublisher
    {
        Task<string> PublishToSalesforceAsync(Bestelling bestelling);
        Task<string> PubliceerBestellingUpdateAsync(int bestellingId, string status, string bericht);
        Task<string> PubliceerBestellingVerwijderdAsync(int bestellingId, string reden);
        Task<string> PubliceerKlantSyncAsync(Klant klant, BerichtType berichtType);
        Task<string> PubliceerVoorraadUpdateAsync(int boekId, string titel, int aantal, bool isVoorraadLaag);
        Task<string> PubliceerMeldingAsync(string titel, string bericht, string ontvanger);
        Task<string> PubliceerAuditLogAsync(string actie, string entiteit, int entiteitId, string gebruikerId, object details);
        
        // Aliassen voor backwards compatibility
        Task<string> PublishOrderUpdateAsync(int orderId, string status, string message);
        Task<string> PublishOrderDeleteAsync(int orderId, string reason);
        Task<string> PublishCustomerSyncAsync(Klant customer, BerichtType messageType);
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
                HostName = rabbitConfig["HostName"] ?? "10.2.160.223",
                UserName = rabbitConfig["UserName"] ?? "guest",
                Password = rabbitConfig["Password"] ?? "guest"
            };
        }

        #region Bestelling Berichten

        /// <summary>
        /// Publiceer nieuwe bestelling naar Salesforce queue
        /// </summary>
        public async Task<string> PublishToSalesforceAsync(Bestelling bestelling)
        {
            var bericht = new
            {
                BerichtType = BerichtType.BestellingAangemaakt.ToString(),
                BestellingId = bestelling.Id,
                KlantId = bestelling.KlantId,
                KlantNaam = bestelling.KlantNaam,
                Datum = bestelling.Datum,
                Totaal = bestelling.Totaal,
                Artikelen = bestelling.Items.Select(item => new
                {
                    BoekId = item.BoekId,
                    BoekTitel = item.BoekTitel,
                    Aantal = item.Aantal,
                    Prijs = item.Prijs
                }),
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.Bestellingen, bericht, $"Bestelling {bestelling.Id} aangemaakt");
        }

        /// <summary>
        /// Publiceer bestelling status update
        /// </summary>
        public async Task<string> PubliceerBestellingUpdateAsync(int bestellingId, string status, string bericht)
        {
            var updateBericht = new
            {
                BerichtType = BerichtType.BestellingBijgewerkt.ToString(),
                BestellingId = bestellingId,
                Status = status,
                Bericht = bericht,
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.BestellingUpdates, updateBericht, $"Bestelling {bestellingId} bijgewerkt naar {status}");
        }

        /// <summary>
        /// Publiceer bestelling verwijdering
        /// </summary>
        public async Task<string> PubliceerBestellingVerwijderdAsync(int bestellingId, string reden)
        {
            var verwijderBericht = new
            {
                BerichtType = BerichtType.BestellingVerwijderd.ToString(),
                BestellingId = bestellingId,
                Reden = reden,
                VerwijderdOp = DateTime.UtcNow,
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.BestellingVerwijderd, verwijderBericht, $"Bestelling {bestellingId} verwijderd");
        }

        #endregion

        #region Klant Berichten

        /// <summary>
        /// Publiceer klant synchronisatie bericht
        /// </summary>
        public async Task<string> PubliceerKlantSyncAsync(Klant klant, BerichtType berichtType)
        {
            var klantBericht = new
            {
                BerichtType = berichtType.ToString(),
                KlantId = klant.Id,
                Naam = klant.Naam,
                Email = klant.Email,
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.KlantSynchronisatie, klantBericht, $"Klant {klant.Id} {berichtType}");
        }

        #endregion

        #region Voorraad Berichten

        /// <summary>
        /// Publiceer voorraad update bericht
        /// </summary>
        public async Task<string> PubliceerVoorraadUpdateAsync(int boekId, string titel, int aantal, bool isVoorraadLaag)
        {
            var voorraadBericht = new
            {
                BerichtType = isVoorraadLaag ? BerichtType.VoorraadLaag.ToString() : BerichtType.VoorraadAangevuld.ToString(),
                BoekId = boekId,
                Titel = titel,
                Aantal = aantal,
                IsVoorraadLaag = isVoorraadLaag,
                AlertNiveau = isVoorraadLaag ? "Waarschuwing" : "Informatie",
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.VoorraadUpdates, voorraadBericht, $"Voorraad update voor boek {boekId}");
        }

        #endregion

        #region Melding Berichten

        /// <summary>
        /// Publiceer algemene melding
        /// </summary>
        public async Task<string> PubliceerMeldingAsync(string titel, string bericht, string ontvanger)
        {
            var meldingBericht = new
            {
                BerichtType = BerichtType.MeldingVerstuurd.ToString(),
                Titel = titel,
                Bericht = bericht,
                Ontvanger = ontvanger,
                VerstuurdOp = DateTime.UtcNow,
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.Meldingen, meldingBericht, $"Melding verstuurd naar {ontvanger}");
        }

        #endregion

        #region Audit Berichten

        /// <summary>
        /// Publiceer audit log item
        /// </summary>
        public async Task<string> PubliceerAuditLogAsync(string actie, string entiteit, int entiteitId, string gebruikerId, object details)
        {
            var auditBericht = new
            {
                BerichtType = BerichtType.AuditLogItem.ToString(),
                Actie = actie,
                Entiteit = entiteit,
                EntiteitId = entiteitId,
                GebruikerId = gebruikerId,
                Details = details,
                Tijdstempel = DateTime.UtcNow
            };

            return await PubliceerBerichtAsync(QueueNames.AuditLog, auditBericht, $"Audit: {actie} op {entiteit} {entiteitId}");
        }

        #endregion

        #region Backwards Compatibility Aliassen

        // Deze methodes roepen de Nederlandse versies aan voor backwards compatibility
        public Task<string> PublishOrderUpdateAsync(int orderId, string status, string message) 
            => PubliceerBestellingUpdateAsync(orderId, status, message);

        public Task<string> PublishOrderDeleteAsync(int orderId, string reason) 
            => PubliceerBestellingVerwijderdAsync(orderId, reason);

        public Task<string> PublishCustomerSyncAsync(Klant customer, BerichtType messageType) 
            => PubliceerKlantSyncAsync(customer, messageType);

        public Task<string> PublishInventoryUpdateAsync(int boekId, string titel, int quantity, bool isLowStock) 
            => PubliceerVoorraadUpdateAsync(boekId, titel, quantity, isLowStock);

        public Task<string> PublishNotificationAsync(string title, string message, string recipient) 
            => PubliceerMeldingAsync(title, message, recipient);

        public Task<string> PublishAuditLogAsync(string action, string entity, int entityId, string userId, object details) 
            => PubliceerAuditLogAsync(action, entity, entityId, userId, details);

        #endregion

        #region Private Helper Methodes

        /// <summary>
        /// Generieke methode om berichten naar een queue te publiceren
        /// </summary>
        private async Task<string> PubliceerBerichtAsync(string queueNaam, object bericht, string logBeschrijving)
        {
            try
            {
                var connection = await _factory.CreateConnectionAsync();
                var channel = await connection.CreateChannelAsync();

                // Declareer queue (idempotente operatie)
                await channel.QueueDeclareAsync(
                    queue: queueNaam,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                var jsonBericht = JsonSerializer.Serialize(bericht, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                var body = Encoding.UTF8.GetBytes(jsonBericht);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    ContentType = "application/json"
                };

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: queueNaam,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                var trackingId = properties.MessageId;
                
                // Mooie console output voor PowerShell
                ToonConsoleOutput(queueNaam, trackingId, jsonBericht, logBeschrijving);

                await channel.CloseAsync();
                await connection.CloseAsync();

                return trackingId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Kon bericht niet publiceren naar {queueNaam}: {logBeschrijving}");
                Console.WriteLine();
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine($"❌ FOUT: Kon bericht niet publiceren");
                Console.WriteLine($"   Queue: {queueNaam}");
                Console.WriteLine($"   Fout: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.WriteLine();
                throw;
            }
        }

        /// <summary>
        /// Toont mooie console output voor berichten
        /// </summary>
        private void ToonConsoleOutput(string queueNaam, string trackingId, string jsonBericht, string beschrijving)
        {
            var tijdstempel = DateTime.Now.ToString("HH:mm:ss");
            var queueIcon = GetQueueIcon(queueNaam);
            
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"{queueIcon} {beschrijving.ToUpper()}");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"   📅 Tijdstempel  : {tijdstempel}");
            Console.WriteLine($"   📨 Queue        : {queueNaam}");
            Console.WriteLine($"   🔑 Tracking ID  : {trackingId}");
            Console.WriteLine("───────────────────────────────────────────────────────────────");
            Console.WriteLine("   📄 JSON Bericht:");
            Console.WriteLine();
            
            // Toon JSON met indentatie
            foreach (var line in jsonBericht.Split('\n'))
            {
                Console.WriteLine($"      {line.TrimEnd()}");
            }
            
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine();
            
            _logger.LogInformation($"{beschrijving}. Queue: {queueNaam}, Tracking ID: {trackingId}");
        }

        /// <summary>
        /// Geeft een icon terug op basis van queue naam
        /// </summary>
        private string GetQueueIcon(string queueNaam)
        {
            return queueNaam switch
            {
                "bestellingen" => "✅ BESTELLING GEPLAATST!",
                "bestelling_updates" => "🔄 BESTELLING BIJGEWERKT",
                "bestelling_verwijderd" => "🗑️ BESTELLING VERWIJDERD",
                "klant_synchronisatie" => "👤 KLANT GESYNCHRONISEERD",
                "voorraad_updates" => "📦 VOORRAAD UPDATE",
                "meldingen" => "🔔 MELDING VERSTUURD",
                "audit_log" => "📝 AUDIT LOG",
                _ => "📨 BERICHT GEPUBLICEERD"
            };
        }

        #endregion
    }
}
