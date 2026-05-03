using System;

namespace PeerChat
{
    public class DebugFrameLog
    {
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; }
        public byte Type { get; set; }
        public int PayloadSize { get; set; }

        public string Display
        {
            get
            {
                return string.Format("{0:HH:mm:ss.fff}  {1}  type=0x{2:X2}  bytes={3}", Timestamp, Direction, Type, PayloadSize);
            }
        }
    }
}
