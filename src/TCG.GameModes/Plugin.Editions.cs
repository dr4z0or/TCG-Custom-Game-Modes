using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TCG.GameModes.Core;

namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private readonly Dictionary<string,string> editionShops=new Dictionary<string,string>(StringComparer.Ordinal);
        private void InstallEditionHooks()
        {
            ModeRules.RegisterNativeEditions();
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("EnhancedPrefabLoader"))
            {
                
                var descriptorCount=0;var productCount=0;
                foreach(var file in Directory.EnumerateFiles(BepInEx.Paths.PluginPath,"*.json",SearchOption.AllDirectories).Where(p=>p.IndexOf("_prefabloader"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0))
                {
                    try
                    {
                        var data=PackDefinitions.Read(file);descriptorCount++;productCount+=data.Items.Length;
                        foreach(var item in data?.Items ?? Array.Empty<PackItem>())
                            {
                                if(item.IsCardPack && item.CardExpansion!="" && item.PhoneAppId!="")editionShops[new string(item.CardExpansion.Where(char.IsLetterOrDigit).ToArray())]=item.PhoneAppId;
                                var expansion=data!.CardExpansions?.FirstOrDefault(e=>e.CardExpansion==item.CardExpansion);
                                var family=expansion?.MenuCategory??"";
                                var relative=file.Substring(BepInEx.Paths.PluginPath.Length).TrimStart(Path.DirectorySeparatorChar);
                                var folder=Path.GetFileName(Path.GetDirectoryName(file)).Replace("_prefabloader","");
                                var origin=family!=""?family:relative.IndexOf("Pokemon",StringComparison.OrdinalIgnoreCase)>=0?"Pokémon":relative.Split(Path.DirectorySeparatorChar)[0];
                                var sourceName=origin+" — "+folder;
                                RegisterPackItem(item,"content:"+data.BundleId,sourceName,family);
                            }
                    }
                    catch(Exception ex){Logger.LogWarning("Définition EPL illisible : "+Path.GetFileName(file)+" / "+ex.Message);}
                }
                Logger.LogInfo("Inventaire EPL : "+descriptorCount+" fichiers, "+productCount+" produits, "+ModeRules.ContentNames.Count+" packs.");
                Hook("EnhancedPrefabLoader.Core.LoadingPipeline.Processors.ItemProcessor","ProcessItem",nameof(ProcessedPackItem),true);
                Hook("EnhancedPrefabLoader.Core.Shops.CustomShopScreen","GetSortedItems",nameof(EplSortedItems),true);
            }
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.qolmods.purchaseoverhaul"))
            {
                Hook("PurchaseOverhaul.Product","get_CanBuy",nameof(OverhaulCanBuy),true);
                Hook("PurchaseOverhaul.Product","get_CanBuyLicense",nameof(OverhaulCanBuy),true);
                Hook("PurchaseOverhaul.Catalogue","Matches",nameof(OverhaulMatches),true);
                Hook("PurchaseOverhaul.Catalogue","Rebuild",nameof(AuditPurchaseCatalogue),true);
                harmony!.Patch(AccessTools.Method(Type("PurchaseOverhaul.Cart"),"Add"),prefix:new HarmonyMethod(typeof(Plugin),nameof(OverhaulAdd)));
                harmony.Patch(AccessTools.Method(Type("PurchaseOverhaul.Cart"),"Set"),prefix:new HarmonyMethod(typeof(Plugin),nameof(OverhaulAdd)));
            }
            Logger.LogInfo("Éditions de boosters détectées : "+ModeRules.EditionNames.Count);
        }
        private static void RegisterPackItem(PackItem item,string source="",string sourceName="",string family="")
        {
            var category=item.IsCardBox?ProductCategory.BoosterBoxes:item.IsCardPack?ProductCategory.Boosters:
                item.IsBulkBox?ProductCategory.BulkCards:item.IsSprayCan?ProductCategory.Hygiene:
                item.AddItemAsFigurine?ProductCategory.Figurines:item.AddItemAsBoardGame?ProductCategory.BoardGames:ModeRules.Classify(item.ItemType);
            ModeRules.RegisterItem(item.ItemType,category,(item.IsCardBox || item.IsCardPack) && !string.IsNullOrWhiteSpace(item.CardExpansion)?"epl:"+item.CardExpansion:"",family!=""?family+" — "+item.CardExpansion:item.CardExpansion,item.SpawnsPackType,source,sourceName);
        }
        private static void ProcessedPackItem(object __0)
        {
            object? Value(string name)=>AccessTools.Property(__0.GetType(),name)?.GetValue(__0,null);
            var runtimeItem=(string?)Value("ItemType")??"";
            if(ModeRules.PreserveRegisteredItem(runtimeItem))return;
            var edition=(string?)Value("CardExpansion")??"";
            var display=ModeRules.EditionNames.TryGetValue("epl:"+edition,out var name)?name:edition;
            RegisterPackItem(new PackItem{ItemType=(string?)Value("ItemType")??"",CardExpansion=(string?)Value("CardExpansion")??"",SpawnsPackType=(string?)Value("SpawnsPackType")??"",IsCardPack=(bool?)Value("IsCardPack")??false,IsCardBox=(bool?)Value("IsCardBox")??false,IsBulkBox=(bool?)Value("IsBulkBox")??false,IsSprayCan=(bool?)Value("IsSprayCan")??false,AddItemAsFigurine=(bool?)Value("AddItemAsFigurine")??false,AddItemAsBoardGame=(bool?)Value("AddItemAsBoardGame")??false});
            if(edition!="")ModeRules.EditionNames["epl:"+edition]=display;
        }
        private static readonly HashSet<string> auditedProducts=new HashSet<string>();
        private static void AuditPurchaseCatalogue()
        {
            try
            {
                var products=(IEnumerable)AccessTools.Field(Type("PurchaseOverhaul.Catalogue"),"Products").GetValue(null);
                foreach(var product in products)
                {
                    var item=Read(product,"Type")!.ToString()!;
                    var name=Read(product,"Name")?.ToString()??"";
                    if((name.IndexOf("Base",StringComparison.OrdinalIgnoreCase)>=0 || !ModeRules.CanBuy(instance!.active,item)) && auditedProducts.Add(instance!.active.CampaignId+":"+item))
                        instance!.Logger.LogInfo("Catalogue achat / "+name+" / "+ModeRules.DescribePurchase(instance.active,item));
                }
            }
            catch(Exception ex){instance!.Logger.LogWarning("Diagnostic catalogue : "+ex.Message);}
        }
        private static bool AllowedOverhaulProduct(object product)=>!Restricted || ModeRules.CanBuy(instance!.active,Read(product,"Type")!.ToString()!);
        private static void EplSortedItems(object __result)
        {
            if(!Restricted)return;
            var indices=(IList)__result;
            for(var i=indices.Count-1;i>=0;i--)if(!AllowedIndex((int)indices[i]!))indices.RemoveAt(i);
        }
        private static void OverhaulCanBuy(object __instance,ref bool __result){if(!AllowedOverhaulProduct(__instance))__result=false;}
        private static void OverhaulMatches(object __0,ref bool __result){if(!AllowedOverhaulProduct(__0))__result=false;}
        private static bool OverhaulAdd(object __0,int __1,ref int __result)
        {
            if(__1<=0 || AllowedOverhaulProduct(__0))return true;
            __result=0;return false;
        }
    }
}
