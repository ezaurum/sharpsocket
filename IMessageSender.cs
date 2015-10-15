namespace SharpSocket
{
    /// <summary>
    ///     Message sender interface
    /// </summary>
    /// <typeparam name="TReq"></typeparam>
    public interface IMessageSender<in TReq>
    {
        /// <summary>
        ///     Send message, if message converting error occurs,
        ///  return error code.
        /// </summary>
        /// <param name="message"></param>
        /// <returns>Same as message converter error code</returns>
        int Send(TReq message);
    }

    /// <summary>
    ///     Message sender interface
    /// </summary>
    /// <typeparam name="TAck"></typeparam>
    public interface IMessageReader<in TAck>
    {


    }
}