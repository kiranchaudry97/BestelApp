namespace BestelApp.Shared.Models
{
    /// <summary>
    /// Response model sent from Backend API to MAUI app
    /// Contains both Salesforce and SAP information
    /// </summary>
    public class OrderResponse
    {
        public string SalesforceOrderId { get; set; }
        public string SapDocumentNumber { get; set; }
        public int SapStatus { get; set; }
        public string StatusMessage { get; set; }
        public bool IsSuccess { get; set; }
        public string StockCode { get; set; }
        public DateTime ResponseDateTime { get; set; }
        public string ErrorMessage { get; set; }
    }
}
