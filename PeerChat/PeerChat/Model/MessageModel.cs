using PeerChat.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeerChat.Model
{
    public class MessageModel : BaseVM
    {
        private double _progress;

        public string Text { get; set; }
        public bool IsSentByMe { get; set; }
        public bool IsFile { get; set; }
        public byte[] FileBytes { get; set; }
        public long TotalBytes { get; set; }
        public long ReceivedBytes { get; set; }
        //public double Progress => TotalBytes == 0 ? 0 : (double)ReceivedBytes / TotalBytes;
        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public bool IsReceiving { get; set; }
        public bool IsVideo { get; set; }
        public string TempFilePath { get; set; }
    }
}
