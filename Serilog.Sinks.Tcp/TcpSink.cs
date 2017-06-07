using System;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace Serilog.Sinks.Tcp
{
    public class TcpSink : ILogEventSink, IDisposable
    {
        readonly ITextFormatter _formatter;
        readonly ITcpClient _client;

        ConcurrentQueue<Tuple<byte[], int, int>> _sendqueue;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly object _syncQueue;
        readonly Task _sendThread;

        public TcpSink(ITcpClient clientImpl, Uri uri)
        {
            _formatter = new JsonFormatter();

            if (uri.Scheme.ToLower() == "tls")
                _client = clientImpl.Create(uri.Host, uri.Port, true);
            else
                _client = clientImpl.Create(uri.Host, uri.Port, false);

            _cancellationTokenSource = new CancellationTokenSource();
            _sendqueue = new ConcurrentQueue<Tuple<byte[], int, int>>();
            _sendThread = Task.Factory.StartNew((arg) => SendLoop(), _cancellationTokenSource.Token, TaskCreationOptions.LongRunning);
            _syncQueue = new object();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _client.Close();
            lock (_syncQueue)
                Monitor.Pulse(_syncQueue);
            _sendThread.Wait();
            _cancellationTokenSource.Dispose();
            _sendqueue = new ConcurrentQueue<Tuple<byte[], int, int>>();
        }

        public void Emit(LogEvent logEvent)
        {
            var sb = new StringBuilder();
            _formatter.Format(logEvent, new StringWriter(sb));
            var data = sb.ToString().Replace("RenderedMessage", "message");

            /* Don't allow more than ... messages in the queue */
            if (_sendqueue.Count > 3000)
            {
                Tuple<byte[], int, int> tmp;
                _sendqueue.TryDequeue(out tmp);
            }
            _sendqueue.Enqueue(new Tuple<byte[], int, int>(Encoding.UTF8.GetBytes(data), 0, data.Length));
            lock (_syncQueue)
                Monitor.Pulse(_syncQueue);

        }

        void SendLoop()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (_client != null && _client.Connected)
                    {
                        if (_sendqueue.Count == 0)
                        {
                            /* This is where we make sure that the while loop doesn't eat CPU time when 
							 * there's nothing in the queue */
                            lock (_syncQueue)
                                Monitor.Wait(_syncQueue);
                        }
                        /* Because of the Dispose method, this can still be 0 after the Monitor.Wait */
                        if (_sendqueue.Count > 0)
                        {
                            /* Here we actually get a buffer and only remove it when sending has succeeded */
                            Tuple<byte[], int, int> buffer;
                            if (_sendqueue.TryPeek(out buffer))
                            {
                                _client.Write(buffer.Item1, buffer.Item2, buffer.Item3);
                                while (!_sendqueue.TryDequeue(out buffer)) ;
                            }
                        }
                    }
                    else
                    {
                        /* This is a very easy reconnect state machine with a simple backoff timer of
						 * maximum 1 minute */
                        int backoffCntr = 0;
                        while (!_client.Connected)
                        {
                            Task.Delay(TimeSpan.FromMilliseconds(backoffCntr * 500)).Wait(_cancellationTokenSource.Token);
                            try
                            {
                                _client.Connect();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                            }
                            if (backoffCntr < 120)
                                ++backoffCntr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    _client.Close();
                }
            }
        }
    }
}
