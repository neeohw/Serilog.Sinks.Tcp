using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Net.Sockets;

namespace Serilog.Sinks.Tcp.Connector
{
    public class DefaultTcpConnector : ITcpClient
    {
        string _host;
        int _port;
        bool _useTls;
        TcpClient _client;
        Stream _stream;

        public bool Connected => _client != null && _client.Connected;

        public DefaultTcpConnector() { }
        public DefaultTcpConnector(string host, int port, bool useTls)
        {
            _host = host;
            _port = port;
            _useTls = useTls;
        }

        public ITcpClient Create(string host, int port, bool useTls)
        {
            return new DefaultTcpConnector(host, port, useTls);
        }

        public void Connect()
        {
            _client = new TcpClient();
            _client.Connect(_host, _port);
            /* When connected we check if we need to securify the stream and get a hold of it */
            if (_useTls)
            {
                var sslStream = new SslStream(_client.GetStream());
                sslStream.AuthenticateAsClient(_host);
                _stream = sslStream;
                if (!sslStream.IsAuthenticated)
                    throw new AuthenticationException();
            }
            else
                _stream = _client.GetStream();
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public void Close()
        {
            if (_stream != null)
                _stream.Dispose();
            if (_client != null)
                _client.Close();
        }
    }
}