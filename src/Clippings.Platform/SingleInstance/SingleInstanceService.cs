using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Clippings.Platform.SingleInstance;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\Clippings.SingleInstance.7F2A";
    private const string PipeName = "Clippings.Activation.7F2A";
    private const int MaxPayloadBytes = 1024;

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;
    private bool _disposed;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
        }
    }

    public bool IsFirstInstance { get; }

    public event EventHandler<ActivationRequestedEventArgs>? ActivationRequested;

    public void StartListening()
    {
        if (!IsFirstInstance || _listenerTask is not null)
        {
            return;
        }

        _listenerTask = Task.Run(() => ListenAsync(_cancellation.Token));
    }

    public bool NotifyExistingInstance(IReadOnlyList<string> arguments)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguments) + "\n");
            client.Write(payload, 0, payload.Length);
            client.Flush();
            return true;
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                var arguments = await ReadArgumentsAsync(server, token).ConfigureAwait(false);
                ActivationRequested?.Invoke(this, new ActivationRequestedEventArgs(arguments));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                try
                {
                    await Task.Delay(200, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static async Task<IReadOnlyList<string>> ReadArgumentsAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[MaxPayloadBytes];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length), token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            length += read;
            var newlineIndex = Array.IndexOf(buffer, (byte)'\n', 0, length);
            if (newlineIndex >= 0)
            {
                length = newlineIndex;
                break;
            }
        }

        if (length == 0)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(buffer.AsSpan(0, length)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();

        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            _mutex.Dispose();
        }
    }
}
