using BestelApp.Shared.Models;
using RabbitMQ.Client;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BestelApp.Services
{
    public class OrderService : IOrderService
    {
        private readonly ConnectionFactory _factory;
        private readonly HttpClient _httpClient;
        private readonly ConfigurationService _configService;
        private readonly AppConfiguration _config;
        private const string QueueName = "salesforce_orders";

        public OrderService(ConfigurationService configService)
        {
            _configService = configService;
            _config = _configService.GetConfiguration();

            // Initialize RabbitMQ factory
            _factory = new ConnectionFactory()
            {
                HostName = _config.RabbitMQ.HostName,
                UserName = _config.RabbitMQ.UserName,
                Password = _config.RabbitMQ.Password
            };

            // Initialize HttpClient for backend API communication
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_config.BackendApiUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            // Add default headers
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Places order via backend API (handles both RabbitMQ and SAP iDoc)
        /// This is the primary method for order placement
        /// </summary>
        public async Task<OrderResponse> PlaatsBestellingAsync(Bestelling bestelling)
        {
            try
            {
                // Create request with API key for authentication
                var orderRequest = new OrderRequest
                {
                    ApiKey = _config.ApiKey,
                    Order = bestelling,
                    RequestDateTime = DateTime.UtcNow,
                    RequestId = Guid.NewGuid().ToString()
                };

                // Send HTTPS POST request to backend
                var response = await _httpClient.PostAsJsonAsync("/orders", orderRequest);

                if (response.IsSuccessStatusCode)
                {
                    // Parse the response containing both Salesforce ID and SAP status
                    var orderResponse = await response.Content.ReadFromJsonAsync<OrderResponse>();
                    
                    if (orderResponse != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Order placed successfully:");
                        System.Diagnostics.Debug.WriteLine($"  Salesforce ID: {orderResponse.SalesforceOrderId}");
                        System.Diagnostics.Debug.WriteLine($"  SAP Document: {orderResponse.SapDocumentNumber}");
                        System.Diagnostics.Debug.WriteLine($"  SAP Status: {orderResponse.SapStatus} ({GetSapStatusDescription(orderResponse.SapStatus)})");
                        System.Diagnostics.Debug.WriteLine($"  Stock Code: {orderResponse.StockCode}");
                        
                        return orderResponse;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Backend API error: {response.StatusCode} - {errorContent}");
                }

                // Return error response if backend failed
                return new OrderResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Backend API returned status: {response.StatusCode}",
                    ResponseDateTime = DateTime.UtcNow
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP error communicating with backend: {ex.Message}");
                
                // Fallback: Try direct RabbitMQ if backend is unavailable (loose coupling)
                var rabbitMqSuccess = await PlaatsBestelling(bestelling);
                
                return new OrderResponse
                {
                    IsSuccess = rabbitMqSuccess,
                    ErrorMessage = rabbitMqSuccess 
                        ? "Backend unavailable, order sent via RabbitMQ directly" 
                        : "Both backend and RabbitMQ failed",
                    ResponseDateTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error placing order: {ex.Message}");
                return new OrderResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error: {ex.Message}",
                    ResponseDateTime = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Legacy direct RabbitMQ method (fallback when backend is unavailable)
        /// </summary>
        public Task<bool> PlaatsBestelling(Bestelling bestelling)
        {
            try
            {
                using (var connection = _factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: QueueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    var messageObject = new
                    {
                        ApiKey = _config.ApiKey,
                        Order = bestelling,
                        Timestamp = DateTime.UtcNow
                    };

                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(messageObject));

                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;

                    channel.BasicPublish(exchange: "",
                                         routingKey: QueueName,
                                         basicProperties: properties,
                                         body: body);
                    
                    System.Diagnostics.Debug.WriteLine("Order published directly to RabbitMQ (fallback mode)");
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not publish to RabbitMQ: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        private string GetSapStatusDescription(int status)
        {
            return status switch
            {
                53 => "Success - Document processed",
                51 => "Error - Processing failed",
                64 => "Ready for processing",
                _ => "Unknown status"
            };
        }
    }
}
