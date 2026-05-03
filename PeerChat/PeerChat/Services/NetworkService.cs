using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PeerChat.Services
{
    public class NetworkService
    {
        public async Task<TcpClient> StartHostAsync(int port, CancellationToken token)
        {
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            using (token.Register(() => listener.Stop()))
            {
                return await listener.AcceptTcpClientAsync();
            }
        }
        public async Task<TcpClient> ConnectAsync(string ip, int port)
        {
            var client = new TcpClient();

            var connectTask = client.ConnectAsync(ip, port);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                client.Close();
                throw new TimeoutException("Connection timed out (5s)");
            }

            await connectTask; // ensures exception if failed
            return client;
        }
    }
}
