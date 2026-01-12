using BestelApp.Backend.Services;
using BestelApp.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace BestelApp.Backend.Controllers
{
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
        /// Places an order - handles parallel processing to RabbitMQ/Salesforce and SAP
        /// </summary>
        /// <param name="request">Order request with API key and order details</param>
        /// <returns>Combined response with Salesforce ID and SAP status</returns>
        [HttpPost]
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<OrderResponse>> PlaceOrder([FromBody] OrderRequest request)
        {
            try
            {
                _logger.LogInformation($"Received order request. RequestId: {request.RequestId}");

                // Step 1: Validate API Key (GDPR compliance)
                if (!_apiKeyValidator.Validate(request.ApiKey))
                {
                    _logger.LogWarning($"Unauthorized order attempt. RequestId: {request.RequestId}");
                    return Unauthorized(new { message = _apiKeyValidator.GetValidationError(request.ApiKey) });
                }

                // Step 2: Validate order data
                if (!ValidateOrder(request.Order))
                {
                    _logger.LogWarning($"Invalid order data. RequestId: {request.RequestId}");
                    return BadRequest(new { message = "Invalid order data. Check required fields." });
                }

                _logger.LogInformation($"Processing order {request.Order.Id}. RequestId: {request.RequestId}");

                // Step 3: Parallel processing - RabbitMQ (? Salesforce) and SAP iDoc
                var rabbitTask = _rabbitMQPublisher.PublishToSalesforceAsync(request.Order);
                var sapTask = _sapService.SendIDocAsync(request.Order);

                // Wait for both to complete
                await Task.WhenAll(rabbitTask, sapTask);

                var salesforceTrackingId = await rabbitTask;
                var sapResponse = await sapTask;

                // Step 4: Create combined response
                var response = new OrderResponse
                {
                    SalesforceOrderId = salesforceTrackingId,
                    SapDocumentNumber = sapResponse.DocumentNumber,
                    SapStatus = sapResponse.Status,
                    IsSuccess = sapResponse.IsSuccess,
                    StockCode = sapResponse.StockCode,
                    StatusMessage = GetStatusMessage(sapResponse.Status),
                    ResponseDateTime = DateTime.UtcNow,
                    ErrorMessage = sapResponse.ErrorMessage
                };

                _logger.LogInformation($"Order {request.Order.Id} processed successfully. " +
                    $"Salesforce Tracking: {salesforceTrackingId}, SAP Doc: {sapResponse.DocumentNumber}, SAP Status: {sapResponse.Status}");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing order. RequestId: {request?.RequestId}");
                
                return StatusCode(500, new OrderResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Internal server error processing order",
                    ResponseDateTime = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Validates order data
        /// </summary>
        private bool ValidateOrder(Bestelling order)
        {
            if (order == null) return false;
            if (order.KlantId <= 0) return false;
            if (order.Totaal <= 0) return false;
            if (order.Items == null || order.Items.Count == 0) return false;
            
            // Validate all items
            foreach (var item in order.Items)
            {
                if (item.BoekId <= 0 || item.Aantal <= 0 || item.Prijs <= 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Converts SAP status code to human-readable message
        /// </summary>
        private string GetStatusMessage(int sapStatus)
        {
            return sapStatus switch
            {
                53 => "Order successfully processed in both systems",
                51 => "SAP processing error",
                64 => "Order ready for processing in SAP",
                _ => $"Unknown SAP status: {sapStatus}"
            };
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}
