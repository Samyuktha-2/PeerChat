using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeerChat
{
    public static class MessageProtocol
    {
        public const byte Text = 0x01;
        public const byte Image = 0x02;
        public const byte Video = 0x03;
        public const byte Typing = 0x04;
        public const byte Disconnect = 0x05;

        public static async Task SendFrameAsync(NetworkStream stream, byte type, byte[] payload, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            payload = payload ?? new byte[0];

            var header = new byte[5];
            header[0] = type;
            WriteInt32BigEndian(header, 1, payload.Length);

            await stream.WriteAsync(header, 0, header.Length, cancellationToken);
            if (payload.Length > 0)
            {
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken);
            }

            await stream.FlushAsync(cancellationToken);
        }

        public static async Task<Tuple<byte, byte[]>> ReceiveFrameAsync(NetworkStream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var header = new byte[5];
            await ReadExactlyAsync(stream, header, 0, header.Length, cancellationToken);

            var length = ReadInt32BigEndian(header, 1);
            if (length < 0)
            {
                throw new IOException("Invalid frame length.");
            }

            var payload = new byte[length];
            if (length > 0)
            {
                await ReadExactlyAsync(stream, payload, 0, length, cancellationToken);
            }

            return Tuple.Create(header[0], payload);
        }

        public static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken = default(CancellationToken))
        {
            var totalRead = 0;
            while (totalRead < count)
            {
                var bytesRead = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
                if (bytesRead == 0)
                {
                    throw new IOException("The remote peer closed the connection.");
                }

                totalRead += bytesRead;
            }
        }

        public static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
        }

        public static int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            var bytes = new byte[4];
            Buffer.BlockCopy(buffer, offset, bytes, 0, 4);
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes, 0));
        }

        public static void WriteInt64BigEndian(byte[] buffer, int offset, long value)
        {
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 8);
        }

        public static long ReadInt64BigEndian(byte[] buffer, int offset)
        {
            var bytes = new byte[8];
            Buffer.BlockCopy(buffer, offset, bytes, 0, 8);
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt64(bytes, 0));
        }
    }
}
