using BestelApp.Views;

namespace BestelApp
{
    public partial class AppShell : Shell
    {
        public AppShell(BestellingPage bestellingPage)
        {
            InitializeComponent();

            // Set the initial page directly
            Items.Clear();
            Items.Add(new ShellContent
            {
                Title = "BestelApp",
                Route = "BestellingPage",
                Content = bestellingPage
            });
        }
    }
}
