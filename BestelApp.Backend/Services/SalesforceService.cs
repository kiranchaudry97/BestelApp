using BestelApp.Shared.Models;
using System.Text;
using System.Text.Json;

namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Interface for Salesforce integration
    /// </summary>
    public interface ISalesforceService
    {
        Task<string> CreateOrderAsync(Bestelling order);
    }

    public class SalesforceService : ISalesforceService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SalesforceService> _logger;
        private string _accessToken;
        private DateTime _tokenExpiration;

        public SalesforceService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SalesforceService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("Salesforce");
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> CreateOrderAsync(Bestelling order)
        {
            try
            {
                await EnsureAuthenticatedAsync();

                var salesforceUrl = _configuration["Salesforce:InstanceUrl"];
                
                if (string.IsNullOrEmpty(salesforceUrl))
                {
                    _logger.LogWarning("Salesforce not configured. Using mock ID.");
                    return CreateMockSalesforceId(order);
                }

                // Create Salesforce Order object
                var salesforceOrder = new
                {
                    Name = $"Order {order.Id}",
                    OrderNumber = order.Id.ToString(),
                    EffectiveDate = order.Datum.ToString("yyyy-MM-dd"),
                    Status = "Draft",
                    TotalAmount = order.Totaal,
                    // Custom fields
                    External_Order_Id__c = order.Id.ToString(),
                    Customer_Id__c = order.KlantId.ToString()
                };

                var json = JsonSerializer.Serialize(salesforceOrder);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.PostAsync($"{salesforceUrl}/services/data/v58.0/sobjects/Order", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SalesforceCreateResponse>(responseContent);
                    
                    _logger.LogInformation($"Order {order.Id} created in Salesforce with ID: {result?.id}");
                    return result?.id ?? CreateMockSalesforceId(order);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Salesforce API error: {response.StatusCode} - {error}");
                    return CreateMockSalesforceId(order);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating order in Salesforce for order {order.Id}");
                return CreateMockSalesforceId(order);
            }
        }

        private async Task EnsureAuthenticatedAsync()
        {
            // Check if token is still valid
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration)
            {
                return;
            }

            // OAuth 2.0 Authentication with Salesforce
            var clientId = _configuration["Salesforce:ClientId"];
            var clientSecret = _configuration["Salesforce:ClientSecret"];
            var username = _configuration["Salesforce:Username"];
            var password = _configuration["Salesforce:Password"];

            if (string.IsNullOrEmpty(clientId))
            {
                _logger.LogWarning("Salesforce credentials not configured. Skipping authentication.");
                return;
            }

            var tokenEndpoint = "https://login.salesforce.com/services/oauth2/token";
            
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "username", username },
                { "password", password }
            };

            var content = new FormUrlEncodedContent(parameters);

            try
            {
                var response = await _httpClient.PostAsync(tokenEndpoint, content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<SalesforceTokenResponse>(responseContent);
                    
                    _accessToken = tokenResponse?.access_token;
                    _tokenExpiration = DateTime.UtcNow.AddHours(1); // Tokens typically last 2 hours, we refresh earlier
                    
                    _logger.LogInformation("Successfully authenticated with Salesforce");
                }
                else
                {
                    _logger.LogError($"Failed to authenticate with Salesforce: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating with Salesforce");
            }
        }

        private string CreateMockSalesforceId(Bestelling order)
        {
            // Generate mock Salesforce ID (18 character alphanumeric)
            return $"a00{order.Id:D15}";
        }

        private class SalesforceTokenResponse
        {
            public string access_token { get; set; }
            public string instance_url { get; set; }
            public string id { get; set; }
            public string token_type { get; set; }
        }

        private class SalesforceCreateResponse
        {
            public string id { get; set; }
            public bool success { get; set; }
            public List<object> errors { get; set; }
        }
    }
}
