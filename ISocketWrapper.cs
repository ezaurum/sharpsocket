using System;
using System.Net;
using System.Net.Sockets;

namespace SharpSocket
{
    public interface ISocketWrapper : IDisposable
    {
        event EventHandler<SocketAsyncEventArgs> Disconnected;
        event Action<SocketState> OnStateChange;

        SocketState State { get; set; }
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }

        void Activate();
        /// <summary>
        /// For reuse, Socket and eventargs are not disposed.
        /// </summary>
        void Disconnect(SocketAsyncEventArgs e = null);

        bool SendAsync(SocketAsyncEventArgs writeEventArgs);
        bool ReceiveAsync(SocketAsyncEventArgs readEventArgs);
    }
}