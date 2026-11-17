using PaPHideoutServer;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace RaygunMk2Server;

/// <summary>
/// The Pack-a-Punched Mark II (Porter's Mark II Ray Gun) holds 42 shots per clip, as in the original. Registered with PaP's
/// server API (PaPServerApi), which runs it inside PaP's own upgrade and strip and sends the changed gun back to the
/// client: upgrading tops the loaded clip up to 42, however many it had left; stripping trims it back to a
/// plain gun's 21. Without PaP installed none of this loads.
/// </summary>
internal static class RaygunMk2PaPServer
{
    private const string Chamber = "patron_in_weapon";

    internal static ISptLogger<RaygunMk2PaPHook>? Logger;

    public static PaPWeaponHandler Handler => new()
    {
        Name = "RaygunMk2",
        Upgraded = Recharge,
        Stripped = Trim,
    };

    /// <summary>Tops the loaded clip up to 42 shots where it sits: the chamber first, then the clip's stack.</summary>
    private static void Recharge(PaPWeaponContext ctx)
    {
        Item? clip = ctx.InSlot("mod_magazine");
        if (clip == null)
            return;
        Item? stack = ctx.InSlot("cartridges", clip);
        bool chambered = ctx.InSlot(Chamber) != null;
        int have = (int)(stack?.Upd?.StackObjectsCount ?? (stack == null ? 0 : 1)) + (chambered ? 1 : 0);
        int need = RaygunMk2Ids.UpgradedClipShots - have;
        if (need <= 0)
            return;

        if (!chambered)
        {
            ctx.Attach(RaygunMk2Ids.Charge, Chamber, upd: new Upd { StackObjectsCount = 1 });
            need--;
        }
        if (need > 0 && stack != null)
            ctx.SetStack(stack, (stack.Upd?.StackObjectsCount ?? 1) + need);
        else if (need > 0)
            ctx.Attach(RaygunMk2Ids.Charge, "cartridges", clip, new Upd { StackObjectsCount = need }).Location = 0;
        Logger?.Info($"[RaygunMk2] Porter's Mark II {ctx.Weapon.Id}: clip {clip.Id} topped up to {RaygunMk2Ids.UpgradedClipShots} shots");
    }

    /// <summary>The loaded clip keeps at most a plain gun's 21 shots (the chambered one counts first).</summary>
    private static void Trim(PaPWeaponContext ctx)
    {
        Item? clip = ctx.InSlot("mod_magazine");
        Item? stack = clip == null ? null : ctx.InSlot("cartridges", clip);
        if (stack == null)
            return;
        bool chambered = ctx.InSlot(Chamber) != null;
        int keep = Math.Max(0, RaygunMk2Ids.ClipShots - (chambered ? 1 : 0));
        if ((stack.Upd?.StackObjectsCount ?? 1) <= keep)
            return;
        if (keep == 0)
            ctx.Remove(stack);
        else
            ctx.SetStack(stack, keep);
        Logger?.Info($"[RaygunMk2] RaygunMk2 {ctx.Weapon.Id}: 115 powers stripped, clip back to {RaygunMk2Ids.ClipShots} shots");
    }
}

/// <summary>Registers the Mark II with PaP when the PaP Hideout Module's server mod is loaded.</summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class RaygunMk2PaPHook(ISptLogger<RaygunMk2PaPHook> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // Our assembly references PaPHideoutServer, but only this class touches its types (at JIT time), so check first
        if (!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "PaPHideoutServer"))
        {
            logger.Info("[RaygunMk2] PaP Hideout Module not installed; no Pack-a-Punched Mark II");
            return Task.CompletedTask;
        }
        Enable(logger);
        return Task.CompletedTask;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void Enable(ISptLogger<RaygunMk2PaPHook> logger)
    {
        RaygunMk2PaPServer.Logger = logger;
        try
        {
            PaPServerApi.Register(RaygunMk2Ids.Weapon.ToString(), RaygunMk2PaPServer.Handler);
            logger.Info("[RaygunMk2] Mark II registered with Pack-a-Punch: upgrades top the loaded clip up to 42 shots");
        }
        catch (Exception e)
        {
            // An older PaP without the server API
            logger.Error($"[RaygunMk2] Mark II registration failed (PaP 1.1.0 or newer needed): {e.GetBaseException().Message}");
        }
    }
}
