using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Kits", "Reneb", "3.2.92", ResourceId = 668)]
    class Kits : RustPlugin
    {
        readonly int playerLayer = LayerMask.GetMask("Player (Server)");

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Plugin initialization
        //////////////////////////////////////////////////////////////////////////////////////////
        [PluginReference]
        Plugin CopyPaste, ImageLibrary, EventManager;

        void Loaded()
        {
            LoadData();
            try
            {
                kitsData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, KitData>>>("Kits_Data");
            }
            catch
            {
                kitsData = new Dictionary<ulong, Dictionary<string, KitData>>();
            }
            lang.RegisterMessages(messages, this);
        }

        void OnServerInitialized()
        {
            InitializePermissions();
            if (!string.IsNullOrEmpty(BackgroundURL))
                if (ImageLibrary)
                    AddImage(BackgroundURL, "Background", (ulong)ResourceId);
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        void OnPlayerInit(BasePlayer player)
        {
            BindKeys(player);
        }

        void InitializePermissions()
        {
            permission.RegisterPermission(this.Title + ".admin", this);
            permission.RegisterPermission(this.Title + ".ConsoleGive", this);
            foreach (var kit in storedData.Kits.Values)
            {
                if (!string.IsNullOrEmpty(kit.permission) && !permission.PermissionExists(kit.permission))
                    permission.RegisterPermission(kit.permission, this);
                if (ImageLibrary)
                    AddImage(kit.image ?? "http://i.imgur.com/xxQnE1R.png", kit.name.Replace(" ", ""), (ulong)ResourceId);
            }
        }

        void BindKeys(BasePlayer player, bool unbind = false)
        {
            if (string.IsNullOrEmpty(UIKeyBinding)) return;
            if (unbind)
                player.Command($"bind {UIKeyBinding} \"\"");
            else
                player.Command($"bind {UIKeyBinding} \"UI_ToggleKitMenu\"");
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Configuration
        //////////////////////////////////////////////////////////////////////////////////////////

        Dictionary<ulong, GUIKit> GUIKits;
        List<string> CopyPasteParameters = new List<string>();
        string BackgroundURL;
        string UIKeyBinding;
        bool KitLogging;
        bool ShowUnavailableKits;
        public Dictionary<int, string> AutoKits = new Dictionary<int, string>();

        class GUIKit
        {
            public string description = string.Empty;
            public List<string> kits = new List<string>();
        }

        protected override void LoadDefaultConfig() { }

        void Init()
        {
            var config = Config.ReadObject<Dictionary<string, object>>();
            if (!config.ContainsKey("NPC - GUI Kits"))
            {
                config["NPC - GUI Kits"] = GetExampleGUIKits();
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("CopyPaste - Parameters"))
            {
                config["CopyPaste - Parameters"] = new List<string> { "deployables", "true", "inventories", "true" };
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("Custom AutoKits"))
            {
                config["Custom AutoKits"] = new Dictionary<int, string> {{0, "KitName" },{1, "KitName" },{2, "KitName" }};
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("UI KeyBinding"))
            {
                config["UI KeyBinding"] = string.Empty;
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("Kit - Logging"))
            {
                config["Kit - Logging"] = false;
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("Show All Kits"))
            {
                config["Show All Kits"] = false;
                Config.WriteObject(config);
            }
            if (!config.ContainsKey("Background - URL"))
            {
                config["Background - URL"] = string.Empty;
                Config.WriteObject(config);
            }
            var keys = config.Keys.ToArray();
            if (keys.Length > 1)
            {
                foreach (var key in keys)
                {
                    if (!key.Equals("NPC - GUI Kits") && !key.Equals("CopyPaste - Parameters") && !key.Equals("Custom AutoKits") && !key.Equals("UI KeyBinding") && !key.Equals("Background - URL") && !key.Equals("Kit - Logging") && !key.Equals("Show All Kits"))
                        config.Remove(key);
                }
                Config.WriteObject(config);
            }
            CopyPasteParameters = JsonConvert.DeserializeObject<List<string>>(JsonConvert.SerializeObject(config["CopyPaste - Parameters"]));
            GUIKits = JsonConvert.DeserializeObject<Dictionary<ulong, GUIKit>>(JsonConvert.SerializeObject(config["NPC - GUI Kits"]));
            AutoKits = JsonConvert.DeserializeObject<Dictionary<int, string>>(JsonConvert.SerializeObject(config["Custom AutoKits"]));
            UIKeyBinding = JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(config["UI KeyBinding"]));
            BackgroundURL = JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(config["Background - URL"]));
            KitLogging = JsonConvert.DeserializeObject<bool>(JsonConvert.SerializeObject(config["Kit - Logging"]));
            ShowUnavailableKits = JsonConvert.DeserializeObject<bool>(JsonConvert.SerializeObject(config["Show All Kits"]));
        }

        static Dictionary<ulong, GUIKit> GetExampleGUIKits()
        {
            return new Dictionary<ulong, GUIKit>
            {
                {
                    0, new GUIKit
                    {
                        kits = {"kit1", "kit2"},
                        description = "Welcome on this server! Here is a list of free kits that you can get.<color=green>Enjoy your stay</color>"
                    }
                },
{
                    1235439, new GUIKit
                    {
                        kits = {"kit1", "kit2"},
                        description = "Welcome on this server! Here is a list of free kits that you can get.<color=green>Enjoy your stay</color>"
                    }
                },
                {
                    8753201223, new GUIKit
                    {
                        kits = {"kit1", "kit3"},
                        description = "<color=red>VIPs Kits</color>"
                    }
                }
            };
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            var thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn == null)
            {
                if (storedData.Kits.ContainsKey("autokit"))
                {
                    player.inventory.Strip();
                    GiveKit(player, "autokit");
                    return;
                }
                foreach (var entry in AutoKits.OrderBy(k=>k.Key))
                    {
                        var success = CanRedeemKit(player, entry.Value, true) as string;
                        if (success != null) continue;
                        player.inventory.Strip();
                        success = GiveKit(player, entry.Value) as string;
                        if (success != null) continue;
                        proccessKitGiven(player, entry.Value);
                        return;
                    }
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Language
        //////////////////////////////////////////////////////////////////////////////////////////

        string GetMsg(string key, object steamid = null) { return lang.GetMessage(key, this, steamid == null ? null : steamid.ToString()); }

        Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {"title", "Kits: " },
            {"Name", "Name" },
            {"Description", "Description" },
            {"Redeem", "Redeem" },
            {"AddKit", "Add Kit" },
            {"Close", "Close" },
            {"NoKitsFound", "All Available Kits are already in this Menu!" },
            {"AddKitToMenu", "Select a Kit to Add to this Menu" },
            {"KitCooldown", "Cooldown: {0}" },
            {"Unavailable", "Unavailable" },
            {"Last", "Last" },
            {"Next", "Next" },
            {"Back", "Back" },
            {"First", "First" },
            {"RemoveKit", "Remove Kit?" },
            {"KitUses", "Uses: {0}" },
            {"NoInventorySpace", "You do not have enough inventory space for this Kit!" },
            {"Unlimited", "Unlimited" },
            {"None", "None" },
            {"KitRedeemed", "Kit Redeemed" },
            {"Emptykitname", "Empty Kit Name" },
            {"KitExistError","This kit doesn't exist"},
            {"CantRedeemNow","You are not allowed to redeem a kit at the moment"},
            {"NoAuthToRedeem","You don't have the Auth Level to use this kit"},
            {"NoPermKit","You don't have the permissions to use this kit"},
            {"NoRemainingUses","You already redeemed all of these kits"},
            {"CooldownMessage","You need to wait {0} seconds to use this kit"},
            {"NPCError","You must find the NPC that gives this kit to redeem it."},
            {"PastingError", "Something went wrong while pasting, is CopyPaste installed?"},
            {"NoKitFound", "This kit doesn't exist" },
};

        private readonly Dictionary<int, string> _itemIdShortnameConversions = new Dictionary<int, string>
        {
            [-1461508848] = "rifle.ak",
            [2115555558] = "ammo.handmade.shell",
            [-533875561] = "ammo.pistol",
            [1621541165] = "ammo.pistol.fire",
            [-422893115] = "ammo.pistol.hv",
            [815896488] = "ammo.rifle",
            [805088543] = "ammo.rifle.explosive",
            [449771810] = "ammo.rifle.incendiary",
            [1152393492] = "ammo.rifle.hv",
            [1578894260] = "ammo.rocket.basic",
            [1436532208] = "ammo.rocket.fire",
            [542276424] = "ammo.rocket.hv",
            [1594947829] = "ammo.rocket.smoke",
            [-1035059994] = "ammo.shotgun",
            [1818890814] = "ammo.shotgun.fire",
            [1819281075] = "ammo.shotgun.slug",
            [1685058759] = "antiradpills",
            [93029210] = "apple",
            [-1565095136] = "apple.spoiled",
            [-1775362679] = "arrow.bone",
            [-1775249157] = "arrow.fire",
            [-1280058093] = "arrow.hv",
            [-420273765] = "arrow.wooden",
            [563023711] = "autoturret",
            [790921853] = "axe.salvaged",
            [-337261910] = "bandage",
            [498312426] = "barricade.concrete",
            [504904386] = "barricade.metal",
            [-1221200300] = "barricade.sandbags",
            [510887968] = "barricade.stone",
            [-814689390] = "barricade.wood",
            [1024486167] = "barricade.woodwire",
            [2021568998] = "battery.small",
            [97329] = "bbq",
            [1046072789] = "trap.bear",
            [97409] = "bed",
            [-1480119738] = "tool.binoculars",
            [1611480185] = "black.raspberries",
            [-1386464949] = "bleach",
            [93832698] = "blood",
            [-1063412582] = "blueberries",
            [-1887162396] = "blueprintbase",
            [-55660037] = "rifle.bolt",
            [919780768] = "bone.club",
            [-365801095] = "bone.fragments",
            [68998734] = "botabag",
            [-853695669] = "bow.hunting",
            [271534758] = "box.wooden.large",
            [-770311783] = "box.wooden",
            [-1192532973] = "bucket.water",
            [-307490664] = "building.planner",
            [707427396] = "burlap.shirt",
            [707432758] = "burlap.shoes",
            [-2079677721] = "cactusflesh",
            [-1342405573] = "tool.camera",
            [-139769801] = "campfire",
            [-1043746011] = "can.beans",
            [2080339268] = "can.beans.empty",
            [-171664558] = "can.tuna",
            [1050986417] = "can.tuna.empty",
            [-1693683664] = "candycaneclub",
            [523409530] = "candycane",
            [1300054961] = "cctv.camera",
            [-2095387015] = "ceilinglight",
            [1428021640] = "chainsaw",
            [94623429] = "chair",
            [1436001773] = "charcoal",
            [1711323399] = "chicken.burned",
            [1734319168] = "chicken.cooked",
            [-1658459025] = "chicken.raw",
            [-726947205] = "chicken.spoiled",
            [-341443994] = "chocholate",
            [1540879296] = "xmasdoorwreath",
            [94756378] = "cloth",
            [3059095] = "coal",
            [3059624] = "corn",
            [2045107609] = "clone.corn",
            [583366917] = "seed.corn",
            [2123300234] = "crossbow",
            [1983936587] = "crude.oil",
            [1257201758] = "cupboard.tool",
            [-1144743963] = "diving.fins",
            [-1144542967] = "diving.mask",
            [-1144334585] = "diving.tank",
            [1066729526] = "diving.wetsuit",
            [-1598790097] = "door.double.hinged.metal",
            [-933236257] = "door.double.hinged.toptier",
            [-1575287163] = "door.double.hinged.wood",
            [-2104481870] = "door.hinged.metal",
            [-1571725662] = "door.hinged.toptier",
            [1456441506] = "door.hinged.wood",
            [1200628767] = "door.key",
            [-778796102] = "door.closer",
            [1526866730] = "xmas.door.garland",
            [1925723260] = "dropbox",
            [1891056868] = "ducttape",
            [1295154089] = "explosive.satchel",
            [498591726] = "explosive.timed",
            [1755466030] = "explosives",
            [726730162] = "facialhair.style01",
            [-1034048911] = "fat.animal",
            [252529905] = "femalearmpithair.style01",
            [471582113] = "femaleeyebrow.style01",
            [-1138648591] = "femalepubichair.style01",
            [305916740] = "female_hairstyle_01",
            [305916742] = "female_hairstyle_03",
            [305916744] = "female_hairstyle_05",
            [1908328648] = "fireplace.stone",
            [-2078972355] = "fish.cooked",
            [-533484654] = "fish.raw",
            [1571660245] = "fishingrod.handmade",
            [1045869440] = "flamethrower",
            [1985408483] = "flameturret",
            [97513422] = "flare",
            [1496470781] = "flashlight.held",
            [1229879204] = "weapon.mod.flashlight",
            [-1722829188] = "floor.grill",
            [1849912854] = "floor.ladder.hatch",
            [-1266285051] = "fridge",
            [-1749787215] = "boots.frog",
            [28178745] = "lowgradefuel",
            [-505639592] = "furnace",
            [1598149413] = "furnace.large",
            [-1779401418] = "gates.external.high.stone",
            [-57285700] = "gates.external.high.wood",
            [98228420] = "gears",
            [1422845239] = "geiger.counter",
            [277631078] = "generator.wind.scrap",
            [115739308] = "burlap.gloves",
            [-522149009] = "gloweyes",
            [3175989] = "glue",
            [718197703] = "granolabar",
            [384204160] = "grenade.beancan",
            [-1308622549] = "grenade.f1",
            [-217113639] = "fun.guitar",
            [-1580059655] = "gunpowder",
            [-1832205789] = "male_hairstyle_01",
            [305916741] = "female_hairstyle_02",
            [936777834] = "attire.hide.helterneck",
            [-1224598842] = "hammer",
            [-1976561211] = "hammer.salvaged",
            [-1406876421] = "hat.beenie",
            [-1397343301] = "hat.boonie",
            [1260209393] = "bucket.helmet",
            [-1035315940] = "burlap.headwrap",
            [-1381682752] = "hat.candle",
            [696727039] = "hat.cap",
            [-2128719593] = "coffeecan.helmet",
            [-1178289187] = "deer.skull.mask",
            [1351172108] = "heavy.plate.helmet",
            [-450738836] = "hat.miner",
            [-966287254] = "attire.reindeer.headband",
            [340009023] = "riot.helmet",
            [124310981] = "hat.wolf",
            [1501403549] = "wood.armor.helmet",
            [698310895] = "hatchet",
            [523855532] = "hazmatsuit",
            [2045246801] = "clone.hemp",
            [583506109] = "seed.hemp",
            [-148163128] = "attire.hide.boots",
            [-132588262] = "attire.hide.skirt",
            [-1666761111] = "attire.hide.vest",
            [-465236267] = "weapon.mod.holosight",
            [-1211618504] = "hoodie",
            [2133577942] = "hq.metal.ore",
            [-1014825244] = "humanmeat.burned",
            [-991829475] = "humanmeat.cooked",
            [-642008142] = "humanmeat.raw",
            [661790782] = "humanmeat.spoiled",
            [-1440143841] = "icepick.salvaged",
            [569119686] = "bone.armor.suit",
            [1404466285] = "heavy.plate.jacket",
            [-1616887133] = "jacket.snow",
            [-1167640370] = "jacket",
            [-1284735799] = "jackolantern.angry",
            [-1278649848] = "jackolantern.happy",
            [776005741] = "knife.bone",
            [108061910] = "ladder.wooden.wall",
            [255101535] = "trap.landmine",
            [-51678842] = "lantern",
            [-789202811] = "largemedkit",
            [516382256] = "weapon.mod.lasersight",
            [50834473] = "leather",
            [-975723312] = "lock.code",
            [1908195100] = "lock.key",
            [-1097452776] = "locker",
            [146685185] = "longsword",
            [-1716193401] = "rifle.lr300",
            [193190034] = "lmg.m249",
            [371156815] = "pistol.m92",
            [3343606] = "mace",
            [825308669] = "machete",
            [830965940] = "mailbox",
            [1662628660] = "male.facialhair.style02",
            [1662628661] = "male.facialhair.style03",
            [1662628662] = "male.facialhair.style04",
            [-1832205788] = "male_hairstyle_02",
            [-1832205786] = "male_hairstyle_04",
            [1625090418] = "malearmpithair.style01",
            [-1269800768] = "maleeyebrow.style01",
            [429648208] = "malepubichair.style01",
            [-1832205787] = "male_hairstyle_03",
            [-1832205785] = "male_hairstyle_05",
            [107868] = "map",
            [997973965] = "mask.balaclava",
            [-46188931] = "mask.bandana",
            [-46848560] = "metal.facemask",
            [-2066726403] = "bearmeat.burned",
            [-2043730634] = "bearmeat.cooked",
            [1325935999] = "bearmeat",
            [-225234813] = "deermeat.burned",
            [-202239044] = "deermeat.cooked",
            [-322501005] = "deermeat.raw",
            [-1851058636] = "horsemeat.burned",
            [-1828062867] = "horsemeat.cooked",
            [-1966381470] = "horsemeat.raw",
            [968732481] = "meat.pork.burned",
            [991728250] = "meat.pork.cooked",
            [-253819519] = "meat.boar",
            [-1714986849] = "wolfmeat.burned",
            [-1691991080] = "wolfmeat.cooked",
            [179448791] = "wolfmeat.raw",
            [431617507] = "wolfmeat.spoiled",
            [688032252] = "metal.fragments",
            [-1059362949] = "metal.ore",
            [1265861812] = "metal.plate.torso",
            [374890416] = "metal.refined",
            [1567404401] = "metalblade",
            [-1057402571] = "metalpipe",
            [-758925787] = "mining.pumpjack",
            [-1411620422] = "mining.quarry",
            [88869913] = "fish.minnows",
            [-2094080303] = "smg.mp5",
            [843418712] = "mushroom",
            [-1569356508] = "weapon.mod.muzzleboost",
            [-1569280852] = "weapon.mod.muzzlebrake",
            [449769971] = "pistol.nailgun",
            [590532217] = "ammo.nailgun.nails",
            [3387378] = "note",
            [1767561705] = "burlap.trousers",
            [106433500] = "pants",
            [-1334615971] = "heavy.plate.pants",
            [-135651869] = "attire.hide.pants",
            [-1595790889] = "roadsign.kilt",
            [-459156023] = "pants.shorts",
            [106434956] = "paper",
            [-578028723] = "pickaxe",
            [-586116979] = "jar.pickle",
            [-1379225193] = "pistol.eoka",
            [-930579334] = "pistol.revolver",
            [548699316] = "pistol.semiauto",
            [142147109] = "planter.large",
            [148953073] = "planter.small",
            [102672084] = "attire.hide.poncho",
            [640562379] = "pookie.bear",
            [-1732316031] = "xmas.present.large",
            [-2130280721] = "xmas.present.medium",
            [-1725510067] = "xmas.present.small",
            [1974032895] = "propanetank",
            [-225085592] = "pumpkin",
            [509654999] = "clone.pumpkin",
            [466113771] = "seed.pumpkin",
            [2033918259] = "pistol.python",
            [2069925558] = "target.reactive",
            [-1026117678] = "box.repair.bench",
            [1987447227] = "research.table",
            [540154065] = "researchpaper",
            [1939428458] = "riflebody",
            [-288010497] = "roadsign.jacket",
            [-847065290] = "roadsigns",
            [3506021] = "rock",
            [649603450] = "rocket.launcher",
            [3506418] = "rope",
            [569935070] = "rug.bear",
            [113284] = "rug",
            [1916127949] = "water.salt",
            [-1775234707] = "salvaged.cleaver",
            [-388967316] = "salvaged.sword",
            [2007564590] = "santahat",
            [-1705696613] = "scarecrow",
            [670655301] = "hazmatsuit_scientist",
            [1148128486] = "hazmatsuit_scientist_peacekeeper",
            [-141135377] = "weapon.mod.small.scope",
            [109266897] = "scrap",
            [-527558546] = "searchlight",
            [-1745053053] = "rifle.semiauto",
            [1223860752] = "semibody",
            [-419069863] = "sewingkit",
            [-1617374968] = "sheetmetal",
            [2057749608] = "shelves",
            [24576628] = "shirt.collared",
            [-1659202509] = "shirt.tanktop",
            [2107229499] = "shoes.boots",
            [191795897] = "shotgun.double",
            [-1009492144] = "shotgun.pump",
            [2077983581] = "shotgun.waterpipe",
            [378365037] = "guntrap",
            [-529054135] = "shutter.metal.embrasure.a",
            [-529054134] = "shutter.metal.embrasure.b",
            [486166145] = "shutter.wood.a",
            [1628490888] = "sign.hanging.banner.large",
            [1498516223] = "sign.hanging",
            [-632459882] = "sign.hanging.ornate",
            [-626812403] = "sign.pictureframe.landscape",
            [385802761] = "sign.pictureframe.portrait",
            [2117976603] = "sign.pictureframe.tall",
            [1338515426] = "sign.pictureframe.xl",
            [-1455694274] = "sign.pictureframe.xxl",
            [1579245182] = "sign.pole.banner.large",
            [-587434450] = "sign.post.double",
            [-163742043] = "sign.post.single",
            [-1224714193] = "sign.post.town",
            [644359987] = "sign.post.town.roof",
            [-1962514734] = "sign.wooden.huge",
            [-705305612] = "sign.wooden.large",
            [-357728804] = "sign.wooden.medium",
            [-698499648] = "sign.wooden.small",
            [1213686767] = "weapon.mod.silencer",
            [386382445] = "weapon.mod.simplesight",
            [1859976884] = "skull_fire_pit",
            [960793436] = "skull.human",
            [1001265731] = "skull.wolf",
            [1253290621] = "sleepingbag",
            [470729623] = "small.oil.refinery",
            [1051155022] = "stash.small",
            [865679437] = "fish.troutsmall",
            [927253046] = "smallwaterbottle",
            [109552593] = "smg.2",
            [-2092529553] = "smgbody",
            [691633666] = "snowball",
            [-2055888649] = "snowman",
            [621575320] = "shotgun.spas12",
            [-2118132208] = "spear.stone",
            [-1127699509] = "spear.wooden",
            [-685265909] = "spikes.floor",
            [552706886] = "spinner.wheel",
            [1835797460] = "metalspring",
            [-892259869] = "sticks",
            [-1623330855] = "stocking.large",
            [-1616524891] = "stocking.small",
            [789892804] = "stone.pickaxe",
            [-1289478934] = "stonehatchet",
            [-892070738] = "stones",
            [-891243783] = "sulfur",
            [889398893] = "sulfur.ore",
            [-1625468793] = "supply.signal",
            [1293049486] = "surveycharge",
            [1369769822] = "fishtrap.small",
            [586484018] = "syringe.medical",
            [110115790] = "table",
            [1490499512] = "targeting.computer",
            [3552619] = "tarp",
            [1471284746] = "techparts",
            [456448245] = "smg.thompson",
            [110547964] = "torch",
            [1588977225] = "xmas.decoration.baubels",
            [918540912] = "xmas.decoration.candycanes",
            [-471874147] = "xmas.decoration.gingerbreadmen",
            [205978836] = "xmas.decoration.lights",
            [-1044400758] = "xmas.decoration.pinecone",
            [-2073307447] = "xmas.decoration.star",
            [435230680] = "xmas.decoration.tinsel",
            [-864578046] = "tshirt",
            [1660607208] = "tshirt.long",
            [260214178] = "tunalight",
            [-1847536522] = "vending.machine",
            [-496055048] = "wall.external.high.stone",
            [-1792066367] = "wall.external.high",
            [562888306] = "wall.frame.cell.gate",
            [-427925529] = "wall.frame.cell",
            [995306285] = "wall.frame.fence.gate",
            [-378017204] = "wall.frame.fence",
            [447918618] = "wall.frame.garagedoor",
            [313836902] = "wall.frame.netting",
            [1175970190] = "wall.frame.shopfront",
            [525244071] = "wall.frame.shopfront.metal",
            [-1021702157] = "wall.window.bars.metal",
            [-402507101] = "wall.window.bars.toptier",
            [-1556671423] = "wall.window.bars.wood",
            [61936445] = "wall.window.glass.reinforced",
            [112903447] = "water",
            [1817873886] = "water.catcher.large",
            [1824679850] = "water.catcher.small",
            [-1628526499] = "water.barrel",
            [547302405] = "waterjug",
            [1840561315] = "water.purifier",
            [-460592212] = "xmas.window.garland",
            [3655341] = "wood",
            [1554697726] = "wood.armor.jacket",
            [-1883959124] = "wood.armor.pants",
            [-481416622] = "workbench1",
            [-481416621] = "workbench2",
            [-481416620] = "workbench3",
            [-1151126752] = "xmas.lightstring",
            [-1926458555] = "xmas.tree"
        };

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Creator
        //////////////////////////////////////////////////////////////////////////////////////////

        static List<KitItem> GetPlayerItems(BasePlayer player)
        {
            List<KitItem> kititems = new List<KitItem>();
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "wear");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "main");
                    kititems.Add(iteminfo);
                }
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    var iteminfo = ProcessItem(item, "belt");
                    kititems.Add(iteminfo);
                }
            }
            return kititems;
        }
        static private KitItem ProcessItem(Item item, string container)
        {
            KitItem iItem = new KitItem();
            iItem.amount = item.amount;
            iItem.mods = new List<int>();
            iItem.container = container;
            iItem.skinid = item.skin;
            iItem.itemid = item.info.itemid;
            iItem.weapon = false;
            iItem.blueprintTarget = item.blueprintTarget;

            if (item.info.category.ToString() == "Weapon")
            {
                BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                if (weapon != null)
                {
                    if (weapon.primaryMagazine != null)
                    {
                        iItem.weapon = true;
                        if (item.contents != null)
                            foreach (var mod in item.contents.itemList)
                            {
                                if (mod.info.itemid != 0)
                                    iItem.mods.Add(mod.info.itemid);
                            }
                    }
                }
            }
            return iItem;
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Kit Redeemer
        //////////////////////////////////////////////////////////////////////////////////////////

        void TryGiveKit(BasePlayer player, string kitname)
        {
            var success = CanRedeemKit(player, kitname) as string;
            if (success != null)
            {
                OnScreen(player, success);
                return;
            }
            success = GiveKit(player, kitname) as string;
            if (success != null)
            {
                OnScreen(player, success);
                return;
            }
            OnScreen(player, GetMsg("KitRedeemed",player.userID));
            Interface.CallHook("OnKitRedeemed", player, kitname);
            proccessKitGiven(player, kitname);
        }
        void proccessKitGiven(BasePlayer player, string kitname)
        {
            if (string.IsNullOrEmpty(kitname)) return;
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return;

            var kitData = GetKitData(player.userID, kitname);
            if (kit.max > 0)
                kitData.max += 1;

            if (kit.cooldown > 0)
                kitData.cooldown = CurrentTime() + kit.cooldown;
            if (PlayerGUI.ContainsKey(player.userID) && PlayerGUI[player.userID].open)
                RefreshKitPanel(player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page);
        }

        private object GiveKit(BasePlayer player, string kitname)
        {
            if (string.IsNullOrEmpty(kitname)) return GetMsg("Emptykitname", player.userID);
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return GetMsg("NoKitFound",player.userID);

            foreach (var kitem in kit.items)
            {
                GiveItem(player.inventory,
                    kitem.weapon
                        ? BuildWeapon(kitem.itemid, kitem.skinid, kitem.mods)
                        : BuildItem(kitem.itemid, kitem.amount, kitem.skinid, kitem.blueprintTarget),
                    kitem.container == "belt"
                        ? player.inventory.containerBelt
                        : kitem.container == "wear"
                            ? player.inventory.containerWear
                            : player.inventory.containerMain);
            }
            if (!string.IsNullOrEmpty(kit.building))
            {
                var success = CopyPaste?.CallHook("TryPasteFromSteamID", player.userID, kit.building, CopyPasteParameters.ToArray());
                return success;
            }

            if (KitLogging) LogToFile("received", $"{player.displayName}<{player.UserIDString}> - Received Kit: {kitname}", this);
            return true;
        }

        bool GiveItem(PlayerInventory inv, Item item, ItemContainer container = null)
        {
            if (item == null) { return false; }
            int position = -1;
            return (((container != null) && item.MoveToContainer(container, position, true)) || (item.MoveToContainer(inv.containerMain, -1, true) || item.MoveToContainer(inv.containerBelt, -1, true)));
        }
        private Item BuildItem(int itemid, int amount, ulong skin, int blueprintTarget)
        {
            if (amount < 1) amount = 1;
            Item item = CreateByItemID(itemid, amount, skin);
            if (blueprintTarget != 0)
                item.blueprintTarget = blueprintTarget;
            return item;
        }
        private Item BuildWeapon(int id, ulong skin, List<int> mods)
        {
            Item item = CreateByItemID(id, 1, skin);
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents = (item.GetHeldEntity() as BaseProjectile).primaryMagazine.capacity;
            }
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    item.contents.AddItem(BuildItem(mod, 1, 0, 0).info, 1);
                }
            }
            return item;
        }

        private Item CreateByItemID(int itemID, int amount = 1, ulong skin = 0)
        {
            string shortName = "";
            if (_itemIdShortnameConversions.TryGetValue(itemID, out shortName))
            {
                return ItemManager.CreateByName(shortName, amount, skin);
            }
            else
            {
                return ItemManager.CreateByItemID(itemID, amount, skin);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////
        ///// Check Kits
        //////////////////////////////////////////////////////////////////////////////////////////

        bool isKit(string kitname)
        {
            return !string.IsNullOrEmpty(kitname) && storedData.Kits.ContainsKey(kitname.ToLower());
        }

        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() { return DateTime.UtcNow.Subtract(epoch).TotalSeconds; }

        bool CanSeeKit(BasePlayer player, string kitname, bool fromNPC, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrEmpty(kitname)) return false;
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return false;
            if (kit.hide)
                return false;
            if (kit.authlevel > 0)
                if (player.net.connection.authLevel < kit.authlevel)
                    return false;
            if (!string.IsNullOrEmpty(kit.permission))
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, kit.permission))
                    return false;
            if (kit.npconly && !fromNPC)
                return false;
            if (kit.max > 0)
            {
                int left = GetKitData(player.userID, kitname).max;
                if (left >= kit.max)
                {
                    reason += "- 0 left";
                    return false;
                }
                reason += $"- {(kit.max - left)} left";
            }
            if (kit.cooldown > 0)
            {
                double cd = GetKitData(player.userID, kitname).cooldown;
                double ct = CurrentTime();
                if (cd > ct && cd != 0.0)
                {
                    reason += $"- {Math.Abs(Math.Ceiling(cd - ct))} seconds";
                    return false;
                }
            }
            return true;
        }

        object CanRedeemKit(BasePlayer player, string kitname, bool skipAuth = false)
        {
            if (string.IsNullOrEmpty(kitname)) return GetMsg("Emptykitname", player.userID);
            kitname = kitname.ToLower();
            Kit kit;
            if (!storedData.Kits.TryGetValue(kitname, out kit)) return GetMsg("KitExistError", player.userID);

            object thereturn = Interface.Oxide.CallHook("canRedeemKit", player);
            if (thereturn != null)
            {
                if (thereturn is string) return thereturn;
                return GetMsg("CantRedeemNow", player.userID);
            }

            if (kit.authlevel > 0 && !skipAuth)
                if (player.net.connection.authLevel < kit.authlevel)
                    return GetMsg("NoAuthToRedeem", player.userID);

            if (!string.IsNullOrEmpty(kit.permission))
                if (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, kit.permission))
                    return GetMsg("NoPermKit", player.userID);

            var kitData = GetKitData(player.userID, kitname);
            if (kit.max > 0)
                if (kitData.max >= kit.max)
                    return GetMsg("NoRemainingUses", player.userID);

            if (kit.cooldown > 0)
            {
                var ct = CurrentTime();
                if (kitData.cooldown > ct && kitData.cooldown != 0.0)
                    return string.Format(GetMsg("CooldownMessage", player.userID), Math.Abs(Math.Ceiling(kitData.cooldown - ct)));
            }

            if (kit.npconly)
            {
                bool foundNPC = false;
                var neededNpc = new List<ulong>();
                foreach (var pair in GUIKits)
                {
                    if (pair.Value.kits.Contains(kitname))
                        neededNpc.Add(pair.Key);
                }
                foreach (var col in Physics.OverlapSphere(player.transform.position, 3f, playerLayer, QueryTriggerInteraction.Collide))
                {
                    var targetplayer = col.GetComponentInParent<BasePlayer>();
                    if (targetplayer == null) continue;

                    if (neededNpc.Contains(targetplayer.userID))
                    {
                        foundNPC = true;
                        break;
                    }
                }
                if (!foundNPC)
                    return GetMsg("NPCError", player.userID);
            }
            int beltcount = kit.items.Where(k => k.container == "belt").Count();
            int wearcount = kit.items.Where(k => k.container == "wear").Count();
            int maincount = kit.items.Where(k => k.container == "main").Count();
            int totalcount = beltcount + wearcount + maincount;
            if ((player.inventory.containerBelt.capacity - player.inventory.containerBelt.itemList.Count) < beltcount || (player.inventory.containerWear.capacity - player.inventory.containerWear.itemList.Count) < wearcount || (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count) < maincount)
                if (totalcount > (player.inventory.containerMain.capacity - player.inventory.containerMain.itemList.Count))
                    return GetMsg("NoInventorySpace", player.userID);
            return true;
        }


        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Class
        //////////////////////////////////////////////////////////////////////////////////////
        class KitItem
        {
            public int itemid;
            public string container;
            public int amount;
            public ulong skinid;
            public bool weapon;
            public int blueprintTarget;
            public List<int> mods = new List<int>();
        }

        class Kit
        {
            public string name;
            public string description;
            public int max;
            public double cooldown;
            public int authlevel;
            public bool hide;
            public bool npconly;
            public string permission;
            public string image;
            public string building;
            public List<KitItem> items = new List<KitItem>();
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Data Manager
        //////////////////////////////////////////////////////////////////////////////////////

        private void SaveKitsData()
        {
            if (kitsData == null) return;
            Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", kitsData);
        }


        private StoredData storedData;
        private Dictionary<ulong, Dictionary<string, KitData>> kitsData;

        class StoredData
        {
            public Dictionary<string, Kit> Kits = new Dictionary<string, Kit>();
        }
        class KitData
        {
            public int max;
            public double cooldown;
        }
        void ResetData()
        {
            kitsData.Clear();
            SaveKitsData();
        }

        void Unload()
        {
            SaveKitsData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAllGUI(player);
            }
        }
        void OnServerSave()
        {
            SaveKitsData();
        }

        void SaveKits()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Kits", storedData);
        }

        void LoadData()
        {
            var kits = Interface.Oxide.DataFileSystem.GetFile("Kits");
            try
            {
                kits.Settings.NullValueHandling = NullValueHandling.Ignore;
                storedData = kits.ReadObject<StoredData>();
                var update = new List<string>();
                foreach (var kit in storedData.Kits)
                {
                    if (!kit.Key.Equals(kit.Key.ToLower()))
                        update.Add(kit.Key);
                }
                foreach (var key in update)
                {
                    storedData.Kits[key.ToLower()] = storedData.Kits[key];
                    storedData.Kits.Remove(key);
                }
            }
            catch
            {
                storedData = new StoredData();
            }
            kits.Settings.NullValueHandling = NullValueHandling.Include;
        }

        KitData GetKitData(ulong userID, string kitname)
        {
            Dictionary<string, KitData> kitDatas;
            if (!kitsData.TryGetValue(userID, out kitDatas))
                kitsData[userID] = kitDatas = new Dictionary<string, KitData>();
            KitData kitData;
            if (!kitDatas.TryGetValue(kitname, out kitData))
                kitDatas[kitname] = kitData = new KitData();
            return kitData;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Kit Editor
        //////////////////////////////////////////////////////////////////////////////////////

        readonly Dictionary<ulong, string> kitEditor = new Dictionary<ulong, string>();

        //////////////////////////////////////////////////////////////////////////////////////
        // ImageLibrary Hooks ---->> Absolut
        //////////////////////////////////////////////////////////////////////////////////////

        private string TryForImage(string shortname, ulong skin = 99)
        {
            if (shortname.Contains("http")) return shortname;
            if (skin == 99) skin = (ulong)ResourceId;
            return GetImage(shortname, skin, true);
        }

        public string GetImage(string shortname, ulong skin = 0, bool returnUrl = false) => (string)ImageLibrary.Call("GetImage", shortname.ToLower(), skin, returnUrl);
        public bool HasImage(string shortname, ulong skin = 0) => (bool)ImageLibrary.Call("HasImage", shortname.ToLower(), skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname.ToLower(), skin);
        public List<ulong> GetImageList(string shortname) => (List<ulong>)ImageLibrary.Call("GetImageList", shortname.ToLower());
        public bool isReady() => (bool)ImageLibrary?.Call("IsReady");

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI CREATION ---->> Absolut
        //////////////////////////////////////////////////////////////////////////////////////

        private string PanelOnScreen = "PanelOnScreen";
        private string PanelKits = "PanelKits";
        private string PanelBackground = "PanelBackground";
        public class UI
        {
            static public CuiElementContainer CreateElementContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent,
                    panelName
                }
            };
                return NewElement;
            }
            static public CuiElementContainer CreateOverlayContainer(string panelName, string color, string aMin, string aMax, bool cursor = false)
            {
                var NewElement = new CuiElementContainer()
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = color},
                        RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                        CursorEnabled = cursor
                    },
                    new CuiElement().Parent = "Overlay",
                    panelName
                }
            };
                return NewElement;
            }

            static public void CreatePanel(ref CuiElementContainer container, string panel, string color, string aMin, string aMax, bool cursor = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    CursorEnabled = cursor
                },
                panel);
            }
            static public void CreateLabel(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { Color = color, FontSize = size, Align = align, FadeIn = 1.0f, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            static public void CreateButton(ref CuiElementContainer container, string panel, string color, string text, int size, string aMin, string aMax, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 1.0f },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            static public void LoadImage(ref CuiElementContainer container, string panel, string img, string aMin, string aMax)
            {
                if (img.StartsWith("http") || img.StartsWith("www"))
                {
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Url = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
                }
                else
                    container.Add(new CuiElement
                    {
                        Parent = panel,
                        Components =
                    {
                        new CuiRawImageComponent {Png = img, Sprite = "assets/content/textures/generic/fulltransparent.tga" },
                        new CuiRectTransformComponent {AnchorMin = aMin, AnchorMax = aMax }
                    }
                    });
            }

            static public void CreateTextOutline(ref CuiElementContainer element, string panel, string colorText, string colorOutline, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                element.Add(new CuiElement
                {
                    Parent = panel,
                    Components =
                    {
                        new CuiTextComponent{Color = colorText, FontSize = size, Align = align, Text = text },
                        new CuiOutlineComponent {Distance = "1 1", Color = colorOutline},
                        new CuiRectTransformComponent {AnchorMax = aMax, AnchorMin = aMin }
                    }
                });
            }
        }

        private Dictionary<string, string> UIColors = new Dictionary<string, string>
        {
            {"black", "0 0 0 1.0" },
            {"dark", "0.1 0.1 0.1 0.98" },
            {"header", "1 1 1 0.3" },
            {"light", ".564 .564 .564 1.0" },
            {"grey1", "0.6 0.6 0.6 1.0" },
            {"brown", "0.3 0.16 0.0 1.0" },
            {"yellow", "0.9 0.9 0.0 1.0" },
            {"orange", "1.0 0.65 0.0 1.0" },
            {"limegreen", "0.42 1.0 0 1.0" },
            {"blue", "0.2 0.6 1.0 1.0" },
            {"red", "1.0 0.1 0.1 1.0" },
            {"white", "1 1 1 1" },
            {"green", "0.28 0.82 0.28 1.0" },
            {"grey", "0.85 0.85 0.85 1.0" },
            {"lightblue", "0.6 0.86 1.0 1.0" },
            {"buttonbg", "0.2 0.2 0.2 0.7" },
            {"buttongreen", "0.133 0.965 0.133 0.9" },
            {"buttonred", "0.964 0.133 0.133 0.9" },
            {"buttongrey", "0.8 0.8 0.8 0.9" },
        };

        //////////////////////////////////////////////////////////////////////////////////////
        // GUI
        //////////////////////////////////////////////////////////////////////////////////////
        private Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        void OnScreen(BasePlayer player, string msg)
        {
            if (timers.ContainsKey(player.userID.ToString()))
            {
                timers[player.userID.ToString()].Destroy();
                timers.Remove(player.userID.ToString());
            }
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var element = UI.CreateOverlayContainer(PanelOnScreen, "0.0 0.0 0.0 0.0", "0.15 0.65", "0.85 .85", false);
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["black"], UIColors["white"], msg, 24, "0.0 0.0", "1.0 1.0");
            CuiHelper.AddUi(player, element);
            timers.Add(player.userID.ToString(), timer.Once(3, () => CuiHelper.DestroyUi(player, PanelOnScreen)));
        }

        void BackgroundPanel(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, PanelBackground);
            var element = UI.CreateOverlayContainer(PanelBackground, "0 0 0 0",".1 .15", ".9 .95", true);
            UI.CreatePanel(ref element, PanelBackground, "0.1 0.1 0.1 0.8", "0 0", "1 1");
            if (!string.IsNullOrEmpty(BackgroundURL))
            {
                var image = BackgroundURL;
                if (ImageLibrary)
                    image = TryForImage("Background");
                UI.LoadImage(ref element, PanelBackground, image, "0 0", "1 1");
            }
            CuiHelper.AddUi(player, element);
        }


        readonly Dictionary<ulong, PLayerGUI> PlayerGUI = new Dictionary<ulong, PLayerGUI>();

        class PLayerGUI
        {
            public ulong guiid;
            public int page;
            public bool open;
        }

        [ConsoleCommand("UI_ToggleKitMenu")]
        private void cmdUI_ToggleKitMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || string.IsNullOrEmpty(UIKeyBinding)) return;
            if (PlayerGUI.ContainsKey(player.userID))
                if (PlayerGUI[player.userID].open)
                {
                    PlayerGUI[player.userID].open = false;
                    DestroyAllGUI(player);
                    return;
                }
            NewKitPanel(player);
        }

        void NewKitPanel(BasePlayer player, ulong guiId = 0)
        {
            DestroyAllGUI(player);
            GUIKit kitpanel;
            if (!GUIKits.TryGetValue(guiId, out kitpanel)) return;
            BackgroundPanel(player);
            RefreshKitPanel(player, guiId);
        }

        void RefreshKitPanel(BasePlayer player, ulong guiId, int page = 0)
        {
            CuiHelper.DestroyUi(player, PanelKits);
            var element = UI.CreateOverlayContainer(PanelKits, "0 0 0 0", ".1 .15", ".9 .95");
            PLayerGUI playerGUI;
            if (!PlayerGUI.TryGetValue(player.userID, out playerGUI))
                PlayerGUI[player.userID] = playerGUI = new PLayerGUI();
            playerGUI.guiid = guiId;
            playerGUI.page = page;
            PlayerGUI[player.userID].open = true;
            bool npcCheck = false;
            if (guiId != 0)
                npcCheck = true;
            var kitpanel = GUIKits[guiId];
            List<string> Kits = new List<string>();
            if (ShowUnavailableKits)
                Kits = kitpanel.kits.Where(k=> storedData.Kits.ContainsKey(k.ToLower())).ToList();
            else
            {
                foreach (var entry in kitpanel.kits)
                {
                    string reason;
                    var cansee = CanSeeKit(player, entry.ToLower(), npcCheck, out reason);
                    if (!cansee && string.IsNullOrEmpty(reason)) continue;
                    Kits.Add(entry);
                }
            }
            int entriesallowed = 10;
            int remainingentries = Kits.Count - (page * entriesallowed);
            UI.CreateTextOutline(ref element, PanelKits, UIColors["white"], UIColors["black"], kitpanel.description, 20, "0.1 0.9", "0.9 1");
            var i = 0;
            var n = 0;
            int shownentries = page * entriesallowed;
            foreach (var entry in Kits)
            {
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    var pos = KitSquarePos(n, remainingentries);
                    CreateKitEntry(player, ref element, PanelKits, pos, entry);
                    n++;
                    if (n == entriesallowed) break;
                }
            }
            if (player.net.connection.authLevel == 2 || permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))
                UI.CreateButton(ref element, PanelKits, UIColors["buttongrey"], GetMsg("AddKit", player.userID), 14, $".02 .02", ".07 .06", $"UI_AddKit {0}");
            if (page >= 1)
            UI.CreateButton(ref element, PanelKits, UIColors["buttongrey"], "<<", 20, $".79 .02", ".84 .06", $"kit.show {page - 1}");
            if (remainingentries > entriesallowed)
            UI.CreateButton(ref element, PanelKits, UIColors["buttongrey"], ">>", 20, $".86 .02", ".91 .06", $"kit.show {page + 1}");
            UI.CreateButton(ref element, PanelKits, UIColors["buttonred"], GetMsg("Close", player.userID), 14, $".93 .02", ".98 .06", $"kit.close");
            CuiHelper.AddUi(player, element);
        }

        void CreateKitEntry(BasePlayer player, ref CuiElementContainer element, string panel, float[] pos, string entry)
        {
            Kit kit = storedData.Kits[entry.ToLower()];
            var kitData = GetKitData(player.userID, entry.ToLower());
            var image = kit.image ?? "http://i.imgur.com/xxQnE1R.png";
            if (ImageLibrary)
                image = TryForImage(kit.name.Replace(" ", ""));
            UI.CreatePanel(ref element, PanelKits, UIColors["header"], $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}");
            UI.LoadImage(ref element, PanelKits, image, $"{pos[2] - .06f} {pos[1] + 0.01f}", $"{pos[2] - .005f} {pos[1] + .1f}");
            UI.CreateLabel(ref element, PanelKits, UIColors["white"], kit.name, 12, $"{pos[0] + .005f} {pos[3] - .07f}", $"{pos[2] - .005f} {pos[3] - .01f}");
            UI.CreateLabel(ref element, PanelKits, UIColors["white"], kit.description ?? string.Empty, 12, $"{pos[0] + .005f} {pos[3] - .18f}", $"{pos[2] - .005f} {pos[3] - .07f}", TextAnchor.UpperLeft);
            UI.CreateLabel(ref element, PanelKits, UIColors["white"], string.Format(GetMsg("KitCooldown", player.userID), kit.cooldown <= 0 ? GetMsg("None", player.userID) : CurrentTime() > kitData.cooldown ? MinuteFormat(kit.cooldown / 60).ToString() : "<color=red> -" + MinuteFormat((double)Math.Abs(Math.Ceiling(CurrentTime() - kitData.cooldown)) / 60) + "</color>"), 12, $"{pos[0] + .005f} {pos[3] - .24f}", $"{pos[2] - .005f} {pos[3] - .21f}", TextAnchor.MiddleLeft);
            UI.CreateLabel(ref element, PanelKits, UIColors["white"], string.Format(GetMsg("KitUses", player.userID), kit.max <= 0 ? GetMsg("Unlimited") : kit.max - kitData.max < kit.max ? $"<color=red>{kit.max - kitData.max }</color>/{kit.max}" : $"{kit.max - kitData.max }/{kit.max}"), 12, $"{pos[0] + .005f} {pos[3] - .27f}", $"{pos[2] - .005f} {pos[3] - .24f}", TextAnchor.MiddleLeft);
            if (player.net.connection.authLevel == 2 || permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))
                UI.CreateButton(ref element, PanelKits, UIColors["buttonred"], GetMsg("RemoveKit", player.userID), 14, $"{pos[0] + .067f} {pos[1] + 0.01f}", $"{pos[0] + .122f} {pos[1] + .07f}", $"UI_RemoveKit {entry.ToLower()}");
            if (!ShowUnavailableKits)
                UI.CreateButton(ref element, PanelKits, UIColors["dark"], GetMsg("Redeem"), 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}", $"kit.gui {entry.ToLower()}");
            else
            {
                string reason;
                var cansee = CanSeeKit(player, entry.ToLower(), PlayerGUI[player.userID].guiid == 0 ? false : true, out reason);
                if (!cansee && string.IsNullOrEmpty(reason))
                {
                    UI.CreatePanel(ref element, PanelKits, UIColors["header"], $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}");
                    UI.CreateLabel(ref element, PanelKits, UIColors["white"],"<color=red>"+GetMsg("Unavailable", player.userID)+"</color>", 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}");
                }
                else
                    UI.CreateButton(ref element, PanelKits, UIColors["dark"], GetMsg("Redeem"), 12, $"{pos[0] + .005f} {pos[1] + 0.01f}", $"{pos[0] + .06f} {pos[1] + .07f}", $"kit.gui {entry.ToLower()}");
            }
        }

        private float[] KitSquarePos(int number, double count)
        {
            Vector2 position = new Vector2(0.015f, 0.5f);
            Vector2 dimensions = new Vector2(0.19f, 0.4f);
            float offsetY = 0;
            float offsetX = 0;
            if (count < 10)
            {
                position.x = (float)(1 - (((dimensions.x + .005f) * (count > 1 ? (Math.Round((count / 2), MidpointRounding.AwayFromZero)) : 1)))) / 2;
            }
            if (number >= 0 && number < 2)
            {
                offsetY = (-0.01f - dimensions.y) * number;
            }
            if (number > 1 && number < 4)
            {
                offsetX = (.005f + dimensions.x) * 1;
                offsetY = (-0.01f - dimensions.y) * (number - 2);
            }
            if (number > 3 && number < 6)
            {
                offsetX = (.005f + dimensions.x) * 2;
                offsetY = (-0.01f - dimensions.y) * (number - 4);
            }
            if (number > 5 && number < 8)
            {
                offsetX = (.005f + dimensions.x) * 3;
                offsetY = (-0.01f - dimensions.y) * (number - 6);
            }
            if (number > 7 && number < 10)
            {
                offsetX = (.005f + dimensions.x) * 4;
                offsetY = (-0.01f - dimensions.y) * (number - 8);
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }



        [ConsoleCommand("UI_RemoveKit")]
        private void cmdUI_RemoveKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))) return;
            CuiHelper.DestroyUi(player, PanelOnScreen);
            var kit = string.Join(" ", arg.Args);
            if (GUIKits[PlayerGUI[player.userID].guiid].kits.Contains(kit))
            {
                GUIKits[PlayerGUI[player.userID].guiid].kits.Remove(kit);
                var config = Config.ReadObject<Dictionary<string, object>>();
                config["NPC - GUI Kits"] = GUIKits;
                Config.WriteObject(config);
                CuiHelper.DestroyUi(player, PanelOnScreen);
                RefreshKitPanel(player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page);
            }
        }

        [ConsoleCommand("UI_AddKit")]
        private void cmdUI_AddKit(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null || (player.net.connection.authLevel < 2 && !permission.UserHasPermission(player.UserIDString, this.Title + ".admin"))) return;
            CuiHelper.DestroyUi(player, PanelOnScreen);
            int page;
            if (!int.TryParse(arg.Args[0], out page))
                if (arg.Args[0] == "close")
                    return;
                else
                {
                    GUIKits[PlayerGUI[player.userID].guiid].kits.Add(string.Join(" ",arg.Args));
                    var config = Config.ReadObject<Dictionary<string, object>>();
                    config["NPC - GUI Kits"] = GUIKits;
                    Config.WriteObject(config);
                    CuiHelper.DestroyUi(player, PanelOnScreen);
                    RefreshKitPanel(player, PlayerGUI[player.userID].guiid, PlayerGUI[player.userID].page);
                    return;
                }
            double count = GetAllKits().Count() - GUIKits[PlayerGUI[player.userID].guiid].kits.Where(k => storedData.Kits.ContainsKey(k.ToLower())).ToList().Count();
            if (count == 0)
            {
                SendReply(player, GetMsg("NoKitsFound", player.userID));
                return;
            }
            var element = UI.CreateOverlayContainer(PanelOnScreen, UIColors["dark"], ".1 .15", ".9 .95");
            UI.CreateTextOutline(ref element, PanelOnScreen, UIColors["white"], UIColors["black"], GetMsg("AddKitToMenu", player.userID), 20, "0.1 0.9", "0.9 .99", TextAnchor.UpperCenter);
            int entriesallowed = 30;
            double remainingentries = count - (page * (entriesallowed));
            double totalpages = (Math.Floor(count / (entriesallowed)));
            {
                if (page < totalpages - 1)
                {
                    UI.CreateButton(ref element, PanelOnScreen, UIColors["buttongrey"], GetMsg("Last", player.userID), 16, "0.8 0.02", "0.85 0.06", $"UI_AddKit {totalpages}");
                }
                if (remainingentries > entriesallowed)
                {
                    UI.CreateButton(ref element, PanelOnScreen, UIColors["buttongrey"], GetMsg("Next", player.userID), 16, "0.74 0.02", "0.79 0.06", $"UI_AddKit {page + 1}");
                }
                if (page > 0)
                {
                    UI.CreateButton(ref element, PanelOnScreen, UIColors["buttongrey"], GetMsg("Back", player.userID), 16, "0.68 0.02", "0.73 0.06", $"UI_AddKit {page - 1}");
                }
                if (page > 1)
                {
                    UI.CreateButton(ref element, PanelOnScreen, UIColors["buttongrey"], GetMsg("First", player.userID), 16, "0.62 0.02", "0.67 0.06", $"UI_AddKit {0}");
                }
            }
            var i = 0;
            int n = 0;
            double shownentries = page * entriesallowed;
            foreach (string kitname in GetAllKits().Where(k => !GUIKits[PlayerGUI[player.userID].guiid].kits.Contains(k)).OrderBy(k=>k))
            {
                i++;
                if (i < shownentries + 1) continue;
                else if (i <= shownentries + entriesallowed)
                {
                    CreateKitButton(ref element, PanelOnScreen, UIColors["header"], kitname, $"UI_AddKit {kitname}", n);
                    n++;
                }
            }
            UI.CreateButton(ref element, PanelOnScreen, UIColors["buttonred"], GetMsg("Close", player.userID), 14, $".93 .02", ".98 .06", $"UI_AddKit close");
            CuiHelper.AddUi(player, element);
        }

        private void CreateKitButton(ref CuiElementContainer container, string panelName, string color, string name, string cmd, int num)
        {
            var pos = CalcKitButtonPos(num);
            UI.CreateButton(ref container, panelName, color, name, 14, $"{pos[0]} {pos[1]}", $"{pos[2]} {pos[3]}", cmd);
        }

        private float[] CalcKitButtonPos(int number)
        {
            Vector2 position = new Vector2(0.05f, 0.82f);
            Vector2 dimensions = new Vector2(0.125f, 0.125f);
            float offsetY = 0;
            float offsetX = 0;
            if (number >= 0 && number < 6)
            {
                offsetX = (0.03f + dimensions.x) * number;
            }
            if (number > 5 && number < 12)
            {
                offsetX = (0.03f + dimensions.x) * (number - 6);
                offsetY = (-0.06f - dimensions.y) * 1;
            }
            if (number > 11 && number < 18)
            {
                offsetX = (0.03f + dimensions.x) * (number - 12);
                offsetY = (-0.06f - dimensions.y) * 2;
            }
            if (number > 17 && number < 24)
            {
                offsetX = (0.03f + dimensions.x) * (number - 18);
                offsetY = (-0.06f - dimensions.y) * 3;
            }
            if (number > 23 && number < 36)
            {
                offsetX = (0.03f + dimensions.x) * (number - 24);
                offsetY = (-0.06f - dimensions.y) * 4;
            }
            Vector2 offset = new Vector2(offsetX, offsetY);
            Vector2 posMin = position + offset;
            Vector2 posMax = posMin + dimensions;
            return new float[] { posMin.x, posMin.y, posMax.x, posMax.y };
        }

        private string MinuteFormat(double minutes)
        {
            TimeSpan dateDifference = TimeSpan.FromMinutes(minutes);
            var hours = dateDifference.Hours;
            hours += (dateDifference.Days * 24);
            return string.Format("{0:00}:{1:00}:{2:00}", hours, dateDifference.Minutes, dateDifference.Seconds);
        }

        void DestroyAllGUI(BasePlayer player) { CuiHelper.DestroyUi(player, "KitOverlay"); CuiHelper.DestroyUi(player, PanelOnScreen); CuiHelper.DestroyUi(player, PanelKits); CuiHelper.DestroyUi(player, PanelBackground); }
        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (!GUIKits.ContainsKey(npc.userID)) return;
            NewKitPanel(player, npc.userID);
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // External Hooks
        //////////////////////////////////////////////////////////////////////////////////////
        [HookMethod("GetAllKits")]
        public string[] GetAllKits() => storedData.Kits.Keys.ToArray();

        [HookMethod("GetKitInfo")]
        public object GetKitInfo(string kitname)
        {
            if (storedData.Kits.ContainsKey(kitname.ToLower()))
            {
                var kit = storedData.Kits[kitname.ToLower()];
                JObject obj = new JObject();
                obj["name"] = kit.name;
                obj["permission"] = kit.permission;
                obj["npconly"] = kit.npconly;
                obj["max"] = kit.max;
                obj["image"] = kit.image;
                obj["hide"] = kit.hide;
                obj["description"] = kit.description;
                obj["cooldown"] = kit.cooldown;
                obj["building"] = kit.building;
                obj["authlevel"] = kit.authlevel;
                JArray items = new JArray();
                foreach(var itemEntry in kit.items)
                {
                    JObject item = new JObject();
                    item["amount"] = itemEntry.amount;
                    item["container"] = itemEntry.container;
                    item["itemid"] = itemEntry.itemid;
                    item["skinid"] = itemEntry.skinid;
                    item["weapon"] = itemEntry.weapon;
                    item["blueprint"] = itemEntry.blueprintTarget > 0;
                    JArray mods = new JArray();
                    foreach (var mod in itemEntry.mods)
                        mods.Add(mod);
                    item["mods"] = mods;
                    items.Add(item);
                }
                obj["items"] = items;
                return obj;
            }
            return null;
        }

        [HookMethod("GetKitContents")]
        public string[] GetKitContents(string kitname)
        {
            if (storedData.Kits.ContainsKey(kitname.ToLower()))
            {
                List<string> items = new List<string>();
                foreach (var item in storedData.Kits[kitname.ToLower()].items)
                {
                    var itemstring = $"{item.itemid}_{item.amount}";
                    if (item.mods.Count > 0)
                        foreach (var mod in item.mods)
                            itemstring = itemstring + $"_{mod}";
                    items.Add(itemstring);
                }
                if (items.Count > 0)
                    return items.ToArray();
            }
            return null;
        }

        [HookMethod("KitCooldown")]
        public double KitCooldown(string kitname) => storedData.Kits[kitname].cooldown;

        [HookMethod("PlayerKitCooldown")]
        public double PlayerKitCooldown(ulong ID, string kitname) => storedData.Kits[kitname].cooldown <= 0 ? 0 : CurrentTime() > GetKitData(ID, kitname).cooldown ? storedData.Kits[kitname].cooldown : CurrentTime() - GetKitData(ID, kitname).cooldown;

        [HookMethod("KitDescription")]
        public string KitDescription(string kitname) => storedData.Kits[kitname].description;

        [HookMethod("KitMax")]
        public int KitMax(string kitname) => storedData.Kits[kitname].max;

        [HookMethod("PlayerKitMax")]
        public double PlayerKitMax(ulong ID, string kitname) => storedData.Kits[kitname].max <= 0 ? 0 : storedData.Kits[kitname].max - GetKitData(ID, kitname).max < storedData.Kits[kitname].max ? storedData.Kits[kitname].max - GetKitData(ID, kitname).max : 0;

        [HookMethod("KitImage")]
        public string KitImage(string kitname) => storedData.Kits[kitname].image;

        //////////////////////////////////////////////////////////////////////////////////////
        // Console Command
        //////////////////////////////////////////////////////////////////////////////////////
        [ConsoleCommand("kit.gui")]
        void cmdConsoleKitGui(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if (!arg.HasArgs())
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }
            var player = arg.Player();
            var kitname = arg.Args[0]/*.Substring(1, arg.Args[0].Length - 2)*/;
            TryGiveKit(player, kitname);
        }

        [ConsoleCommand("kit.close")]
        void cmdConsoleKitClose(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            PlayerGUI[arg.Player().userID].open = false;
            DestroyAllGUI(arg.Player());
        }

        [ConsoleCommand("kit.show")]
        void cmdConsoleKitShow(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            {
                SendReply(arg, "You can't use this command from the server console");
                return;
            }
            if (!arg.HasArgs())
            {
                SendReply(arg, "You are not allowed to use manually this command");
                return;
            }

            var player = arg.Player();
            PLayerGUI playerGUI;
            if (!PlayerGUI.TryGetValue(player.userID, out playerGUI)) return;
            RefreshKitPanel(player, playerGUI.guiid, arg.GetInt(0));
        }

        List<BasePlayer> FindPlayer(string arg)
        {
            var listPlayers = new List<BasePlayer>();

            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (steamid != 0L)
                    if (player.userID == steamid)
                    {
                        listPlayers.Clear();
                        listPlayers.Add(player);
                        return listPlayers;
                    }
                string lowername = player.displayName.ToLower();
                if (lowername.Contains(lowerarg))
                {
                    listPlayers.Add(player);
                }
            }
            return listPlayers;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        // Chat Command
        //////////////////////////////////////////////////////////////////////////////////////

        bool hasAccess(BasePlayer player)
        {
            if (player?.net?.connection?.authLevel > 1 || permission.UserHasPermission(player.UserIDString, this.Title +".admin"))
                return true;
            return false;
        }
        void SendListKitEdition(BasePlayer player)
        {
            SendReply(player, "authlevel XXX\r\nbuilding \"filename\" => buy a building to paste from\r\ncooldown XXX\r\ndescription \"description text here\" => set a description for this kit\r\nhide TRUE/FALSE => dont show this kit in lists (EVER)\r\nimage \"image http url\" => set an image for this kit (gui only)\r\nitems => set new items for your kit (will copy your inventory)\r\nmax XXX\r\nnpconly TRUE/FALSE => only get this kit out of a NPC\r\npermission \"permission name\" => set the permission needed to get this kit");
        }
        [ChatCommand("kit")]
        void cmdChatKit(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                if (GUIKits.ContainsKey(0))
                    NewKitPanel(player, 0);
                else
                {
                    string reason = string.Empty;
                    foreach (var pair in storedData.Kits)
                    {
                        var cansee = CanSeeKit(player, pair.Key, false, out reason);
                        if (!cansee && string.IsNullOrEmpty(reason)) continue;
                        SendReply(player, $"{pair.Value.name} - {pair.Value.description} {reason}");
                    }
                }
                return;
            }
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "help":
                        SendReply(player, "====== Player Commands ======");
                        SendReply(player, "/kit => to get the list of kits");
                        SendReply(player, "/kit KITNAME => to redeem the kit");
                        if (!hasAccess(player)) { return; }
                        SendReply(player, "====== Admin Commands ======");
                        SendReply(player, "/kit add KITNAME => add a kit");
                        SendReply(player, "/kit remove KITNAME => remove a kit");
                        SendReply(player, "/kit edit KITNAME => edit a kit");
                        SendReply(player, "/kit list => get a raw list of kits (the real full list)");
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME => give a kit to a player");
                        SendReply(player, "/kit resetkits => deletes all kits");
                        SendReply(player, "/kit resetdata => reset player data");
                        break;
                    case "add":
                    case "remove":
                    case "edit":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        SendReply(player, $"/kit {args[0]} KITNAME");
                        break;
                    case "give":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME");
                        break;
                    case "list":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        foreach (var kit in storedData.Kits.Values)
                        {
                            SendReply(player, $"{kit.name} - {kit.description}");
                        }
                        break;
                    case "items":
                        break;
                    case "resetkits":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        storedData.Kits.Clear();
                        kitEditor.Clear();
                        ResetData();
                        SaveKits();
                        SendReply(player, "Resetted all kits and player data");
                        break;
                    case "resetdata":
                        if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }
                        ResetData();
                        SendReply(player, "Resetted all player data");
                        break;
                    default:
                        TryGiveKit(player, args[0].ToLower());
                        break;
                }
                if (args[0] != "items")
                    return;

            }
            if (!hasAccess(player)) { SendReply(player, "You don't have access to this command"); return; }

            string kitname;
            switch (args[0])
            {
                case "add":
                    kitname = args[1].ToLower();
                    if (storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit already exists.");
                        return;
                    }
                    storedData.Kits[kitname] = new Kit { name = args[1] };
                    kitEditor[player.userID] = kitname;
                    SendReply(player, "You've created a new kit: " + args[1]);
                    SendListKitEdition(player);
                    break;
                case "give":
                    if (args.Length < 3)
                    {
                        SendReply(player, "/kit give PLAYER/STEAMID KITNAME");
                        return;
                    }
                    kitname = args[2].ToLower();
                    if (!storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist.");
                        return;
                    }
                    var findPlayers = FindPlayer(args[1]);
                    if (findPlayers.Count == 0)
                    {
                        SendReply(player, "No players found.");
                        return;
                    }
                    if (findPlayers.Count > 1)
                    {
                        SendReply(player, "Multiple players found.");
                        return;
                    }
                    GiveKit(findPlayers[0], kitname);
                    SendReply(player, $"You gave {findPlayers[0].displayName} the kit: {storedData.Kits[kitname].name}");
                    SendReply(findPlayers[0], string.Format("You've received the kit {1} from {0}", player.displayName, storedData.Kits[kitname].name));
                    break;
                case "edit":
                    kitname = args[1].ToLower();
                    if (!storedData.Kits.ContainsKey(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist");
                        return;
                    }
                    kitEditor[player.userID] = kitname;
                    SendReply(player, $"You are now editing the kit: {kitname}");
                    SendListKitEdition(player);
                    break;
                case "remove":
                    kitname = args[1].ToLower();
                    if (!storedData.Kits.Remove(kitname))
                    {
                        SendReply(player, "This kit doesn't seem to exist");
                        return;
                    }
                    SendReply(player, $"{kitname} was removed");
                    if (kitEditor[player.userID] == kitname) kitEditor.Remove(player.userID);
                    break;
                default:
                    if (!kitEditor.TryGetValue(player.userID, out kitname))
                    {
                        SendReply(player, "You are not creating or editing a kit");
                        return;
                    }
                    Kit kit;
                    if (!storedData.Kits.TryGetValue(kitname, out kit))
                    {
                        SendReply(player, "There was an error while getting this kit, was it changed while you were editing it?");
                        return;
                    }
                    for (var i = 0; i < args.Length; i++)
                    {
                        object editvalue;
                        var key = args[i].ToLower();
                        switch (key)
                        {
                            case "items":
                                kit.items = GetPlayerItems(player);
                                SendReply(player, "The items were copied from your inventory");
                                continue;
                            case "building":
                                var buildingvalue = args[++i];
                                if (buildingvalue.ToLower() == "false")
                                    editvalue = kit.building = string.Empty;
                                else
                                    editvalue = kit.building = buildingvalue;
                                break;
                            case "name":
                                continue;
                            case "description":
                                editvalue = kit.description = args[++i];
                                break;
                            case "max":
                                editvalue = kit.max = int.Parse(args[++i]);
                                break;
                            case "cooldown":
                                editvalue = kit.cooldown = double.Parse(args[++i]);
                                break;
                            case "authlevel":
                                editvalue = kit.authlevel = int.Parse(args[++i]);
                                break;
                            case "hide":
                                editvalue = kit.hide = bool.Parse(args[++i]);
                                break;
                            case "npconly":
                                editvalue = kit.npconly = bool.Parse(args[++i]);
                                break;
                            case "permission":
                                editvalue = kit.permission = args[++i];
                                if (!kit.permission.StartsWith("kits."))
                                    editvalue = kit.permission = $"kits.{kit.permission}";
                                InitializePermissions();
                                break;
                            case "image":
                                editvalue = kit.image = args[++i];
                                break;
                            default:
                                SendReply(player, $"{args[i]} is not a valid argument");
                                continue;
                        }
                        SendReply(player, $"{key} set to {editvalue ?? "null"}");
                    }
                    break;
            }
            SaveKits();
        }
    }
}
