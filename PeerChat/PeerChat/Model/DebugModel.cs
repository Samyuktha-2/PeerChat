using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeerChat.Model
{
    public class DebugModel
    {
        public DateTime GetDateTime { get; set; } 
        public string Direction { get; set; }
        public string ContentType { get; set; }
        public string ContentSize { get; set; }
    }
}
