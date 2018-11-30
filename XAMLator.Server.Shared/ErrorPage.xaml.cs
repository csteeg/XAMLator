using System.ComponentModel;
using System.Windows.Input;
using Xamarin.Forms;

namespace XAMLator.Server
{
    public partial class ErrorPage : ContentPage, INotifyPropertyChanged
    {
        private ICommand _closeCommand;

        public ErrorPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand CloseCommand
        {
            get => _closeCommand;
            set
            {
                _closeCommand = value;
                EmitPropertyChanged(nameof(CloseCommand));
            }
        }

        void EmitPropertyChanged(string v)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(v));
        }
    }
}
