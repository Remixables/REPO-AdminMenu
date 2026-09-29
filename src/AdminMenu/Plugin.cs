using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace AdminMenu;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "remixables.repo.adminmenu";
    public const string PluginName = "Admin Menu";
    public const string PluginVersion = "0.1.0";

    private AdminMenuController? _controller;

    private void Awake()
    {
        try
        {
            AdminMenuConfig.Initialize(Config);
            AdminLog.Initialize(Logger);

            _controller = gameObject.AddComponent<AdminMenuController>();
            _controller.Initialize();

            AdminLog.Info($"{PluginName} {PluginVersion} initialized.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Admin Menu failed during startup: {ex}");
            AdminLog.EmergencyWrite("Startup failure", ex);
        }
    }

    private void OnDestroy()
    {
        AdminLog.Shutdown();
    }
}

public enum ExpandedLogSide
{
    Left,
    Right
}

public static class AdminMenuConfig
{
    public static ConfigEntry<KeyboardShortcut> OpenMenu { get; private set; } = null!;
    public static ConfigEntry<KeyboardShortcut> SuspendRestore { get; private set; } = null!;
    public static ConfigEntry<KeyboardShortcut> TargetMode { get; private set; } = null!;
    public static ConfigEntry<ExpandedLogSide> LogSide { get; private set; } = null!;
    public static ConfigEntry<float> UiScale { get; private set; } = null!;

    public static void Initialize(ConfigFile config)
    {
        OpenMenu = config.Bind("Hotkeys", "OpenMenu", new KeyboardShortcut(KeyCode.F7), "Open or close Admin Menu.");
        SuspendRestore = config.Bind("Hotkeys", "SuspendRestore", new KeyboardShortcut(KeyCode.F8), "Temporarily suspend or restore Admin Menu overrides.");
        TargetMode = config.Bind("Hotkeys", "TargetMode", new KeyboardShortcut(KeyCode.F6), "Reserved for Admin Target Mode.");
        LogSide = config.Bind("UI", "ExpandedLogSide", ExpandedLogSide.Right, "Side used by the expanded log.");
        UiScale = config.Bind("UI", "Scale", 1.0f, new ConfigDescription("Admin Menu UI scale.", new AcceptableValueRange<float>(0.75f, 1.5f)));
    }
}

public enum AdminRoute
{
    Home,
    Players,
    Enemies,
    ItemsValuables,
    World,
    Modded,
    Settings
}

public sealed class AdminMenuController : MonoBehaviour
{
    private const int MainWindowId = 778410;
    private const int LogWindowId = 778411;
    private const float BaseWidth = 720f;
    private const float BaseHeight = 620f;
    private const float CompactLogHeight = 78f;
    private const float Gap = 8f;

    private readonly Stack<AdminRoute> _history = new();
    private AdminRoute _route = AdminRoute.Home;

    private Rect _mainRect;
    private Vector2 _expandedScroll;
    private bool _initialized;
    private bool _menuOpen;
    private bool _expandedLog;
    private bool _suspended;
    private bool _currentSessionOnly;
    private string _logSearch = string.Empty;

    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private bool _cursorStateCaptured;

    public void Initialize()
    {
        if (_initialized)
            return;

        CenterMainWindow();
        AdminLog.ErrorRaised += OnAdminError;
        _initialized = true;
        AdminLog.Action("Admin Menu UI controller initialized.", "UI");
    }

    private void OnDestroy()
    {
        AdminLog.ErrorRaised -= OnAdminError;
        RestoreCursor();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (AdminMenuConfig.OpenMenu.Value.IsDown())
            SetMenuOpen(!_menuOpen);

        if (AdminMenuConfig.SuspendRestore.Value.IsDown())
        {
            _suspended = !_suspended;
            AdminLog.Action(_suspended ? "Admin overrides temporarily suspended." : "Admin overrides restored.", "System");
        }
    }

    private void SetMenuOpen(bool open)
    {
        if (_menuOpen == open)
            return;

        _menuOpen = open;

        if (open)
        {
            CaptureCursor();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AdminLog.Action("Opened Admin Menu.", "UI");
        }
        else
        {
            RestoreCursor();
            AdminLog.Action("Closed Admin Menu.", "UI");
        }
    }

    private void CaptureCursor()
    {
        if (_cursorStateCaptured)
            return;

        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _cursorStateCaptured = true;
    }

    private void RestoreCursor()
    {
        if (!_cursorStateCaptured)
            return;

        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        _cursorStateCaptured = false;
    }

    private void OnAdminError()
    {
        if (!_menuOpen)
            SetMenuOpen(true);

        _expandedLog = true;
        _expandedScroll.y = float.MaxValue;
    }

    private void OnGUI()
    {
        if (!_initialized)
            return;

        DrawHudDot();

        if (!_menuOpen)
            return;

        GUI.depth = -1000;
        ClampMainWindow();
        _mainRect = GUI.Window(MainWindowId, _mainRect, DrawMainWindow, GetRouteTitle(_route));

        if (_expandedLog)
            DrawExpandedLog();
    }

    private void DrawHudDot()
    {
        var previous = GUI.color;
        GUI.color = _suspended ? new Color(1f, 0.25f, 0.25f) : Color.white;
        GUI.Label(new Rect(Screen.width - 28f, 8f, 20f, 20f), "●");
        GUI.color = previous;
    }

    private void DrawMainWindow(int id)
    {
        var footer = _expandedLog ? 0f : CompactLogHeight;
        var contentRect = new Rect(12f, 28f, _mainRect.width - 24f, _mainRect.height - 42f - footer);

        GUILayout.BeginArea(contentRect);

        if (_route != AdminRoute.Home)
        {
            if (GUILayout.Button("< Back", GUILayout.Width(90f)))
                GoBack();

            GUILayout.Space(8f);
        }

        GUILayout.Label(GetRouteHeading(_route), HeaderStyle());
        GUILayout.Space(10f);

        if (_route == AdminRoute.Home)
            DrawHome();
        else
            DrawPlaceholder(_route);

        GUILayout.EndArea();

        if (!_expandedLog)
            DrawCompactLog(new Rect(10f, _mainRect.height - CompactLogHeight - 8f, _mainRect.width - 20f, CompactLogHeight));
    }

    private void DrawHome()
    {
        DrawRouteButton("Players", AdminRoute.Players);
        DrawRouteButton("Enemies", AdminRoute.Enemies);
        DrawRouteButton("Items & Valuables", AdminRoute.ItemsValuables);
        DrawRouteButton("World", AdminRoute.World);
        DrawRouteButton("Modded", AdminRoute.Modded);
        DrawRouteButton("Settings", AdminRoute.Settings);

        GUILayout.FlexibleSpace();
        GUILayout.Label("F7: Open/Close  |  F8: Suspend/Restore  |  F6: Target Mode (reserved)", MutedStyle());
        GUILayout.Label("Foundation build — gameplay controls are added in the next phases.", MutedStyle());
    }

    private void DrawRouteButton(string label, AdminRoute route)
    {
        if (GUILayout.Button(label, GUILayout.Height(42f)))
        {
            _history.Push(_route);
            _route = route;
            AdminLog.Action($"Opened {label}.", "UI");
        }
    }

    private void DrawPlaceholder(AdminRoute route)
    {
        string message = route switch
        {
            AdminRoute.Players => "Player browser and player admin effects are next.",
            AdminRoute.Enemies => "Enemy discovery, spawning, spawn prevention, and per-enemy controls are planned.",
            AdminRoute.ItemsValuables => "Items, Weapons, and Valuables will use searchable icon browsers.",
            AdminRoute.World => "Current Level, Next Level, Extraction, and Progression controls are planned.",
            AdminRoute.Modded => "Per-mod content categories and soft compatibility handling are planned.",
            AdminRoute.Settings => "Foundation hotkeys and UI scale are already stored in BepInEx config.",
            _ => string.Empty
        };

        GUILayout.Label(message, MutedStyle());
    }

    private void GoBack()
    {
        if (_history.Count == 0)
        {
            _route = AdminRoute.Home;
            return;
        }

        _route = _history.Pop();
    }

    private void DrawCompactLog(Rect rect)
    {
        GUI.Box(rect, GUIContent.none);
        var inner = new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, rect.height - 10f);

        if (GUI.Button(new Rect(inner.xMax - 30f, inner.y, 30f, inner.height), ">"))
        {
            _expandedLog = true;
            _expandedScroll.y = float.MaxValue;
            return;
        }

        var entries = AdminLog.GetRecent(3);
        var linesRect = new Rect(inner.x, inner.y, inner.width - 38f, inner.height);
        var lineHeight = linesRect.height / 3f;

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var style = e.IsError ? ErrorStyle() : CompactLogStyle();
            GUI.Label(new Rect(linesRect.x, linesRect.y + lineHeight * i, linesRect.width, lineHeight), e.CompactText, style);
        }
    }

    private void DrawExpandedLog()
    {
        Rect rect;

        if (AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left)
        {
            var width = Mathf.Max(180f, _mainRect.x - Gap);
            rect = new Rect(0f, 0f, width, Screen.height);
        }
        else
        {
            var x = _mainRect.xMax + Gap;
            var width = Mathf.Max(180f, Screen.width - x);
            rect = new Rect(x, 0f, width, Screen.height);
        }

        GUI.Window(LogWindowId, rect, DrawExpandedLogWindow, "Action History");
    }

    private void DrawExpandedLogWindow(int id)
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button(AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left ? "Position: Left" : "Position: Right", GUILayout.Width(120f)))
        {
            AdminMenuConfig.LogSide.Value = AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                ? ExpandedLogSide.Right
                : ExpandedLogSide.Left;
        }

        if (GUILayout.Button(_currentSessionOnly ? "Current Session" : "All Sessions", GUILayout.Width(120f)))
            _currentSessionOnly = !_currentSessionOnly;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Close", GUILayout.Width(70f)))
        {
            _expandedLog = false;
            GUILayout.EndHorizontal();
            return;
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(50f));
        _logSearch = GUILayout.TextField(_logSearch);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Newest", GUILayout.Width(80f)))
            _expandedScroll.y = float.MaxValue;

        IEnumerable<AdminLogEntry> entries = AdminLog.Entries;

        if (_currentSessionOnly)
            entries = entries.Where(x => x.CurrentSession);

        if (!string.IsNullOrWhiteSpace(_logSearch))
        {
            entries = entries.Where(x =>
                x.Message.IndexOf(_logSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                x.Category.IndexOf(_logSearch, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        _expandedScroll = GUILayout.BeginScrollView(_expandedScroll, false, true);

        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                GUILayout.Space(4f);
                GUILayout.Label(entry.Message, entry.CurrentSession ? CompactLogStyle() : PreviousLogStyle());
                GUILayout.Space(2f);
                continue;
            }

            var style = entry.IsError ? ErrorStyle() : entry.CurrentSession ? CompactLogStyle() : PreviousLogStyle();
            GUILayout.Label($"[{entry.Timestamp:HH:mm:ss}] [{entry.Category}] {entry.Message}", style);

            if (entry.IsError && !string.IsNullOrWhiteSpace(entry.Details))
                GUILayout.Label(entry.Details, ErrorStyle());
        }

        GUILayout.EndScrollView();
    }

    private static GUIStyle HeaderStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
    }

    private static GUIStyle MutedStyle()
    {
        var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
        style.normal.textColor = new Color(0.72f, 0.72f, 0.72f);
        return style;
    }

    private static GUIStyle CompactLogStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            clipping = TextClipping.Clip
        };
    }

    private static GUIStyle PreviousLogStyle()
    {
        var style = CompactLogStyle();
        style.normal.textColor = new Color(0.62f, 0.62f, 0.62f);
        return style;
    }

    private static GUIStyle ErrorStyle()
    {
        var style = CompactLogStyle();
        style.normal.textColor = new Color(1f, 0.35f, 0.35f);
        style.wordWrap = true;
        return style;
    }

    private static string GetRouteTitle(AdminRoute route)
    {
        return route == AdminRoute.Home ? "Admin Menu" : GetRouteHeading(route);
    }

    private static string GetRouteHeading(AdminRoute route)
    {
        return route switch
        {
            AdminRoute.Home => "R.E.P.O. Admin Menu",
            AdminRoute.Players => "Players",
            AdminRoute.Enemies => "Enemies",
            AdminRoute.ItemsValuables => "Items & Valuables",
            AdminRoute.World => "World",
            AdminRoute.Modded => "Modded",
            AdminRoute.Settings => "Settings",
            _ => "Admin Menu"
        };
    }

    private void CenterMainWindow()
    {
        var scale = AdminMenuConfig.UiScale.Value;
        var width = BaseWidth * scale;
        var height = BaseHeight * scale;
        _mainRect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
    }

    private void ClampMainWindow()
    {
        var scale = AdminMenuConfig.UiScale.Value;
        var width = BaseWidth * scale;
        var height = BaseHeight * scale;

        _mainRect.width = Mathf.Min(width, Screen.width - 40f);
        _mainRect.height = Mathf.Min(height, Screen.height - 40f);
        _mainRect.x = (Screen.width - _mainRect.width) / 2f;
        _mainRect.y = (Screen.height - _mainRect.height) / 2f;
    }
}

public sealed class AdminLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = "General";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public bool IsError { get; set; }
    public bool CurrentSession { get; set; }
    public bool IsSeparator { get; set; }

    public string CompactText => $"[{Timestamp:HH:mm:ss}] {Message}";
}

public static class AdminLog
{
    private static readonly object Gate = new();
    private static readonly List<AdminLogEntry> InternalEntries = new();
    private static ManualLogSource? _logger;
    private static string? _logPath;
    private static bool _initialized;

    public static event Action? ErrorRaised;

    public static IReadOnlyList<AdminLogEntry> Entries
    {
        get
        {
            lock (Gate)
                return InternalEntries.ToArray();
        }
    }

    public static void Initialize(ManualLogSource logger)
    {
        _logger = logger;

        var directory = Path.Combine(Paths.ConfigPath, "AdminMenu", "Logs", "NoSave");
        Directory.CreateDirectory(directory);
        _logPath = Path.Combine(directory, "Admin_Menu.log");

        LoadPreviousHistory();
        WriteSessionMarker("SESSION START", current: true);

        _initialized = true;
    }

    public static void Info(string message, string category = "General")
        => Add(message, category, false, null);

    public static void Action(string message, string category = "Action")
        => Add(message, category, false, null);

    public static void Error(string message, Exception ex, string category = "Error")
        => Add(message, category, true, ex.ToString());

    public static IReadOnlyList<AdminLogEntry> GetRecent(int count)
    {
        lock (Gate)
        {
            var normal = InternalEntries.Where(x => !x.IsSeparator).ToList();
            var start = Math.Max(0, normal.Count - count);
            return normal.Skip(start).ToArray();
        }
    }

    public static void Shutdown()
    {
        if (!_initialized)
            return;

        WriteSessionMarker("SESSION END", current: true);
        _initialized = false;
    }

    public static void EmergencyWrite(string message, Exception ex)
    {
        try
        {
            var directory = Path.Combine(Paths.ConfigPath, "AdminMenu", "Logs", "Emergency");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "Admin_Menu.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] {message}{Environment.NewLine}{ex}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void Add(string message, string category, bool isError, string? details)
    {
        var entry = new AdminLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            Details = details,
            IsError = isError,
            CurrentSession = true
        };

        lock (Gate)
        {
            InternalEntries.Add(entry);
            WriteLine(entry);
        }

        if (isError)
        {
            _logger?.LogError(details == null ? message : $"{message}{Environment.NewLine}{details}");
            ErrorRaised?.Invoke();
        }
        else
        {
            _logger?.LogInfo(message);
        }
    }

    private static void WriteLine(AdminLogEntry entry)
    {
        if (_logPath == null)
            return;

        try
        {
            var level = entry.IsError ? "ERROR" : "INFO";
            File.AppendAllText(_logPath, $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{level}] [{entry.Category}] {entry.Message}{Environment.NewLine}");

            if (!string.IsNullOrWhiteSpace(entry.Details))
                File.AppendAllText(_logPath, entry.Details + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Could not write Admin_Menu.log: {ex.Message}");
        }
    }

    private static void WriteSessionMarker(string label, bool current)
    {
        var now = DateTime.Now;

        lock (Gate)
        {
            InternalEntries.Add(new AdminLogEntry
            {
                Timestamp = now,
                Category = "Session",
                Message = label == "SESSION START" ? "──── Current Session ────" : "──── Session End ────",
                CurrentSession = current,
                IsSeparator = true
            });

            if (_logPath != null)
            {
                try
                {
                    File.AppendAllText(_logPath, $"========== {label} | {now:yyyy-MM-dd HH:mm:ss} =========={Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Could not write Admin_Menu.log marker: {ex.Message}");
                }
            }
        }
    }

    private static void LoadPreviousHistory()
    {
        if (_logPath == null || !File.Exists(_logPath))
            return;

        try
        {
            var lines = File.ReadAllLines(_logPath);
            var start = Math.Max(0, lines.Length - 500);

            for (var i = start; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("==========", StringComparison.Ordinal))
                {
                    InternalEntries.Add(new AdminLogEntry
                    {
                        Timestamp = DateTime.MinValue,
                        Category = "Session",
                        Message = line.Contains("SESSION START") ? "──── Previous Session ────" : "──── Session End ────",
                        CurrentSession = false,
                        IsSeparator = true
                    });
                    continue;
                }

                if (TryParseStoredLine(line, out var entry))
                    InternalEntries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Could not load previous Admin_Menu.log history: {ex.Message}");
        }
    }

    private static bool TryParseStoredLine(string line, out AdminLogEntry entry)
    {
        entry = null!;

        if (string.IsNullOrWhiteSpace(line) || line[0] != '[')
            return false;

        var first = line.IndexOf(']');
        if (first <= 1)
            return false;

        if (!DateTime.TryParse(line.Substring(1, first - 1), out var time))
            return false;

        var remainder = line.Substring(first + 1).TrimStart();
        var second = remainder.IndexOf(']');

        if (remainder.Length < 3 || remainder[0] != '[' || second <= 1)
            return false;

        var level = remainder.Substring(1, second - 1);
        remainder = remainder.Substring(second + 1).TrimStart();

        var category = "Previous";
        if (remainder.StartsWith("[", StringComparison.Ordinal))
        {
            var third = remainder.IndexOf(']');
            if (third > 1)
            {
                category = remainder.Substring(1, third - 1);
                remainder = remainder.Substring(third + 1).TrimStart();
            }
        }

        entry = new AdminLogEntry
        {
            Timestamp = time,
            Category = category,
            Message = remainder,
            IsError = string.Equals(level, "ERROR", StringComparison.OrdinalIgnoreCase),
            CurrentSession = false
        };

        return true;
    }
}
