using BestelApp.Shared.Models;
using System.Threading.Tasks;

namespace BestelApp.Backend.Services
{
    public interface ISalesforceService
    {
        Task<bool> SendOrderToSalesforceAsync(Bestelling bestelling);
        Task<string> GetOrderStatusAsync(string bestelnummer);
    }
}