using BestelApp.Shared.Models;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace BestelApp.Backend.Services
{
    public interface ISapIdocService
    {
        Task<XDocument> CreateIdocFromOrderAsync(Bestelling bestelling);
        Task<bool> SendIdocToSapAsync(XDocument idoc);
        Task<string> ProcessIdocResponseAsync(string sapResponse);
    }
}