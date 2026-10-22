using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TCG.GameModes.Core;
namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private void InstallTrackerHooks()
        {
            if(!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("iitzsamurai.collectiontracker"))return;
            Hook("CollectionTracker.CTCustomShopScreen","BuildExpansionList",nameof(RefreshTrackerEditionCache));
            Hook("CollectionTracker.CTCustomShopScreen","TabletExpansions",nameof(FilterTrackerRows),true);
            Hook("CollectionTracker.CTCustomShopScreen","GetFilteredExpansions",nameof(FilterTrackerRows),true);
            Hook("CollectionTracker.CTCustomShopScreen","TabletShops",nameof(FilterTrackerShops),true);
        }
        private static void RefreshTrackerEditionCache()
        {
            var helper=Type("CollectionTracker.EPLAPIHelper");
            AccessTools.Field(helper,"s_CachedEPLTypes").SetValue(null,null);
            AccessTools.Field(helper,"s_CachedEPLTotals").SetValue(null,null);
            var editions=(IList)AccessTools.Method(helper,"FindAllEPLExpansionTypes").Invoke(null,null);
            var cache=Type("CollectionTracker.CTExpansionCache");
            AccessTools.Field(cache,"_initialized").SetValue(null,false);
            AccessTools.Method(cache,"InitializeFromExpansionTypes").Invoke(null,new object[]{editions});
            var map=(IDictionary)AccessTools.Field(cache,"PhoneAppMap").GetValue(null);
            var shops=(IList)AccessTools.Field(cache,"PhoneAppIds").GetValue(null);
            foreach(var expansion in editions)
            {
                var key=new string(expansion!.ToString()!.Where(char.IsLetterOrDigit).ToArray());
                if(instance!.editionShops.TryGetValue(key,out var shop))
                {map[expansion]=shop;if(!shops.Contains(shop))shops.Add(shop);}
            }
            instance!.Logger.LogInfo("Collection Tracker : nouvelle détection / "+editions.Count+" éditions EPL.");
        }
        private static void FilterTrackerRows(object __result)
        {
            if(!Restricted)return;
            var rows=(IList)__result;
            for(var i=rows.Count-1;i>=0;i--)
            {
                var row=rows[i]!;
                var type=AccessTools.Field(row.GetType(),"Type")??AccessTools.Field(row.GetType(),"ExpansionType");
                if(type!=null && !BinderAllowed(type.GetValue(row)))rows.RemoveAt(i);
            }
        }
        private static void FilterTrackerShops(object __instance,object __result)
        {
            if(!Restricted)return;
            var allowed=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var row in (IEnumerable)Invoke(__instance,"TabletExpansions",false))
                allowed.Add(Read(row,"ShopId")?.ToString()??"");
            var shops=(IList)__result;
            for(var i=shops.Count-1;i>=0;i--)
            {
                var pair=shops[i]!;
                var id=AccessTools.Property(pair.GetType(),"Key").GetValue(pair,null)?.ToString()??"";
                if(!allowed.Contains(id))shops.RemoveAt(i);
            }
        }
        private void InstallBinderHooks()
        {
            Hook("CollectionBinderFlipAnimCtrl","StartShowCardAlbum",nameof(SelectAllowedBinderEdition));
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("EnhancedPrefabLoader"))
                Hook("EnhancedPrefabLoader.Core.Services.Internal.CollectionBinderService","PopulateMainMenu",nameof(FilterBinderMenu),true);
            else Hook("CollectionBinderUI","OpenSortAlbumScreen",nameof(FilterNativeBinderButtons),true);
        }
        private static bool BinderAllowed(object expansion)=>!Restricted || ModeRules.CanBrowseEdition(instance!.active,expansion.ToString()!);
        private static void SelectAllowedBinderEdition(object __instance)
        {
            // An error here must never stop the binder from opening (seen once: NullReferenceException).
            try
            {
                var current=Read(__instance,"m_ExpansionType");
                if(!Restricted || current==null || BinderAllowed(current))return;
                foreach(var expansion in Enum.GetValues(Type("ECardExpansionType")))
                {
                    if(expansion.ToString()=="None" || expansion.ToString()=="MAX" || !BinderAllowed(expansion))continue;
                    Invoke(__instance,"OnPressSwitchExpansion",Convert.ToInt32(expansion));
                    instance!.Logger.LogInfo("Classeur : édition initiale autorisée / "+expansion);return;
                }
            }
            catch(Exception ex){instance!.Logger.LogWarning("Classeur : choix de l’édition initiale ignoré ("+(ex.InnerException??ex).GetType().Name+").");}
        }
        private static void FilterBinderMenu(object __result)
        {
            if(!Restricted)return;
            void Filter(IList entries)
            {
                for(var i=entries.Count-1;i>=0;i--)
                {
                    var entry=entries[i]!;
                    var children=AccessTools.Field(entry.GetType(),"SubMenu")?.GetValue(entry) as IList;
                    if(children!=null && children.Count>0){Filter(children);if(children.Count==0)entries.RemoveAt(i);continue;}
                    var action=AccessTools.Field(entry.GetType(),"OnClick")?.GetValue(entry) as Delegate;
                    var target=action?.Target;if(target==null)continue;
                    var expansion=AccessTools.Field(target.GetType(),"expansion")?.GetValue(target);
                    var pair=AccessTools.Field(target.GetType(),"kvp")?.GetValue(target);
                    if(pair!=null)expansion=AccessTools.Property(pair.GetType(),"Key")?.GetValue(pair,null);
                    if(expansion!=null && !BinderAllowed(expansion))entries.RemoveAt(i);
                }
            }
            Filter((IList)__result);
        }
        private static void FilterNativeBinderButtons(object __instance)
        {
            if(!Restricted)return;
            var buttons=(IList)Read(__instance,"m_ExpansionBtnList")!;
            for(var i=0;i<buttons.Count;i++)((Transform)buttons[i]!).gameObject.SetActive(BinderAllowed(Enum.ToObject(Type("ECardExpansionType"),i)));
        }
    }
}
