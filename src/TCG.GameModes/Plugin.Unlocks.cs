using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using TCG.GameModes.Core;

using static TCG.Shared.Lang;
namespace TCG.GameModes
{
    // 0.3.0 : furniture that can be locked per shop, level-up notices limited to what the rules
    // allow, and a catalogue of products and furniture recorded in game for the rules screen.
    public sealed partial class Plugin
    {
        private GameCatalogue? catalogue;
        private GameCatalogue Catalogue => catalogue ??= GameCatalogue.Load(campaigns);

        private void InstallUnlockHooks()
        {
            Hook("LevelUpNotificationUI", "RefreshUnlockableTextData", nameof(FilterUnlocks), true);
            // Runs after the "card tables at start" postfix registered earlier on the same method.
            harmony!.Patch(AccessTools.Method(Type("FurnitureShopPanelUI"), "Init"), postfix: new HarmonyMethod(typeof(Plugin), nameof(LockFurniturePanel)) { priority = Priority.Low });
            Hook("FurnitureShopUIScreen", "Init", nameof(HideLockedFurniturePanels), true);
            Hook("FurnitureShopUIScreen", "OnPressPanelUIButton", nameof(FurniturePurchaseAllowed));
            // Furniture Overhaul replaces the phone shop: its catalogue is rebuilt at each opening
            // and already knows how to hide an article (ShopItem.Hidden).
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.Values.Any(p => p.Instance != null && p.Instance.GetType().Assembly.GetName().Name == "FurnitureOverhaul"))
            {
                var build = AccessTools.Method(AccessTools.TypeByName("FurnitureOverhaul.Catalogue"), "Build");
                if (build != null) harmony.Patch(build, postfix: new HarmonyMethod(typeof(Plugin), nameof(HideLockedOverhaulItems)));
                else Logger.LogWarning("Furniture Overhaul : catalogue non reconnu, les meubles bloqués restent visibles (achat refusé).");
            }
            Hook("FurnitureShopUIScreen", "EvaluateCartCheckout", nameof(FurnitureCheckoutAllowed));
        }

        // The game's singleton, read from its static field so that none is created at the title menu.
        private static object? Singleton(string type)
        {
            var closed = Type("CSingleton`1").MakeGenericType(Type(type));
            return AccessTools.Field(closed, "instance")?.GetValue(null);
        }

        private static IList? RestockList(object inventory) => Read(Read(inventory, "m_StockItemData_SO")!, "m_RestockDataList") as IList;
        private static IList? FurnitureList(object inventory) => Read(Read(inventory, "m_ObjectData_SO")!, "m_FurniturePurchaseDataList") as IList;
        private static int ShopLevel => (int)AccessTools.Field(Type("CPlayerData"), "m_ShopLevel").GetValue(null);

        private static string FurnitureType(int index)
        {
            var data = AccessTools.Method(Type("InventoryBase"), "GetFurniturePurchaseData", new[] { typeof(int) }).Invoke(null, new object[] { index });
            return Read(data, "objectType")!.ToString()!;
        }
        private static bool FurnitureAllowed(string objectType) => !Restricted || ModeRules.CanBuyFurniture(instance!.active, objectType);

        // LevelUpNotificationUI.RefreshUnlockableTextData lists, for every future level, the products,
        // furniture, rooms and employees that will unlock. Entries the rules make unobtainable are removed.
        private static void FilterUnlocks(object __instance)
        {
            var self = instance!;
            try
            {
                var inventory = Singleton("InventoryBase");
                if (inventory == null) return;
                var restock = RestockList(inventory);
                var furniture = FurnitureList(inventory);
                self.RecordCatalogue(restock, furniture);
                if (!Restricted) return;
                var level = ShopLevel;
                if (restock != null)
                    Keep((IList)Read(__instance, "m_UnlockableItem")!, restock.Cast<object>().Where(r => level + 1 < (int)Read(r, "licenseShopLevelRequired")!),
                        r => ModeRules.CanBuy(self.active, Read(r, "itemType")!.ToString()!));
                if (furniture != null)
                    Keep((IList)Read(__instance, "m_UnlockableFurniture")!, furniture.Cast<object>().Where(f => level + 1 < (int)Read(f, "levelRequirement")!),
                        f => ModeRules.CanBuyFurniture(self.active, Read(f, "objectType")!.ToString()!));
                if (self.active.Mode == GameMode.Custom && !self.active.AllowEmployees) ((IList)Read(__instance, "m_UnlockableWorker")!).Clear();
            }
            catch (Exception e) { self.Logger.LogWarning("Annonces de niveau non filtrées : " + e.Message); }
        }

        // The game adds one notice per source entry, in order: notices and sources are paired by position.
        private static void Keep(IList notices, IEnumerable<object> sources, Func<object, bool> allowed)
        {
            var list = sources.ToList();
            if (list.Count != notices.Count) { instance!.Logger.LogWarning($"Annonces de niveau : {notices.Count} annonces pour {list.Count} entrées, filtrage ignoré."); return; }
            for (var i = list.Count - 1; i >= 0; i--) if (!allowed(list[i])) notices.RemoveAt(i);
        }

        private void RecordCatalogue(IList? restock, IList? furniture)
        {
            try
            {
                var items = new List<CatalogueEntry>();
                if (restock != null)
                    foreach (var r in restock.Cast<object>())
                    {
                        var type = Read(r, "itemType")!;
                        if (items.Any(i => i.Id == type.ToString())) continue;
                        var data = AccessTools.Method(Type("InventoryBase"), "GetItemData").Invoke(null, new[] { type });
                        var name = data == null ? type.ToString()! : Invoke(data, "GetName") as string ?? type.ToString()!;
                        items.Add(new CatalogueEntry { Id = type.ToString()!, Name = name, Level = (int)Read(r, "licenseShopLevelRequired")! });
                    }
                var pieces = new List<CatalogueEntry>();
                if (furniture != null)
                    foreach (var f in furniture.Cast<object>())
                    {
                        var type = Read(f, "objectType")!.ToString()!;
                        if (pieces.Any(p => p.Id == type)) continue;
                        pieces.Add(new CatalogueEntry { Id = type, Name = Invoke(f, "GetName") as string ?? type, Level = (int)Read(f, "levelRequirement")! });
                    }
                catalogue = GameCatalogue.Merge(campaigns, items, pieces);
            }
            catch (Exception e) { Logger.LogWarning("Catalogue des produits non enregistré : " + e.Message); }
        }

        // Phone furniture shop of the base game: locked pieces are not shown at all.
        private static void HideLockedFurniturePanels(object __instance)
        {
            if (!Restricted || instance!.active.LockedFurniture.Count == 0) return;
            try
            {
                var panels = (IList)Read(__instance, "m_FurnitureShopPanelUIList")!;
                var inventory = Singleton("InventoryBase");
                var count = inventory == null ? panels.Count : Math.Min(panels.Count, FurnitureList(inventory)?.Count ?? panels.Count);
                for (var i = 0; i < count; i++)
                    if (!FurnitureAllowed(FurnitureType(i))) ((Component)panels[i]!).gameObject.SetActive(false);
            }
            catch (Exception e) { instance!.Logger.LogWarning("Meubles bloqués non masqués : " + e.Message); }
        }
        // Furniture Overhaul: its own articles and the base game's, all built from the game's furniture list.
        private static void HideLockedOverhaulItems(object __result)
        {
            var self = instance!;
            try
            {
                var inventory = Singleton("InventoryBase");
                // Furniture Overhaul adds its own pieces to the game's list when its shop opens: record them too.
                if (inventory != null) self.RecordCatalogue(RestockList(inventory), FurnitureList(inventory));
                if (!Restricted || self.active.LockedFurniture.Count == 0 || !(__result is IEnumerable items)) return;
                foreach (var item in items)
                    if (item != null && !FurnitureAllowed(Read(item, "Type")!.ToString()!)) Write(item, "Hidden", true);
            }
            catch (Exception e) { self.Logger.LogWarning("Furniture Overhaul : meubles bloqués non masqués : " + e.Message); }
        }

        // Furniture shop on the phone: a locked piece shows as unavailable and cannot be ordered.
        private static void LockFurniturePanel(object __instance, int __1)
        {
            try
            {
                if (FurnitureAllowed(FurnitureType(__1))) return;
                Write(__instance, "m_LevelRequired", int.MaxValue);
                ((GameObject)Read(__instance, "m_CompanyTitle")!).SetActive(false);
                ((GameObject)Read(__instance, "m_LockPurchaseBtn")!).SetActive(true);
                var text = (Component)Read(__instance, "m_LevelRequirementText")!;
                text.gameObject.SetActive(true);
                AccessTools.Property(text.GetType(), "text")?.SetValue(text, T("Bloqué par les règles"), null);
            }
            catch (Exception e) { instance!.Logger.LogWarning("Meuble non verrouillé : " + e.Message); }
        }
        private static bool FurniturePurchaseAllowed(int __0) => FurnitureAllowed(FurnitureType(__0)) || RefuseActivity(T("Ce meuble est bloqué par les règles de cette boutique."));
        private static bool FurnitureCheckoutAllowed(int __1) => FurnitureAllowed(FurnitureType(__1)) || RefuseActivity(T("Ce meuble est bloqué par les règles de cette boutique."));
    }
}
