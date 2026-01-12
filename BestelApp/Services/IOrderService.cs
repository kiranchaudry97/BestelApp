using BestelApp.Shared.Models;
using System.Threading.Tasks;

namespace BestelApp.Services
{
    /// <summary>
    /// Interface voor bestelling en klant verwerking met backend communicatie
    /// </summary>
    public interface IOrderService
    {
        #region Bestelling Methodes

        /// <summary>
        /// Plaatst een bestelling via de backend API
        /// De backend handelt RabbitMQ en SAP iDoc verwerking af
        /// </summary>
        Task<OrderResponse> PlaatsBestellingAsync(Bestelling bestelling);

        /// <summary>
        /// Verwijdert een bestelling via de backend API
        /// De backend publiceert naar RabbitMQ bestelling_verwijderd queue
        /// </summary>
        Task<bool> DeleteOrderAsync(int orderId, string reason = "Verwijderd door gebruiker");

        /// <summary>
        /// Legacy methode voor directe RabbitMQ publishing (backwards compatibility)
        /// </summary>
        Task<bool> PlaatsBestelling(Bestelling bestelling);

        #endregion

        #region Klant Synchronisatie Methodes

        /// <summary>
        /// Synchroniseert een nieuwe klant naar de backend/RabbitMQ
        /// </summary>
        Task<bool> SyncKlantAangemaaktAsync(Klant klant);

        /// <summary>
        /// Synchroniseert een bijgewerkte klant naar de backend/RabbitMQ
        /// </summary>
        Task<bool> SyncKlantBijgewerktAsync(Klant klant);

        /// <summary>
        /// Synchroniseert een verwijderde klant naar de backend/RabbitMQ
        /// </summary>
        Task<bool> SyncKlantVerwijderdAsync(int klantId, string naam);

        #endregion
    }
}
