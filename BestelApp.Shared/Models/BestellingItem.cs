using SQLite;

namespace BestelApp.Shared.Models
{
    public class BestellingItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        
        [Indexed]
        public int BestellingId { get; set; }
        
        public int BoekId { get; set; }
        public int Aantal { get; set; }
        public decimal Prijs { get; set; }
    }
}
