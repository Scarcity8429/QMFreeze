using HarmonyLib;
using Il2Cpp;
using Il2CppVRC.SDKBase;
using MelonLoader;
using UnityEngine;
using VRC.UI.Elements;
using EntryPoint = QMFreeze.EntryPoint;

[assembly: MelonInfo(typeof(EntryPoint), "QMFreeze", "1.0.0", "Tetra & wyn.orrific")]
[assembly: MelonGame("VRChat", "VRChat")]
[assembly: MelonAdditionalDependencies("WynLib")]

namespace QMFreeze;

public class EntryPoint : MelonMod
{
    private static bool _freezeAllowed;
    public static bool Frozen { get; private set; }
    private static bool _enforcingFreeze;
    private static bool _hasSavedGravity;
    private static Vector3 _originalGravity;
    private static Vector3 _originalVelocity;

    public override void OnInitializeMelon()
    {
        Settings.Register();
        Settings.Apply();
        ApplyHarmonyPatches();
        CheckState();
    }

    public override void OnUpdate()
    {
        CheckState();

        if (_freezeAllowed)
            CaptureGravityIfPresent();

        if (!_enforcingFreeze || !Frozen || !_freezeAllowed || !Settings.Enabled)
            return;

        ForceState();
    }

    private static void ApplyHarmonyPatches()
    {
        var harmony = new HarmonyLib.Harmony(BuildInfo.Name);
        var mod = typeof(EntryPoint);
        
        harmony.Patch(
            AccessTools.Method(typeof(QuickMenu), nameof(QuickMenu.OnEnable)),
            postfix: new HarmonyMethod(mod, nameof(OnQuickMenuEnabled)));
        
        harmony.Patch(
            AccessTools.Method(typeof(QuickMenu), nameof(QuickMenu.OnDisable)),
            postfix: new HarmonyMethod(mod, nameof(OnQuickMenuDisabled)));

        harmony.Patch(
            AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.OnJoinedRoom)),
            prefix: new HarmonyMethod(mod, nameof(OnJoinedRoom)));

        harmony.Patch(
            AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.OnLeftRoom)),
            prefix: new HarmonyMethod(mod, nameof(OnLeftRoom)));
    }

    private static void OnQuickMenuEnabled()
    {
        if (!_freezeAllowed || !Settings.Enabled || Frozen)
            return;

        CaptureGravityIfPresent();

        var localPlayer = Networking.LocalPlayer;
        _originalVelocity = localPlayer?.GetVelocity() ?? Vector3.zero;

        _enforcingFreeze = true;
        Frozen = true;
        ForceState();
    }

    private static void OnQuickMenuDisabled() => Unfreeze();
    private static void CheckState()
    {
        if (_freezeAllowed)
            return;

        if (!Networking.IsNetworkSettled || Networking.LocalPlayer == null)
            return;

        _freezeAllowed = true;
    }

    private static void OnJoinedRoom() => _freezeAllowed = true;

    private static void OnLeftRoom()
    {
        Unfreeze();
        _freezeAllowed = false;
    }

    private static void CaptureGravityIfPresent()
    {
        var gravity = Physics.gravity;
        if (gravity == Vector3.zero)
            return;

        _originalGravity = gravity;
        _hasSavedGravity = true;
    }

    private static void ForceState()
    {
        Physics.gravity = Vector3.zero;
        Networking.LocalPlayer?.SetVelocity(Vector3.zero);
    }

    public static void Unfreeze()
    {
        if (!Frozen)
            return;

        _enforcingFreeze = false;
        Frozen = false;

        if (_hasSavedGravity)
            Physics.gravity = _originalGravity;

        if (Settings.RestoreVelocity)
        {
            var localPlayer = Networking.LocalPlayer;
            if (localPlayer != null)
                localPlayer.SetVelocity(_originalVelocity);
        }
    }
    public override void OnPreferencesLoaded() => Settings.Apply();

    public override void OnPreferencesSaved() => Settings.Apply();
}