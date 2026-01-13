using BestelApp.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BestelApp.Backend.Services
{
    public class SapIdocService : ISapIdocService
    {
        private readonly ILogger<SapIdocService> _logger;

        public SapIdocService(ILogger<SapIdocService> logger)
        {
            _logger = logger;
        }

        public async Task<XDocument> CreateIdocFromOrderAsync(Bestelling bestelling)
        {
            return await Task.Run(() =>
            {
                var bestelnummer = bestelling.GetBestelnummer();

                var idoc = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    new XElement("ORDERS05",
                        new XElement("IDOC",
                            new XAttribute("BEGIN", "1"),

                            // Control record
                            new XElement("EDI_DC40",
                                new XAttribute("SEGMENT", "1"),
                                new XElement("TABNAM", "EDI_DC40"),
                                new XElement("DOCNUM", bestelnummer),
                                new XElement("IDOCTYP", "ORDERS05"),
                                new XElement("MESTYP", "ORDERS"),
                                new XElement("SNDPOR", "BESTELAPP"),
                                new XElement("SNDPRT", "LS")
                            ),

                            // Order header
                            new XElement("E1EDK01",
                                new XAttribute("SEGMENT", "1"),
                                new XElement("CURCY", "EUR"),
                                new XElement("BELNR", bestelnummer),
                                new XElement("BSTKD", bestelnummer),
                                new XElement("KUNNR", bestelling.KlantId.ToString().PadLeft(10, '0')),
                                new XElement("AEDAT", bestelling.Datum.ToString("yyyyMMdd"))
                            ),

                            // Order items (POSEX begint bij 000010)
                            CreateOrderItems(bestelling)
                        )
                    )
                );

                _logger.LogInformation($"📄 IDoc gegenereerd voor bestelling {bestelnummer}");
                return idoc;
            });
        }

        private XElement[] CreateOrderItems(Bestelling bestelling)
        {
            if (bestelling.Items == null || !bestelling.Items.Any())
                return new XElement[0];

            var items = new System.Collections.Generic.List<XElement>();
            int position = 10;

            foreach (var item in bestelling.Items)
            {
                items.Add(new XElement("E1EDP01",
                    new XAttribute("SEGMENT", "1"),
                    new XElement("POSEX", position.ToString("D6")),
                    new XElement("MENGE", item.Aantal),
                    new XElement("MENEE", "ST"),
                    new XElement("NETPR", item.Prijs.ToString("F2")),
                    new XElement("PEINH", "1"),
                    new XElement("MATNR", item.BoekId.ToString().PadLeft(18, '0')), // SAP materiaalnummer
                    new XElement("ARKTX", item.BoekTitel ?? $"Boek {item.BoekId}")
                ));
                position += 10;
            }

            return items.ToArray();
        }

        public async Task<bool> SendIdocToSapAsync(XDocument idoc)
        {
            var docnum = idoc.Descendants("DOCNUM").FirstOrDefault()?.Value ?? "UNKNOWN";
            _logger.LogInformation($"📤 IDoc {docnum} naar SAP (mock)");

            // Hier zou je echte SAP connectie maken
            // Bijvoorbeeld: HTTP POST naar SAP PI/PO of RFC call

            await Task.Delay(500); // Simuleer SAP verwerking
            return true;
        }

        public async Task<string> ProcessIdocResponseAsync(string sapResponse)
        {
            _logger.LogInformation($"📥 SAP response ontvangen: {sapResponse}");
            await Task.Delay(100);
            return "IDoc succesvol verwerkt in SAP";
        }
    }
}