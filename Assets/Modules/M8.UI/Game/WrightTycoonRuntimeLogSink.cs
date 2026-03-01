using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace WrightTycoon.Game.RuntimeData;

public static class WrightTycoonRuntimeLogSink
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static string? _logsDirectory;
    private static string? _sessionLogPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        Application.logMessageReceivedThreaded += HandleLogMessageReceived;

        try
        {
            WrightTycoonRuntimePaths.EnsurePlayerInfoLayout();
            _logsDirectory = WrightTycoonRuntimePaths.GetLogsDirectory();
            _sessionLogPath = BuildSessionLogPath();
            AppendRaw(
                $"=== Wright Tycoon session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}" +
                $"Player Info root: {WrightTycoonRuntimePaths.GetPlayerInfoRootPath()}{Environment.NewLine}");
        }
        catch
        {
            // Logging should never block startup.
        }
    }

    public static void EnsureInitializedForCurrentProcess()
    {
        Initialize();
    }

    public static void WriteDiagnostic(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Initialize();
        AppendRaw($"[Diagnostic] {message}{Environment.NewLine}");
    }

    private static void HandleLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        var builder = new StringBuilder(256);
        builder.Append('[')
            .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("] [")
            .Append(type)
            .Append("] ")
            .AppendLine(condition ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            builder.AppendLine(stackTrace.TrimEnd());
        }

        AppendRaw(builder.ToString());
    }

    private static void AppendRaw(string message)
    {
        lock (SyncRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_logsDirectory))
                {
                    WrightTycoonRuntimePaths.EnsurePlayerInfoLayout();
                    _logsDirectory = WrightTycoonRuntimePaths.GetLogsDirectory();
                }

                Directory.CreateDirectory(_logsDirectory!);

                if (string.IsNullOrWhiteSpace(_sessionLogPath))
                {
                    _sessionLogPath = BuildSessionLogPath();
                }

                File.AppendAllText(_sessionLogPath!, message);
            }
            catch
            {
                // Logging should never recurse or throw into game code.
            }
        }
    }

    private static string BuildSessionLogPath()
    {
        var logsDirectory = string.IsNullOrWhiteSpace(_logsDirectory)
            ? WrightTycoonRuntimePaths.GetLogsDirectory()
            : _logsDirectory!;

        return Path.Combine(
            logsDirectory,
            $"wrighttycoon-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }
}
