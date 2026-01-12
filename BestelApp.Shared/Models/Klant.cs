using SQLite;

namespace BestelApp.Shared.Models
{
    public class Klant
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Naam { get; set; }
        public string Email { get; set; }
    }
}
