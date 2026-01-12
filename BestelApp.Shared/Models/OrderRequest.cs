namespace BestelApp.Shared.Models
{
    /// <summary>
    /// Request model sent from MAUI app to Backend API
    /// </summary>
    public class OrderRequest
    {
        public string ApiKey { get; set; }
        public Bestelling Order { get; set; }
        public DateTime RequestDateTime { get; set; }
        public string RequestId { get; set; }
    }
}
