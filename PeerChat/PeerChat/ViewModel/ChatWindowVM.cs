using GoLibrary;
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
        private string selectedUserName;
        private string _outgoingMessage;
        private readonly MainVM _main;
        private readonly string _myName;
        private string _peerName;
        private readonly NetworkStream _stream;

        private DateTime _lastTypingSent = DateTime.MinValue;
        private DispatcherTimer _typingTimer;

        private bool _isPeerTyping;
        private DispatcherTimer _peerTypingTimer;
        private bool _isAttachmentMenuOpen;

        private ImageSource _imagePreview;
        private string _videoPreviewPath;

        // 🔥 unified stream type
        private Stream _videoStream;
        private long _expectedSize;
        private string _incomingVideoName;
        private bool _isReceivingVideo;
        private MessageModel _currentVideoMsg;
        private bool _isProfileMenuOpen;

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

        public bool IsAttachmentMenuOpen
        {
            get => _isAttachmentMenuOpen;
            set
            {
                _isAttachmentMenuOpen = value;
                OnPropertyChanged(nameof(IsAttachmentMenuOpen));
            }
        }
        public bool IsProfileMenuOpen
        {
            get => _isProfileMenuOpen;
            set
            {
                _isProfileMenuOpen = value;
                OnPropertyChanged(nameof(IsProfileMenuOpen));
            }
        }

        public byte[] SelectedImageByte { get; set; }
        public byte[] SelectedDocumentByte { get; set; }

        public string SelectedImageName { get; set; }
        public string SelectedVideoName { get; set; }
        public string SelectedDocumentName { get; set; }

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

        public string SelectedVideoPath { get; set; }

        public ObservableCollection<UserModel> Users { get; set; } = new ObservableCollection<UserModel>();
        public ObservableCollection<MessageModel> Messages { get; set; } = new ObservableCollection<MessageModel>();

        public ICommand SendCommand { get; }
        public ICommand ShowAttachmentMenuCommand { get; }
        public ICommand DocumentCommand { get; }
        public ICommand ImageCommand { get; }
        public ICommand VideoCommand { get; }
        public ICommand OpenFileCommand => new RelayCommandT<MessageModel>(OpenFile);
        public ICommand ClearPreviewCommand { get; }
        public ICommand PlayVideoCommand => new RelayCommandT<MessageModel>(PlayVideo);
        public ICommand ProfileClickCommand { get; }

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
                await SendTyping(false);
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

            SendCommand = new RelayCommand(() => _ = SendMessage());

            ShowAttachmentMenuCommand = new RelayCommand(() =>
            {
                IsAttachmentMenuOpen = true;
            });

            ProfileClickCommand = new RelayCommand(() =>
            {
                IsProfileMenuOpen = true;
            });

            ImageCommand = new RelayCommand(() => _ = PickImage());

            ClearPreviewCommand = new RelayCommand(() =>
            {
                ImagePreview = null;
                SelectedImageByte = null;
                SelectedImageName = null;
            });

            VideoCommand = new RelayCommand(() => _ = PickVideo());

            DocumentCommand = new RelayCommand(() => _ = PickDocument());
        }

        private async Task SendMessage()
        {
            if (SelectedImageByte != null)
            {
                await SendImage();
                return;
            }

            if (!string.IsNullOrEmpty(SelectedVideoPath))
            {
                await SendVideo();
                return;
            }

            if (SelectedDocumentByte != null)
            {
                await SendDocument();
                return;
            }

            if (!string.IsNullOrWhiteSpace(OutgoingMessage))
            {
                await SendTextMessage();
            }
        }

        private async Task SendTextMessage()
        {
            if (string.IsNullOrWhiteSpace(OutgoingMessage))
                return;

            byte[] data = Encoding.UTF8.GetBytes(OutgoingMessage);

            await MessageProtocol.SendFrameAsync(_stream, 0x01, data);

            Messages.Add(new MessageModel
            {
                Text = OutgoingMessage,
                IsSentByMe = true
            });

            OutgoingMessage = string.Empty;
        }

        private async Task SendImage()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(SelectedImageName);

            byte[] payload = new byte[260 + SelectedImageByte.Length];

            Array.Clear(payload, 0, payload.Length); // ensure null padding
            Array.Copy(nameBytes, payload, Math.Min(260, nameBytes.Length));
            Array.Copy(SelectedImageByte, 0, payload, 260, SelectedImageByte.Length);

            await MessageProtocol.SendFrameAsync(_stream, 0x02, payload);

            Messages.Add(new MessageModel
            {
                Text = SelectedImageName,
                IsSentByMe = true,
                IsFile = true,
                FileBytes = SelectedImageByte // 🔥 important for preview
            });

            SelectedImageByte = null;
            SelectedImageName = null;
            ImagePreview = null;
        }

        private async Task SendVideo()
        {
            var fileInfo = new FileInfo(SelectedVideoPath);
            long fileSize = fileInfo.Length;

            byte[] fileNameBytes = Encoding.UTF8.GetBytes(SelectedVideoName);

            using (var fs = new FileStream(SelectedVideoPath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[64 * 1024];
                int bytesRead;
                bool isFirstChunk = true;

                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    byte[] chunk;

                    if (isFirstChunk)
                    {
                        chunk = new byte[260 + 8 + bytesRead];

                        Array.Clear(chunk, 0, chunk.Length);
                        Array.Copy(fileNameBytes, chunk, Math.Min(260, fileNameBytes.Length));

                        byte[] sizeBytes = BitConverter.GetBytes(fileSize);
                        if (BitConverter.IsLittleEndian)
                            Array.Reverse(sizeBytes);

                        Array.Copy(sizeBytes, 0, chunk, 260, 8);
                        Array.Copy(buffer, 0, chunk, 268, bytesRead);

                        isFirstChunk = false;
                    }
                    else
                    {
                        chunk = new byte[bytesRead];
                        Array.Copy(buffer, 0, chunk, 0, bytesRead);
                    }

                    await MessageProtocol.SendFrameAsync(_stream, 0x03, chunk);
                }
            }

            Messages.Add(new MessageModel
            {
                Text = SelectedVideoName,
                IsSentByMe = true,
                IsFile = true,
                IsVideo = true
            });

            SelectedVideoPath = null;
            SelectedVideoName = null;
        }

        private async Task SendDocument()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(SelectedDocumentName);

            byte[] payload = new byte[260 + SelectedDocumentByte.Length];

            Array.Clear(payload, 0, payload.Length);
            Array.Copy(nameBytes, payload, Math.Min(260, nameBytes.Length));
            Array.Copy(SelectedDocumentByte, 0, payload, 260, SelectedDocumentByte.Length);

            await MessageProtocol.SendFrameAsync(_stream, 0x07, payload);

            Messages.Add(new MessageModel
            {
                Text = SelectedDocumentName,
                IsSentByMe = true,
                IsFile = true,
                FileBytes = SelectedDocumentByte
            });

            SelectedDocumentByte = null;
            SelectedDocumentName = null;
        }

        private async Task SendMyName()
        {
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

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            // username
                            if (type == 0x06)
                            {
                                string text = Encoding.UTF8.GetString(payload);
                                _peerName = text;
                                SelectedUserName = text;
                                return;
                            }

                            // message
                            if (type == 0x01)
                            {
                                string text = Encoding.UTF8.GetString(payload);

                                Messages.Add(new MessageModel
                                {
                                    Text = text,
                                    IsSentByMe = false
                                });
                            }

                            // image
                            if (type == 0x02)
                            {
                                byte[] nameBytes = payload.Take(260).ToArray();
                                string fileName = Encoding.UTF8.GetString(nameBytes).Trim('\0');

                                byte[] imageBytes = payload.Skip(260).ToArray();

                                Messages.Add(new MessageModel
                                {
                                    Text = fileName,
                                    IsSentByMe = false,
                                    IsFile = true,
                                    FileBytes = imageBytes
                                });
                            }

                            // video
                            if (type == 0x03)
                            {
                                if (!_isReceivingVideo)
                                {
                                    byte[] nameBytes = payload.Take(260).ToArray();
                                    _incomingVideoName = Encoding.UTF8.GetString(nameBytes).Trim('\0');

                                    byte[] sizeBytes = payload.Skip(260).Take(8).ToArray();
                                    if (BitConverter.IsLittleEndian) Array.Reverse(sizeBytes);
                                    _expectedSize = BitConverter.ToInt64(sizeBytes, 0);

                                    if (_expectedSize > 50 * 1024 * 1024)
                                    {
                                        string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
                                        _videoStream = new FileStream(temp, FileMode.Create, FileAccess.Write);

                                        _currentVideoMsg = new MessageModel
                                        {
                                            Text = _incomingVideoName,
                                            IsSentByMe = false,
                                            IsFile = true,
                                            IsVideo = true,
                                            TempFilePath = temp,
                                            TotalBytes = _expectedSize,
                                            ReceivedBytes = 0,
                                            IsReceiving = true
                                        };
                                    }
                                    else
                                    {
                                        _videoStream = new MemoryStream();

                                        _currentVideoMsg = new MessageModel
                                        {
                                            Text = _incomingVideoName,
                                            IsSentByMe = false,
                                            IsFile = true,
                                            IsVideo = true,
                                            TotalBytes = _expectedSize,
                                            ReceivedBytes = 0,
                                            IsReceiving = true
                                        };
                                    }

                                    Messages.Add(_currentVideoMsg);

                                    byte[] firstData = payload.Skip(268).ToArray();
                                    _videoStream.Write(firstData, 0, firstData.Length);
                                    _currentVideoMsg.ReceivedBytes += firstData.Length;

                                    _isReceivingVideo = true;
                                }
                                else
                                {
                                    _videoStream.Write(payload, 0, payload.Length);
                                    _currentVideoMsg.ReceivedBytes += payload.Length;
                                }


                                if (_currentVideoMsg.ReceivedBytes >= _expectedSize)
                                {
                                    _videoStream.Flush();
                                    _videoStream.Dispose();

                                    if (_videoStream is MemoryStream ms)
                                    {
                                        _currentVideoMsg.FileBytes = ms.ToArray();
                                    }

                                    _currentVideoMsg.IsReceiving = false;

                                    _isReceivingVideo = false;
                                    _incomingVideoName = null;
                                    _expectedSize = 0;
                                }
                            }

                            // typing
                            if (type == 0x04)
                            {
                                bool typing = payload[0] == 1;
                                IsPeerTyping = typing;
                            }

                            // document
                            if (type == 0x07)
                            {
                                byte[] nameBytes = payload.Take(260).ToArray();
                                string fileName = Encoding.UTF8.GetString(nameBytes).Trim('\0');

                                byte[] fileBytes = payload.Skip(260).ToArray();

                                Messages.Add(new MessageModel
                                {
                                    Text = fileName,
                                    IsSentByMe = false,
                                    IsFile = true,
                                    FileBytes = fileBytes
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

        private async Task PickImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dialog.ShowDialog() != true)
                return;

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

        private void OpenFile(MessageModel msg)
        {
            if (msg == null || string.IsNullOrWhiteSpace(msg.Text))
                return;

            string extension = Path.GetExtension(msg.Text).ToLower();

            string tempPath = !string.IsNullOrEmpty(msg.TempFilePath)
                ? msg.TempFilePath
                : Path.Combine(Path.GetTempPath(), msg.Text);

            if (msg.FileBytes != null && string.IsNullOrEmpty(msg.TempFilePath))
            {
                File.WriteAllBytes(tempPath, msg.FileBytes);
            }

            if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".bmp")
            {
                using (var ms = new MemoryStream(msg.FileBytes))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    var window = new Window
                    {
                        Title = msg.Text,
                        Width = 600,
                        Height = 600,
                        Content = new System.Windows.Controls.Image
                        {
                            Source = bitmap,
                            Stretch = System.Windows.Media.Stretch.Uniform
                        }
                    };

                    window.ShowDialog();
                }
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
        }

        private async Task PickVideo()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Videos|*.mp4;*.avi;*.mkv"
            };

            if (dialog.ShowDialog() != true)
                return;

            VideoPreviewPath = dialog.FileName;
            SelectedVideoName = Path.GetFileName(dialog.FileName);
            SelectedVideoPath = dialog.FileName;

            OnPropertyChanged(nameof(VideoPreviewPath));
            OnPropertyChanged(nameof(SelectedVideoName));
        }

        private async Task PickDocument()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "All Files|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedDocumentByte = File.ReadAllBytes(dialog.FileName);
            SelectedDocumentName = Path.GetFileName(dialog.FileName);
        }

        private void PlayVideo(MessageModel msg)
        {
            string path;

            if (!string.IsNullOrEmpty(msg.TempFilePath))
                path = msg.TempFilePath;
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
    }
}
