using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NO_SVOBS;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal new static ManualLogSource Logger { get; private set; } = null!;
    private Harmony? Harmony { get; set; }
    
    private void Awake()
    {
        Logger = base.Logger;
        
        Harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        Harmony.PatchAll();
    }
    
    private void OnDestroy()
    {
        Harmony?.UnpatchSelf();
    }
}

[HarmonyPatch]
internal static class HarmonyPatches
{
    [HarmonyPatch(typeof(BulletSim), nameof(BulletSim.AddBullet))]
    [HarmonyPrefix]
    private static bool SkipUnneededServerVisualBullet(Unit ___owner, bool ___visualOnly, Transform muzzle,
        Vector3 inheritedVelocity, Unit target)
    {
        // Only filtering for dedicated server, visualOnly (non-authoritative) bullets
        // AI units' guns are not visualOnly so they're not filtered here either
        if (!GameManager.IsHeadless || !NetworkManagerNuclearOption.i.Server.Active || !___visualOnly || ___owner == null || muzzle == null)
            return true;
        
        // Preserve simulating Proximity Fuse bullets as server has authority over calling DamageEffects.BlastFrag on them
        bool proximityFuse = target != null && target.definition.armorTier < 2f;
        if (proximityFuse)
            return true;
        
        // Log the firing as this is what's used for HitValidator later
        HitValidator.LogFiring(___owner.persistentID, muzzle.position - Datum.origin.position, inheritedVelocity);
        
        // Stop needlessly simulating every other bullet
        return false;
    }
}