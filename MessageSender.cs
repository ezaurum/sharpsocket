using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace SharpSocket
{
    /// <summary>
    ///     Message Sender, 
    /// Socket Wrapper, Request, 
    /// </summary>
    /// <typeparam name="TReq"></typeparam>
    public class MessageSender<TReq> : IMessageSender<TReq>, IDisposable
    {
        private readonly Queue<byte[]> _sendingQueue = new Queue<byte[]>();
        private readonly object _sendingQueueLock = new object();
        private readonly ISocketWrapper _socket;
        private readonly SocketAsyncEventArgs _writeEventArgs;
        private readonly int _noErrorCode;
        private readonly IMessageToByteConverter<TReq> _converter;

        private int _sendingMessages;

        internal MessageSender(ISocketWrapper socket, IMessageToByteConverter<TReq> converter)
        {
            _socket = socket;
            _converter = converter;
            _writeEventArgs = new SocketAsyncEventArgs();
            _writeEventArgs.Completed += IOCompleted;
            _writeEventArgs.Completed += WriteEventCompleted;
            _noErrorCode = _converter.NoErrorCode;
        }

        public void Dispose()
        {
            try
            {
                _writeEventArgs.Dispose();
            }
            catch
            {
                //ignore some error
            }
        } 

        public int Send(TReq message)
        {
            byte[] messageBytes;
            int errorCode;
            _converter.GetByte(message, out messageBytes, out errorCode);
            if (_noErrorCode != errorCode) return errorCode;

            lock (_sendingQueueLock)
            {
                if (Interlocked.Increment(ref _sendingMessages) > 1)
                {
                    _sendingQueue.Enqueue(messageBytes);
                    return _noErrorCode;
                }
            }
            SendAsync(messageBytes);
            return _noErrorCode;
        }

        private void SendAsyncFromQueue()
        {
            byte[] byteArray;
            lock (_sendingQueueLock)
            {
                byteArray = _sendingQueue.Dequeue();
            }

            SendAsync(byteArray);
        }

        protected void SendAsync(byte[] byteArray)
        {
            try
            {
                _writeEventArgs.SetBuffer(byteArray, 0, byteArray.Length);
                if (_socket.SendAsync(_writeEventArgs)) return;
                WriteEventCompleted(_socket, _writeEventArgs);
            }
            catch (Exception)
            {
                // ignore all exception. just disconnect.
                _socket.Disconnect(_writeEventArgs);
            }
        }

        private void WriteEventCompleted(object o, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success) return;
            if (Interlocked.Decrement(ref _sendingMessages) < 1) return;
            SendAsyncFromQueue();
        }

        // ReSharper disable once InconsistentNaming
        private void IOCompleted(object sender, SocketAsyncEventArgs args)
        {
            if (0 != args.BytesTransferred 
                && args.SocketError == SocketError.Success)
                return;

            _socket.Disconnect(args);
        }

#if DEBUG
        /// <summary>
        ///     debug event args for log or something
        /// </summary>
        public event EventHandler<SocketAsyncEventArgs> DebugWriteEvent
        {
            add { _writeEventArgs.Completed += value; }
            remove { _writeEventArgs.Completed -= value; }
        }
#endif
    }
}