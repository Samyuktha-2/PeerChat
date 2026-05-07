using PeerChat.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PeerChat.Model
{
    public class MessageModel : BaseVM
    { 
        private double _transferProgress;
        private bool _isTransferCompleted;
        private string _transferStatus;
        private string _videoPath;
        private ImageSource _videoThumbnail;

        public DateTime GetDateTime { get;set;}

        public string Text { get; set; }
        public bool IsSentByMe { get; set; }

        public bool IsFile { get; set; }
        public byte[] FileBytes { get; set; }

        public ImageSource Image { get; set; }
        public string FileName { get; set; }
        public bool HasImage => Image != null;

        public bool IsVideo { get; set; }

        public string VideoPath
        {
            get => _videoPath;
            set
            {
                _videoPath = value;
                OnPropertyChanged(nameof(VideoPath));
            }
        }

        public ImageSource VideoThumbnail
        {
            get => _videoThumbnail;
            set
            {
                _videoThumbnail = value;
                OnPropertyChanged(nameof(VideoThumbnail));
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

        public double TransferProgress
        {
            get => _transferProgress;
            set
            {
                _transferProgress = value;
                OnPropertyChanged(nameof(TransferProgress));
            }
        }

        public string TransferStatus
        {
            get => _transferStatus;
            set
            {
                _transferStatus = value;
                OnPropertyChanged(nameof(TransferStatus));
            }
        }

        public bool IsSystemMessage { get; set; }

    }
}
