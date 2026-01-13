using BestelApp.Shared.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BestelApp.Backend.Services
{
    public class SalesforceService : ISalesforceService
    {
        private readonly ILogger<SalesforceService> _logger;

        public SalesforceService(ILogger<SalesforceService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> SendOrderToSalesforceAsync(Bestelling bestelling)
        {
            try
            {
                // Gebruik JOUW model met extensions
                var salesforceData = new
                {
                    Bestelnummer__c = bestelling.GetBestelnummer(),
                    Klantnaam__c = bestelling.GetKlantnaam(),
                    KlantId__c = bestelling.KlantId,
                    Producten__c = bestelling.GetProductenSamenvatting(),
                    AantalItems__c = bestelling.GetTotaalAantal(),
                    Totaalbedrag__c = bestelling.GetTotaalPrijs(),
                    Besteldatum__c = bestelling.Datum.ToString("yyyy-MM-dd"),
                    Status__c = "Nieuw",
                    Bron_Systeem__c = "BestelApp",
                    IsVerwerkt__c = false
                };

                _logger.LogInformation($"📤 Order {bestelling.GetBestelnummer()} naar Salesforce: {salesforceData}");
                await Task.Delay(500); // Simuleer API call

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Fout bij verzenden naar Salesforce");
                return false;
            }
        }

        public async Task<string> GetOrderStatusAsync(string bestelnummer)
        {
            await Task.Delay(100);
            return "Verwerkt";
        }
    }
}