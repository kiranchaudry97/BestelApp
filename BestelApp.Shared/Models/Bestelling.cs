using SQLite;

namespace BestelApp.Shared.Models
{
    public class Bestelling
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int KlantId { get; set; }
        
        /// <summary>
        /// Klantnaam voor weergave in berichten (niet opgeslagen in DB)
        /// </summary>
        [Ignore]
        public string KlantNaam { get; set; } = string.Empty;
        
        public DateTime Datum { get; set; }
        public decimal Totaal { get; set; }

        [Ignore]
        public List<BestellingItem> Items { get; set; } = new List<BestellingItem>();
    }
}
