using HarmonyLib;
using UnityEngine;

namespace AdminMenu;

[HarmonyPatch(typeof(RunManager), "Update")]
internal static class RunManagerUpdatePatch
{
    private static bool _menuWasDown;
    private static bool _suspendWasDown;

    [HarmonyPostfix]
    private static void Postfix()
    {
        var menuDown = Input.GetKey(AdminMenuConfig.OpenMenu.Value);
        if (menuDown && !_menuWasDown)
            NativeAdminMenu.Toggle();
        _menuWasDown = menuDown;

        var suspendDown = Input.GetKey(AdminMenuConfig.SuspendRestore.Value);
        if (suspendDown && !_suspendWasDown)
            AdminState.ToggleSuspended();
        _suspendWasDown = suspendDown;
    }
}
