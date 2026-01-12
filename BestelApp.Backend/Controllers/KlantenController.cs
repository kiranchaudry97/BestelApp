using BestelApp.Backend.Services;
using BestelApp.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace BestelApp.Backend.Controllers
{
    /// <summary>
    /// Controller voor klant beheer met RabbitMQ synchronisatie
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class KlantenController : ControllerBase
    {
        private readonly IApiKeyValidator _apiKeyValidator;
        private readonly IRabbitMQPublisher _rabbitMQPublisher;
        private readonly ILogger<KlantenController> _logger;

        public KlantenController(
            IApiKeyValidator apiKeyValidator,
            IRabbitMQPublisher rabbitMQPublisher,
            ILogger<KlantenController> logger)
        {
            _apiKeyValidator = apiKeyValidator;
            _rabbitMQPublisher = rabbitMQPublisher;
            _logger = logger;
        }

        /// <summary>
        /// Synchroniseert een nieuwe klant naar RabbitMQ
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateKlant([FromBody] KlantSyncRequest request)
        {
            try
            {
                _logger.LogInformation($"Ontvangen klant aanmaak verzoek voor klant {request.Klant.Id}");

                if (!_apiKeyValidator.Validate(request.ApiKey))
                {
                    _logger.LogWarning($"Ongeautoriseerde klant aanmaak poging");
                    return Unauthorized(new { bericht = "Ongeldige API key" });
                }

                var trackingId = await _rabbitMQPublisher.PublishCustomerSyncAsync(request.Klant, BerichtType.KlantAangemaakt);

                _logger.LogInformation($"Klant {request.Klant.Id} aangemaakt en gepubliceerd naar RabbitMQ. Tracking ID: {trackingId}");

                return Ok(new
                {
                    succes = true,
                    bericht = $"Klant {request.Klant.Naam} aangemaakt",
                    trackingId = trackingId,
                    tijdstempel = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij aanmaken klant");
                return StatusCode(500, new { succes = false, bericht = "Fout bij aanmaken klant" });
            }
        }

        /// <summary>
        /// Synchroniseert een bijgewerkte klant naar RabbitMQ
        /// </summary>
        [HttpPut("{klantId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateKlant(int klantId, [FromBody] KlantSyncRequest request)
        {
            try
            {
                _logger.LogInformation($"Ontvangen klant update verzoek voor klant {klantId}");

                if (!_apiKeyValidator.Validate(request.ApiKey))
                {
                    _logger.LogWarning($"Ongeautoriseerde klant update poging voor klant {klantId}");
                    return Unauthorized(new { bericht = "Ongeldige API key" });
                }

                var trackingId = await _rabbitMQPublisher.PublishCustomerSyncAsync(request.Klant, BerichtType.KlantBijgewerkt);

                _logger.LogInformation($"Klant {klantId} bijgewerkt en gepubliceerd naar RabbitMQ. Tracking ID: {trackingId}");

                return Ok(new
                {
                    succes = true,
                    bericht = $"Klant {request.Klant.Naam} bijgewerkt",
                    trackingId = trackingId,
                    tijdstempel = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij bijwerken klant {klantId}");
                return StatusCode(500, new { succes = false, bericht = "Fout bij bijwerken klant" });
            }
        }

        /// <summary>
        /// Synchroniseert een verwijderde klant naar RabbitMQ
        /// </summary>
        [HttpDelete("{klantId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> DeleteKlant(int klantId, [FromHeader(Name = "X-Api-Key")] string apiKey, [FromQuery] string naam = "Onbekend")
        {
            try
            {
                _logger.LogInformation($"Ontvangen klant verwijder verzoek voor klant {klantId}");

                if (!_apiKeyValidator.Validate(apiKey))
                {
                    _logger.LogWarning($"Ongeautoriseerde klant verwijder poging voor klant {klantId}");
                    return Unauthorized(new { bericht = "Ongeldige API key" });
                }

                var klant = new Klant { Id = klantId, Naam = naam, Email = "" };
                var trackingId = await _rabbitMQPublisher.PublishCustomerSyncAsync(klant, BerichtType.KlantVerwijderd);

                _logger.LogInformation($"Klant {klantId} verwijderd en gepubliceerd naar RabbitMQ. Tracking ID: {trackingId}");

                return Ok(new
                {
                    succes = true,
                    bericht = $"Klant {klantId} verwijderd",
                    trackingId = trackingId,
                    tijdstempel = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij verwijderen klant {klantId}");
                return StatusCode(500, new { succes = false, bericht = "Fout bij verwijderen klant" });
            }
        }
    }

    /// <summary>
    /// Request model voor klant synchronisatie
    /// </summary>
    public class KlantSyncRequest
    {
        public string ApiKey { get; set; } = string.Empty;
        public Klant Klant { get; set; } = new Klant();
    }
}
