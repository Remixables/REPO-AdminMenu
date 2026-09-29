using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "remixables.repo.adminmenu";
    public const string PluginName = "Admin Menu";
    public const string PluginVersion = "0.1.1";

    internal static ManualLogSource Log = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        AdminMenuConfig.Initialize(Config);
        AdminLog.Initialize(Logger);

        NativeAdminMenu.Initialize();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        AdminLog.Info("Admin Menu 0.1.1 loaded. MenuLib UI registered.", "Startup");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        NativeAdminMenu.Shutdown();
        AdminLog.Shutdown();
    }
}

internal static class AdminMenuConfig
{
    internal static ConfigEntry<KeyCode> OpenMenu { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> SuspendRestore { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> TargetMode { get; private set; } = null!;
    internal static ConfigEntry<ExpandedLogSide> LogSide { get; private set; } = null!;

    internal static void Initialize(ConfigFile config)
    {
        OpenMenu = config.Bind("Hotkeys", "OpenMenu", KeyCode.F7, "Open or close Admin Menu while in a level.");
        SuspendRestore = config.Bind("Hotkeys", "SuspendRestore", KeyCode.F8, "Suspend or restore Admin Menu overrides.");
        TargetMode = config.Bind("Hotkeys", "TargetMode", KeyCode.F6, "Reserved for Admin Target Mode.");
        LogSide = config.Bind("UI", "ExpandedLogSide", ExpandedLogSide.Right, "Side used by the expanded action log.");
    }
}

internal enum ExpandedLogSide
{
    Left,
    Right
}

internal static class AdminState
{
    internal static bool Suspended { get; private set; }

    internal static void ToggleSuspended()
    {
        Suspended = !Suspended;
        AdminLog.Action(Suspended
            ? "Admin overrides temporarily suspended."
            : "Admin overrides restored.", "System");
    }
}
