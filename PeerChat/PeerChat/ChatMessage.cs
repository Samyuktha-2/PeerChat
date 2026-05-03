using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace PeerChat
{
    public enum ChatMessageKind
    {
        Text,
        Image,
        Video,
        System
    }

    public class ChatMessage : INotifyPropertyChanged
    {
        private double _progress;
        private bool _isComplete;
        private string _videoPath;

        public ChatMessageKind Kind { get; set; }
        public bool IsMine { get; set; }
        public string Sender { get; set; }
        public string Text { get; set; }
        public string FileName { get; set; }
        public BitmapImage Image { get; set; }

        public double Progress
        {
            get { return _progress; }
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public bool IsComplete
        {
            get { return _isComplete; }
            set
            {
                _isComplete = value;
                OnPropertyChanged(nameof(IsComplete));
                OnPropertyChanged(nameof(IsInProgress));
            }
        }

        public bool IsInProgress
        {
            get { return !IsComplete; }
        }

        public string VideoPath
        {
            get { return _videoPath; }
            set
            {
                _videoPath = value;
                OnPropertyChanged(nameof(VideoPath));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
