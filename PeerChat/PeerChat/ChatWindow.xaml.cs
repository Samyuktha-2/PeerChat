using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PeerChat
{
    public partial class ChatWindow : Window
    {
        private const int FileNameHeaderSize = 260;
        private const int VideoChunkSize = 64 * 1024;
        private const long LargeVideoThreshold = 50L * 1024L * 1024L;
        private const string NameHandshakePrefix = "__PEERCHAT_NAME__:";

        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly string _localName;
        private readonly string _targetIp;
        private readonly CancellationTokenSource _receiveCts = new CancellationTokenSource();
        private readonly DispatcherTimer _typingStopTimer;
        private readonly DispatcherTimer _peerTypingSilenceTimer;
        private DateTime _lastTypingSentUtc = DateTime.MinValue;
        private bool _isClosing;
        private bool _inputDisabled;

        private VideoReceiveState _videoReceiveState;
        private string _peerName = "Peer";

        public ObservableCollection<ChatMessage> Messages { get; private set; }
        public ObservableCollection<DebugFrameLog> DebugLog { get; private set; }

        public ChatWindow(TcpClient client, string localName, bool isHost, string targetIp)
        {
            InitializeComponent();

            _client = client;
            _stream = client.GetStream();
            _localName = localName;
            _targetIp = GetRemoteIp(targetIp);

            Messages = new ObservableCollection<ChatMessage>();
            DebugLog = new ObservableCollection<DebugFrameLog>();
            DataContext = this;

            _typingStopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _typingStopTimer.Tick += async (s, e) =>
            {
                _typingStopTimer.Stop();
                await SendTypingFrameAsync(false);
            };

            _peerTypingSilenceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _peerTypingSilenceTimer.Tick += (s, e) =>
            {
                _peerTypingSilenceTimer.Stop();
                TypingTextBlock.Visibility = Visibility.Collapsed;
            };

            UpdateTitle();
            Task.Run(async () => await ReceiveLoopAsync());
            Loaded += async (s, e) => await SendTextFrameAsync(NameHandshakePrefix + _localName, false);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendCurrentTextAsync();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await SendCurrentTextAsync();
            }
        }

        private async void MessageTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_inputDisabled || _isClosing || string.IsNullOrEmpty(MessageTextBox.Text))
            {
                return;
            }

            if ((DateTime.UtcNow - _lastTypingSentUtc).TotalMilliseconds >= 500)
            {
                _lastTypingSentUtc = DateTime.UtcNow;
                await SendTypingFrameAsync(true);
            }

            _typingStopTimer.Stop();
            _typingStopTimer.Start();
        }

        private async Task SendCurrentTextAsync()
        {
            var text = MessageTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || _inputDisabled)
            {
                return;
            }

            MessageTextBox.Clear();
            AddMessage(new ChatMessage
            {
                Kind = ChatMessageKind.Text,
                IsMine = true,
                Sender = _localName,
                Text = text
            });

            await SendTextFrameAsync(text, true);
            await SendTypingFrameAsync(false);
        }

        private async Task SendTextFrameAsync(string text, bool logVisible)
        {
            var payload = Encoding.UTF8.GetBytes(text);
            await MessageProtocol.SendFrameAsync(_stream, MessageProtocol.Text, payload);
            AddLog("SEND", MessageProtocol.Text, payload.Length);
        }

        private async void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                var imageBytes = await ReadAllBytesAsync(dialog.FileName);
                var payload = BuildFilePayload(Path.GetFileName(dialog.FileName), imageBytes);
                await MessageProtocol.SendFrameAsync(_stream, MessageProtocol.Image, payload);
                AddLog("SEND", MessageProtocol.Image, payload.Length);

                AddMessage(new ChatMessage
                {
                    Kind = ChatMessageKind.Image,
                    IsMine = true,
                    Sender = _localName,
                    FileName = Path.GetFileName(dialog.FileName),
                    Image = DecodeBitmap(imageBytes)
                });
            }
            catch (Exception ex)
            {
                AddSystemMessage("Image send failed: " + ex.Message);
            }
        }

        private async void VideoButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Video files (*.mp4;*.avi;*.mkv)|*.mp4;*.avi;*.mkv"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                var fileInfo = new FileInfo(dialog.FileName);
                var localMessage = new ChatMessage
                {
                    Kind = ChatMessageKind.Video,
                    IsMine = true,
                    Sender = _localName,
                    FileName = fileInfo.Name,
                    Progress = 0,
                    IsComplete = false,
                    VideoPath = dialog.FileName
                };
                AddMessage(localMessage);

                long sent = 0;
                var buffer = new byte[VideoChunkSize];
                using (var fileStream = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read, VideoChunkSize, true))
                {
                    var firstChunk = true;
                    int bytesRead;
                    while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        byte[] payload;
                        if (firstChunk)
                        {
                            payload = new byte[FileNameHeaderSize + 8 + bytesRead];
                            WriteFileNameHeader(payload, 0, fileInfo.Name);
                            MessageProtocol.WriteInt64BigEndian(payload, FileNameHeaderSize, fileInfo.Length);
                            Buffer.BlockCopy(buffer, 0, payload, FileNameHeaderSize + 8, bytesRead);
                            firstChunk = false;
                        }
                        else
                        {
                            payload = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, payload, 0, bytesRead);
                        }

                        await MessageProtocol.SendFrameAsync(_stream, MessageProtocol.Video, payload);
                        AddLog("SEND", MessageProtocol.Video, payload.Length);
                        sent += bytesRead;
                        localMessage.Progress = fileInfo.Length == 0 ? 100 : sent * 100.0 / fileInfo.Length;
                    }
                }

                localMessage.Progress = 100;
                localMessage.IsComplete = true;
            }
            catch (Exception ex)
            {
                AddSystemMessage("Video send failed: " + ex.Message);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            while (!_receiveCts.IsCancellationRequested)
            {
                try
                {
                    var frame = await MessageProtocol.ReceiveFrameAsync(_stream, _receiveCts.Token);
                    Dispatcher.Invoke(() => AddLog("RECV", frame.Item1, frame.Item2.Length));
                    await Dispatcher.InvokeAsync(async () => await HandleFrameAsync(frame.Item1, frame.Item2));
                }
                catch (IOException)
                {
                    Dispatcher.Invoke(HandlePeerDisconnected);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_isClosing)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddSystemMessage("Connection ended: " + ex.Message);
                            DisableInputControls();
                        });
                    }
                    break;
                }
            }
        }

        private async Task HandleFrameAsync(byte type, byte[] payload)
        {
            switch (type)
            {
                case MessageProtocol.Text:
                    HandleTextFrame(payload);
                    break;
                case MessageProtocol.Image:
                    HandleImageFrame(payload);
                    break;
                case MessageProtocol.Video:
                    await HandleVideoFrameAsync(payload);
                    break;
                case MessageProtocol.Typing:
                    HandleTypingFrame(payload);
                    break;
                case MessageProtocol.Disconnect:
                    HandlePeerDisconnected();
                    break;
            }
        }

        private void HandleTextFrame(byte[] payload)
        {
            var text = Encoding.UTF8.GetString(payload);
            if (text.StartsWith(NameHandshakePrefix, StringComparison.Ordinal))
            {
                _peerName = text.Substring(NameHandshakePrefix.Length);
                UpdateTitle();
                return;
            }

            AddMessage(new ChatMessage
            {
                Kind = ChatMessageKind.Text,
                IsMine = false,
                Sender = _peerName,
                Text = text
            });
        }

        private void HandleImageFrame(byte[] payload)
        {
            if (payload.Length < FileNameHeaderSize)
            {
                AddSystemMessage("Received an invalid image frame.");
                return;
            }

            var fileName = ReadFileNameHeader(payload, 0);
            var imageBytes = new byte[payload.Length - FileNameHeaderSize];
            Buffer.BlockCopy(payload, FileNameHeaderSize, imageBytes, 0, imageBytes.Length);

            AddMessage(new ChatMessage
            {
                Kind = ChatMessageKind.Image,
                IsMine = false,
                Sender = _peerName,
                FileName = fileName,
                Image = DecodeBitmap(imageBytes)
            });
        }

        private async Task HandleVideoFrameAsync(byte[] payload)
        {
            if (_videoReceiveState == null)
            {
                if (payload.Length < FileNameHeaderSize + 8)
                {
                    AddSystemMessage("Received an invalid video frame.");
                    return;
                }

                var fileName = ReadFileNameHeader(payload, 0);
                var totalSize = MessageProtocol.ReadInt64BigEndian(payload, FileNameHeaderSize);
                _videoReceiveState = new VideoReceiveState(fileName, totalSize);
                AddMessage(_videoReceiveState.Message);

                await _videoReceiveState.AppendAsync(payload, FileNameHeaderSize + 8, payload.Length - FileNameHeaderSize - 8);
            }
            else
            {
                await _videoReceiveState.AppendAsync(payload, 0, payload.Length);
            }

            _videoReceiveState.Message.Progress = _videoReceiveState.TotalSize == 0
                ? 100
                : _videoReceiveState.BytesReceived * 100.0 / _videoReceiveState.TotalSize;

            if (_videoReceiveState.BytesReceived >= _videoReceiveState.TotalSize)
            {
                _videoReceiveState.Message.Progress = 100;
                _videoReceiveState.Message.VideoPath = await _videoReceiveState.CompleteAsync();
                _videoReceiveState.Message.IsComplete = true;
                _videoReceiveState = null;
            }
        }

        private void HandleTypingFrame(byte[] payload)
        {
            var active = payload.Length == 0 || payload[0] == 1;
            TypingTextBlock.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            _peerTypingSilenceTimer.Stop();
            if (active)
            {
                _peerTypingSilenceTimer.Start();
            }
        }

        private async Task SendTypingFrameAsync(bool active)
        {
            if (_isClosing || _inputDisabled)
            {
                return;
            }

            var payload = active ? new byte[] { 1 } : new byte[] { 0 };
            try
            {
                await MessageProtocol.SendFrameAsync(_stream, MessageProtocol.Typing, payload);
                AddLog("SEND", MessageProtocol.Typing, payload.Length);
            }
            catch
            {
                DisableInputControls();
            }
        }

        private void HandlePeerDisconnected()
        {
            if (_inputDisabled)
            {
                return;
            }

            AddSystemMessage("Peer has disconnected");
            DisableInputControls();
        }

        private void DisableInputControls()
        {
            _inputDisabled = true;
            MessageTextBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            ImageButton.IsEnabled = false;
            VideoButton.IsEnabled = false;
            TypingTextBlock.Visibility = Visibility.Collapsed;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            _receiveCts.Cancel();

            try
            {
                await MessageProtocol.SendFrameAsync(_stream, MessageProtocol.Disconnect, new byte[0]);
                AddLog("SEND", MessageProtocol.Disconnect, 0);
            }
            catch
            {
            }
            finally
            {
                _client.Close();
            }
        }

        private void ImagePreview_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var message = button == null ? null : button.CommandParameter as ChatMessage;
            if (message == null || message.Image == null)
            {
                return;
            }

            var preview = new ImagePreviewWindow(message.Image, message.FileName ?? "Image Preview");
            preview.Owner = this;
            preview.ShowDialog();
        }

        private void PlayVideo_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var message = button == null ? null : button.CommandParameter as ChatMessage;
            if (message == null || string.IsNullOrWhiteSpace(message.VideoPath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(message.VideoPath) { UseShellExecute = true });
        }

        private void AddMessage(ChatMessage message)
        {
            Messages.Add(message);
            ScrollToBottom();
        }

        private void AddSystemMessage(string text)
        {
            AddMessage(new ChatMessage
            {
                Kind = ChatMessageKind.System,
                Sender = "System",
                Text = text
            });
        }

        private void AddLog(string direction, byte type, int payloadSize)
        {
            DebugLog.Add(new DebugFrameLog
            {
                Direction = direction,
                Type = type,
                PayloadSize = payloadSize,
                Timestamp = DateTime.Now
            });
            DebugLogListBox.ScrollIntoView(DebugLogListBox.Items[DebugLogListBox.Items.Count - 1]);
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(new Action(() => MessagesScrollViewer.ScrollToEnd()), DispatcherPriority.Background);
        }

        private void UpdateTitle()
        {
            Title = string.Format("PeerChat - {0} <-> {1} ({2})", _localName, _targetIp, _peerName);
        }

        private string GetRemoteIp(string fallback)
        {
            try
            {
                var endPoint = _client.Client.RemoteEndPoint as IPEndPoint;
                if (endPoint != null)
                {
                    return endPoint.Address.ToString();
                }
            }
            catch
            {
            }

            return fallback;
        }

        private static async Task<byte[]> ReadAllBytesAsync(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory);
                return memory.ToArray();
            }
        }

        private static byte[] BuildFilePayload(string fileName, byte[] fileBytes)
        {
            var payload = new byte[FileNameHeaderSize + fileBytes.Length];
            WriteFileNameHeader(payload, 0, fileName);
            Buffer.BlockCopy(fileBytes, 0, payload, FileNameHeaderSize, fileBytes.Length);
            return payload;
        }

        private static void WriteFileNameHeader(byte[] payload, int offset, string fileName)
        {
            var nameBytes = Encoding.UTF8.GetBytes(fileName ?? "file");
            var length = Math.Min(nameBytes.Length, FileNameHeaderSize);
            Buffer.BlockCopy(nameBytes, 0, payload, offset, length);
        }

        private static string ReadFileNameHeader(byte[] payload, int offset)
        {
            var length = 0;
            while (length < FileNameHeaderSize && payload[offset + length] != 0)
            {
                length++;
            }

            return Encoding.UTF8.GetString(payload, offset, length);
        }

        private static BitmapImage DecodeBitmap(byte[] imageBytes)
        {
            using (var memory = new MemoryStream(imageBytes))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memory;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        private class VideoReceiveState
        {
            private readonly MemoryStream _memoryStream;
            private readonly FileStream _fileStream;
            private readonly string _tempPath;

            public VideoReceiveState(string fileName, long totalSize)
            {
                FileName = string.IsNullOrWhiteSpace(fileName) ? "received-video" : fileName;
                TotalSize = totalSize;
                Message = new ChatMessage
                {
                    Kind = ChatMessageKind.Video,
                    IsMine = false,
                    Sender = "Peer",
                    FileName = FileName,
                    Progress = 0,
                    IsComplete = false
                };

                if (TotalSize > LargeVideoThreshold)
                {
                    _tempPath = Path.GetTempFileName();
                    _fileStream = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None, VideoChunkSize, true);
                }
                else
                {
                    _memoryStream = new MemoryStream();
                }
            }

            public string FileName { get; private set; }
            public long TotalSize { get; private set; }
            public long BytesReceived { get; private set; }
            public ChatMessage Message { get; private set; }

            public async Task AppendAsync(byte[] payload, int offset, int count)
            {
                if (count <= 0)
                {
                    return;
                }

                if (_fileStream != null)
                {
                    await _fileStream.WriteAsync(payload, offset, count);
                }
                else
                {
                    await _memoryStream.WriteAsync(payload, offset, count);
                }

                BytesReceived += count;
            }

            public async Task<string> CompleteAsync()
            {
                var finalPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + "_" + SanitizeFileName(FileName));
                if (_fileStream != null)
                {
                    await _fileStream.FlushAsync();
                    _fileStream.Dispose();
                    if (File.Exists(finalPath))
                    {
                        File.Delete(finalPath);
                    }
                    File.Move(_tempPath, finalPath);
                }
                else
                {
                    using (var output = new FileStream(finalPath, FileMode.Create, FileAccess.Write, FileShare.Read, VideoChunkSize, true))
                    {
                        var bytes = _memoryStream.ToArray();
                        await output.WriteAsync(bytes, 0, bytes.Length);
                    }
                    _memoryStream.Dispose();
                }

                return finalPath;
            }

            private static string SanitizeFileName(string fileName)
            {
                foreach (var invalidChar in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(invalidChar, '_');
                }

                return fileName;
            }
        }
    }
}
