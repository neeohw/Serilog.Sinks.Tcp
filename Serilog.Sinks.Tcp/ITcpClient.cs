namespace Serilog.Sinks.Tcp
{
    public interface ITcpClient
    {
        bool Connected { get; }

        ITcpClient Create(string host, int port, bool useTls);
        void Connect();
        void Write(byte[] buffer, int offset, int count);
        void Close(); // Will dispose
    }
}
