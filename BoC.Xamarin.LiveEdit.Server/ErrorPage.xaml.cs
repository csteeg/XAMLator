using System.ComponentModel;
using System.Windows.Input;
using Xamarin.Forms;

namespace XAMLator.Server
{
	public partial class ErrorPage : ContentPage
	{
		private ICommand _closeCommand;

		public ErrorPage()
		{
			InitializeComponent();
		}

		public ICommand CloseCommand
		{
			get => _closeCommand;
			set
			{
				_closeCommand = value;
				OnPropertyChanged(nameof(CloseCommand));
			}
		}
	}
}
