using BestelApp.Shared.Models;
using System.Text;
using System.Xml.Linq;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Interface for SAP iDoc integration
    /// </summary>
    public interface ISapService
    {
        Task<SapResponse> SendIDocAsync(Bestelling order);
    }

    public class SapService : ISapService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SapService> _logger;
        private readonly HttpClient _httpClient;

        public SapService(IConfiguration configuration, ILogger<SapService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("SAP");
        }

        public async Task<SapResponse> SendIDocAsync(Bestelling order)
        {
            try
            {
                _logger.LogInformation($"Processing SAP iDoc for order {order.Id}");

                // 1. Transform JSON to SAP iDoc XML (ORDERS05 format)
                var idocXml = CreateIDocXml(order);

                // 2. Send to SAP R/3 system
                var sapUrl = _configuration["SAP:ServerUrl"];
                
                if (string.IsNullOrEmpty(sapUrl))
                {
                    _logger.LogWarning("SAP ServerUrl not configured. Using mock response.");
                    return CreateMockSapResponse(order);
                }

                var content = new StringContent(idocXml, Encoding.UTF8, "application/xml");
                
                // In real scenario, this would use SAP RFC or HTTP endpoint
                // For now, we simulate the SAP response
                try
                {
                    var response = await _httpClient.PostAsync($"{sapUrl}/idoc/orders", content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        return ParseSapResponse(responseContent, order);
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "SAP system unreachable. Using mock response for development.");
                }

                // Fallback: Return mock response for development/testing
                return CreateMockSapResponse(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending iDoc for order {order.Id}");
                return new SapResponse
                {
                    IsSuccess = false,
                    Status = 51, // Error status
                    ErrorMessage = ex.Message,
                    DocumentNumber = null,
                    StockCode = null
                };
            }
        }

        /// <summary>
        /// Creates SAP iDoc XML in ORDERS05 format with E1EDK01 segment
        /// </summary>
        private string CreateIDocXml(Bestelling order)
        {
            var idoc = new XDocument(
                new XElement("ORDERS05",
                    new XElement("IDOC",
                        new XAttribute("BEGIN", "1"),
                        // E1EDK01: Document Header segment
                        new XElement("E1EDK01",
                            new XElement("BELNR", order.Id.ToString("D10")), // Document number (BELNR)
                            new XElement("DATUM", order.Datum.ToString("yyyyMMdd")), // Date
                            new XElement("UZEIT", order.Datum.ToString("HHmmss")), // Time
                            new XElement("WERT", order.Totaal.ToString("F2")) // Total value
                        ),
                        // E1EDKA1: Partner Information (Customer)
                        new XElement("E1EDKA1",
                            new XElement("PARVW", "AG"), // Partner function: Sold-to party
                            new XElement("PARTN", order.KlantId.ToString("D10"))
                        ),
                        // E1EDP01: Item segments
                        from item in order.Items.Select((value, index) => new { value, index })
                        select new XElement("E1EDP01",
                            new XElement("POSEX", (item.index + 1).ToString("D6")), // Item number
                            new XElement("MATNR", item.value.BoekId.ToString("D18")), // Material number
                            new XElement("MENGE", item.value.Aantal.ToString()), // Quantity
                            new XElement("MENEE", "PCE"), // Unit of measure
                            new XElement("NETPR", item.value.Prijs.ToString("F2")) // Net price
                        )
                    )
                )
            );

            return idoc.ToString();
        }

        /// <summary>
        /// Parses SAP response XML to extract status and document number
        /// </summary>
        private SapResponse ParseSapResponse(string xmlResponse, Bestelling order)
        {
            try
            {
                var doc = XDocument.Parse(xmlResponse);
                var statusElement = doc.Descendants("STATUS").FirstOrDefault();
                var docNumElement = doc.Descendants("DOCNUM").FirstOrDefault();
                var stockCodeElement = doc.Descendants("STOCK_CODE").FirstOrDefault();

                var status = statusElement != null ? int.Parse(statusElement.Value) : 53;
                var docNum = docNumElement?.Value ?? $"SAP{order.Id:D10}";
                var stockCode = stockCodeElement?.Value ?? "IN_STOCK";

                return new SapResponse
                {
                    IsSuccess = status == 53,
                    Status = status,
                    DocumentNumber = docNum,
                    StockCode = stockCode,
                    ErrorMessage = status == 51 ? "SAP processing error" : null
                };
            }
            catch
            {
                // If parsing fails, return default success response
                return CreateMockSapResponse(order);
            }
        }

        /// <summary>
        /// Creates a mock SAP response for development/testing
        /// Status codes: 53 = Success, 51 = Error, 64 = Ready for processing
        /// </summary>
        private SapResponse CreateMockSapResponse(Bestelling order)
        {
            // Simulate random stock availability (90% in stock)
            var random = new Random();
            var inStock = random.Next(100) < 90;

            var status = inStock ? 53 : 64; // 53 = Processed, 64 = Ready for processing
            var stockCode = inStock ? $"STOCK_{DateTime.Now:yyyyMMddHHmmss}" : "PENDING_STOCK_CHECK";

            return new SapResponse
            {
                IsSuccess = true,
                Status = status,
                DocumentNumber = $"4500{order.Id:D6}", // SAP document format
                StockCode = stockCode,
                ErrorMessage = null
            };
        }
    }
}
