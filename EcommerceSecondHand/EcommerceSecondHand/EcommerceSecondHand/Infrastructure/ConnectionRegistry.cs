using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace EcommerceSecondHand.Infrastructure;

public sealed class ConnectionRegistry
{
    private readonly ConcurrentDictionary<string, (TcpClient Client, NetworkStream Stream)> _conns = new();

    public string Add(TcpClient client)
    {
        var id = Guid.NewGuid().ToString("N");
        _conns[id] = (client, client.GetStream());
        return id;
    }

    public void Remove(string id)
    {
        if (_conns.TryRemove(id, out var v))
        {
            try { v.Stream.Close(); } catch { }
            try { v.Client.Close(); } catch { }
        }
    }

    public int Count => _conns.Count;
    public IReadOnlyCollection<string> ConnectionIds => _conns.Keys.ToList();


    public async Task BroadcastAsync(string line, CancellationToken ct = default)
    {
        var payload = Encoding.UTF8.GetBytes(line.EndsWith("\n") ? line : line + "\n");
        var toRemove = new List<string>();

        foreach (var kv in _conns)
        {
            var (client, stream) = kv.Value;
            if (!client.Connected) { toRemove.Add(kv.Key); continue; }

            try { await stream.WriteAsync(payload, ct); }
            catch { toRemove.Add(kv.Key); }
        }
        foreach (var id in toRemove) Remove(id);
    }
}
