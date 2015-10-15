using System;

namespace SharpSocket
{
    public interface IMessageConverter<in TReq, TAck> : IMessageToByteConverter<TReq> ,IByteToMessageConverter<TAck>
    {
        event Action<TAck, int> MessageConverted;
    }

    public interface IMessageToByteConverter<in TReq> : IErrorCodeContainer
    {
        void GetByte(TReq message, out byte[] messageBytes, out int errorCode);
    }

    public interface IByteToMessageConverter<out TAck> : IErrorCodeContainer
    {
        TAck Read(byte[] buffer, int offset, int bytesTransferred);
    }

    public interface IErrorCodeContainer
    {
        int NoErrorCode { get; }
    }
}