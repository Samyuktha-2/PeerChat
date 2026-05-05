using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeerChat.Model
{
    public class DebugModel
    {
        public string User { get; set; }
        public string Direction { get; set; }
        public string ContentType { get; set; }
        public byte[] PayloadBytes { get; set; }
    }
}
