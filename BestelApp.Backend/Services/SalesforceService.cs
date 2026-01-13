using BestelApp.Shared.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace BestelApp.Backend.Services
{
    public class SalesforceService : ISalesforceService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SalesforceService> _logger;
        private readonly string _salesforceUrl;
        private readonly string _accessToken;

        public SalesforceService(HttpClient httpClient, ILogger<SalesforceService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _salesforceUrl = Environment.GetEnvironmentVariable("SALESFORCE_URL");
            _accessToken = Environment.GetEnvironmentVariable("SALESFORCE_ACCESS_TOKEN");

            if (!string.IsNullOrEmpty(_accessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }
        }

        public async Task<bool> SendOrderToSalesforceAsync(Bestelling bestelling)
        {
            try
            {
                var salesforceOrder = new
                {
                    Bestelnummer__c = bestelling.Bestelnummer,
                    Klantnaam__c = bestelling.Klantnaam,
                    Product__c = bestelling.Product,
                    Aantal__c = bestelling.Aantal,
                    Totaalprijs__c = bestelling.TotaalPrijs,
                    Besteldatum__c = bestelling.Besteldatum.ToString("yyyy-MM-dd"),
                    Status__c = "Nieuw",
                    Bron_Systeem__c = "BestelApp"
                };

                var json = JsonConvert.SerializeObject(salesforceOrder);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_salesforceUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Bestelling {bestelling.Bestelnummer} naar Salesforce gestuurd");
                    return true;
                }
                else
                {
                    _logger.LogError($"Salesforce fout: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Fout bij verzenden naar Salesforce: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetOrderStatusAsync(string bestelnummer)
        {
            // Implementeer status check naar Salesforce
            return await Task.FromResult("Verwerkt");
        }
    }
}