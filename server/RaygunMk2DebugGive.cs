using System.Text.Json.Serialization;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Utils;

namespace RaygunMk2Server;

/// <summary>Body of the client's debug give: "weapon" (the gun, loaded) or "clip" (a full spare clip).</summary>
public record RaygunMk2GiveRequest : BaseInteractionRequestData
{
    [JsonPropertyName("item")]
    public string? Item { get; set; }
}

/// <summary>
/// The F12 debug give (hideout, for testing): the gun loaded plus three spare clips, or one more clip, straight into
/// the stash through the reply, so the client sees them at once. Free. The client only offers it in dev builds.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class RaygunMk2DebugGive(ISptLogger<RaygunMk2DebugGive> logger, InventoryHelper inventoryHelper, HttpResponseUtil httpResponseUtil)
{
    public const string GiveAction = "RaygunMk2DebugGive";

    public ItemEventRouterResponse Give(PmcData pmcData, RaygunMk2GiveRequest body, MongoId sessionId, ItemEventRouterResponse output)
    {
        var bundles = new List<List<Item>>();
        if (body.Item == "weapon")
        {
            bundles.Add(Weapon());
            for (int i = 0; i < 3; i++)
                bundles.Add(Clip(null, null, RaygunMk2Ids.ClipShots));
        }
        else if (body.Item == "clip")
        {
            bundles.Add(Clip(null, null, RaygunMk2Ids.ClipShots));
        }
        if (bundles.Count == 0)
            return httpResponseUtil.AppendErrorToOutput(output, "Nothing to give.");
        if (!inventoryHelper.CanPlaceItemsInInventory(sessionId, bundles))
            return httpResponseUtil.AppendErrorToOutput(output, "No room in your stash.");
        inventoryHelper.AddItemsToStash(sessionId, new AddItemsDirectRequest { ItemsWithModsToAdd = bundles, FoundInRaid = false }, pmcData, output);
        logger.Info($"[RaygunMk2] Debug give to {pmcData.Info?.Nickname}: {body.Item} ({bundles.Count} item(s))");
        return output;
    }

    /// <summary>The gun ready to fire: one charge chambered, the rest of a full clip in it.</summary>
    private static List<Item> Weapon()
    {
        MongoId gun = new();
        var items = new List<Item>
        {
            new() { Id = gun, Template = RaygunMk2Ids.Weapon, Upd = new Upd { FireMode = new UpdFireMode { FireMode = "burst" } } },
            new() { Id = new MongoId(), Template = RaygunMk2Ids.Charge, ParentId = gun.ToString(), SlotId = "patron_in_weapon", Upd = new Upd { StackObjectsCount = 1 } },
        };
        items.AddRange(Clip(gun.ToString(), "mod_magazine", RaygunMk2Ids.ClipShots - 1));
        return items;
    }

    private static List<Item> Clip(string? parent, string? slot, int shots)
    {
        MongoId clip = new();
        var items = new List<Item> { new() { Id = clip, Template = RaygunMk2Ids.Clip, ParentId = parent, SlotId = slot } };
        if (shots > 0)
            items.Add(new Item { Id = new MongoId(), Template = RaygunMk2Ids.Charge, ParentId = clip.ToString(), SlotId = "cartridges", Location = 0, Upd = new Upd { StackObjectsCount = shots } });
        return items;
    }
}

/// <summary>Routes the client's RaygunMk2DebugGive item event.</summary>
[Injectable(TypePriority = OnLoadOrder.Routers)]
public class RaygunMk2DebugGiveRouter(RaygunMk2DebugGive give) : ItemEventRouter([
    new ItemRouteAction<RaygunMk2GiveRequest>(RaygunMk2DebugGive.GiveAction,
        (url, pmcData, body, sessionID, output, cancellationToken) => ValueTask.FromResult(give.Give(pmcData, body, sessionID, output))),
]) { }
