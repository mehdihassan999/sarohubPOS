// src/SaroHub.Infrastructure/Services/FileAppLogger.cs
using SaroHub.Core.Abstractions;

namespace SaroHub.Infrastructure.Services;

/// <summary>Simple daily-rolling file logger. Thread-safe via a dedicated lock.</summary>
public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private readonly string _folder;
    private readonly object _lock = new();
    private string   _currentDate = "";
    private StreamWriter? _writer;

    public FileAppLogger(string folder) => _folder = folder;

    public void Info(string message)  => Write("INFO ", message, null);
    public void Warn(string message)  => Write("WARN ", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private void Write(string level, string message, Exception? ex)
    {
        lock (_lock)
        {
            try
            {
                EnsureWriter();
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                _writer!.WriteLine(line);
                if (ex is not null) _writer.WriteLine(ex.ToString());
                _writer.Flush();
            }
            catch { /* logger must never crash the app */ }
        }
    }

    private void EnsureWriter()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (today == _currentDate && _writer is not null) return;

        _writer?.Dispose();
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"sarohub-{today}.log");
        _writer = new StreamWriter(path, append: true, System.Text.Encoding.UTF8);
        _currentDate = today;
    }

    public void Dispose()
    {
        lock (_lock) { _writer?.Dispose(); }
    }
}
