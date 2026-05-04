using PeerChat.Command;
using PeerChat.Model;
using PeerChat.Services;
using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PeerChat.ViewModel
{
    public class ChatWindowVM : BaseVM
    {
        private TcpClient _client;
        private string selectedUserName;
        private string _outgoingMessage;
        private readonly MainVM _main;
        private readonly string _myName;
        private string _peerName;
        private readonly NetworkStream _stream;
        private bool _isTyping;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private DispatcherTimer _typingTimer;
        private bool _isPeerTyping;
        private DispatcherTimer _peerTypingTimer;

        public string SelectedUserName
        {
            get => selectedUserName;
            set
            {
                selectedUserName = value;
                OnPropertyChanged(nameof(SelectedUserName));
            }
        }
        public string OutgoingMessage
        {
            get => _outgoingMessage;
            set
            {
                _outgoingMessage = value;
                OnPropertyChanged(nameof(OutgoingMessage));
                OnUserTyping();
            }
        }
        public bool IsPeerTyping
        {
            get => _isPeerTyping;
            set
            {
                _isPeerTyping = value;
                OnPropertyChanged(nameof(IsPeerTyping));
            }
        }




        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();

        public ICommand SendCommand { get; }
        public ICommand AttachmentCommand { get; }

        public ChatWindowVM(string myName, MainVM main, TcpClient client)
        {
            _main = main;
            _client = client;
            _myName = myName;
            _stream = _client.GetStream();

            Users.Add(new UserModel { UserName = _myName });

            _typingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _typingTimer.Tick += async (s, e) =>
            {
                _typingTimer.Stop();
                await SendTyping(false); // stopped typing
            };
            _peerTypingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _peerTypingTimer.Tick += (s, e) =>
            {
                _peerTypingTimer.Stop();
                IsPeerTyping = false;
            };


            StartReceiveLoop();
            _ = SendMyName();

            SendCommand = new RelayCommand(() => _ = SendTextMessage());
            //AttachmentCommand = new RelayCommand();
        }

        private async Task SendTextMessage()
        {
            if (string.IsNullOrWhiteSpace(OutgoingMessage))
                return;

            var stream = _client.GetStream();

            byte[] data = Encoding.UTF8.GetBytes(OutgoingMessage);

            await MessageProtocol.SendFrameAsync(_stream, 0x01, data);

            // UI update
            Messages.Add(new MessageModel
            {
                Text = OutgoingMessage,
                IsSentByMe = true
            });

            OutgoingMessage = string.Empty;
        }

        //private async Task SendImage()
        //{
        //    var dialog = new OpenFileDialog
        //    {
        //        Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
        //    };

        //    if (dialog.ShowDialog() != true)
        //        return;

        //    byte[] imageBytes = await File.ReadAllBytesAsync(dialog.FileName);

        //    byte[] payload = new byte[260 + imageBytes.Length];

        //    // filename (260 bytes)
        //    byte[] nameBytes = Encoding.UTF8.GetBytes(Path.GetFileName(dialog.FileName));
        //    Array.Copy(nameBytes, payload, Math.Min(260, nameBytes.Length));

        //    // image data
        //    Array.Copy(imageBytes, 0, payload, 260, imageBytes.Length);

        //    var stream = _client.GetStream();
        //    await MessageProtocol.SendFrameAsync(stream, 0x02, payload);

        //    Messages.Add(new MessageModel
        //    {
        //        Text = "[Image]",
        //        IsSentByMe = true
        //    });
        //} 

        private async Task SendMyName()
        {
            var stream = _client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(_myName);

            await MessageProtocol.SendFrameAsync(_stream, 0x06, data);
        }

        private void StartReceiveLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    var stream = _client.GetStream();

                    while (true)
                    {
                        var (type, payload) = await MessageProtocol.ReceiveFrameAsync(stream);

                        string text = Encoding.UTF8.GetString(payload);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (type == 0x06) // username
                            {
                                _peerName = text;
                                SelectedUserName = text;
                                return;
                            }

                            if (type == 0x01) // message
                            {
                                Messages.Add(new MessageModel
                                {
                                    Text = text,
                                    IsSentByMe = false
                                });
                            }

                            if (type == 0x04)
                            {
                                bool typing = payload[0] == 1;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    IsPeerTyping = typing;
                                });
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Receive error: " + ex.Message);
                }
            });
        }

        private async void OnUserTyping()
        {
            if ((DateTime.Now - _lastTypingSent).TotalMilliseconds > 500)
            {
                await SendTyping(true);
                _lastTypingSent = DateTime.Now;
            }

            _typingTimer.Stop();
            _typingTimer.Start();
        }

        private async Task SendTyping(bool isTyping)
        {
            if (_stream == null) return;
            byte[] payload = new byte[] { (byte)(isTyping ? 1 : 0) };
            await MessageProtocol.SendFrameAsync(_stream, 0x04, payload);
        }

    }
}
