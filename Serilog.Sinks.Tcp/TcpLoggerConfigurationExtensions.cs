using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace Serilog.Sinks.Tcp
{
    public static class TcpLoggerConfigurationExtensions
    {
        public static LoggerConfiguration TcpSink(this LoggerSinkConfiguration loggerConfiguration, ITcpClient clientImpl,
                                                  string uri, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            var sink = new TcpSink(clientImpl, new Uri(uri));
            return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
        }
    }
}
