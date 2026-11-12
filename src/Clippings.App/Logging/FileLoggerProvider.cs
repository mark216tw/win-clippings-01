using System.Text;
using System.Threading.Channels;
using Clippings.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace Clippings.App.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 7;
    private const int ChannelCapacity = 2048;
    private const int MaxBatchSize = 64;

    private readonly string _directory;
    private readonly Channel<LogEntry> _channel;
    private readonly Task _writerTask;
    private bool _disposed;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        _ = Task.Run(CleanupOldLogs);

        _channel = Channel.CreateBounded<LogEntry>(
            new BoundedChannelOptions(ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
    }

    private void Enqueue(string category, LogLevel level, string message, Exception? exception)
    {
        if (_disposed)
        {
            return;
        }

        _channel.Writer.TryWrite(new LogEntry(DateTimeOffset.Now, category, level, message, exception));
    }

    private async Task WriteLoopAsync()
    {
        StreamWriter? writer = null;
        DateOnly? writerDate = null;

        void ResetWriter()
        {
            writer?.Dispose();
            writer = null;
            writerDate = null;
        }

        try
        {
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                var builder = new StringBuilder();
                var count = 0;
                while (count < MaxBatchSize && _channel.Reader.TryRead(out var entry))
                {
                    AppendEntry(builder, entry);
                    count++;
                }

                if (count == 0)
                {
                    continue;
                }

                var today = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);
                try
                {
                    var activeWriter = writer;
                    if (activeWriter is null || writerDate != today)
                    {
                        ResetWriter();
                        var path = Path.Combine(_directory, $"app-{today:yyyyMMdd}.log");
                        var stream = new FileStream(
                            path,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.ReadWrite,
                            4096,
                            FileOptions.Asynchronous);
                        activeWriter = new StreamWriter(stream);
                        writer = activeWriter;
                        writerDate = today;
                    }

                    await activeWriter.WriteAsync(builder.ToString()).ConfigureAwait(false);
                    await activeWriter.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    ResetWriter();
                }
                catch (UnauthorizedAccessException)
                {
                    ResetWriter();
                }
            }
        }
        finally
        {
            ResetWriter();
        }
    }

    private static void AppendEntry(StringBuilder builder, LogEntry entry)
    {
        builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [")
            .Append(entry.Level)
            .Append("] ")
            .Append(entry.Category)
            .Append(": ")
            .Append(entry.Message)
            .AppendLine();

        if (entry.Exception is not null)
        {
            builder.AppendLine(entry.Exception.ToString());
        }
    }

    private void CleanupOldLogs()
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        try
        {
            foreach (var file in Directory.EnumerateFiles(_directory, "app-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private readonly record struct LogEntry(
        DateTimeOffset Timestamp,
        string Category,
        LogLevel Level,
        string Message,
        Exception? Exception);

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _provider.Enqueue(_category, logLevel, formatter(state, exception), exception);
        }
    }

    public static string DefaultDirectory => PathUtil.LogDirectory(PathUtil.DefaultDataRoot);
}
