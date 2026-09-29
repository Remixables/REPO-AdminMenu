using System;
using System.Linq;
using MenuLib;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace AdminMenu;

internal static class NativeAdminMenu
{
    private static bool _initialized;
    private static REPOPopupPage? _currentPage;
    private static REPOPopupPage? _expandedLogPage;

    internal static void Initialize()
    {
        if (_initialized)
            return;

        MenuAPI.AddElementToMainMenu(parent =>
        {
            MenuAPI.CreateREPOButton(
                "Admin Menu",
                OpenRoot,
                parent,
                new Vector2(145f, 22f));
        });

        MenuAPI.AddElementToEscapeMenu(parent =>
        {
            MenuAPI.CreateREPOButton(
                "Admin Menu",
                OpenRoot,
                parent,
                new Vector2(126f, 100f));
        });

        AdminLog.ErrorRaised += OnAdminError;
        _initialized = true;
        AdminLog.Info("Registered Admin Menu buttons with MenuLib.", "UI");
    }

    internal static void Shutdown()
    {
        AdminLog.ErrorRaised -= OnAdminError;
        _currentPage = null;
        _expandedLogPage = null;
        _initialized = false;
    }

    internal static void Toggle()
    {
        if (_currentPage != null && _currentPage.isActiveAndEnabled)
        {
            _currentPage.ClosePage(true);
            _currentPage = null;
            return;
        }

        OpenRoot();
    }

    internal static void OpenRoot()
    {
        try
        {
            var page = CreatePage("Admin Menu", OppositeOfLogSide());
            _currentPage = page;

            AddButton(page, "Players", () => OpenPlaceholder("Players"));
            AddButton(page, "Enemies", () => OpenPlaceholder("Enemies"));
            AddButton(page, "Items & Valuables", () => OpenPlaceholder("Items & Valuables"));
            AddButton(page, "World", () => OpenPlaceholder("World"));
            AddButton(page, "Modded", () => OpenPlaceholder("Modded"));
            AddButton(page, "Settings", () => OpenPlaceholder("Settings"));

            AddCompactLog(page);

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton("Close", () =>
                {
                    page.ClosePage(true);
                    if (_currentPage == page)
                        _currentPage = null;
                }, parent, new Vector2(270f, 20f)));

            page.OpenPage(false);
            AdminLog.Action("Opened Admin Menu.", "UI");
        }
        catch (Exception ex)
        {
            AdminLog.Error("Failed to open Admin Menu.", ex, "UI");
        }
    }

    private static void OpenPlaceholder(string title)
    {
        try
        {
            var page = CreatePage(title, OppositeOfLogSide());

            page.AddElementToScrollView(scroll =>
            {
                var label = MenuAPI.CreateREPOLabel(
                    title + " foundation page. Gameplay controls will be added in the next phases.",
                    scroll);
                return label.rectTransform;
            });

            AddCompactLog(page);

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton("Back", () => page.ClosePage(false), parent, new Vector2(250f, 20f)));

            page.OpenPage(false);
            _currentPage = page;
            AdminLog.Action("Opened " + title + ".", "UI");
        }
        catch (Exception ex)
        {
            AdminLog.Error("Failed to open " + title + " page.", ex, "UI");
        }
    }

    private static REPOPopupPage CreatePage(string title, REPOPopupPage.PresetSide side, bool dimBackground = true)
    {
        return MenuAPI.CreateREPOPopupPage(
            title,
            side,
            shouldCachePage: false,
            pageDimmerVisibility: dimBackground,
            spacing: 1.5f);
    }

    private static void AddButton(REPOPopupPage page, string text, Action action)
    {
        page.AddElementToScrollView(scroll =>
        {
            var button = MenuAPI.CreateREPOButton(text, action, scroll);
            return button.rectTransform;
        });
    }

    private static void AddCompactLog(REPOPopupPage page)
    {
        var recent = AdminLog.GetRecent(3);

        page.AddElementToScrollView(scroll =>
        {
            var label = MenuAPI.CreateREPOLabel("Action History", scroll);
            StyleLogLabel(label, 18f, 22f);
            return label.rectTransform;
        });

        foreach (var entry in recent)
        {
            page.AddElementToScrollView(scroll =>
            {
                var text = entry.IsError ? "<color=#ff5555>" + entry.CompactText + "</color>" : entry.CompactText;
                var label = MenuAPI.CreateREPOLabel(text, scroll);
                StyleLogLabel(label, 11f, 16f);
                return label.rectTransform;
            });
        }

        AddButton(page, "Expand Log >", OpenExpandedLog);
    }

    internal static void OpenExpandedLog()
    {
        try
        {
            if (_expandedLogPage != null && _expandedLogPage.isActiveAndEnabled)
                return;

            var side = AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                ? REPOPopupPage.PresetSide.Left
                : REPOPopupPage.PresetSide.Right;

            var page = CreatePage("Action History", side, dimBackground: false);
            _expandedLogPage = page;

            AddButton(page,
                AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left ? "Position: Left" : "Position: Right",
                () =>
                {
                    AdminMenuConfig.LogSide.Value = AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                        ? ExpandedLogSide.Right
                        : ExpandedLogSide.Left;

                    page.ClosePage(false);
                    _expandedLogPage = null;
                    OpenExpandedLog();
                });

            foreach (var entry in AdminLog.Entries.TakeLast(150))
            {
                page.AddElementToScrollView(scroll =>
                {
                    var text = entry.IsError
                        ? "<color=#ff5555>" + entry.FullText + "</color>"
                        : entry.FullText;

                    var label = MenuAPI.CreateREPOLabel(text, scroll);
                    StyleLogLabel(label, 11f, 18f);
                    return label.rectTransform;
                });
            }

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton("Close", () =>
                {
                    page.ClosePage(false);
                    if (_expandedLogPage == page)
                        _expandedLogPage = null;
                }, parent, new Vector2(270f, 20f)));

            page.OpenPage(true);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Failed to open expanded Admin Menu log: " + ex);
        }
    }

    private static void StyleLogLabel(REPOLabel label, float fontSize, float height)
    {
        const float width = 455f;

        label.labelTMP.fontSize = fontSize;
        label.labelTMP.enableAutoSizing = false;
        label.labelTMP.enableWordWrapping = false;

        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.labelTMP.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static REPOPopupPage.PresetSide OppositeOfLogSide()
    {
        return AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
            ? REPOPopupPage.PresetSide.Right
            : REPOPopupPage.PresetSide.Left;
    }

    private static void OnAdminError()
    {
        if (MenuManager.instance == null)
            return;

        OpenExpandedLog();
    }
}
