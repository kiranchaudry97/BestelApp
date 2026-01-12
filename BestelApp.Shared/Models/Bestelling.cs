using SQLite;

namespace BestelApp.Shared.Models
{
    public class Bestelling
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int KlantId { get; set; }
        
        public DateTime Datum { get; set; }
        public decimal Totaal { get; set; }

        [Ignore]
        public List<BestellingItem> Items { get; set; } = new List<BestellingItem>();
    }
}
