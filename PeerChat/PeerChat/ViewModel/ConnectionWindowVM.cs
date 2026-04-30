using PeerChat.Command;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace PeerChat.ViewModel
{
    public class ConnectionWindowVM : BaseVM
    {
        private string _displayUserName;
        private string _iPAddressText;
        private string _port = "9000";
        private string _statusMessage;
        private bool _isValidIP;
        private bool _isValidDisplayName;
        private bool _isValidPort;

        public string DisplayUserName
        {
            get => _displayUserName;
            set
            {
                _displayUserName = value;
                OnPropertyChanged(nameof(DisplayUserName)); 
            }
        }
        public string IPAddressText
        {
            get => _iPAddressText;
            set
            {
                _iPAddressText = value;
                OnPropertyChanged(nameof(IPAddressText)); 
            }
        }
        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port)); 
            }
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
        public bool IsValidIP
        {
            get => _isValidIP;
            set
            {
                _isValidIP = value;
                OnPropertyChanged(nameof(IsValidIP));
            }
        }
        public bool IsValidDisplayName
        {
            get => _isValidDisplayName;
            set
            {
                _isValidDisplayName = value;
                OnPropertyChanged(nameof(IsValidDisplayName));
            }
        }
        public bool IsValidPort
        {
            get => _isValidPort;
            set
            {
                _isValidPort = value;
                OnPropertyChanged(nameof(IsValidPort));
            }
        }

        public ICommand HostCommand { get; }
        public ICommand JoinCommand { get; }

        public ConnectionWindowVM()
        {
            HostCommand = new RelayCommand(OnHostClick);
            JoinCommand = new RelayCommand(OnJoinClick);
        }

        private void OnHostClick()
        {
            if (!ValidateAll())
            {
                return;
            }
            StatusMessage = "Host Clicked";
        }

        private void OnJoinClick()
        {
            StatusMessage = "Join click";
        }

        private bool ValidateAll()
        {
            if (!string.IsNullOrWhiteSpace(DisplayUserName) && Regex.IsMatch(DisplayUserName, @"^\d+$"))
            {
                StatusMessage = "Display name cannot be only numbers";
                return false;
            }

            if (string.IsNullOrWhiteSpace(DisplayUserName))
            {
                StatusMessage = "Display name required";
                return false;
            }

            if (!IPAddress.TryParse(IPAddressText, out _))
            {
                StatusMessage = "Enter valid IP Address";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(Port) && !Port.All(char.IsDigit))
            {
                StatusMessage = "Port must be numeric";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Port))
            {
                StatusMessage = "Port required";
                return false;
            } 
            return true;
        }


    }
}
