namespace BestelApp.Backend.Services
{
    /// <summary>
    /// Interface for API Key validation (GDPR compliant authentication)
    /// </summary>
    public interface IApiKeyValidator
    {
        bool Validate(string apiKey);
        string GetValidationError(string apiKey);
    }

    public class ApiKeyValidator : IApiKeyValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyValidator> _logger;

        public ApiKeyValidator(IConfiguration configuration, ILogger<ApiKeyValidator> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool Validate(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("API Key validation failed: Empty or null key");
                return false;
            }

            var validKeys = _configuration.GetSection("Authentication:ValidApiKeys").Get<string[]>();
            
            if (validKeys == null || validKeys.Length == 0)
            {
                _logger.LogError("No valid API keys configured in appsettings.json");
                return false;
            }

            bool isValid = validKeys.Contains(apiKey);
            
            if (!isValid)
            {
                _logger.LogWarning("API Key validation failed: Invalid key provided");
            }
            else
            {
                _logger.LogInformation("API Key validated successfully");
            }

            return isValid;
        }

        public string GetValidationError(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "API Key is required";

            return "Invalid API Key";
        }
    }
}
