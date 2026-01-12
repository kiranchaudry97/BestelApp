using BestelApp.Shared.Models;
using System.Threading.Tasks;

namespace BestelApp.Services
{
    /// <summary>
    /// Interface for order processing with backend communication
    /// </summary>
    public interface IOrderService
    {
        /// <summary>
        /// Places an order by sending it to the backend API
        /// The backend will handle RabbitMQ and SAP iDoc processing
        /// </summary>
        /// <param name="bestelling">The order to place</param>
        /// <returns>OrderResponse containing Salesforce ID and SAP status</returns>
        Task<OrderResponse> PlaatsBestellingAsync(Bestelling bestelling);

        /// <summary>
        /// Legacy method for direct RabbitMQ publishing (kept for backwards compatibility)
        /// </summary>
        /// <param name="bestelling">The order to place</param>
        /// <returns>True if published successfully to RabbitMQ</returns>
        Task<bool> PlaatsBestelling(Bestelling bestelling);
    }
}
