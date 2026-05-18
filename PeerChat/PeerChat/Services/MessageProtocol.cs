using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PeerChat.Services
{
    public static class MessageProtocol
    {

        public static void SendFrameAsync(NetworkStream stream, byte type, byte[] payload)
        {
            byte[] header = new byte[5];

            header[0] = type;

            int length = payload.Length;
            header[1] = (byte)((length >> 24) & 0xFF);
            header[2] = (byte)((length >> 16) & 0xFF);
            header[3] = (byte)((length >> 8) & 0xFF);
            header[4] = (byte)(length & 0xFF);

            stream.WriteAsync(header, 0, 5);  //sends header
            stream.WriteAsync(payload, 0, payload.Length);  //sends payload ie data
        }

        public static (byte type, byte[] payload) ReceiveFrameAsync(NetworkStream stream)
        {
            byte[] header = new byte[5];
            int read = 0;

            while (read < 5)
            {
                int r = stream.ReadAsync(header, read, 5 - read).GetAwaiter().GetResult();
                if (r == 0) throw new Exception("Disconnected");
                read += r;
            }

            byte type = header[0];

            int length =
                (header[1] << 24) |
                (header[2] << 16) |
                (header[3] << 8) |
                header[4];

            byte[] payload = new byte[length];
            read = 0;

            while (read < length)
            {
                int r = stream.ReadAsync(payload, read, length - read).GetAwaiter().GetResult();
                if (r == 0) throw new Exception("Disconnected");
                read += r;
            }

            return (type, payload);
        }
    }
}
