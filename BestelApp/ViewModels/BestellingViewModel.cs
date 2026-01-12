using BestelApp.Shared.Models;
using BestelApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace BestelApp.ViewModels
{
    /// <summary>
    /// ViewModel voor bestelling beheer met RabbitMQ synchronisatie
    /// </summary>
    public partial class BestellingViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly IOrderService _orderService;

        // Eigenschappen voor Bestelling Beheer
        [ObservableProperty]
        private ObservableCollection<Bestelling> _bestellingen;
        [ObservableProperty]
        private ObservableCollection<Boek> _winkelmandje;
        [ObservableProperty]
        private Boek? _selectedBoekForOrder;
        [ObservableProperty]
        private Klant? _selectedKlantForOrder;
        [ObservableProperty]
        private decimal _totaalWinkelmandje;

        // Eigenschappen voor Klant Beheer
        [ObservableProperty]
        private ObservableCollection<Klant> _klanten;
        [ObservableProperty]
        private ObservableCollection<Boek> _boeken;
        [ObservableProperty]
        private Klant? _selectedKlantForEdit;
        [ObservableProperty]
        private string _naam = string.Empty;
        [ObservableProperty]
        private string _email = string.Empty;

        // Bestelling bevestiging eigenschappen
        [ObservableProperty]
        private string _lastOrderStatus = string.Empty;
        [ObservableProperty]
        private bool _isProcessingOrder;


        public BestellingViewModel(DatabaseService databaseService, IOrderService orderService)
        {
            _databaseService = databaseService;
            _orderService = orderService;

            Klanten = new ObservableCollection<Klant>();
            Boeken = new ObservableCollection<Boek>();
            Bestellingen = new ObservableCollection<Bestelling>();
            Winkelmandje = new ObservableCollection<Boek>();

            // Load data asynchronously without blocking the constructor
            _ = LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await Task.Delay(500); // Give time for database initialization
                await LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            }
        }

        partial void OnSelectedKlantForEditChanged(Klant value)
        {
            if (value != null)
            {
                Naam = value.Naam;
                Email = value.Email;
            }
        }

        [RelayCommand]
        private async Task LoadData()
        {
            var klanten = await _databaseService.GetKlantenAsync();
            Klanten.Clear();
            foreach (var k in klanten) Klanten.Add(k);

            var boeken = await _databaseService.GetBoekenAsync();
            Boeken.Clear();
            foreach (var b in boeken) Boeken.Add(b);

            var bestellingen = await _databaseService.GetBestellingenAsync();
            Bestellingen.Clear();
            foreach (var b in bestellingen) Bestellingen.Add(b);
        }

        // --- Klant Beheer Commando's ---
        [RelayCommand]
        private async Task SaveKlant()
        {
            if (string.IsNullOrWhiteSpace(Naam) || string.IsNullOrWhiteSpace(Email))
                return;

            bool isNieuweKlant = SelectedKlantForEdit == null;
            Klant klantToSave = SelectedKlantForEdit ?? new Klant();
            klantToSave.Naam = Naam;
            klantToSave.Email = Email;

            // 1. Opslaan in lokale SQLite database
            await _databaseService.SaveKlantAsync(klantToSave);

            // 2. Synchroniseren naar backend/RabbitMQ
            try
            {
                if (isNieuweKlant)
                {
                    var synced = await _orderService.SyncKlantAangemaaktAsync(klantToSave);
                    if (synced)
                        System.Diagnostics.Debug.WriteLine($"Klant {klantToSave.Naam} aangemaakt en gesynchroniseerd naar RabbitMQ");
                }
                else
                {
                    var synced = await _orderService.SyncKlantBijgewerktAsync(klantToSave);
                    if (synced)
                        System.Diagnostics.Debug.WriteLine($"Klant {klantToSave.Naam} bijgewerkt en gesynchroniseerd naar RabbitMQ");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backend sync mislukt (lokaal wel opgeslagen): {ex.Message}");
            }

            await LoadData();
            ClearKlantForm();
        }

        [RelayCommand]
        private async Task DeleteKlant(Klant klant)
        {
            if (klant != null)
            {
                // 1. Synchroniseren naar backend/RabbitMQ
                try
                {
                    var synced = await _orderService.SyncKlantVerwijderdAsync(klant.Id, klant.Naam);
                    if (synced)
                        System.Diagnostics.Debug.WriteLine($"Klant {klant.Naam} verwijdering gesynchroniseerd naar RabbitMQ");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Backend sync mislukt: {ex.Message}");
                }

                // 2. Verwijderen uit lokale SQLite database
                await _databaseService.DeleteKlantAsync(klant);
                await LoadData();
            }
        }

        [RelayCommand]
        private void ClearKlantForm()
        {
            SelectedKlantForEdit = null;
            Naam = string.Empty;
            Email = string.Empty;
        }


        // --- Order Management Commands ---
        [RelayCommand]
        private void AddToWinkelmandje()
        {
            if (SelectedBoekForOrder != null)
            {
                Winkelmandje.Add(SelectedBoekForOrder);
                CalculateTotal();
            }
        }

        [RelayCommand]
        private void RemoveFromWinkelmandje(Boek boek)
        {
            if (boek != null)
            {
                Winkelmandje.Remove(boek);
                CalculateTotal();
            }
        }

        [RelayCommand]
        private void ClearWinkelmandje()
        {
            Winkelmandje.Clear();
            SelectedBoekForOrder = null;
            SelectedKlantForOrder = null;
            CalculateTotal();
        }
        
        private void CalculateTotal()
        {
            TotaalWinkelmandje = Winkelmandje.Sum(b => b.Prijs);
        }

        [RelayCommand]
        private async Task PlaatsBestelling()
        {
            if (SelectedKlantForOrder == null || Winkelmandje.Count == 0) return;

            IsProcessingOrder = true;
            LastOrderStatus = "Bestelling wordt verwerkt...";

            try
            {
                var nieuweBestelling = new Bestelling
                {
                    KlantId = SelectedKlantForOrder.Id,
                    KlantNaam = SelectedKlantForOrder.Naam,
                    Datum = DateTime.Now,
                    Totaal = TotaalWinkelmandje
                };

                var items = Winkelmandje.GroupBy(b => b.Id)
                                         .Select(g => new BestellingItem
                                         {
                                             BoekId = g.Key,
                                             BoekTitel = g.First().Titel,
                                             Aantal = g.Count(),
                                             Prijs = g.First().Prijs
                                         }).ToList();

                nieuweBestelling.Items.AddRange(items);

                // Save to local database first
                await _databaseService.SaveBestellingAsync(nieuweBestelling);

                // Send to backend API (handles RabbitMQ + SAP iDoc)
                var response = await _orderService.PlaatsBestellingAsync(nieuweBestelling);

                // Show order confirmation with both Salesforce and SAP info
                if (response.IsSuccess)
                {
                    LastOrderStatus = $"? Bestelling geplaatst!\n" +
                                    $"Salesforce ID: {response.SalesforceOrderId}\n" +
                                    $"SAP Document: {response.SapDocumentNumber}\n" +
                                    $"SAP Status: {GetSapStatusText(response.SapStatus)}\n" +
                                    $"Voorraad Code: {response.StockCode}";
                }
                else
                {
                    LastOrderStatus = $"? Fout: {response.ErrorMessage ?? "Onbekende fout"}";
                }

                Winkelmandje.Clear();
                CalculateTotal();
                await LoadData();
            }
            catch (Exception ex)
            {
                LastOrderStatus = $"? Fout bij plaatsen bestelling: {ex.Message}";
            }
            finally
            {
                IsProcessingOrder = false;
            }
        }

        private string GetSapStatusText(int status)
        {
            return status switch
            {
                53 => "53 - Succesvol verwerkt",
                51 => "51 - Fout bij verwerking",
                64 => "64 - Klaar voor verwerking",
                _ => $"{status} - Onbekende status"
            };
        }

        [RelayCommand]
        private async Task DeleteBestelling(Bestelling bestelling)
        {
            if (bestelling != null)
            {
                try
                {
                    // 1. Send delete to backend API (publishes to RabbitMQ order_deletes queue)
                    var backendDeleted = await _orderService.DeleteOrderAsync(bestelling.Id, "Verwijderd door verkoper");
                    
                    if (backendDeleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"Order {bestelling.Id} delete published to RabbitMQ");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Backend unavailable, order {bestelling.Id} only deleted locally");
                    }

                    // 2. Delete from local SQLite database
                    await _databaseService.DeleteBestellingAsync(bestelling);
                    
                    // 3. Refresh the UI
                    await LoadData();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting order: {ex.Message}");
                    // Still delete locally even if backend fails
                    await _databaseService.DeleteBestellingAsync(bestelling);
                    await LoadData();
                }
            }
        }
    }
}
