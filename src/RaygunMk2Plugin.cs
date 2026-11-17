using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace RaygunMk2
{
    /// <summary>
    /// Client side of the Ray Gun Mark II. Pack-a-Punch support is optional (soft dependency): nothing here touches
    /// PaP unless it's installed.
    /// </summary>
    [BepInPlugin(PluginGuid, "Kenner-RaygunMk2", "1.0.0")] // Forge rule: "Username-ModName"
    [BepInDependency("com.kennerz.paphideoutmodule", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(RaygunMk2Draw.HideoutUncensoredGuid, "1.3.0")] // hard: the wall buy hands you the gun through it
    public class RaygunMk2Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.kennerz.raygunmk2"; // same GUID as the server mod (Forge rule)

        internal static ManualLogSource Log;

        /// <summary>Runs the mod's coroutines (the shot push-back).</summary>
        internal static MonoBehaviour Runner;

        private void Awake()
        {
            // Release builds: F12 shows players only their own settings
            ReleaseMenu.Install(this, d => d.Section == "1. Normal gameplay" || d.Section == RaygunMk2Settings.VolumeSection); // volumes + normal play only (user, 2026-11-17)
            Log = Logger;
            Runner = this;
            RaygunMk2Settings.Bind(Config);
            RaygunMk2NormalGame.Enable(Config, gameObject);
            RaygunMk2Sounds.Enable();
            var harmony = new HarmonyLib.Harmony(PluginGuid);
            Try("Weapon root reset", () =>
            {
                RaygunMk2RootReset.Enable(harmony);
                gameObject.AddComponent<RaygunMk2FitKeeper>();
            });
            Try("Magazine size", () => RaygunMk2MagSize.Enable(harmony));
            Try("No shells", () => RaygunMk2NoShells.Enable(harmony));
            Try("Sell price", () => RaygunMk2SellPrice.Enable(harmony));
            Try("Hideout stations", () => RaygunMk2Stations.Enable(harmony, gameObject));
            Try("Model and icon", () =>
            {
                RaygunMk2Models.Enable(harmony);
                RaygunMk2Icons.Enable(harmony);
            });
            Try("Item window centring", () => RaygunMk2Preview.Enable(harmony));
            Try("Draw and menu pose", () =>
            {
                RaygunMk2BootUp.Enable(harmony);
                RaygunMk2MenuPose.Enable(harmony);
            });
            Try("Shot push-back", () => RaygunMk2PushBack.Enable(harmony));
            Try("Burst gap", () => RaygunMk2BurstGap.Enable(harmony));
            Try("Aim", () => RaygunMk2Aim.Enable(harmony));
            Try("Dry click", () => RaygunMk2DryClick.Enable(harmony));
            Try("Bolt effects", () =>
            {
                RaygunMk2Bolts.Enable(harmony);
                gameObject.AddComponent<RaygunMk2Bolts>();
            });
            RaygunMk2PaP.Setup();
            gameObject.AddComponent<RaygunMk2Tracker>();
            Log.LogInfo("Ray Gun Mark II loaded");
        }

        private static void Try(string what, System.Action enable)
        {
            try
            {
                enable();
            }
            catch (System.Exception e)
            {
                Log.LogError($"{what} patches failed: {e}");
            }
        }
    }
}
