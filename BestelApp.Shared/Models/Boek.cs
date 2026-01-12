using SQLite;

namespace BestelApp.Shared.Models
{
    public class Boek
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Titel { get; set; }
        public string Auteur { get; set; }
        public decimal Prijs { get; set; }
    }
}
