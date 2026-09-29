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

    private static readonly Vector2 NormalContentPosition = Vector2.zero;
    private static readonly Vector2 LeftContentPosition = new(-280f, 0f);
    private static readonly Vector2 RightContentPosition = new(40f, 0f);

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

            AddCompactLog(page);

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton(
                    "Close",
                    CloseAdminMenu,
                    parent,
                    new Vector2(270f, 20f)));

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
            var previousPage = _currentPage;
            var page = CreateContentPage(title);

            page.AddElementToScrollView(scroll =>
            {
                var label = MenuAPI.CreateREPOLabel(
                    title + " foundation page. Gameplay controls will be added in the next phases.",
                    scroll);
                return label.rectTransform;
            });

            AddCompactLog(page);

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton(
                    "Back",
                    () => GoBack(page),
                    parent,
                    new Vector2(250f, 20f)));

            page.onEscapePressed = () =>
            {
                GoBack(page);
                return false;
            };

            if (previousPage != null)
                PageStack.Push(previousPage);

            page.OpenPage(false);
            _currentPage = page;
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
        page.ClosePage(false);

        if (_currentPage == page)
            _currentPage = PageStack.Count > 0 ? PageStack.Pop() : null;

        ApplyCurrentPagePosition();
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
        return MenuAPI.CreateREPOPopupPage(
            title,
            shouldCachePage: false,
            pageDimmerVisibility: true,
            spacing: 1.5f,
            localPosition: GetContentPosition());
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
                var text = entry.IsError
                    ? "<color=#ff5555>" + entry.CompactText + "</color>"
                    : entry.CompactText;

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
                    StyleLogLabel(label, 11f, 18f);
                    return label.rectTransform;
                });
            }

            page.AddElement(parent =>
                MenuAPI.CreateREPOButton(
                    "Close",
                    () => CloseExpandedLog(page),
                    parent,
                    new Vector2(270f, 20f)));

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

        ApplyCurrentPagePosition(expandedLogExpected: true);

        currentLogPage.ClosePage(false);

        if (_expandedLogPage == currentLogPage)
            _expandedLogPage = null;

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

    private static void StyleLogLabel(REPOLabel label, float fontSize, float height)
    {
        const float width = 455f;

        label.labelTMP.fontSize = fontSize;
        label.labelTMP.enableAutoSizing = false;
        label.labelTMP.enableWordWrapping = false;

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
