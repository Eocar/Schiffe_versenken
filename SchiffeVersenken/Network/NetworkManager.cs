using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SchiffeVersenken.Network
{
    public class NetworkManager : IDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private readonly CancellationTokenSource _cts = new();

        public event Action<string>? MessageReceived;
        public event Action? Connected;
        public event Action<string?>? Disconnected;

        public bool IsConnected => _client?.Connected == true;

        public async Task HostAsync(int port)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _client = await _listener.AcceptTcpClientAsync(_cts.Token);
            SetupStreams();
            Connected?.Invoke();
            _ = ReceiveLoopAsync();
        }

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port, _cts.Token);
            SetupStreams();
            Connected?.Invoke();
            _ = ReceiveLoopAsync();
        }

        private void SetupStreams()
        {
            var stream = _client!.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };
        }

        public async Task SendAsync(string message)
        {
            if (_writer != null)
                await _writer.WriteLineAsync(message);
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var line = await _reader!.ReadLineAsync(_cts.Token);
                    if (line == null) break;
                    MessageReceived?.Invoke(line);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown via cancellation token - do not raise Disconnected
                return;
            }
            catch (Exception ex)
            {
                // Connection closed unexpectedly; surface the error via Disconnected
                Disconnected?.Invoke(ex.Message);
                return;
            }
            Disconnected?.Invoke(null);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Dispose();
            _listener?.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
