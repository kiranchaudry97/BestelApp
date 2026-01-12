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
        
        /// <summary>
        /// Boektitel voor weergave in berichten (niet opgeslagen in DB)
        /// </summary>
        [Ignore]
        public string BoekTitel { get; set; } = string.Empty;
        
        public int Aantal { get; set; }
        public decimal Prijs { get; set; }
    }
}
