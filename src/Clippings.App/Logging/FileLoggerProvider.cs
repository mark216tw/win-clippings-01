using System.Text;
using System.Threading.Channels;
using Clippings.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace Clippings.App.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const int RetentionDays = 7;

    private readonly string _directory;
    private readonly Channel<LogEntry> _channel;
    private readonly Task _writerTask;
    private bool _disposed;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
        CleanupOldLogs();

        _channel = Channel.CreateUnbounded<LogEntry>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
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

        _channel.Writer.TryWrite(new LogEntry(category, level, message, exception));
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            var builder = new StringBuilder();
            builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
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

            try
            {
                var path = Path.Combine(_directory, $"app-{DateTimeOffset.Now:yyyyMMdd}.log");
                await File.AppendAllTextAsync(path, builder.ToString()).ConfigureAwait(false);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
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
