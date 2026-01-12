using BestelApp.Backend.Services;
using BestelApp.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace BestelApp.Backend.Controllers
{
    /// <summary>
    /// Controller voor bestelling beheer met RabbitMQ en SAP integratie
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IApiKeyValidator _apiKeyValidator;
        private readonly IRabbitMQPublisher _rabbitMQPublisher;
        private readonly ISapService _sapService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IApiKeyValidator apiKeyValidator,
            IRabbitMQPublisher rabbitMQPublisher,
            ISapService sapService,
            ILogger<OrdersController> logger)
        {
            _apiKeyValidator = apiKeyValidator;
            _rabbitMQPublisher = rabbitMQPublisher;
            _sapService = sapService;
            _logger = logger;
        }

        /// <summary>
        /// Plaatst een bestelling - verwerkt parallel naar RabbitMQ/Salesforce en SAP
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<OrderResponse>> PlaatsBestelling([FromBody] OrderRequest request)
        {
            try
            {
                _logger.LogInformation($"Bestelling ontvangen. RequestId: {request.RequestId}");

                // Stap 1: Valideer API Key (GDPR compliance)
                if (!_apiKeyValidator.Validate(request.ApiKey))
                {
                    _logger.LogWarning($"Ongeautoriseerde bestelling poging. RequestId: {request.RequestId}");
                    return Unauthorized(new { bericht = _apiKeyValidator.GetValidationError(request.ApiKey) });
                }

                // Stap 2: Valideer bestelling data
                if (!ValideerBestelling(request.Order))
                {
                    _logger.LogWarning($"Ongeldige bestelling data. RequestId: {request.RequestId}");
                    return BadRequest(new { bericht = "Ongeldige bestelling data. Controleer verplichte velden." });
                }

                _logger.LogInformation($"Verwerken bestelling {request.Order.Id}. RequestId: {request.RequestId}");

                // Stap 3: Parallelle verwerking - RabbitMQ (? Salesforce) en SAP iDoc
                var rabbitTask = _rabbitMQPublisher.PublishToSalesforceAsync(request.Order);
                var sapTask = _sapService.SendIDocAsync(request.Order);

                // Wacht op beide taken
                await Task.WhenAll(rabbitTask, sapTask);

                var salesforceTrackingId = await rabbitTask;
                var sapResponse = await sapTask;

                // Stap 4: Maak gecombineerde response
                var response = new OrderResponse
                {
                    SalesforceOrderId = salesforceTrackingId,
                    SapDocumentNumber = sapResponse.DocumentNumber,
                    SapStatus = sapResponse.Status,
                    IsSuccess = sapResponse.IsSuccess,
                    StockCode = sapResponse.StockCode,
                    StatusMessage = GetStatusBericht(sapResponse.Status),
                    ResponseDateTime = DateTime.UtcNow,
                    ErrorMessage = sapResponse.ErrorMessage
                };

                // Mooie console output voor SAP response
                ToonSapResponseConsole(request.Order, salesforceTrackingId, sapResponse);

                _logger.LogInformation($"Bestelling {request.Order.Id} succesvol verwerkt. " +
                    $"Salesforce Tracking: {salesforceTrackingId}, SAP Doc: {sapResponse.DocumentNumber}, SAP Status: {sapResponse.Status}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij verwerken bestelling. RequestId: {request?.RequestId}");
                
                return StatusCode(500, new OrderResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Interne server fout bij verwerken bestelling",
                    ResponseDateTime = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Toont mooie SAP response in console
        /// </summary>
        private void ToonSapResponseConsole(Bestelling bestelling, string salesforceId, SapResponse sapResponse)
        {
            var statusIcon = sapResponse.Status == 53 ? "?" : (sapResponse.Status == 51 ? "?" : "?");
            var statusTekst = GetStatusBericht(sapResponse.Status);
            
            Console.WriteLine();
            Console.WriteLine("?????????????????????????????????????????????????????????????????");
            Console.WriteLine("?           ?? SAP iDOC RESPONSE ONTVANGEN                      ?");
            Console.WriteLine("?????????????????????????????????????????????????????????????????");
            Console.WriteLine($"?  {statusIcon} Status: {sapResponse.Status} - {(sapResponse.IsSuccess ? "SUCCESVOL" : "FOUT")}");
            Console.WriteLine("?????????????????????????????????????????????????????????????????");
            Console.WriteLine($"?  ?? Bestelling ID    : {bestelling.Id}");
            Console.WriteLine($"?  ?? Klant            : {bestelling.KlantNaam}");
            Console.WriteLine($"?  ?? Totaal           : €{bestelling.Totaal:N2}");
            Console.WriteLine("?????????????????????????????????????????????????????????????????");
            Console.WriteLine($"?  ?? Salesforce ID    : {salesforceId}");
            Console.WriteLine($"?  ?? SAP Document     : {sapResponse.DocumentNumber}");
            Console.WriteLine($"?  ?? SAP Status       : {sapResponse.Status} - {statusTekst}");
            Console.WriteLine($"?  ?? Voorraad Code    : {sapResponse.StockCode}");
            Console.WriteLine("?????????????????????????????????????????????????????????????????");
            Console.WriteLine();
        }


        /// <summary>
        /// Valideert bestelling data
        /// </summary>
        private bool ValideerBestelling(Bestelling bestelling)
        {
            if (bestelling == null) return false;
            if (bestelling.KlantId <= 0) return false;
            if (bestelling.Totaal <= 0) return false;
            if (bestelling.Items == null || bestelling.Items.Count == 0) return false;
            
            // Valideer alle items
            foreach (var item in bestelling.Items)
            {
                if (item.BoekId <= 0 || item.Aantal <= 0 || item.Prijs <= 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Converteert SAP status code naar leesbaar bericht
        /// </summary>
        private string GetStatusBericht(int sapStatus)
        {
            return sapStatus switch
            {
                53 => "Bestelling succesvol verwerkt in beide systemen",
                51 => "SAP verwerkingsfout",
                64 => "Bestelling klaar voor verwerking in SAP",
                _ => $"Onbekende SAP status: {sapStatus}"
            };
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "gezond", tijdstempel = DateTime.UtcNow });
        }

        /// <summary>
        /// Verwijdert een bestelling en publiceert naar RabbitMQ bestelling_verwijderd queue
        /// </summary>
        [HttpDelete("{bestellingId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> VerwijderBestelling(int bestellingId, [FromHeader(Name = "X-Api-Key")] string apiKey, [FromQuery] string reden = "Verwijderd door gebruiker")
        {
            try
            {
                _logger.LogInformation($"Verwijder verzoek ontvangen voor bestelling {bestellingId}");

                // Valideer API Key
                if (!_apiKeyValidator.Validate(apiKey))
                {
                    _logger.LogWarning($"Ongeautoriseerde verwijder poging voor bestelling {bestellingId}");
                    return Unauthorized(new { bericht = "Ongeldige API key" });
                }

                // Publiceer verwijder bericht naar RabbitMQ
                var trackingId = await _rabbitMQPublisher.PublishOrderDeleteAsync(bestellingId, reden);

                _logger.LogInformation($"Bestelling {bestellingId} verwijdering gepubliceerd naar RabbitMQ. Tracking ID: {trackingId}");

                return Ok(new 
                { 
                    succes = true, 
                    bericht = $"Bestelling {bestellingId} succesvol verwijderd",
                    trackingId = trackingId,
                    tijdstempel = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij verwijderen bestelling {bestellingId}");
                return StatusCode(500, new { succes = false, bericht = "Fout bij verwijderen bestelling" });
            }
        }
    }
}
