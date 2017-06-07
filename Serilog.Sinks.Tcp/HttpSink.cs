using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Serilog;
using Serilog.Core;
using Serilog.Formatting;
using Serilog.Events;
using Serilog.Configuration;

namespace Serilog.Sinks.Tcp
{
    public class HttpSink : ILogEventSink
    {
        HttpMessageHandler _msgHandler;
        string _url;
        string _apiKey;
        readonly ITextFormatter _formatter;

        public HttpSink(string url, string apiKey, HttpMessageHandler handler = null)
        {
            _formatter = new Serilog.Sinks.Tcp.JsonFormatter();
            _url = url;
            _apiKey = apiKey;
            _msgHandler = handler == null ? new HttpClientHandler() : handler;
        }

        public void Emit(LogEvent logEvent)
        {
            var sb = new StringBuilder();
            _formatter.Format(logEvent, new StringWriter(sb));
            var data = sb.ToString().Replace("RenderedMessage", "message");

            Task.Factory.StartNew(async () => 
            {
                using (var httpClient = new HttpClient(_msgHandler, false))
                {
                    var content = new StringContent(data);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    content.Headers.Add("ApiKey", _apiKey);

                    await httpClient.PostAsync(_url, content);
                }
            });
        }
    }

    public static class HttpLoggerConfigurationExtensions
    {
        public static LoggerConfiguration HttpSink(this LoggerSinkConfiguration loggerConfiguration, string url, string apiKey, LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum)
        {
            var sink = new HttpSink(url, apiKey);
            return loggerConfiguration.Sink(sink, restrictedToMinimumLevel);
        } 
    }
}