using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeerChat.Model
{
    public class MessageModel
    {
        public string Text { get; set; }
        public bool IsSentByMe { get; set; }
    }
}
