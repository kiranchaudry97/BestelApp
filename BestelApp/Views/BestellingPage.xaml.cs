using BestelApp.ViewModels;

namespace BestelApp.Views;

public partial class BestellingPage : ContentPage
{
	public BestellingPage(BestellingViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
