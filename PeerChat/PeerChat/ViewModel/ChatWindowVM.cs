using Microsoft.Win32;
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
        private string _videoPreviewPath;
        private bool isDark = false;
        private string _myIp;
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

        public DispatcherTimer _typingHideTimer;
        public DispatcherTimer _typingStopTimer;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private bool _isConnected = true;
        private string _peerStatus = "Online";
        private bool _isProfileMenuOpen;
        private string _myName;
        private string _windowTitle;
        private bool _isLogVisible;

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
                HandleTyping();
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
        public string VideoPreviewPath
        {
            get => _videoPreviewPath;
            set
            {
                _videoPreviewPath = value;
                OnPropertyChanged(nameof(VideoPreviewPath));
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
        public bool IsLogVisible
        {
            get => _isLogVisible;
            set
            {
                _isLogVisible = value;
                OnPropertyChanged(nameof(IsLogVisible));
            }
        }

        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();
        public ObservableCollection<DebugModel> DebugLogs { get; set; } = new ObservableCollection<DebugModel>();

        public ICommand SendCommand { get; }
        public ICommand ShowAttachmentMenuCommand { get; }
        public ICommand DocumentCommand { get; }
        public ICommand ImageCommand { get; }
        public ICommand VideoCommand { get; }
        public ICommand ClearPreviewCommand { get; }
        public ICommand PlayVideoCommand { get; }
        public ICommand ProfileClickCommand { get; }
        public ICommand ThemeCommand { get; }
        public ICommand DebugLogCommand { get; } 

        public ChatWindowVM(string myName, MainVM main, TcpClient client)
        {
            MyName = myName;
            _main = main;
            _client = client;
            _stream = _client.GetStream();


            Users.Add(new UserModel { UserName = MyName });

            StartRecieveLoop();
            _ = SendMyName();

            SendCommand = new RelayCommand(() => _ = SendMessage());

            ShowAttachmentMenuCommand = new RelayCommand(() =>
              {
                  IsAttachmentMenuOpen = true;
              });

            ImageCommand = new RelayCommand(() => _ = PickImage());

            VideoCommand = new RelayCommand(() => _ = PickVideo());

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

            DebugLogCommand = new RelayCommand(() =>
              {
                  IsLogVisible = !IsLogVisible;
              });

            _typingStopTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _typingStopTimer.Tick += async (s, e) =>
            {
                _typingStopTimer.Stop();
                await SendTypingStatus(false);
            };

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
                                return;
                            }

                            //text
                            if (type == 0x01)
                            {
                                string text = Encoding.UTF8.GetString(payload);
                                Messages.Add(new MessageModel
                                {
                                    Text = text,
                                    IsSentByMe = false
                                });
                                AddLog(_peerName, "Received", "Text", payload);
                            }

                            //image
                            if (type == 0x02)
                            {
                                using (var ms = new MemoryStream(payload))
                                using (var reader = new BinaryReader(ms))
                                {
                                    // 1. Read filename
                                    int nameLength = reader.ReadInt32();
                                    byte[] nameBytes = reader.ReadBytes(nameLength);
                                    string fileName = Encoding.UTF8.GetString(nameBytes);

                                    // 2. Read image bytes ONLY
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
                                            Image = bitmap,
                                            FileName = fileName,
                                            IsSentByMe = false
                                        });
                                    }
                                    AddLog(_peerName, "Received", "Image", imageBytes);
                                }
                            }

                            //video
                            if (type == 0x03)
                            {
                                using (var ms = new MemoryStream(payload))
                                using (var reader = new BinaryReader(ms))
                                {
                                    // ✅ FIRST CHUNK ONLY
                                    if (!_isReceivingVideo)
                                    {
                                        _isReceivingVideo = true;

                                        byte[] nameBytes = reader.ReadBytes(260);
                                        string rawName = Encoding.UTF8.GetString(nameBytes);

                                        _videoFileName = new string(rawName
                                            .Where(c => !Path.GetInvalidFileNameChars().Contains(c))
                                            .ToArray())
                                            .Trim();

                                        byte[] sizeBytes = reader.ReadBytes(8);
                                        if (BitConverter.IsLittleEndian)
                                            Array.Reverse(sizeBytes);

                                        _totalBytes = BitConverter.ToInt64(sizeBytes, 0);

                                        // create storage
                                        if (_totalBytes > 50 * 1024 * 1024)
                                        {
                                            _tempFilePath = Path.GetTempFileName();
                                            _tempFileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write);
                                        }
                                        else
                                        {
                                            _videoStream = new MemoryStream();
                                        }

                                        _receivedBytes = 0;

                                        _currentVideoMessage = new MessageModel
                                        {
                                            IsVideo = true,
                                            Progress = 0,
                                            IsCompleted = false,
                                            IsSentByMe = false
                                        };

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            Messages.Add(_currentVideoMessage);
                                        });
                                    }

                                    // ✅ read chunk data
                                    byte[] chunk = reader.ReadBytes((int)(ms.Length - ms.Position));

                                    if (_videoStream != null)
                                        _videoStream.Write(chunk, 0, chunk.Length);
                                    else
                                        _tempFileStream.Write(chunk, 0, chunk.Length);

                                    _receivedBytes += chunk.Length;

                                    // update UI
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        _currentVideoMessage.Progress = (double)_receivedBytes / _totalBytes * 100;
                                    });

                                    // ✅ COMPLETION
                                    if (_receivedBytes >= _totalBytes)
                                    {
                                        string finalPath;

                                        if (_videoStream != null)
                                        {
                                            finalPath = Path.Combine(Path.GetTempPath(), _videoFileName);
                                            File.WriteAllBytes(finalPath, _videoStream.ToArray());
                                            _videoStream.Dispose();
                                            _videoStream = null;
                                        }
                                        else
                                        {
                                            _tempFileStream.Close();
                                            finalPath = _tempFilePath;
                                            _tempFileStream = null;
                                        }

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            _currentVideoMessage.VideoFilePath = finalPath;
                                            _currentVideoMessage.IsCompleted = true;
                                            _currentVideoMessage.Progress = 100;
                                        });

                                        AddLog(_peerName, "Received", "Video", chunk);
                                        // 🔥 RESET STATE
                                        _isReceivingVideo = false;
                                        _currentVideoMessage = null;
                                        _receivedBytes = 0;
                                    }
                                }
                            }

                            //typing status
                            if (type == 0x04)
                            {
                                bool isTyping = payload.Length > 0 && payload[0] == 1;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (isTyping)
                                    {
                                        IsPeerTyping = true;

                                        // restart hide timer
                                        if (_typingHideTimer == null)
                                        {
                                            _typingHideTimer = new DispatcherTimer
                                            {
                                                Interval = TimeSpan.FromSeconds(2)
                                            };

                                            _typingHideTimer.Tick += (s, e) =>
                                            {
                                                _typingHideTimer.Stop();
                                                IsPeerTyping = false;
                                            };
                                        }

                                        _typingHideTimer.Stop();
                                        _typingHideTimer.Start();
                                    }
                                    else
                                    {
                                        IsPeerTyping = false;
                                    }
                                });
                            }

                            //disconnect
                            if (type == 0x05)
                            {
                                HandlePeerDisconnected();
                                return; // stop loop
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
            if (string.IsNullOrWhiteSpace(OutGoingMessage)) return;

            var msg = OutGoingMessage;

            byte[] data = Encoding.UTF8.GetBytes(msg);

            await MessageProtocol.SendFrameAsync(_stream, 0x01, data);
            AddLog(MyName, "Sent", "Text", data);

            Messages.Add(new MessageModel
            {
                Text = msg,
                IsSentByMe = true
            });

            OutGoingMessage = string.Empty;
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
                bitmap.Freeze(); // 🔥 fix

                ImagePreview = bitmap;
            }
        }

        private async Task SendImage()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // filename
                byte[] nameBytes = Encoding.UTF8.GetBytes(SelectedImageName);

                writer.Write(nameBytes.Length);      // length
                writer.Write(nameBytes);             // filename

                writer.Write(SelectedImageByte);     // image bytes

                await MessageProtocol.SendFrameAsync(_stream, 0x02, ms.ToArray());
                AddLog(MyName, "Sent", "Image: ", SelectedImageByte);
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

            VideoPreviewPath = dialog.FileName;
            SelectedVideoName = Path.GetFileName(dialog.FileName);
            SelectedVideoPath = dialog.FileName;

            OnPropertyChanged(nameof(VideoPreviewPath));
            OnPropertyChanged(nameof(SelectedVideoName));

            await SendVideo(dialog.FileName);
        }

        private async Task SendVideo(string filePath)
        {
            const int CHUNK_SIZE = 64 * 1024;

            string fileName = Path.GetFileName(filePath);
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);

            long totalSize = new FileInfo(filePath).Length;

            // ✅ 1. CREATE MESSAGE (like image)
            var message = new MessageModel
            {
                IsVideo = true,
                Progress = 0,
                IsCompleted = false,
                IsSentByMe = true,
                VideoFilePath = filePath // for local playback
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(message);
            });

            long sentBytes = 0;

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[CHUNK_SIZE];
                int bytesRead;
                bool isFirstChunk = true;

                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    { 
                        if (isFirstChunk)
                        { 
                            byte[] fixedName = new byte[260];
                            Array.Clear(fixedName, 0, fixedName.Length);
                            Array.Copy(nameBytes, fixedName, Math.Min(260, nameBytes.Length));

                            writer.Write(fixedName);
                             
                            byte[] sizeBytes = BitConverter.GetBytes(totalSize);
                            if (BitConverter.IsLittleEndian)
                                Array.Reverse(sizeBytes);

                            writer.Write(sizeBytes);

                            writer.Write(buffer, 0, bytesRead);

                            isFirstChunk = false;
                        }
                        else
                        {
                            writer.Write(buffer, 0, bytesRead);
                        }

                        await MessageProtocol.SendFrameAsync(_stream, 0x03, ms.ToArray());
                        //AddLog(MyName, "Sent", "Video: ", sizeBytes);
                    }
                     
                    sentBytes += bytesRead;

                    double progress = (double)sentBytes / totalSize * 100;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        message.Progress = progress;
                        OnPropertyChanged(nameof(Messages)); 
                    });
                }
            }
             
            Application.Current.Dispatcher.Invoke(() =>
            {
                message.Progress = 100;
                message.IsCompleted = true;
            });
        }

        private void PlayVideo(string path)
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }

        private async void HandleTyping()
        {
            var now = DateTime.Now;
             
            if ((now - _lastTypingSent).TotalMilliseconds > 500)
            {
                _lastTypingSent = now;
                await SendTypingStatus(true); // typing
            }
             
            _typingStopTimer.Stop();
            _typingStopTimer.Start();
        }

        private async Task SendTypingStatus(bool isTyping)
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
                // empty payload is fine
                await MessageProtocol.SendFrameAsync(_stream, 0x05, Array.Empty<byte>());
            }
            catch { /* ignore – we’re closing anyway */ }
        }

        private void HandlePeerDisconnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new MessageModel
                {
                    Text = "Peer has disconnected",
                    IsSystemMessage = true
                });

                AddLog(_peerName, "Recieved", "Peer Status");
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
            WindowTitle = $"PeerChat — {MyName} ↔ {_peerIp} ({_peerName})";
        }

        private void AddLog(string user, string direction, string type, byte[] payload = null )
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DebugLogs.Add(new DebugModel
                {
                    User = user,
                    Direction = direction,
                    ContentType = type,
                    PayloadBytes = payload
                });
            });
        }
    }
} 