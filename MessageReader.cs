using System;
using System.Net.Sockets;

namespace SharpSocket
{
    public class MessageReader<TAck> : IMessageReader<TAck>, IDisposable
    {
        #region Fields

        private readonly ISocketWrapper _socket;
        private readonly SocketAsyncEventArgs _readEventArgs;

        #endregion
        
        public MessageReader(ISocketWrapper socket, byte[] buffer, int offset, int bufferSize, IByteToMessageConverter<TAck> converter)
        {
            _socket = socket;

            _readEventArgs = new SocketAsyncEventArgs();
            _readEventArgs.SetBuffer(buffer, offset, bufferSize);
            _readEventArgs.Completed += IOCompleted;
            _readEventArgs.Completed += ReadEventCompleted;
        }

        public event EventHandler<SocketAsyncEventArgs> OnReadCompleted;

        /// <summary>
        ///     Read repeat
        /// </summary>
        private void ReadRepeat()
        {
            try
            {
                if (_socket.ReceiveAsync(_readEventArgs)) return;
                ReadEventCompleted(_socket, _readEventArgs);
            }
            catch (Exception)
            {
                //ignore disposed
                _socket.Disconnect();
            }
        }

        // ReSharper disable once InconsistentNaming
        private void IOCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (0 != args.BytesTransferred && args.SocketError == SocketError.Success)
                return;

            _socket.Disconnect(args);
        }

        /// <summary>
        ///     Default receive arg complete event handler
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="args"></param>
        private void ReadEventCompleted(object socket,
            SocketAsyncEventArgs args)
        {
            if (args.SocketError != SocketError.Success) return;

            if (args.BytesTransferred < 1) return;

            try
            {
                OnReadCompleted?.Invoke(socket, args);
                args.SetBuffer(args.Offset, args.Count);
            }
            catch (Exception)
            {
                //ignore
                _socket.Disconnect(args);
                return;
            }

            ReadRepeat();
        }

        #region IDisposal

        public void Dispose()
        {
            try
            {
                _readEventArgs.Dispose();
            }
            catch
            {
                //ignore some error
            }
        }

        #endregion


#if DEBUG
        /// <summary>
        ///     debug event args for log or something
        /// </summary>
        public event EventHandler<SocketAsyncEventArgs> DebugReadEvent
        {
            add { _readEventArgs.Completed += value; }
            remove { _readEventArgs.Completed -= value; }
        }
#endif
    }
}