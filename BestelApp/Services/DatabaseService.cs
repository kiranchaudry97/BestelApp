using BestelApp.Shared.Models;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace BestelApp.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;

        public DatabaseService()
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "BestelApp.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            // We don't await initialization here to avoid blocking the constructor.
            // A separate async Init method will be called.
        }

        public async Task Init()
        {
            await _database.CreateTableAsync<Klant>();
            await _database.CreateTableAsync<Boek>();
            await _database.CreateTableAsync<Bestelling>();
            await _database.CreateTableAsync<BestellingItem>();

            // Seed data if tables are empty
            if (await _database.Table<Klant>().CountAsync() == 0)
            {
                await SeedData();
            }
        }

        private async Task SeedData()
        {
            var klanten = new List<Klant>
            {
                new Klant { Naam = "Jan Jansen", Email = "jan.jansen@example.com" },
                new Klant { Naam = "Piet Pietersen", Email = "piet.pietersen@example.com" },
                new Klant { Naam = "Klaas Klaassen", Email = "klaas.klaassen@example.com" }
            };
            await _database.InsertAllAsync(klanten);

            var boeken = new List<Boek>
            {
                new Boek { Titel = "C# in Depth", Auteur = "Jon Skeet", Prijs = 49.99m },
                new Boek { Titel = "Clean Code", Auteur = "Robert C. Martin", Prijs = 39.99m },
                new Boek { Titel = "The Pragmatic Programmer", Auteur = "Andrew Hunt", Prijs = 44.99m }
            };
            await _database.InsertAllAsync(boeken);
        }

        // Klant CRUD
        public Task<List<Klant>> GetKlantenAsync() => _database.Table<Klant>().ToListAsync();
        public Task<Klant> GetKlantAsync(int id) => _database.Table<Klant>().Where(k => k.Id == id).FirstOrDefaultAsync();
        public Task<int> SaveKlantAsync(Klant klant)
        {
            if (klant.Id != 0)
            {
                return _database.UpdateAsync(klant);
            }
            else
            {
                return _database.InsertAsync(klant);
            }
        }
        public Task<int> DeleteKlantAsync(Klant klant) => _database.DeleteAsync(klant);

        // Boek CRUD
        public Task<List<Boek>> GetBoekenAsync() => _database.Table<Boek>().ToListAsync();

        // Bestelling CRUD
        public async Task<List<Bestelling>> GetBestellingenAsync()
        {
            var bestellingen = await _database.Table<Bestelling>().OrderByDescending(b => b.Datum).ToListAsync();
            foreach (var bestelling in bestellingen)
            {
                bestelling.Items = await _database.Table<BestellingItem>().Where(i => i.BestellingId == bestelling.Id).ToListAsync();
            }
            return bestellingen;
        }

        public async Task<int> SaveBestellingAsync(Bestelling bestelling)
        {
            if (bestelling.Id != 0)
            {
                await _database.UpdateAsync(bestelling);
            }
            else
            {
                await _database.InsertAsync(bestelling);
            }

            foreach (var item in bestelling.Items)
            {
                item.BestellingId = bestelling.Id;
                await _database.InsertAsync(item);
            }
            return bestelling.Id;
        }

        public async Task<int> DeleteBestellingAsync(Bestelling bestelling)
        {
            // First delete associated items
            await _database.Table<BestellingItem>().DeleteAsync(i => i.BestellingId == bestelling.Id);
            // Then delete the order itself
            return await _database.DeleteAsync(bestelling);
        }
    }
}
