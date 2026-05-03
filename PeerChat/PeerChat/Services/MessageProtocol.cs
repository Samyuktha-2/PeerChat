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
        public static async Task SendFrameAsync(NetworkStream stream, int type, byte[] payload)
        {
            byte[] typeBytes = BitConverter.GetBytes(type);
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);

            await stream.WriteAsync(typeBytes, 0, typeBytes.Length);
            await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
            await stream.WriteAsync(payload, 0, payload.Length);
        }

        public static async Task<(int type, byte[] payload)> ReceiveFrameAsync(NetworkStream stream)
        {
            byte[] typeBytes = new byte[4];
            byte[] lengthBytes = new byte[4];

            await stream.ReadAsync(typeBytes, 0, 4);
            await stream.ReadAsync(lengthBytes, 0, 4);

            int type = BitConverter.ToInt32(typeBytes, 0);
            int length = BitConverter.ToInt32(lengthBytes, 0);

            byte[] payload = new byte[length];
            await stream.ReadAsync(payload, 0, length);

            return (type, payload);
        }
    }
}
