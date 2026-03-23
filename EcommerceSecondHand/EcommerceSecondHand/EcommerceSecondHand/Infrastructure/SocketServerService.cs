using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EcommerceSecondHand.Infrastructure
{
    public sealed class SocketServerService : BackgroundService
    {
        private readonly ILogger<SocketServerService> _logger;
        private readonly IPEndPoint _endPoint;
        private readonly ConnectionRegistry _registry;
        private readonly IServerMessageBus _bus; // để stream log lên UI (SSE)

        public SocketServerService(
            ILogger<SocketServerService> logger,
            IConfiguration cfg,
            ConnectionRegistry registry,
            IServerMessageBus bus)
        {
            _logger = logger;
            _registry = registry;
            _bus = bus;

            var ip = cfg.GetValue<string>("TcpSocket:BindIp") ?? "0.0.0.0";
            var port = cfg.GetValue<int?>("TcpSocket:Port") ?? 5005;
            _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(_endPoint);
                listener.Start();
                _logger.LogInformation("TCP server listening on {EndPoint}", _endPoint);
                _bus.Publish($"[server] listening on {_endPoint}");

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (!listener.Pending())
                    {
                        await Task.Delay(60, stoppingToken);
                        continue;
                    }

                    var client = await listener.AcceptTcpClientAsync();
                    client.NoDelay = true;

                    // ĐĂNG KÝ client vào registry để UI đếm được
                    var id = _registry.Add(client);
                    _logger.LogInformation("Client connected: {Remote} (id {Id})", client.Client.RemoteEndPoint, id);
                    _bus.Publish($"[+] {id} connected {client.Client.RemoteEndPoint}");

                    _ = HandleClientAsync(id, client, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SocketServerService stopping.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCP listener error");
            }
            finally
            {
                try { listener?.Stop(); } catch { }
                _logger.LogInformation("TCP server stopped.");
            }
        }

        private async Task HandleClientAsync(string id, TcpClient client, CancellationToken ct)
        {
            using var _ = client;
            using var stream = client.GetStream();
            var buffer = new byte[8192];
            var sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (n == 0) break; // client đóng

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, n));

                    string? line;
                    while ((line = ExtractLine(sb)) != null)
                    {
                        // demo: echo/ping
                        var resp = line.Trim() == "PING" ? "PONG\n" : $"ECHO: {line}\n";
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(resp), ct);

                        // đẩy log ra UI
                        _bus.Publish($"{id}: {line}");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Client {Id} error", id);
            }
            finally
            {
                _registry.Remove(id);
                _bus.Publish($"[-] {id} disconnected");
            }
        }

        private static string? ExtractLine(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++)
                if (sb[i] == '\n')
                {
                    var line = sb.ToString(0, i);
                    sb.Remove(0, i + 1);
                    return line;
                }
            return null;
        }
    }
}
