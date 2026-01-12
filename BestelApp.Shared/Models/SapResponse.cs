namespace BestelApp.Shared.Models
{
    /// <summary>
    /// SAP iDoc response model
    /// </summary>
    public class SapResponse
    {
        public string DocumentNumber { get; set; }
        public int Status { get; set; }
        public string StockCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsSuccess { get; set; }
    }
}
