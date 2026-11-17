using EFT;
using EFT.Communications;
using SPT.Reflection.Utils;

namespace RaygunMk2
{
    /// <summary>
    /// The F12 debug give: asks the server mod (RaygunMk2DebugGive item event) for a loaded Mark II with three spare
    /// clips, or one clip, straight into the stash. Hideout only: in a raid the stash isn't the live inventory.
    /// </summary>
    internal static class RaygunMk2DebugGive
    {
        private const string Action = "RaygunMk2DebugGive";

        public static void Send(string what)
        {
            if (!(LocalPlayer.Get() is HideoutPlayer))
            {
                Notify("Ray Gun Mark II: use the debug give in the hideout.");
                return;
            }
            ClientBackendSession session = ClientAppUtils.GetMainApp()?.GetClientBackEndSession() as ClientBackendSession;
            if (session == null)
            {
                RaygunMk2Plugin.Log.LogWarning("Debug give: no backend session");
                return;
            }
            session.SendOperationRightNow(new GiveOperation { Action = Action, item = what }, result =>
            {
                if (result == null || result.Failed)
                {
                    RaygunMk2Plugin.Log.LogWarning($"Debug give {what} refused: {result?.Error}");
                    Notify($"Ray Gun Mark II: {result?.Error}");
                }
                else
                {
                    RaygunMk2Plugin.Log.LogInfo($"Debug give {what}: done");
                    Notify(what == "weapon" ? "Ray Gun Mark II and 3 clips put in your stash." : "Mark II battery clip put in your stash.");
                }
            });
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

        /// <summary>Item event body; field names match the server's RaygunMk2GiveRequest.</summary>
        private class GiveOperation
        {
            public string Action;
            public string item;
        }
    }
}
