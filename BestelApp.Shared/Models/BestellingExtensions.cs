using System;
using System.Linq;

namespace BestelApp.Shared.Models
{
    public static class BestellingExtensions
    {
        public static string GetBestelnummer(this Bestelling bestelling)
        {
            return $"BEST-{bestelling.Id.ToString().PadLeft(6, '0')}";
        }

        public static string GetKlantnaam(this Bestelling bestelling)
        {
            return bestelling.KlantNaam ?? $"Klant-{bestelling.KlantId}";
        }

        public static string GetProductenSamenvatting(this Bestelling bestelling)
        {
            if (bestelling.Items == null || !bestelling.Items.Any())
                return "Geen producten";

            var producten = bestelling.Items
                .Select(i => $"{i.BoekTitel} ({i.Aantal}x)")
                .ToList();

            return string.Join(", ", producten.Take(3)) +
                   (producten.Count > 3 ? $" + {producten.Count - 3} meer" : "");
        }

        public static int GetTotaalAantal(this Bestelling bestelling)
        {
            return bestelling.Items?.Sum(i => i.Aantal) ?? 0;
        }

        public static decimal GetTotaalPrijs(this Bestelling bestelling)
        {
            return bestelling.Items?.Sum(i => i.Aantal * i.Prijs) ?? bestelling.Totaal;
        }
    }
}