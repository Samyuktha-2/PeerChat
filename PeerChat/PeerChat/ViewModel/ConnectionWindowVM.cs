using peerchat.viewmodel;
using PeerChat.Command;
using PeerChat.Model;
using PeerChat.Services;
using PeerChat.View;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PeerChat.ViewModel
{
    public class ConnectionWindowVM : BaseVM
    {
        private string _displayUserName = Environment.UserName;
        private string _iPAddressText;
        private string _port = "9000";
        private string _statusMessage;
        private bool _isValidIP;
        private bool _isValidDisplayName;
        private bool _isValidPort;
        private bool isWaiting;
        private bool isHosting;

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
        public bool IsWaiting
        {
            get => isWaiting;
            set
            {
                isWaiting = value;
                OnPropertyChanged(nameof(IsWaiting));
            }
        }
        public bool IsHosting
        {
            get => isHosting;
            set
            {
                isHosting = value;
                OnPropertyChanged(nameof(IsHosting));
                OnPropertyChanged(nameof(IsNotHoisting));
            }
        }
        public bool IsNotHoisting => !IsHosting;
        public bool IsHost
        {
            get => _isHost;
            set
            {
                _isHost = value;
                OnPropertyChanged(nameof(IsHost));
            }
        }
        public bool IsClient
        {
            get => _isClient;
            set
            {
                _isClient = value;
                OnPropertyChanged(nameof(IsClient));
            }
        } 

        private CancellationTokenSource _cts;
        private bool _isHost;
        private bool _isClient; 
        private readonly MainVM _main;
        private readonly TcpClient _client;

        private readonly NetworkService _service = new NetworkService();


        public ICommand JoinCommand { get; }
        public ICommand CancelCommand { get; }

        public ConnectionWindowVM(MainVM main)
        {
            _main = main;

            JoinCommand = new RelayCommand(() => _ = Join());
            CancelCommand = new RelayCommand(CancelOperation);

            IPAddressText = GetLocalIPAddress(); ;
        }

        private async Task Join()
        {
            if (IsHost)
            {
                await StartHosting();
            }
            else if (IsClient)
            {
                await StartJoining();
            }
            else
            {
                StatusMessage = "Select mode to join";
            }
        }

        private async Task StartHosting()
        {
            if (!ValidateAll()) return;

            _cts = new CancellationTokenSource();

            IsHosting = true;
            IsWaiting = true;
            StatusMessage = "Waiting for peer to connect...";

            try
            {
                if (!int.TryParse(Port, out int portNumber))
                {
                    StatusMessage = "Invalid Port";
                    return;
                }

                TcpClient client = await _service.StartHostAsync(portNumber, _cts.Token);

                StatusMessage = "Connected"; 
                _main.CurrentView = new ChatWindowVM(DisplayUserName, IPAddressText, _main, client);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Cancelled";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsWaiting = false;
                IsHosting = false;
            }
        }

        private async Task StartJoining()
        {
            if (!ValidateAll()) return;

            StatusMessage = "Connecting...";
            IsWaiting = true;
            IsHosting = true;

            try
            {
                if (!IPAddress.TryParse(IPAddressText, out _))
                {
                    StatusMessage = "Invalid IP Address";
                    return;
                }

                if (!int.TryParse(Port, out int portNumber))
                {
                    StatusMessage = "Invalid Port";
                    return;
                }

                TcpClient client = await _service.ConnectAsync(IPAddressText, portNumber);

                StatusMessage = "Connected";

                _main.CurrentView = new ChatWindowVM(DisplayUserName, IPAddressText, _main, client);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
            }
            finally
            {
                IsWaiting = false;
                IsHosting = false;
            }
        }

        private void CancelOperation()
        {
            _cts?.Cancel();
            StatusMessage = "Operation cancelled";
            IsWaiting = false;
            IsHosting = false;
        }

        private string GetLocalIPAddress()
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                      .AddressList
                      .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                      .ToString();
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

        private async Task SendMyName()
        {
            var stream = _client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(DisplayUserName);

            await MessageProtocol.SendFrameAsync(stream, 0x06, data);
        }



    }
}
