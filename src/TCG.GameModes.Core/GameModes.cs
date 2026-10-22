using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using static TCG.Shared.Lang;
namespace TCG.GameModes.Core
{
    public enum GameMode { Classic, CardsOnly, Custom }
    public enum ProductCategory { Boosters, BoosterBoxes, Figurines, Dice, Playmats, Decks, Sleeves, Binders, DeckBoxes, BoardGames, Manga, Other, Hygiene, BulkCards }
    public sealed class ProductPermission
    {
        public ProductCategory Category { get; set; }
        public bool Buy { get; set; } = true;
        public bool Sell { get; set; } = true;
    }
    public sealed class EditionPermission
    {
        public string Id { get; set; } = "";
        public bool Buy { get; set; } = true;
        public bool Sell { get; set; } = true;
    }
    public sealed class ModeSettings
    {
        public int SchemaVersion { get; set; } = 1;
        public string CampaignId { get; set; } = "";
        public GameMode Mode { get; set; }
        public string PresetName { get; set; } = "";
        public bool AllowNativeProducts { get; set; } = true;
        public bool AllowNewContent { get; set; } = true;
        public List<EditionPermission> Contents { get; set; } = new List<EditionPermission>();
        public List<ProductPermission> NativeProductExceptions { get; set; } = new List<ProductPermission>();
        public bool AllowModProducts { get; set; } = true;
        public bool AllowSingleCardSales { get; set; } = true;
        public bool AllowProductSales { get; set; } = true;
        public bool AllowBoosterPurchases { get; set; } = true;
        public bool AllowAccessoryPurchases { get; set; } = true;
        public bool UnlockCardTablesAtStart { get; set; }
        public bool AllowEmployees { get; set; } = true;
        public bool AllowTableGames { get; set; } = true;
        public bool AllowCardTrading { get; set; } = true;
        public bool AllowTournaments { get; set; } = true;
        public List<ProductPermission> Products { get; set; } = new List<ProductPermission>();
        public List<EditionPermission> Editions { get; set; } = new List<EditionPermission>();
        // 0.2.0 : shop supplies (deodorant and other spray cans) stay purchasable whatever the product rules.
        public bool AllowShopSupplies { get; set; } = true;
        // 0.2.0 : editions whose single cards customers do not buy. Absent edition = sold.
        public List<string> UnsellableCardEditions { get; set; } = new List<string>();
        // 0.3.0 : per-product choices (Id = item type) that take precedence over the category and edition rules.
        public List<EditionPermission> ItemRules { get; set; } = new List<EditionPermission>();
        // 0.3.0 : furniture that cannot be bought in this shop (Id = object type).
        public List<string> LockedFurniture { get; set; } = new List<string>();
        public static ModeSettings Preset(string id, GameMode mode)
        {
            if (!Enum.IsDefined(typeof(GameMode), mode)) throw new ArgumentException(T("Mode inconnu."));
            return new ModeSettings { CampaignId=id, Mode=mode, AllowProductSales=mode!=GameMode.CardsOnly, AllowAccessoryPurchases=mode!=GameMode.CardsOnly, UnlockCardTablesAtStart=mode==GameMode.CardsOnly };
        }
    }
    public static class ModeRules
    {
        private sealed class ItemDefinition
        {
            public ProductCategory Category;
            public string Edition="";
            public string SpawnedPack="";
            public string Source="";
        }
        private static readonly Dictionary<string,ItemDefinition> Definitions=new Dictionary<string,ItemDefinition>(StringComparer.Ordinal);
        public static readonly Dictionary<string,string> ContentNames=new Dictionary<string,string>(StringComparer.Ordinal);
        public static readonly Dictionary<string,string> EditionNames=new Dictionary<string,string>(StringComparer.Ordinal);
        public static void RegisterItem(string item,ProductCategory category,string edition="",string editionName="",string spawnedPack="",string source="",string sourceName="")
        {
            if(string.IsNullOrWhiteSpace(item))return;
            if(source=="" && Definitions.TryGetValue(item,out var previous))source=previous.Source;
            Definitions[item]=new ItemDefinition{Category=category,Edition=edition,SpawnedPack=spawnedPack,Source=source};
            if(source!="")ContentNames[source]=sourceName!=""?sourceName:ContentNames.TryGetValue(source,out var label)?label:source;
            if(edition!="")EditionNames[edition]=string.IsNullOrWhiteSpace(editionName)?edition:editionName;
        }
        public static bool PreserveRegisteredItem(string runtimeItem)
        {
            if(Definitions.ContainsKey(runtimeItem))return true;
            string Key(string name)=>new string(name.Where(char.IsLetterOrDigit).ToArray());
            var matches=Definitions.Where(d=>string.Equals(Key(d.Key),Key(runtimeItem),StringComparison.Ordinal)).ToArray();
            if(matches.Length!=1)return false;
            Definitions[runtimeItem]=matches[0].Value;return true;
        }
        private static string ItemEdition(string item,int depth=0)
        {
            if(depth>8)return "";
            if(Definitions.TryGetValue(item,out var definition))return definition.Edition!=""?definition.Edition:definition.SpawnedPack!=""?ItemEdition(definition.SpawnedPack,depth+1):"";
            if(!Boosters.Contains(item))return "";
            return item.StartsWith("Destiny",StringComparison.Ordinal)?"native:Destiny":
                item.StartsWith("Basic",StringComparison.Ordinal) || item.StartsWith("Rare",StringComparison.Ordinal) || item.StartsWith("Epic",StringComparison.Ordinal) || item.StartsWith("Legendary",StringComparison.Ordinal)?"native:Tetramon":"native:"+item;
        }
        public static void RegisterNativeEditions()
        {
            EditionNames["native:Tetramon"]=T("Jeu d’origine — Tetramon");
            EditionNames["native:Destiny"]=T("Jeu d’origine — Tetramon Destiny");
        }

        private static bool OriginAllowed(ModeSettings settings,string item,bool buy)
        {
            if(!Definitions.TryGetValue(item,out var definition))
            {
                if(settings.AllowNativeProducts)return true;
                var exception=settings.NativeProductExceptions.FirstOrDefault(p=>p.Category==Classify(item));
                return exception!=null && (buy?exception.Buy:exception.Sell);
            }
            if(!settings.AllowModProducts)return false;
            var rule=settings.Contents.FirstOrDefault(c=>c.Id==definition.Source);
            return rule==null?settings.AllowNewContent:(buy?rule.Buy:rule.Sell);
        }
        private static bool EditionAllowed(ModeSettings settings,string item,bool buy)
        {
            var category=Classify(item);
            if(category!=ProductCategory.Boosters && category!=ProductCategory.BoosterBoxes)return true;
            var rule=settings.Editions.FirstOrDefault(e=>e.Id==ItemEdition(item));
            return rule==null?settings.AllowNewContent:(buy?rule.Buy:rule.Sell);
        }
        public static string CategoryName(ProductCategory category) => new[]{T("Boosters"), T("Boîtes de boosters"), T("Figurines"), T("Dés"), T("Tapis de jeu"), T("Decks préconstruits"), T("Pochettes et protections"), T("Albums et classeurs"), T("Boîtes de rangement"), T("Jeux de société"), T("Mangas"), T("Autres produits"), T("Hygiène"), T("Cartes en vrac")}[(int)category];
        public static ProductCategory Classify(string item)
        {
            if(Definitions.TryGetValue(item,out var definition))return definition.Category;
            if(item=="Deodorant")return ProductCategory.Hygiene;
            if(item.StartsWith("BulkBox_",StringComparison.Ordinal))return ProductCategory.BulkCards;
            if(Boosters.Contains(item))return item.EndsWith("CardBox",StringComparison.Ordinal)?ProductCategory.BoosterBoxes:ProductCategory.Boosters;
            if(item.StartsWith("Toy_",StringComparison.Ordinal))return ProductCategory.Figurines;
            if(item.StartsWith("D20DiceBox",StringComparison.Ordinal))return ProductCategory.Dice;
            if(item.StartsWith("Playmat",StringComparison.OrdinalIgnoreCase) || item.StartsWith("UP_Playmat",StringComparison.Ordinal))return ProductCategory.Playmats;
            if(item.StartsWith("PreconDeck_",StringComparison.Ordinal))return ProductCategory.Decks;
            if(item.StartsWith("CardSleeve_",StringComparison.Ordinal) || new[]{"UP_MagneticHolder","UP_CardPreserver","UP_SemiRigidCardHolder","UP_Toploader","UP_PennySleeves"}.Contains(item))return ProductCategory.Sleeves;
            if(item.StartsWith("BinderBook",StringComparison.Ordinal) || item=="UP_Album" || item=="UP_PlatinumSeriesPocketPages")return ProductCategory.Binders;
            if(item.StartsWith("DeckBox",StringComparison.Ordinal) || item=="UP_TowerDeckbox")return ProductCategory.DeckBoxes;
            if(item.StartsWith("Boardgame_",StringComparison.Ordinal))return ProductCategory.BoardGames;
            if(item.StartsWith("Manga",StringComparison.Ordinal))return ProductCategory.Manga;
            return ProductCategory.Other;
        }
        public static void AddCategories(ModeSettings settings)
        {
            foreach(ProductCategory category in Enum.GetValues(typeof(ProductCategory)))
                if(!settings.Products.Any(p=>p.Category==category))
                {
                    var previous=settings.Products.FirstOrDefault(p=>p.Category==ProductCategory.Other);
                    var split=category==ProductCategory.Hygiene || category==ProductCategory.BulkCards;
                    settings.Products.Add(new ProductPermission{Category=category,Buy=!split || previous?.Buy!=false,Sell=!split || previous?.Sell!=false});
                }
        }
        private static bool CategoryAllowed(ModeSettings settings,string item,bool buy)
        {
            var category=Classify(item);
            var rule=settings.Products.FirstOrDefault(p=>p.Category==category);
            if(rule==null && (category==ProductCategory.Hygiene || category==ProductCategory.BulkCards))rule=settings.Products.FirstOrDefault(p=>p.Category==ProductCategory.Other);
            return rule==null || (buy?rule.Buy:rule.Sell);
        }
        // Audited EItemType values from game 1.02. Unknown/new values require review.
        private static readonly HashSet<string> Boosters = new HashSet<string>(new[] {
            "BasicCardPack","BasicCardBox","RareCardPack","RareCardBox","EpicCardPack","EpicCardBox","LegendaryCardPack","LegendaryCardBox",
            "DestinyBasicCardPack","DestinyBasicCardBox","DestinyRareCardPack","DestinyRareCardBox","DestinyEpicCardPack","DestinyEpicCardBox","DestinyLegendaryCardPack","DestinyLegendaryCardBox",
            "GhostPack","MegabotPack","FantasyRPGPack","CatJobPack","FoodieGOPack","FoodieGOBWPack","FoodieGOJPPack","AscensionCardPack" }, StringComparer.Ordinal);
        // Edition identifier of a runtime expansion name (binder pages, single cards).
        public static string EditionOf(string expansion)
        {
            string Key(string name)=>new string(name.Where(char.IsLetterOrDigit).ToArray());
            var edition=expansion=="Tetramon"?"native:Tetramon":expansion=="Destiny"?"native:Destiny":"";
            if(edition=="")edition=EditionNames.Keys.FirstOrDefault(id=>id.StartsWith("epl:",StringComparison.Ordinal) && Key(id.Substring(4))==Key(expansion))??"native:"+expansion;
            return edition;
        }
        public static bool IsShopSupply(string item)=>Classify(item)==ProductCategory.Hygiene;
        // A product's own rule, set in the product list (Custom mode only).
        public static EditionPermission? ItemRule(ModeSettings settings,string item)=>settings.Mode==GameMode.Custom?settings.ItemRules.FirstOrDefault(r=>r.Id==item):null;
        // An edition switched to "Désactivé" is off everywhere: boosters, boxes and single cards.
        public static bool EditionDisabled(ModeSettings settings,string edition)
        {
            if(settings.Mode!=GameMode.Custom)return false;
            var rule=settings.Editions.FirstOrDefault(e=>e.Id==edition);
            return rule!=null?!rule.Buy && !rule.Sell:!settings.AllowNewContent && EditionNames.ContainsKey(edition);
        }
        public static bool CanBuyFurniture(ModeSettings settings,string objectType)=>settings.Mode!=GameMode.Custom || !settings.LockedFurniture.Contains(objectType);
        // Single cards of an edition: sold unless the edition is listed as unsellable.
        public static bool CanSellCardEdition(ModeSettings settings,string expansion)
        {
            if(settings.Mode==GameMode.Classic)return true;
            if(!CanSellCard(settings))return false;
            var edition=EditionOf(expansion);
            return !settings.UnsellableCardEditions.Contains(edition) && !EditionDisabled(settings,edition);
        }
        public static bool RestrictsCardEditions(ModeSettings settings)=>settings.Mode==GameMode.Custom && settings.AllowSingleCardSales &&
            (settings.UnsellableCardEditions.Count>0 || settings.Editions.Any(e=>!e.Buy && !e.Sell) || !settings.AllowNewContent);
        public static bool CanBrowseEdition(ModeSettings settings,string expansion)
        {
            if(settings.Mode==GameMode.Classic)return true;
            var edition=EditionOf(expansion);
            if(edition.StartsWith("native:",StringComparison.Ordinal))
            {
                var permission=settings.Editions.FirstOrDefault(e=>e.Id==edition);
                return settings.AllowNativeProducts && (permission==null?settings.AllowNewContent:permission.Buy || permission.Sell);
            }
            return Definitions.Any(d=>ItemEdition(d.Key)==edition && (CanBuy(settings,d.Key) || CanSell(settings,d.Key)));
        }
        public static string DescribePurchase(ModeSettings settings,string item)
        {
            Definitions.TryGetValue(item,out var definition);
            return "item="+item+", catégorie="+Classify(item)+", source="+(definition?.Source??"native")+", édition="+ItemEdition(item)+", origine="+OriginAllowed(settings,item,true)+", catégorie autorisée="+CategoryAllowed(settings,item,true)+", édition autorisée="+EditionAllowed(settings,item,true)+", achat="+CanBuy(settings,item);
        }
        public static bool CanBuy(ModeSettings settings, string item)
        {
            if (item=="None" || item=="Max" || string.IsNullOrWhiteSpace(item)) return false;
            if (settings.Mode==GameMode.Classic) return true;
            var own=ItemRule(settings,item);
            if (own!=null) return own.Buy;
            var category=Classify(item);
            if (category==ProductCategory.Hygiene && settings.AllowShopSupplies) return true;
            var booster=Boosters.Contains(item) || category==ProductCategory.Boosters || category==ProductCategory.BoosterBoxes;
            if (settings.Mode==GameMode.CardsOnly) return booster && OriginAllowed(settings,item,true) && EditionAllowed(settings,item,true) && CategoryAllowed(settings,item,true);
            return (booster ? settings.AllowBoosterPurchases : settings.AllowAccessoryPurchases) && OriginAllowed(settings,item,true) && CategoryAllowed(settings,item,true) && EditionAllowed(settings,item,true);
        }
        public static bool CanSellProduct(ModeSettings settings) => settings.Mode==GameMode.Classic || settings.Mode==GameMode.Custom && settings.AllowProductSales && Enum.GetValues(typeof(ProductCategory)).Cast<ProductCategory>().Any(c=>(settings.Products.FirstOrDefault(p=>p.Category==c) ?? ((c==ProductCategory.Hygiene || c==ProductCategory.BulkCards)?settings.Products.FirstOrDefault(p=>p.Category==ProductCategory.Other):null))?.Sell!=false);
        public static bool CanSell(ModeSettings settings,string item) => item!="None" && item!="Max" && !string.IsNullOrWhiteSpace(item) && CanSellProduct(settings) && (settings.Mode==GameMode.Classic || (ItemRule(settings,item) is EditionPermission own ? own.Sell : OriginAllowed(settings,item,false) && CategoryAllowed(settings,item,false) && EditionAllowed(settings,item,false)));
        public static bool CanSellCard(ModeSettings settings) => settings.Mode!=GameMode.Custom || settings.AllowSingleCardSales;
    }
    public static class ModeStore
    {
        private static string FilePath(string campaigns, string id)
        {
            if (!Path.IsPathRooted(campaigns) || !Guid.TryParseExact(id,"N",out _)) throw new IOException(T("Boutique invalide."));
            var folder=Path.Combine(campaigns,id);
            if (!File.Exists(Path.Combine(campaigns,".tcg-save-lab")) || !File.Exists(Path.Combine(folder,"campaign.xml"))) throw new IOException(T("Boutique inconnue."));
            for(var d=new DirectoryInfo(folder);d!=null;d=d.Parent) if(d.Exists && (d.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException(T("Lien refusé."));
            return Path.Combine(folder,"game-mode.xml");
        }
        public static ModeSettings Load(string campaigns,string id)
        {
            var path=FilePath(campaigns,id);
            if(!File.Exists(path)) return ModeSettings.Preset(id,GameMode.Classic);
            using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
            {
                var result=(ModeSettings?)new XmlSerializer(typeof(ModeSettings)).Deserialize(reader) ?? throw new IOException(T("Mode vide."));
                if(result.CampaignId!=id || result.SchemaVersion!=1 || !Enum.IsDefined(typeof(GameMode),result.Mode)) throw new IOException(T("Configuration de mode invalide."));
                if(result.Products.Any(p=>!Enum.IsDefined(typeof(ProductCategory),p.Category)) || result.Products.Select(p=>p.Category).Distinct().Count()!=result.Products.Count)throw new IOException(T("Catégories de produits invalides."));
                if(result.Editions.Any(e=>string.IsNullOrWhiteSpace(e.Id)) || result.Editions.Select(e=>e.Id).Distinct().Count()!=result.Editions.Count)throw new IOException(T("Éditions invalides."));
                return result;
            }
        }
        public static void Update(string campaigns,ModeSettings settings)
        {
            var path=FilePath(campaigns,settings.CampaignId);
            if(settings.SchemaVersion!=1 || !Enum.IsDefined(typeof(GameMode),settings.Mode))throw new IOException(T("Mode invalide."));
            var temporary=path+"."+Guid.NewGuid().ToString("N")+".pending";
            try
            {
                using(var writer=XmlWriter.Create(temporary))new XmlSerializer(typeof(ModeSettings)).Serialize(writer,settings);
                if(File.Exists(path))File.Replace(temporary,path,path+".previous");else File.Move(temporary,path);
            }
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
        // Creation only: existing choices cannot be overwritten accidentally.
        public static void Create(string campaigns,ModeSettings settings)
        {
            if(settings.SchemaVersion!=1 || !Enum.IsDefined(typeof(GameMode),settings.Mode)) throw new IOException(T("Mode invalide."));
            var path=FilePath(campaigns,settings.CampaignId); var temporary=path+"."+Guid.NewGuid().ToString("N")+".pending";
            try
            {
                using(var writer=XmlWriter.Create(temporary)) new XmlSerializer(typeof(ModeSettings)).Serialize(writer,settings);
                File.Move(temporary,path);
            }
            finally {if(File.Exists(temporary)) File.Delete(temporary);}
        }
    }
}

