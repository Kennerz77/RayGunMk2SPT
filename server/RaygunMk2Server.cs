using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Modding.Custom;
using Path = System.IO.Path;

namespace RaygunMk2Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.kennerz.raygunmk2"; // same GUID as the client plugin (Forge rule)
    public string Name { get; init; } = "Kenner-RaygunMk2";
    public string Author { get; init; } = "Kenner";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

/// <summary>Settings read from config.json next to the DLL. Never shipped, so an update can't overwrite it.</summary>
public record RaygunMk2Config
{
    /// <summary>The hideout chalk wall's price for a Ray Gun Mark II, in roubles.</summary>
    public int WallBuyPrice { get; init; } = 950_000;

    /// <summary>The hideout ammo box's price for one full battery clip, in roubles.</summary>
    public int CartridgePrice { get; init; } = 15_000;

    internal static RaygunMk2Config Load(Action<string> logError)
    {
        string path = Path.Combine(Path.GetDirectoryName(typeof(RaygunMk2Config).Assembly.Location)!, "config.json");
        try
        {
            RaygunMk2Config config = File.Exists(path) ? System.Text.Json.JsonSerializer.Deserialize<RaygunMk2Config>(File.ReadAllText(path)) ?? new() : new();
            // Written back so settings added in a newer version show up
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return config;
        }
        catch (Exception e)
        {
            logError($"[RaygunMk2] Could not read {path}, using defaults: {e.Message}");
        }
        return new();
    }
}

/// <summary>Fixed template ids. They share a prefix so the profile store can spot them in any item list.</summary>
internal static class RaygunMk2Ids
{
    public const string Prefix = "6a7f2b4e8d115300000000";

    public static readonly MongoId Weapon = new(Prefix + "01");
    public static readonly MongoId Clip = new(Prefix + "02");   // the battery clip (the magazine)
    public static readonly MongoId Charge = new(Prefix + "03"); // one shot

    // Vanilla templates the Mark II items are cloned from
    public static readonly MongoId Saiga = new("576165642459773c7a400233");
    public static readonly MongoId SaigaMagazine = new("57616a9e2459773c7a400234");
    public static readonly MongoId CopperSlug = new("5d6e68b3a4b9361bca7e50b5");
    public static readonly MongoId DefaultInventory = new("55d7217a4bdc2d86028b456d");

    public const string WeaponParent = "5447b6094bdc2dc3278b4567";   // Shotgun (the Saiga's own class)
    public const string MagazineParent = "5448bc234bdc2d3c308b4569"; // Magazine
    public const string AmmoParent = "5485a8684bdc2da71d8b4567";     // Ammo
    public const string HandbookShotguns = "5b5f794b86f77409407a7f92";
    public const string HandbookMagazines = "5b5f754a86f774094242f19b";
    public const string HandbookRounds = "5b47574386f77428ca22b33b";

    // Bundle keys: paths under the mod's bundles folder (bundles.json), built by build_bundle.ps1
    public const string BundleFolder = "RaygunMk2";
    public const string WeaponBundle = "weapon_rgmk2_container.bundle";
    public const string ClipBundle = "mag_rgmk2_clip.bundle";

    /// <summary>The original's clip: 21 shots, 42 Pack-a-Punched.</summary>
    public const int ClipShots = 21;
    public const int UpgradedClipShots = 42;

    public static bool IsOurs(string? templateId) => templateId != null && templateId.StartsWith(Prefix);
}

/// <summary>
/// Adds the Ray Gun Mark II, its battery clip and the charge it holds. Items must exist before profiles load, hence
/// Preload. Until the Mark II bundle exists the clones keep the vanilla Saiga-12K's model.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public class RaygunMk2ItemLoader(
    ISptLogger<RaygunMk2ItemLoader> logger,
    CustomItemService customItemService,
    TemplateTable templateTable,
    LocaleTable localeTable,
    ItemConfig itemConfig,
    ItemFilterService itemFilterService) : IOnLoad
{
    /// <summary>Out of random loot, bot gear, flea and daily rewards.</summary>
    private void KeepOutOfTheWorld(IEnumerable<MongoId> ids)
    {
        List<MongoId> all = ids.ToList();
        foreach (MongoId id in all)
        {
            itemConfig.Blacklist.Add(id);
            itemConfig.LootableItemBlacklist.Add(id);
            itemConfig.RewardItemBlacklist.Add(id);
        }
        itemFilterService.AddItemToBlacklistCache(all);
        itemFilterService.AddItemToLootableBlacklistCache(all);
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var items = templateTable.Items;

        TemplateItem? charge = Clone(RaygunMk2Ids.CopperSlug, RaygunMk2Ids.AmmoParent, RaygunMk2Ids.Charge, "patron_raygunmk2_charge",
            RaygunMk2Ids.HandbookRounds, 500, "Ray Gun Mark II charge", "Mk II",
            "One shot of a Mark II battery. Only exists inside a battery clip.");
        TemplateItem? clip = Clone(RaygunMk2Ids.SaigaMagazine, RaygunMk2Ids.MagazineParent, RaygunMk2Ids.Clip, "mag_raygunmk2_battery_clip",
            RaygunMk2Ids.HandbookMagazines, 15_000, "Mark II battery clip", "Mk II clip",
            "A clip of three glowing batteries for the Ray Gun Mark II: 21 shots, 42 in a Pack-a-Punched gun.");
        TemplateItem? weapon = Clone(RaygunMk2Ids.Saiga, RaygunMk2Ids.WeaponParent, RaygunMk2Ids.Weapon, "weapon_group935_raygun_mk2",
            RaygunMk2Ids.HandbookShotguns, 500_000, "Ray Gun Mark II", "Mk II",
            "Group 935's improved Ray Gun. Fires three-shot bursts of energy bolts that cut straight through.");
        if (charge?.Properties == null || clip?.Properties == null || weapon?.Properties == null)
            return Task.CompletedTask;

        clip.Properties.Width = 1;
        clip.Properties.Height = 1;
        clip.Properties.ExtraSizeLeft = 0;
        clip.Properties.ExtraSizeRight = 0;
        clip.Properties.ExtraSizeUp = 0;
        clip.Properties.ExtraSizeDown = 0;
        clip.Properties.ExtraSizeForceAdd = false;
        clip.Properties.Weight = 0.3;
        // Room for the Pack-a-Punched 42: a plain gun's clips are filled to 21 (the debug give and the zombies mode)
        HashSet<MongoId> chargeOnly = [RaygunMk2Ids.Charge];
        SetCartridges(clip.Properties, RaygunMk2Ids.UpgradedClipShots, chargeOnly);

        // The bolt: a fast, flat, green energy round that hits hard and goes through armour
        TemplateItemProperties bolt = charge.Properties;
        bolt.StackMaxSize = RaygunMk2Ids.UpgradedClipShots;
        bolt.Caliber = Caliber;
        bolt.Damage = 160;
        bolt.PenetrationPower = 45;
        bolt.ArmorDamage = 60;
        bolt.InitialSpeed = 300;
        bolt.BallisticCoeficient = 0.6;
        bolt.ProjectileCount = 1;
        // The client draws the bolt (its ball, line and trail): no game tracer on top
        bolt.Tracer = false;
        bolt.TracerDistance = 0;
        // It bursts where it hits: a small blast of energy, no shrapnel, under its own explosion name (no game effect
        // has it: the client draws the green burst and plays the original's impact sounds)
        bolt.HasGrenaderComponent = true;
        bolt.ExplosionType = ExplosionName;
        bolt.FuzeArmTimeSec = 0;
        bolt.ExplosionStrength = 45;
        bolt.MinExplosionDistance = 0.5;
        bolt.MaxExplosionDistance = 1.8;
        bolt.FragmentsCount = 0;
        bolt.ShowHitEffectOnExplode = false;
        bolt.Blindness = new() { X = 0, Y = 0, Z = 0 };
        bolt.Contusion = new() { X = 0, Y = 0, Z = 0 };
        bolt.DurabilityBurnModificator = 0;
        bolt.HeatFactor = 0;
        bolt.MisfireChance = 0;
        bolt.MalfMisfireChance = 0;
        bolt.MalfFeedChance = 0;
        bolt.Weight = 0;

        TemplateItemProperties gun = weapon.Properties;
        gun.AmmoCaliber = Caliber;
        gun.Weight = 2.4;
        gun.DurabilityBurnRatio = 0;
        // Nothing can go wrong with it: no jams, misfeeds, misfires or overheating
        gun.AllowJam = false;
        gun.AllowFeed = false;
        gun.AllowMisfire = false;
        gun.AllowSlide = false;
        gun.AllowOverheat = false;
        gun.BaseMalfunctionChance = 0;
        gun.Width = 3;
        gun.Height = 2;
        gun.Foldable = false;
        // No shotgun spread from the Saiga it's cloned from: the client gives each bolt its own small spread
        gun.CenterOfImpact = 0.01;
        gun.ShotgunDispersion = 0;
        // Only the clip slot: the Saiga's required handguard and grip slots would leave the gun unusable bare
        Slot? magazineSlot = gun.Slots?.FirstOrDefault(s => s.Name == "mod_magazine");
        gun.Slots = magazineSlot == null ? [] : [WithFilter(magazineSlot, [RaygunMk2Ids.Clip])];
        gun.Chambers = gun.Chambers?.Select(c => WithFilter(c, chargeOnly)).ToList();
        gun.DefMagType = RaygunMk2Ids.Clip;
        gun.DefAmmo = RaygunMk2Ids.Charge;

        // Three-shot bursts at the original's pace (a shot every 0.12 s)
        gun.WeapFireType = ["burst"];
        gun.BurstShotsCount = 3;
        gun.BFirerate = 500;
        gun.SingleFireRate = 500;
        // The kick is the fire animation's: no camera recoil on top
        gun.RecoilForceUp = 0;
        gun.RecoilForceBack = 0;
        gun.RecoilCamera = 0;
        if (gun.WeaponRecoilSettings != null)
            gun.WeaponRecoilSettings.Enable = false;

        foreach (TemplateItem item in new[] { charge, clip, weapon })
        {
            item.Properties!.CanSellOnRagfair = false;
            items[item.Id] = item;
        }
        AllowInWeaponSlots(items);
        UseOwnModels(weapon.Properties, clip.Properties);
        AddCaliberName();
        KeepOutOfTheWorld([RaygunMk2Ids.Weapon, RaygunMk2Ids.Clip, RaygunMk2Ids.Charge]);
        logger.Success("[RaygunMk2] Items added: Ray Gun Mark II, Mark II battery clip, Mark II charge");
        return Task.CompletedTask;
    }

    private const string Caliber = "CaliberRaygunMk2";

    /// <summary>The bolt's explosion name (the client mod draws it): matches RaygunMk2Bolts.ExplosionName.</summary>
    private const string ExplosionName = "raygunmk2_bolt";

    /// <summary>The calibre's name in the stats, every language: the game looks it up both with and without "Caliber".</summary>
    private void AddCaliberName()
    {
        foreach (var locale in localeTable.Global.Values)
        {
            locale.AddTransformer(dictionary =>
            {
                if (dictionary != null)
                {
                    dictionary[Caliber] = "Mark II battery";
                    dictionary[Caliber.Replace("Caliber", "")] = "Mark II battery";
                }
                return dictionary;
            });
        }
    }

    private TemplateItem? Clone(MongoId from, string parent, MongoId id, string name, string handbookParent, int price,
        string locName, string shortName, string description)
    {
        CreateItemResult result = customItemService.CreateItemFromClone(new NewItemFromCloneDetails
        {
            ItemTplToClone = from,
            ParentId = parent,
            NewId = id,
            NewItemName = name,
            AddToHandbook = true,
            HandbookParentId = handbookParent,
            HandbookPriceRoubles = price,
            AddToFleaPriceDb = false,
            AddToWeaponShelf = false,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new() { Name = locName, ShortName = shortName, Description = description },
            },
        });
        if (result.Success != true || !templateTable.Items.TryGetValue(id, out TemplateItem? item))
        {
            logger.Error($"[RaygunMk2] Could not add {name}: {string.Join("; ", result.Errors ?? [])}");
            return null;
        }
        return item;
    }

    /// <summary>
    /// Points the items at the Mark II's own bundles (bundles/RaygunMk2/, served through bundles.json). Without them
    /// the clones keep the Saiga's models, so the items still work while the bundles are being built.
    /// </summary>
    private void UseOwnModels(TemplateItemProperties gun, TemplateItemProperties clip)
    {
        string dir = Path.Combine(Path.GetDirectoryName(typeof(RaygunMk2ItemLoader).Assembly.Location)!, "bundles", RaygunMk2Ids.BundleFolder);
        bool Has(string bundle) => File.Exists(Path.Combine(dir, bundle));
        if (!Has(RaygunMk2Ids.WeaponBundle) || !Has(RaygunMk2Ids.ClipBundle))
        {
            logger.Warning($"[RaygunMk2] Bundles missing in {dir}; using the Saiga-12K's models");
            return;
        }
        gun.Prefab = new Prefab { Path = $"{RaygunMk2Ids.BundleFolder}/{RaygunMk2Ids.WeaponBundle}", Rcid = "" };
        clip.Prefab = new Prefab { Path = $"{RaygunMk2Ids.BundleFolder}/{RaygunMk2Ids.ClipBundle}", Rcid = "" };
    }

    /// <summary>
    /// The player's weapon slots list their guns one template at a time, so a clone is "incompatible" until it's
    /// added by id. The two primary slots only (user, 2026-11-16: no holster).
    /// </summary>
    private void AllowInWeaponSlots(Dictionary<MongoId, TemplateItem> items)
    {
        if (!items.TryGetValue(RaygunMk2Ids.DefaultInventory, out TemplateItem? inventory) || inventory.Properties?.Slots == null)
        {
            logger.Error("[RaygunMk2] Default inventory template not found; the Mark II can't be equipped");
            return;
        }
        foreach (Slot slot in inventory.Properties.Slots.Where(s => s.Name is "FirstPrimaryWeapon" or "SecondPrimaryWeapon"))
        {
            foreach (SlotFilter filter in slot.Properties?.Filters ?? [])
                filter.Filter?.Add(RaygunMk2Ids.Weapon);
        }
    }

    /// <summary>The clone shares nothing with the vanilla magazine: a fresh cartridges slot that takes only charges.</summary>
    private static void SetCartridges(TemplateItemProperties props, int count, HashSet<MongoId> filter)
    {
        Slot? cartridges = props.Cartridges?.FirstOrDefault();
        if (cartridges == null)
            return;
        props.Cartridges = [WithFilter(cartridges, filter) with { MaxCount = count }];
    }

    // New records all the way down, so the vanilla template's filter lists are never touched
    private static Slot WithFilter(Slot slot, HashSet<MongoId> filter) => slot with
    {
        Properties = new SlotProperties { Filters = [new SlotFilter { Filter = [.. filter], Shift = 0 }] },
    };
}
