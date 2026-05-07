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
        public DateTime GetDateTime { get;set;}

        public string Text { get; set; }
        public bool IsSentByMe { get; set; }

        public bool IsFile { get; set; }
        public byte[] FileBytes { get; set; }

        public ImageSource Image { get; set; }
        public string FileName { get; set; }
        public bool HasImage => Image != null;

        public bool IsVideo { get; set; } 
        public string VideoPath { get; set; } 
        public bool IsTransferCompleted { get; set; } 
        public double TransferProgress { get; set; } 
        public string TransferStatus { get; set; }

        public bool IsSystemMessage { get; set; }

    }
}
