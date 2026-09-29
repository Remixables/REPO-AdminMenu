using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;

namespace AdminMenu;

internal sealed class AdminLogEntry
{
    internal DateTime Timestamp { get; set; }
    internal string Category { get; set; } = "General";
    internal string Message { get; set; } = string.Empty;
    internal bool IsError { get; set; }
    internal bool PreviousSession { get; set; }

    internal string CompactText =>
        "[" + Timestamp.ToString("HH:mm:ss") + "] " + Message;

    internal string FullText =>
        "[" + Timestamp.ToString("HH:mm:ss") + "] [" + Category + "] " + Message;
}

internal static class AdminLog
{
    private static readonly object Gate = new();
    private static readonly List<AdminLogEntry> InternalEntries = new();

    private static ManualLogSource? _logger;
    private static string? _logPath;
    private static bool _initialized;

    internal static event Action? ErrorRaised;

    internal static IReadOnlyList<AdminLogEntry> Entries
    {
        get
        {
            lock (Gate)
                return InternalEntries.ToArray();
        }
    }

    internal static void Initialize(ManualLogSource logger)
    {
        _logger = logger;

        var directory = Path.Combine(Paths.ConfigPath, "AdminMenu", "Logs");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "Admin_Menu.log");

        LoadPreviousHistory();
        AppendRaw("========== SESSION START | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==========");
        _initialized = true;
    }

    internal static void Shutdown()
    {
        if (!_initialized)
            return;

        AppendRaw("========== SESSION END | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==========");
        _initialized = false;
    }

    internal static void Info(string message, string category = "General")
        => Add(message, category, false);

    internal static void Action(string message, string category = "Action")
        => Add(message, category, false);

    internal static void Error(string message, Exception ex, string category = "Error")
    {
        Add(message + " | " + ex.GetType().Name + ": " + ex.Message, category, true);
        _logger?.LogError(message + Environment.NewLine + ex);
        ErrorRaised?.Invoke();
    }

    internal static IReadOnlyList<AdminLogEntry> GetRecent(int count)
    {
        lock (Gate)
        {
            var start = Math.Max(0, InternalEntries.Count - count);
            return InternalEntries.Skip(start).ToArray();
        }
    }

    private static void Add(string message, string category, bool error)
    {
        var entry = new AdminLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            IsError = error,
            PreviousSession = false
        };

        lock (Gate)
        {
            InternalEntries.Add(entry);
            if (InternalEntries.Count > 1000)
                InternalEntries.RemoveRange(0, InternalEntries.Count - 1000);

            AppendRaw(
                "[" + entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                "[" + (error ? "ERROR" : "INFO") + "] " +
                "[" + category + "] " +
                message);
        }

        if (!error)
            _logger?.LogInfo(message);
    }

    private static void LoadPreviousHistory()
    {
        if (_logPath == null || !File.Exists(_logPath))
            return;

        try
        {
            var lines = File.ReadAllLines(_logPath);

            foreach (var line in lines.Skip(Math.Max(0, lines.Length - 300)))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("=========="))
                    continue;

                if (TryParseStoredLine(line, out var entry))
                    InternalEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Could not load previous Admin_Menu.log: " + ex.Message);
        }
    }

    private static bool TryParseStoredLine(string line, out AdminLogEntry entry)
    {
        entry = null!;

        var cursor = 0;

        if (!TryReadBracketValue(line, ref cursor, out var timestampText) ||
            !DateTime.TryParse(timestampText, out var timestamp))
        {
            return false;
        }

        SkipSpaces(line, ref cursor);

        if (!TryReadBracketValue(line, ref cursor, out var level))
            return false;

        SkipSpaces(line, ref cursor);

        if (!TryReadBracketValue(line, ref cursor, out var category))
            return false;

        SkipSpaces(line, ref cursor);

        var message = cursor < line.Length
            ? line.Substring(cursor)
            : string.Empty;

        entry = new AdminLogEntry
        {
            Timestamp = timestamp,
            Category = category,
            Message = message,
            IsError = string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase),
            PreviousSession = true
        };

        return true;
    }

    private static bool TryReadBracketValue(
        string text,
        ref int cursor,
        out string value)
    {
        value = string.Empty;

        if (cursor >= text.Length || text[cursor] != '[')
            return false;

        var end = text.IndexOf(']', cursor + 1);
        if (end < 0)
            return false;

        value = text.Substring(cursor + 1, end - cursor - 1);
        cursor = end + 1;
        return true;
    }

    private static void SkipSpaces(string text, ref int cursor)
    {
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
            cursor++;
    }

    private static void AppendRaw(string line)
    {
        if (_logPath == null)
            return;

        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("Could not write Admin_Menu.log: " + ex.Message);
        }
    }
}
