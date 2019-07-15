using System;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;

namespace ConsoleProxy
{
    // A TcpTunnel that passes on encrypted HTTPS requests and information between a client and host and vice versa
    class TcpTunnel : IDisposable
    {
        private const int BufferSize = 8192;

        public TcpTunnel() { }

        // When either has something to send call tunnel with the correct client and host
        public async Task Run(NetworkStream client, NetworkStream host)
        {
            await Task.WhenAny(Tunnel(client, host), Tunnel(host, client));
        }

        // Asychronously write data from the source stream to the destination stream
        private static async Task Tunnel(Stream source, Stream destination)
        {
            var buffer = new byte[BufferSize];

            int bytesRead;
            do
            {
                bytesRead = await source.ReadAsync(buffer, 0, BufferSize);
                await destination.WriteAsync(buffer, 0, bytesRead);
            } while (bytesRead > 0);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
