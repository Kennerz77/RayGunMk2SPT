using System.Collections;
using System.Reflection;
using EFT;
using EFT.Communications;
using HarmonyLib;

namespace RaygunMk2
{
    /// <summary>
    /// F12 dev: hands you the gun in a Kennerz Zombies match the way the mystery box does (the core's own give: the gun,
    /// a full mag of its default ammo, drawn), to test it there. Reached by name, so this mod needs no core.
    /// </summary>
    internal static class RaygunMk2ZombiesGive
    {
        public static void Give()
        {
            try
            {
                PropertyInfo active = AccessTools.TypeByName("KennerZombies.ZombiesMatch")?.GetProperty("Active", BindingFlags.Public | BindingFlags.Static);
                if (active == null || !(bool)active.GetValue(null))
                {
                    Notify("Ray Gun Mark II: the zombies give works in a zombies match (the hideout one is the other button).");
                    return;
                }
                Player me = LocalPlayer.Get();
                System.Type gunType = AccessTools.TypeByName("KennerZombies.BoxGuns+Gun");
                MethodInfo give = AccessTools.Method(AccessTools.TypeByName("KennerZombies.BoxGive"), "Give");
                if (me == null || gunType == null || give == null)
                {
                    Notify("Ray Gun Mark II: the zombies core's give wasn't found (core too old?).");
                    return;
                }
                object gun = System.Activator.CreateInstance(gunType);
                AccessTools.Field(gunType, "Id").SetValue(gun, "debug_" + RaygunMk2Ids.Weapon);
                AccessTools.Field(gunType, "Name").SetValue(gun, RaygunMk2Ids.Name);
                AccessTools.Field(gunType, "Role").SetValue(gun, "box_wonder");
                // Built as a box gun is, gun and its mag apart (user, 2026-11-16: made from the template's preset it came
                // with no mag, one round in the chamber and nothing to reload with): the core fills the mag, racks it in
                // and keeps its reserve. The core's builds are JSON (Newtonsoft), reached by name too
                string build = "{\"tpl\":\"" + RaygunMk2Ids.Weapon + "\",\"parts\":[{\"slot\":\"mod_magazine\",\"tpl\":\"" + RaygunMk2Ids.Clip + "\",\"parts\":[]}]}";
                MethodInfo parse = AccessTools.Method(AccessTools.TypeByName("Newtonsoft.Json.Linq.JObject"), "Parse", new[] { typeof(string) });
                AccessTools.Field(gunType, "Base").SetValue(gun, parse.Invoke(null, new object[] { build }));
                AccessTools.Field(gunType, "Pap").SetValue(gun, parse.Invoke(null, new object[] { build }));
                var routine = (IEnumerator)give.Invoke(null, new[] { gun, (object)me, false });
                RaygunMk2Plugin.Runner.StartCoroutine(routine);
                RaygunMk2Plugin.Log.LogInfo("Zombies debug give: Ray Gun Mark II");
            }
            catch (System.Exception e)
            {
                RaygunMk2Plugin.Log.LogWarning($"Zombies debug give failed: {e.GetBaseException().Message}");
                Notify("Ray Gun Mark II: the zombies give failed (see the log).");
            }
        }

        private static void Notify(string text)
        {
            try
            {
                NotificationManager.DisplayMessageNotification(text);
            }
            catch
            {
            }
        }
    }
}
