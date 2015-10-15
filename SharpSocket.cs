using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SharpSocket
{
    /// <summary>
    ///     Client Socket.
    ///     Has Request, Acknowlege templates
    /// </summary>
    public class SharpSocket<TReq, TAck> : ISocketWrapper
    {
        #region Constants

        public const int DefaultRetryLimit = 10;
        public const int DefaultInterval = 1500;

        #endregion
        
        #region Fields

        private readonly Timer _connectTimer;

        private readonly MessageSender<TReq> _sender;
        private readonly MessageReader<TAck> _reader;

        #endregion


        //TODO private readonly HeartBeatMaker<TReq> _heartBeatMaker;
/*TODO
        public bool HeartBeatEnable { get; set; }
        public TReq HeartBeatMessage { get; set; }
*/


        public SharpSocket(IMessageConverter<TReq, TAck> converter, EndPoint endPoint)
        {
            //ip endpoint set _connect event args property
            IpEndpoint = endPoint;
            _reader = new MessageReader<TAck>(this, new byte[1024],0, 1024, converter);
            _sender = new MessageSender<TReq>(this, converter);

            _connectEventArgs = new SocketAsyncEventArgs {RemoteEndPoint = IpEndpoint};
            _connectEventArgs.Completed += DefaultConnectCompleted;

            RetryLimit = DefaultRetryLimit;
            _connectTimer = new Timer {Interval = DefaultInterval, AutoReset = true};
            _connectTimer.Elapsed += CheckReconnect;

            State = SocketState.Initialized;
        }
        
        
        #region connection

        private readonly object _stateLock = new object();

        protected Socket Socket { set; get; }

        public event Action<SocketState> OnStateChange;
        public SocketState State { get; set; }

        public IPEndPoint RemoteEndPoint => (IPEndPoint) Socket.RemoteEndPoint;

        public IPEndPoint LocalEndPoint => (IPEndPoint) Socket.LocalEndPoint;

        public event EventHandler<SocketAsyncEventArgs> Disconnected;

        /// <summary>
        ///     For reuse, Socket and eventargs are not disposed.
        /// </summary>
        public void Disconnect(SocketAsyncEventArgs e = null)
        {
            if (!OffState(SocketState.Connected)) return;
            try
            {
                Socket.Shutdown(SocketShutdown.Send);
                Socket.Disconnect(false);
            }
            catch
            {
                //ignore
            }

            Disconnected?.Invoke(this, e);
        }

        public bool SendAsync(SocketAsyncEventArgs writeEventArgs)
        {
            return Socket.SendAsync(writeEventArgs);
        }

        public bool ReceiveAsync(SocketAsyncEventArgs readEventArgs)
        {
            return Socket.ReceiveAsync(readEventArgs);
        }

        public virtual void Dispose()
        {
            Disconnect();
            lock (_stateLock)
            {
                State = SocketState.Disposed;
            }
        }

        public virtual void Activate()
        {
            State = SocketState.Active;
        }

        protected bool OffState(SocketState state)
        {
            lock (_stateLock)
            {
                if ((State & state) == 0)
                    return false;
                State ^= state;
                return true;
            }
        }

        protected bool OnState(SocketState state)
        {
            lock (_stateLock)
            {
                if ((State & state) != 0)
                    return false;
                State |= state;
                return true;
            }
        }

        protected bool IsState(SocketState state)
        {
            lock (_stateLock)
            {
                return (State & state) != 0;
            }
        }

        private void InitSocket()
        {
            Socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp)
            {NoDelay = true};
        }

        /// <summary>
        ///     Default Event handler for connection success.
        ///     Activate socket
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DefaultConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                return;
            }
            _connectTimer.Stop();
            // timer set
            RetryCount = 0;

            Activate();

            //TODO if (HeartBeatEnable) _heartBeatMaker.Start();

            ConnectSuccess?.Invoke(sender, e);

            Interlocked.Exchange(ref _connecting, 0);
        }

        public EndPoint IpEndpoint
        {
            get { return _ipEndpoint; }
            set
            {
                _ipEndpoint = value;
                _connectEventArgs.RemoteEndPoint = value;
            }
        }

        public int RetryLimit { get; set; }
        public int RetryCount { get; private set; }

        private readonly SocketAsyncEventArgs _connectEventArgs;
        private EndPoint _ipEndpoint;
        private int _connecting;

        public event EventHandler<SocketAsyncEventArgs> ConnectFailed;
        public event EventHandler<SocketAsyncEventArgs> ConnectSuccess;

        /// <summary>
        ///     Default handler for connect timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckReconnect(object sender, ElapsedEventArgs e)
        {
            //failed. retry
            if (_connectEventArgs.SocketError != SocketError.Success && (0 == RetryLimit || RetryCount < RetryLimit))
            {
                RetryCount++;
                ConnectAsync();
                return;
            }

            //connection retry exceed retry limit
            _connectTimer.Stop();

            if (_connectEventArgs.SocketError == SocketError.Success || ConnectFailed == null) return;

            ConnectFailed(sender, _connectEventArgs);
            Interlocked.Exchange(ref _connecting, 0);
        }

        public void Connect(IPEndPoint endPoint)
        {
            IpEndpoint = endPoint;
            Connect();
        }

        public void Connect(string ipAddress, int port)
        {
            if (Regex.IsMatch(ipAddress, @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}"))
            {
                IpEndpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            }
            else if (Dns.GetHostAddresses(ipAddress).Length > 0)
            {
                IpEndpoint = new IPEndPoint(Dns.GetHostAddresses(ipAddress)[0], port);
            }
            else
            {
                throw new InvalidDataException($"address {ipAddress} is not suitable.");
            }

            Connect();
        }

        public void Connect()
        {
            if (Interlocked.CompareExchange(ref _connecting, 1, 0) > 0) return;
            if (_connectTimer.Enabled) return;

            InitSocket();
            _connectTimer.Start();
            ConnectAsync();
        }

        private void ConnectAsync()
        {
            if (!Socket.ConnectAsync(_connectEventArgs))
                DefaultConnectCompleted(null, _connectEventArgs);
        }

        public void Reconnect(object sender, SocketAsyncEventArgs e)
        {
            Connect();
        }

        #endregion
    }
}