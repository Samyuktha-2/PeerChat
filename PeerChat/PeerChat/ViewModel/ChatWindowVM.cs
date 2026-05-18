using Microsoft.Win32;
using peerchat.viewmodel;
using PeerChat.Command;
using PeerChat.Model;
using PeerChat.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PeerChat.ViewModel
{
    public class ChatWindowVM : BaseVM
    {
        private const int VideoChunkSize = 64 * 1024;
        private const int VideoFileNameHeaderSize = 260;
        private const int VideoSizeHeaderSize = 8;

        private TcpClient _client;
        private string _selectedUserName;
        private readonly MainVM _main;
        private readonly NetworkStream _stream;
        private string _peerName;

        private string _outGoingMessage;
        private bool _isAttachmentMenuOpen;
        private ImageSource _imagePreview;
        private ImageSource _thumbnail;
        private bool isDark = false;
        private string _peerIp;

        private FileStream _videoReceiveStream;
        private long _videoTotalSize;
        private long _videoReceivedBytes;
        private int _receivedVideoChunkNumber;
        private string _tempFilePath;
        private MessageModel _currentVideoMessage;
        private bool _isReceivingVideo;
        private bool _isPeerTyping;

        private bool _isConnected = true;
        private bool _isSendingMessage;
        private string _peerStatus = "Online";
        private bool _isProfileMenuOpen;
        private string _myName;
        private string _windowTitle;
        private bool _isDebugEnabled;

        public byte[] SelectedImageByte { get; set; }
        public string SelectedImageName { get; set; }

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
                    _ = SendTypingStatus(true);
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

            ImageCommand = new RelayCommand(PickImage);

            ClearPreviewCommand = new RelayCommand(() =>
              {
                  ImagePreview = null;
                  SelectedImageByte = null;
                  SelectedImageName = null;
              });

            VideoCommand = new RelayCommand(() => _ = PickVideo());

            PlayVideoCommand = new RelayCommandT<MessageModel>(PlayVideo);

            ProfileClickCommand = new RelayCommand(() =>
           {
               IsProfileMenuOpen = true;
           });

            ThemeCommand = new RelayCommand(ChangeTheme);
            LogoutCommand = new RelayCommand(() => _ = HandleLocalClosingAsync());
        }

        private async Task SendMyName()
        {
            byte[] data = Encoding.UTF8.GetBytes(MyName);
            MessageProtocol.SendFrameAsync(_stream, 0x06, data);
        }

        private void StartRecieveLoop()
        {
            Task.Run(() =>
            {
                try
                {
                    var stream = _client.GetStream();

                    while (true)
                    {
                        var (type, payload) = MessageProtocol.ReceiveFrameAsync(stream);



                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (type == 0x06)
                            {
                                string text = Encoding.UTF8.GetString(payload);
                                _peerName = text;
                                SelectedUserName = text;
                                UpdateTitle();
                                return;
                            }

                            if (type == 0x01) //text
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

                            if (type == 0x03)
                            {
                                _ = HandleVideoFrameAsync(payload);
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
            if (_isSendingMessage)
                return;

            _isSendingMessage = true;

            try
            {
                if (!string.IsNullOrWhiteSpace(OutGoingMessage))
                {
                    await SendTextMessage();
                }

                if (SelectedImageByte != null)
                {
                    await SendImage();
                }
            }
            finally
            {
                _isSendingMessage = false;
            }
        }

        private async Task SendTextMessage()
        {
            if (!IsConnected || _client == null || !_client.Connected)
                return;

            var msg = OutGoingMessage;

            byte[] data = Encoding.UTF8.GetBytes(msg);
            MessageProtocol.SendFrameAsync(_stream, 0x01, data);
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

        private void PickImage()
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
            var imageBytes = SelectedImageByte;
            var imageName = SelectedImageName;

            if (imageBytes == null || string.IsNullOrWhiteSpace(imageName))
                return;

            //sends img
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(imageName);

                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);

                writer.Write(imageBytes);

                byte[] payload = ms.ToArray();
                MessageProtocol.SendFrameAsync(_stream, 0x02, payload);

                AddLog(DateTime.Now, "Sent", "Image", $"{payload.Length} bytes");
            }

            //updates ui with sent img
            using (var ms = new MemoryStream(imageBytes))
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
                    FileName = imageName,
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
                Filter = "Video files (*.mp4;*.avi;*.mkv)|*.mp4;*.avi;*.mkv"
            };

            if (dialog.ShowDialog() != true)
                return;

            await SendVideo(dialog.FileName);
        }

        private async Task SendVideo(string videoPath)
        {
            if (!IsConnected || _client == null || !_client.Connected || string.IsNullOrWhiteSpace(videoPath))
                return;

            var fileInfo = new FileInfo(videoPath);
            var totalSize = fileInfo.Length;
            var bytesSent = 0L;
            var chunkNumber = 0;

            var message = new MessageModel
            {
                GetDateTime = DateTime.Now,
                FileName = fileInfo.Name,
                IsVideo = true,
                IsSentByMe = true,
                VideoPath = videoPath,
                IsTransferCompleted = false,
                TransferProgress = 0,
                TransferStatus = $"0/{totalSize}"
            };

            Application.Current.Dispatcher.Invoke(() => Messages.Add(message));

            var buffer = new byte[VideoChunkSize]; //video chunck size 64 * 1024, creates temp storage for 64KB

            using (var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read, VideoChunkSize, true))
            {
                int bytesRead;
                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    chunkNumber++;

                    byte[] payload;
                    if (chunkNumber == 1)
                    {
                        payload = new byte[VideoFileNameHeaderSize + VideoSizeHeaderSize + bytesRead];  //video header size = 8, video filen name header size = 260

                        //filename header
                        var nameBytes = Encoding.UTF8.GetBytes(fileInfo.Name);
                        var length = Math.Min(nameBytes.Length, VideoFileNameHeaderSize);
                        Buffer.BlockCopy(nameBytes, 0, payload, 0, length); //copying filename into payload

                        var sizeBytes = BitConverter.GetBytes(totalSize);
                        if (BitConverter.IsLittleEndian)  //big-endian to ensure sender interprets size crtly
                            Array.Reverse(sizeBytes);
                        Buffer.BlockCopy(sizeBytes, 0, payload, VideoFileNameHeaderSize, VideoSizeHeaderSize);  //copying filesize into payload

                        Buffer.BlockCopy(buffer, 0, payload, VideoFileNameHeaderSize + VideoSizeHeaderSize, bytesRead);  //copying fist chunck vid size into payload
                    }
                    else
                    {
                        payload = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, payload, 0, bytesRead);
                    }

                    MessageProtocol.SendFrameAsync(_stream, 0x03, payload);

                    bytesSent += bytesRead;
                    UpdateVideoProgress(message, bytesSent, totalSize);
                    //AddLog(DateTime.Now, "Sent", "Video", $"Chunk num: {chunkNumber}-{bytesSent} bytes");
                }
                AddLog(DateTime.Now, "Sent", "Video", $"{totalSize} bytes");
            }

            _thumbnail = await GenerateVideoThumbnailAsync(videoPath);
            Application.Current.Dispatcher.Invoke(() =>
            {
                message.VideoThumbnail = _thumbnail;
                message.IsTransferCompleted = true;
                message.TransferProgress = 100;
                message.TransferStatus = $"{totalSize}/{totalSize}";
            });
        }

        private async Task HandleVideoFrameAsync(byte[] payload)
        {
            if (!_isReceivingVideo)
            {
                if (payload.Length < VideoFileNameHeaderSize + VideoSizeHeaderSize)
                {
                    AddLog(DateTime.Now, "Received", "Video", "Invalid first video chunk");
                    return;
                }

                var fileName = ReadFileNameHeader(payload, 0);
                _videoTotalSize = ReadInt64BigEndian(payload, VideoFileNameHeaderSize);
                _videoReceivedBytes = 0;
                _receivedVideoChunkNumber = 0;
                _isReceivingVideo = true;
                _tempFilePath = Path.GetTempFileName();
                _videoReceiveStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, VideoChunkSize, true);

                _currentVideoMessage = new MessageModel
                {
                    GetDateTime = DateTime.Now,
                    FileName = fileName,
                    IsVideo = true,
                    IsSentByMe = false,
                    VideoPath = _tempFilePath,
                    IsTransferCompleted = false,
                    TransferProgress = 0,
                    TransferStatus = $"0/{_videoTotalSize}"
                };

                Application.Current.Dispatcher.Invoke(() => Messages.Add(_currentVideoMessage));

                var firstChunkSize = payload.Length - VideoFileNameHeaderSize - VideoSizeHeaderSize;
                await WriteReceivedVideoChunkAsync(payload, VideoFileNameHeaderSize + VideoSizeHeaderSize, firstChunkSize);
            }
            else
            {
                await WriteReceivedVideoChunkAsync(payload, 0, payload.Length);
            }
        }

        private async Task WriteReceivedVideoChunkAsync(byte[] payload, int offset, int count)
        {
            if (_videoReceiveStream == null || _currentVideoMessage == null)
                return;

            if (count > 0)
                await _videoReceiveStream.WriteAsync(payload, offset, count);  //write chunck into temp file

            _receivedVideoChunkNumber++;
            _videoReceivedBytes += count;

            UpdateVideoProgress(_currentVideoMessage, _videoReceivedBytes, _videoTotalSize);

            //AddLog(DateTime.Now, "Received", "Video", $"Received Video Chunk {_receivedVideoChunkNumber} - {_videoReceivedBytes} bytes");

            if (_videoReceivedBytes >= _videoTotalSize)
            {
                await _videoReceiveStream.FlushAsync();  //ensure byte written to disk

                _videoReceiveStream.Dispose();
                _videoReceiveStream = null;

                await Task.Delay(3000);

                var completedMessage = _currentVideoMessage;
                var completedPath = _tempFilePath;

                _thumbnail = await GenerateVideoThumbnailAsync(_tempFilePath);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    completedMessage.VideoThumbnail = _thumbnail;
                    completedMessage.VideoPath = completedPath;
                    completedMessage.IsTransferCompleted = true;
                    completedMessage.TransferProgress = 100;
                    completedMessage.TransferStatus = $"{_videoTotalSize}/{_videoTotalSize}";
                });

                AddLog(DateTime.Now, "Recieved", "Video", $"{_videoTotalSize} byte");
                _currentVideoMessage = null;
                _tempFilePath = null;
                _videoTotalSize = 0;
                _videoReceivedBytes = 0;
                _receivedVideoChunkNumber = 0;
                _isReceivingVideo = false;
            }
        }

        private void UpdateVideoProgress(MessageModel message, long transferredBytes, long totalSize)
        {
            var progress = totalSize <= 0 ? 100 : transferredBytes * 100.0 / totalSize;

            Application.Current.Dispatcher.Invoke(() =>
            {
                message.TransferProgress = progress;
                message.TransferStatus = $"{transferredBytes}/{totalSize}";
            });
        }

        private async Task<BitmapSource> GenerateVideoThumbnailAsync(string videoPath)
        {
            try
            {
                var thumbnailTask = await Application.Current.Dispatcher.InvokeAsync(
                    () => GenerateVideoThumbnailOnUiAsync(videoPath));

                return await thumbnailTask;
            }
            catch
            {
                return null;
            }
        }

        public static async Task<BitmapSource> GenerateVideoThumbnailOnUiAsync(string videoPath)
        {
            try
            {
                MediaPlayer player = new MediaPlayer { ScrubbingEnabled = true }; //capture frame

                player.Open(new Uri(videoPath, UriKind.Absolute));

                await Task.Delay(1000);

                player.Position = TimeSpan.FromMilliseconds(250);

                await Task.Delay(300);

                int width = player.NaturalVideoWidth;
                int height = player.NaturalVideoHeight;

                if (width <= 0) width = 250;
                if (height <= 0) height = 160;

                DrawingVisual visual = new DrawingVisual();

                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawVideo(player, new Rect(0, 0, width, height));
                }

                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

                renderBitmap.Render(visual);

                BitmapImage image = new BitmapImage();

                using (MemoryStream ms = new MemoryStream())
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                    encoder.Save(ms);
                    ms.Position = 0;

                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }

                player.Close();

                return image;
            }
            catch
            {
                return null;
            }
        }
        
        private void PlayVideo(MessageModel msg)
        {
            string path;

            if (!string.IsNullOrEmpty(msg.VideoPath))
                path = msg.VideoPath;
            else
            {
                path = Path.Combine(Path.GetTempPath(), msg.Text);
                File.WriteAllBytes(path, msg.FileBytes);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

        }

        private static string ReadFileNameHeader(byte[] payload, int offset)
        {
            var length = 0;
            while (length < VideoFileNameHeaderSize && payload[offset + length] != 0)
                length++;

            var fileName = Encoding.UTF8.GetString(payload, offset, length);
            return string.IsNullOrWhiteSpace(fileName) ? "received-video" : fileName;
        }

        private static long ReadInt64BigEndian(byte[] payload, int offset)
        {
            var sizeBytes = new byte[VideoSizeHeaderSize];
            Buffer.BlockCopy(payload, offset, sizeBytes, 0, VideoSizeHeaderSize);  //copies bytes from payload

            if (BitConverter.IsLittleEndian)
                Array.Reverse(sizeBytes);

            return BitConverter.ToInt64(sizeBytes, 0);
        }

        public async Task SendTypingStatus(bool isTyping)
        {
            byte[] payload = new byte[1];
            payload[0] = isTyping ? (byte)1 : (byte)0;

            MessageProtocol.SendFrameAsync(_stream, 0x04, payload);
        }

        public async Task HandleLocalClosingAsync()
        {
            if (!_isConnected) return;

            try
            {
                MessageProtocol.SendFrameAsync(_stream, 0x05, Array.Empty<byte>());
                _main.GoBack();
            }
            catch { }

            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch { }

            IsConnected = false;
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
