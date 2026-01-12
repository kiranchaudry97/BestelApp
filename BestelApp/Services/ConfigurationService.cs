using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BestelApp.Services
{
    public class AppConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
        public string BackendApiUrl { get; set; } = string.Empty;
        public RabbitMQConfig RabbitMQ { get; set; } = new();
    }

    public class RabbitMQConfig
    {
        public string HostName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
    }

    public class ConfigurationService
    {
        private AppConfiguration _configuration = null!;

        public ConfigurationService()
        {
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                // Determine which config file to load based on platform
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName;

#if ANDROID
                // Use Android-specific configuration (10.0.2.2 for emulator)
                resourceName = "BestelApp.appsettings.android.json";
                System.Diagnostics.Debug.WriteLine("Loading Android configuration...");
#else
                // Use default configuration for Windows/iOS/Mac
                resourceName = "BestelApp.appsettings.json";
                System.Diagnostics.Debug.WriteLine("Loading default configuration...");
#endif

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            var root = JsonSerializer.Deserialize<RootConfig>(json);
                            _configuration = root?.AppSettings!;
                            
                            if (_configuration != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Configuration loaded successfully!");
                                System.Diagnostics.Debug.WriteLine($"Backend URL: {_configuration.BackendApiUrl}");
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Could not find embedded resource: {resourceName}");
                    }
                }

                // Fallback to default values if loading fails
                if (_configuration == null)
                {
                    System.Diagnostics.Debug.WriteLine("Using fallback configuration...");
                    _configuration = GetDefaultConfiguration();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
                // Use default configuration
                _configuration = GetDefaultConfiguration();
            }
        }

        private AppConfiguration GetDefaultConfiguration()
        {
#if ANDROID
            // Android emulator uses 10.0.2.2 to access host machine's localhost
            return new AppConfiguration
            {
                ApiKey = "YOUR-SECRET-API-KEY-HERE",
                BackendApiUrl = "http://10.0.2.2:5179/api",
                RabbitMQ = new RabbitMQConfig
                {
                    HostName = "10.0.2.2",
                    UserName = "guest",
                    Password = "guest",
                    QueueName = "salesforce_orders"
                }
            };
#else
            // Windows/iOS/Mac use localhost
            return new AppConfiguration
            {
                ApiKey = "YOUR-SECRET-API-KEY-HERE",
                BackendApiUrl = "http://localhost:5179/api",
                RabbitMQ = new RabbitMQConfig
                {
                    HostName = "localhost",
                    UserName = "guest",
                    Password = "guest",
                    QueueName = "salesforce_orders"
                }
            };
#endif
        }

        public AppConfiguration GetConfiguration() => _configuration;

        private class RootConfig
        {
            public AppConfiguration AppSettings { get; set; } = new();
        }
    }
}
