using System;
using System.Collections.Generic;
using System.Linq;
using MenuLib;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace AdminMenu;

internal static class NativeAdminMenu
{
    private static readonly Stack<REPOPopupPage> PageStack = new();

    // MenuLib's built-in Left and Right popup positions are about -280 and +40.
    // Their midpoint (-120) is the visually centered position.
    private static readonly Vector2 NormalContentPosition = new(-120f, 0f);
    private static readonly Vector2 LeftContentPosition = new(-280f, 0f);
    private static readonly Vector2 RightContentPosition = new(40f, 0f);

    private const float CompactLogWidth = 440f;
    private const float ExpandedLogWidth = 455f;

    private static bool _initialized;
    private static REPOPopupPage? _currentPage;
    private static REPOPopupPage? _expandedLogPage;

    private static bool ExpandedLogIsOpen =>
        _expandedLogPage != null && _expandedLogPage.isActiveAndEnabled;

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
        PageStack.Clear();
        _currentPage = null;
        _expandedLogPage = null;
        _initialized = false;
    }

    internal static void Toggle()
    {
        if (_currentPage != null && _currentPage.isActiveAndEnabled)
        {
            CloseAdminMenu();
            return;
        }

        OpenRoot();
    }

    internal static void OpenRoot()
    {
        try
        {
            if (_currentPage != null && _currentPage.isActiveAndEnabled)
                CloseAdminMenu();

            PageStack.Clear();

            var page = CreateContentPage("Admin Menu");
            _currentPage = page;

            AddButton(page, "Players", () => OpenPlaceholder("Players"));
            AddButton(page, "Enemies", () => OpenPlaceholder("Enemies"));
            AddButton(page, "Items & Valuables", () => OpenPlaceholder("Items & Valuables"));
            AddButton(page, "World", () => OpenPlaceholder("World"));
            AddButton(page, "Modded", () => OpenPlaceholder("Modded"));
            AddButton(page, "Settings", () => OpenPlaceholder("Settings"));

            AddCompactLogFooter(page);

            AddFixedButton(
                page,
                "Close",
                CloseAdminMenu,
                new Vector2(250f, 20f));

            page.onEscapePressed = () =>
            {
                CloseAdminMenu();
                return false;
            };

            page.OpenPage(false);
            ApplyCurrentPagePosition();
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
            var reopenExpandedLog = DetachExpandedLogForNavigation();
            var previousPage = _currentPage;
            var page = CreateContentPage(title);

            page.AddElementToScrollView(scroll =>
            {
                var label = MenuAPI.CreateREPOLabel(
                    title + " foundation page. Gameplay controls will be added in the next phases.",
                    scroll);

                label.labelTMP.enableWordWrapping = true;
                label.rectTransform.sizeDelta = new Vector2(440f, 55f);
                label.labelTMP.rectTransform.sizeDelta = new Vector2(440f, 55f);
                return label.rectTransform;
            });

            AddCompactLogFooter(page);

            AddFixedButton(
                page,
                "Back",
                () => GoBack(page),
                new Vector2(230f, 20f));

            page.onEscapePressed = () =>
            {
                GoBack(page);
                return false;
            };

            if (previousPage != null)
                PageStack.Push(previousPage);

            page.OpenPage(false);
            _currentPage = page;

            if (reopenExpandedLog)
                OpenExpandedLog();
            else
                ApplyCurrentPagePosition();

            AdminLog.Action("Opened " + title + ".", "UI");
        }
        catch (Exception ex)
        {
            AdminLog.Error("Failed to open " + title + " page.", ex, "UI");
        }
    }

    private static void GoBack(REPOPopupPage page)
    {
        var reopenExpandedLog = DetachExpandedLogForNavigation();

        page.ClosePage(false);

        if (_currentPage == page)
            _currentPage = PageStack.Count > 0 ? PageStack.Pop() : null;

        if (reopenExpandedLog)
            OpenExpandedLog();
        else
            ApplyCurrentPagePosition();
    }

    private static bool DetachExpandedLogForNavigation()
    {
        if (!ExpandedLogIsOpen || _expandedLogPage == null)
            return false;

        var page = _expandedLogPage;
        _expandedLogPage = null;
        page.ClosePage(false);
        return true;
    }

    private static void CloseAdminMenu()
    {
        try
        {
            if (_expandedLogPage != null && _expandedLogPage.isActiveAndEnabled)
                _expandedLogPage.ClosePage(false);

            _expandedLogPage = null;

            if (_currentPage != null && _currentPage.isActiveAndEnabled)
                _currentPage.ClosePage(false);

            while (PageStack.Count > 0)
            {
                var page = PageStack.Pop();
                if (page != null && page.isActiveAndEnabled)
                    page.ClosePage(false);
            }

            _currentPage = null;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Failed to close Admin Menu cleanly: " + ex);
            PageStack.Clear();
            _currentPage = null;
            _expandedLogPage = null;
        }
    }

    private static REPOPopupPage CreateContentPage(string title)
    {
        var page = MenuAPI.CreateREPOPopupPage(
            title,
            shouldCachePage: false,
            pageDimmerVisibility: true,
            spacing: 1.5f,
            localPosition: GetContentPosition());

        // Compact Action History is a fixed footer, not part of the scrolling content.
        // Reserve enough scroll area so normal menu content can never overlap it.
        var padding = page.maskPadding;
        padding.bottom = 175f;
        page.maskPadding = padding;

        return page;
    }

    private static REPOPopupPage CreateSidePage(
        string title,
        REPOPopupPage.PresetSide side,
        bool dimBackground)
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

    private static void AddFixedButton(
        REPOPopupPage page,
        string text,
        Action action,
        Vector2 localPosition)
    {
        MenuAPI.CreateREPOButton(
            text,
            action,
            page.rectTransform,
            localPosition);
    }

    private static void AddCompactLogFooter(REPOPopupPage page)
    {
        var recent = AdminLog.GetRecent(3);

        AddFixedLogLabel(
            page,
            "Action History",
            new Vector2(-215f, 146f),
            18f,
            24f,
            wrap: false);

        // Newest stays at the bottom.
        var firstY = 113f;
        for (var i = 0; i < recent.Count; i++)
        {
            var entry = recent[i];
            var text = entry.IsError
                ? "<color=#ff5555>" + entry.CompactText + "</color>"
                : entry.CompactText;

            AddFixedLogLabel(
                page,
                text,
                new Vector2(-215f, firstY - (i * 30f)),
                11f,
                30f,
                wrap: true);
        }

        AddFixedButton(
            page,
            "Expand Log >",
            OpenExpandedLog,
            new Vector2(-205f, 20f));
    }

    private static void AddFixedLogLabel(
        REPOPopupPage page,
        string text,
        Vector2 localPosition,
        float fontSize,
        float height,
        bool wrap)
    {
        var label = MenuAPI.CreateREPOLabel(
            text,
            page.rectTransform,
            localPosition);

        StyleLogLabel(
            label,
            fontSize,
            CompactLogWidth,
            height,
            wrap,
            dynamicHeight: false);
    }

    internal static void OpenExpandedLog()
    {
        try
        {
            if (ExpandedLogIsOpen)
                return;

            ApplyCurrentPagePosition(expandedLogExpected: true);

            var side = AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                ? REPOPopupPage.PresetSide.Left
                : REPOPopupPage.PresetSide.Right;

            var page = CreateSidePage(
                "Action History",
                side,
                dimBackground: false);

            _expandedLogPage = page;

            AddButton(
                page,
                AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                    ? "Position: Left"
                    : "Position: Right",
                () => ChangeExpandedLogSide(page));

            foreach (var entry in AdminLog.Entries.TakeLast(150))
            {
                page.AddElementToScrollView(scroll =>
                {
                    var text = entry.IsError
                        ? "<color=#ff5555>" + entry.FullText + "</color>"
                        : entry.FullText;

                    var label = MenuAPI.CreateREPOLabel(text, scroll);

                    StyleLogLabel(
                        label,
                        11f,
                        ExpandedLogWidth,
                        18f,
                        wrap: true,
                        dynamicHeight: true);

                    return label.rectTransform;
                });
            }

            AddFixedButton(
                page,
                "Close",
                () => CloseExpandedLog(page),
                new Vector2(250f, 20f));

            page.onEscapePressed = () =>
            {
                CloseExpandedLog(page);
                return false;
            };

            page.OpenPage(true);
            ApplyCurrentPagePosition(expandedLogExpected: true);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError("Failed to open expanded Admin Menu log: " + ex);
        }
    }

    private static void ChangeExpandedLogSide(REPOPopupPage currentLogPage)
    {
        AdminMenuConfig.LogSide.Value =
            AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
                ? ExpandedLogSide.Right
                : ExpandedLogSide.Left;

        currentLogPage.ClosePage(false);

        if (_expandedLogPage == currentLogPage)
            _expandedLogPage = null;

        ApplyCurrentPagePosition(expandedLogExpected: true);
        OpenExpandedLog();
    }

    private static void CloseExpandedLog(REPOPopupPage page)
    {
        page.ClosePage(false);

        if (_expandedLogPage == page)
            _expandedLogPage = null;

        ApplyCurrentPagePosition(expandedLogExpected: false);
    }

    private static Vector2 GetContentPosition()
    {
        if (!ExpandedLogIsOpen)
            return NormalContentPosition;

        return GetContentPositionForExpandedLog();
    }

    private static Vector2 GetContentPositionForExpandedLog()
    {
        return AdminMenuConfig.LogSide.Value == ExpandedLogSide.Left
            ? RightContentPosition
            : LeftContentPosition;
    }

    private static void ApplyCurrentPagePosition(bool expandedLogExpected = false)
    {
        if (_currentPage == null)
            return;

        _currentPage.rectTransform.localPosition =
            expandedLogExpected || ExpandedLogIsOpen
                ? GetContentPositionForExpandedLog()
                : NormalContentPosition;
    }

    private static void StyleLogLabel(
        REPOLabel label,
        float fontSize,
        float width,
        float minimumHeight,
        bool wrap,
        bool dynamicHeight)
    {
        label.labelTMP.fontSize = fontSize;
        label.labelTMP.enableAutoSizing = false;
        label.labelTMP.enableWordWrapping = wrap;

        var height = minimumHeight;

        if (wrap && dynamicHeight)
        {
            var preferred = label.labelTMP.GetPreferredValues(
                label.labelTMP.text,
                width,
                0f);

            height = Mathf.Max(minimumHeight, preferred.y + 4f);
        }

        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.labelTMP.rectTransform.sizeDelta = new Vector2(width, height);
    }

    private static void OnAdminError()
    {
        if (MenuManager.instance == null)
            return;

        OpenExpandedLog();
    }
}
