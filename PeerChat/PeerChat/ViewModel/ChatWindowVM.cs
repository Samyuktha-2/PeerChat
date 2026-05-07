using Microsoft.Win32;
using peerchat.viewmodel;
using PeerChat.Command;
using PeerChat.Model;
using PeerChat.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PeerChat.ViewModel
{
    public class ChatWindowVM : BaseVM
    {
        private TcpClient _client;
        private string _selectedUserName;
        private readonly MainVM _main;
        private readonly NetworkStream _stream;
        private string _peerName;

        private string _outGoingMessage;
        private bool _isAttachmentMenuOpen;
        private ImageSource _imagePreview;
        private bool isDark = false;
        private string _peerIp;

        private MemoryStream _videoStream;
        private FileStream _tempFileStream;
        private long _totalBytes;
        private long _receivedBytes;
        private string _videoFileName;
        private string _tempFilePath;
        private MessageModel _currentVideoMessage;
        public bool _isReceivingVideo = false;
        private bool _isPeerTyping;
         
        private bool _isConnected = true;
        private string _peerStatus = "Online";
        private bool _isProfileMenuOpen;
        private string _myName;
        private string _windowTitle;
        private bool _isDebugEnabled;
        private double _transferProgress;
        private bool _isTransferCompleted;
        private string _videoPath;

        public byte[] SelectedImageByte { get; set; }
        public string SelectedImageName { get; set; }

        public string SelectedVideoName { get; set; }
        public string SelectedVideoPath { get; set; }

        public string MyName
        {
            get => _myName;
            set
            {
                _myName = value;
                OnPropertyChanged(nameof(MyName));
            }
        }
        public string SelectedUserName
        {
            get => _selectedUserName;
            set
            {
                _selectedUserName = value;
                OnPropertyChanged(nameof(SelectedUserName));
            }
        }
        public string OutGoingMessage
        {
            get => _outGoingMessage;
            set
            {
                _outGoingMessage = value;
                OnPropertyChanged(nameof(OutGoingMessage));

                if (!string.IsNullOrWhiteSpace(value))
                    SendTypingStatus(true);
            }
        }
        public bool IsAttachmentMenuOpen
        {
            get => _isAttachmentMenuOpen;
            set
            {
                _isAttachmentMenuOpen = value;
                OnPropertyChanged(nameof(IsAttachmentMenuOpen));
            }
        }
        public ImageSource ImagePreview
        {
            get => _imagePreview;
            set
            {
                _imagePreview = value;
                OnPropertyChanged(nameof(ImagePreview));
            }
        }
        //public string VideoPreviewPath
        //{
        //    get => _videoPreviewPath;
        //    set
        //    {
        //        _videoPreviewPath = value;
        //        OnPropertyChanged(nameof(VideoPreviewPath));
        //    }
        //}
        public bool IsPeerTyping
        {
            get => _isPeerTyping;
            set
            {
                _isPeerTyping = value;
                OnPropertyChanged(nameof(IsPeerTyping));
            }
        }
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
            }
        }
        public string PeerStatus
        {
            get => _peerStatus; set
            {
                _peerStatus = value;
                OnPropertyChanged(nameof(PeerStatus));
            }
        }
        public bool IsProfileMenuOpen
        {
            get => _isProfileMenuOpen; set
            {
                _isProfileMenuOpen = value;
                OnPropertyChanged(nameof(IsProfileMenuOpen));
            }
        }
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
        public bool IsDebugEnabled
        {
            get => _isDebugEnabled;
            set
            {
                _isDebugEnabled = value;
                OnPropertyChanged(nameof(IsDebugEnabled));
            }
        }
        public double TransferProgress
        {
            get => _transferProgress;
            set
            {
                _transferProgress = value;
                OnPropertyChanged(nameof(TransferProgress));
            }
        }
        public bool IsTransferCompleted
        {
            get => _isTransferCompleted;
            set
            {
                _isTransferCompleted = value;
                OnPropertyChanged(nameof(IsTransferCompleted));
            }
        }
        public string VideoPath
        {
            get => _videoPath;
            set
            {
                _videoPath = value;
                OnPropertyChanged(nameof(VideoPath));
            }
        }

        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();
        public ObservableCollection<DebugModel> DebugLogs { get; set; } = new ObservableCollection<DebugModel>();

        public ICommand SendCommand { get; }
        public ICommand ShowAttachmentMenuCommand { get; }
        public ICommand ImageCommand { get; }
        public ICommand VideoCommand { get; }
        public ICommand ClearPreviewCommand { get; }
        public ICommand PlayVideoCommand { get; }
        public ICommand ProfileClickCommand { get; }
        public ICommand ThemeCommand { get; }
        public ICommand LogoutCommand { get; }

        public ChatWindowVM(string myName, string peerIp, MainVM main, TcpClient client)
        {
            MyName = myName;
            _main = main;
            _client = client;
            _stream = _client.GetStream();
            _peerIp = peerIp;

            Users.Add(new UserModel { UserName = MyName });

            StartRecieveLoop();
            _ = SendMyName();

            SendCommand = new RelayCommand(() => _ = SendMessage());

            ShowAttachmentMenuCommand = new RelayCommand(() =>
              {
                  IsAttachmentMenuOpen = true;
              });

            ImageCommand = new RelayCommand(() => _ = PickImage());

            //VideoCommand = new RelayCommand(() => _ = PickVideo());

            ClearPreviewCommand = new RelayCommand(() =>
              {
                  ImagePreview = null;
                  SelectedImageByte = null;
                  SelectedImageName = null;
              });

            PlayVideoCommand = new RelayCommandT<string>(PlayVideo);

            ProfileClickCommand = new RelayCommand(() =>
           {
               IsProfileMenuOpen = true;
           });

            ThemeCommand = new RelayCommand(ChangeTheme); 
        }

        private async Task SendMyName()
        {
            byte[] data = Encoding.UTF8.GetBytes(MyName);
            await MessageProtocol.SendFrameAsync(_stream, 0x06, data);
        }

        private void StartRecieveLoop()
        {
            Task.Run(async () =>
            {
                try
                {
                    var stream = _client.GetStream();

                    while (true)
                    {
                        var (type, payload) = await MessageProtocol.ReceiveFrameAsync(stream);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (type == 0x06)
                            {
                                string text = Encoding.UTF8.GetString(payload);
                                _peerName = text;
                                SelectedUserName = text;
                                UpdateTitle();
                                return;
                            }

                            //text
                            if (type == 0x01)
                            {
                                string text = Encoding.UTF8.GetString(payload);
                                Messages.Add(new MessageModel
                                {
                                    GetDateTime = DateTime.Now,
                                    Text = text,
                                    IsSentByMe = false
                                });
                                AddLog(DateTime.Now, "Received", "Text", $"{payload.Length} bytes");
                            }
                            else if (type == 0x02) //image
                            {
                                using (var ms = new MemoryStream(payload))
                                using (var reader = new BinaryReader(ms))
                                {
                                    int nameLength = reader.ReadInt32();
                                    byte[] nameBytes = reader.ReadBytes(nameLength);
                                    string fileName = Encoding.UTF8.GetString(nameBytes);

                                    byte[] imageBytes = reader.ReadBytes((int)(ms.Length - ms.Position));

                                    using (var imgStream = new MemoryStream(imageBytes))
                                    {
                                        var bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.StreamSource = imgStream;
                                        bitmap.EndInit();
                                        bitmap.Freeze();

                                        Messages.Add(new MessageModel
                                        {
                                            GetDateTime = DateTime.Now,
                                            Image = bitmap,
                                            FileName = fileName,
                                            IsSentByMe = false
                                        });
                                    }
                                    AddLog(DateTime.Now, "Received", "Image", $"{payload.Length} bytes");
                                }
                            }
                            else if (type == 0x03) // video
                            {
                                using (var ms = new MemoryStream(payload))
                                using (var reader = new BinaryReader(ms))
                                {
                                    // filename (260 bytes)
                                    byte[] nameBytes = reader.ReadBytes(260);

                                    string fileName = Encoding.UTF8.GetString(nameBytes).Replace("\0", "").Trim();

                                    // filesize (8 bytes)
                                    byte[] sizeBytes = reader.ReadBytes(8);

                                    if (BitConverter.IsLittleEndian)
                                        Array.Reverse(sizeBytes);

                                    long totalSize = BitConverter.ToInt64(sizeBytes, 0);

                                    // video bytes
                                    byte[] videoBytes = reader.ReadBytes((int)(ms.Length - ms.Position));

                                    // create temp playable file
                                    string tempVideoPath = Path.Combine(Path.GetTempPath(), fileName);

                                    File.WriteAllBytes(tempVideoPath, videoBytes);

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Messages.Add(new MessageModel
                                        {
                                            GetDateTime = DateTime.Now,

                                            Text = fileName,
                                            FileName = fileName,

                                            IsVideo = true,
                                            IsSentByMe = false,

                                            FileBytes = videoBytes,

                                            VideoPath = tempVideoPath,

                                            TransferProgress = 100,
                                            IsTransferCompleted = true
                                        });
                                    });

                                    AddLog(DateTime.Now,
                                           "Received",
                                           "Video",
                                           $"{totalSize} bytes");
                                }
                            }

                            //typing status
                            if (type == 0x04)
                            {
                                bool isTyping = payload.Length > 0 && payload[0] == 1;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    IsPeerTyping = isTyping;
                                });
                            }

                            //disconnect
                            if (type == 0x05)
                            {
                                HandlePeerDisconnected();
                                AddLog(DateTime.Now, "Recieved", "Peer Status", "null");
                                return;
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Recieve Error:" + ex.Message);
                }
            });
        }

        private async Task SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(OutGoingMessage))
            {
                await SendTextMessage();
            }

            if (SelectedImageByte != null)
            {
                await SendImage();
                return;
            }

        }

        private async Task SendTextMessage()
        {
            if (!IsConnected || _client == null || !_client.Connected)
                return;

            var msg = OutGoingMessage;

            byte[] data = Encoding.UTF8.GetBytes(msg);
            await MessageProtocol.SendFrameAsync(_stream, 0x01, data);
            AddLog(DateTime.Now, "Sent", "Text", $"{data.Length} bytes");

            Messages.Add(new MessageModel
            {
                GetDateTime = DateTime.Now,
                Text = msg,
                IsSentByMe = true
            });

            OutGoingMessage = string.Empty;
            await SendTypingStatus(false);
        }

        private async Task PickImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.png;*.jpeg;*.bmp"
            };

            if (dialog.ShowDialog() != true) return;

            SelectedImageByte = File.ReadAllBytes(dialog.FileName);
            SelectedImageName = Path.GetFileName(dialog.FileName);

            using (var ms = new MemoryStream(SelectedImageByte))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImagePreview = bitmap;
            }
        }

        private async Task SendImage()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(SelectedImageName);

                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);

                writer.Write(SelectedImageByte);

                await MessageProtocol.SendFrameAsync(_stream, 0x02, ms.ToArray());
                AddLog(DateTime.Now, "Sent", "Image", $"{SelectedImageByte.Length} bytes");
            }

            using (var ms = new MemoryStream(SelectedImageByte))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                Messages.Add(new MessageModel
                {
                    GetDateTime = DateTime.Now,
                    Image = bitmap,
                    FileName = SelectedImageName,
                    IsSentByMe = true
                });
            }

            ImagePreview = null;
            SelectedImageByte = null;
            SelectedImageName = null;
        }

        private async Task PickVideo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Videos|*.mp4;*.avi;*.mkv"
            };

            if (dialog.ShowDialog() != true) return;

            SelectedVideoName = Path.GetFileName(dialog.FileName);
            SelectedVideoPath = dialog.FileName;

            OnPropertyChanged(nameof(SelectedVideoName));
            OnPropertyChanged(nameof(SelectedVideoPath));
            await SendVideo();
        }

        private async Task SendVideo()
        {
            if (string.IsNullOrEmpty(SelectedVideoPath))
                return;

            FileInfo fileInfo = new FileInfo(SelectedVideoPath);

            //< 50MB
            if (fileInfo.Length > 50 * 1024 * 1024)
            {
                MessageBox.Show("Video exceeds 50 MB");
                return;
            }

            var stream = _client.GetStream();

            FileStream fs = new FileStream(SelectedVideoPath, FileMode.Open, FileAccess.Read);
            MemoryStream ms = new MemoryStream();

            await fs.CopyToAsync(ms);

            byte[] videoBytes = ms.ToArray();

            byte[] payload = new byte[260 + 8 + videoBytes.Length];

            byte[] nameBytes = Encoding.UTF8.GetBytes(SelectedVideoName);

            Array.Copy(nameBytes, payload, Math.Min(260, nameBytes.Length));

            byte[] sizeBytes =
                BitConverter.GetBytes(fileInfo.Length);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeBytes);

            Array.Copy(sizeBytes, 0, payload, 260, 8);

            // video bytes
            Array.Copy(videoBytes, 0, payload, 268, videoBytes.Length);

            await MessageProtocol.SendFrameAsync(stream, 0x03, payload);

            Messages.Add(new MessageModel
            {
                GetDateTime = DateTime.Now,
                Text = SelectedVideoName,
                FileName = SelectedVideoName,
                IsVideo = true,
                IsSentByMe = true,
                FileBytes = videoBytes,
                VideoPath = SelectedVideoPath,
                IsTransferCompleted = true,
                TransferProgress = 100
            });
            AddLog(DateTime.Now, "Sent", "Video", $"{videoBytes.Length} bytes");

        }

        private void PlayVideo(string path)
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        } 

        public async Task SendTypingStatus(bool isTyping)
        {
            byte[] payload = new byte[1];
            payload[0] = isTyping ? (byte)1 : (byte)0;

            await MessageProtocol.SendFrameAsync(_stream, 0x04, payload);
        }

        public async Task HandleLocalClosingAsync()
        {
            if (!_isConnected) return;

            await SendDisconnectAsync();

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }

            IsConnected = false;
        }

        private async Task SendDisconnectAsync()
        {
            try
            {
                await MessageProtocol.SendFrameAsync(_stream, 0x05, Array.Empty<byte>());
            }
            catch { }
        }

        private void HandlePeerDisconnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new MessageModel
                {
                    GetDateTime = DateTime.Now,
                    Text = "Peer has disconnected",
                    IsSystemMessage = true
                });
                PeerStatus = "Offline";
                IsConnected = false;
            });

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }
        }

        private void ChangeTheme()
        {
            var app = Application.Current;
            var dictionaries = app.Resources.MergedDictionaries;

            dictionaries[0] = new ResourceDictionary()
            {
                Source = new Uri(
                    isDark ? "Theme/LightTheme.xaml"
                           : "Theme/DarkTheme.xaml",
                    UriKind.Relative)
            };

            isDark = !isDark;
        }

        private void UpdateTitle()
        {
            Application.Current.MainWindow.Title = $"PeerChat — {_myName} ↔ {_peerIp}({SelectedUserName})";
        }

        private void AddLog(DateTime date, string direction, string type, string size)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DebugLogs.Add(new DebugModel
                {
                    GetDateTime = date,
                    Direction = direction,
                    ContentType = type,
                    ContentSize = size
                });
            });
        }
    }
} 