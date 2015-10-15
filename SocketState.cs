using System;

namespace SharpSocket
{
    [Flags]
    public enum SocketState : byte
    {
        Disposed = 0,
        Connecting = 0x1,
        Connected = 0x2,
        Initialized = 0x4,
        Active = Connected | Initialized,
    }
}
