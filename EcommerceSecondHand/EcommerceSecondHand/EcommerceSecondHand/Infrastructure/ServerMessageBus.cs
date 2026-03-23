using System.Threading.Channels;

namespace EcommerceSecondHand.Infrastructure
{
    public interface IServerMessageBus
    {
        void Publish(string line);
        IAsyncEnumerable<string> SubscribeAsync(CancellationToken ct);

        // THÊM method này để controller gọi
        Task BroadcastAsync(string line, CancellationToken ct = default);
    }

    public sealed class ServerMessageBus : IServerMessageBus
    {
        private readonly Channel<string> _c = Channel.CreateUnbounded<string>();
        private readonly ConnectionRegistry _registry;

        // TIÊM ConnectionRegistry để gửi đến tất cả TCP clients
        public ServerMessageBus(ConnectionRegistry registry)
        {
            _registry = registry;
        }

        public void Publish(string line) => _c.Writer.TryWrite(line);

        public async IAsyncEnumerable<string> SubscribeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            while (await _c.Reader.WaitToReadAsync(ct))
            {
                while (_c.Reader.TryRead(out var s))
                    yield return s;
            }
        }

        // Gửi ra SSE log + phát tán tới tất cả TCP clients
        public async Task BroadcastAsync(string line, CancellationToken ct = default)
        {
            // đẩy lên stream realtime (SSE) trong UI
            Publish(line);

            // gửi qua TCP cho mọi client đã kết nối
            await _registry.BroadcastAsync(line, ct);
        }
    }
}
