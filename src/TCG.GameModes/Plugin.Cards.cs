using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TCG.GameModes.Core;

namespace TCG.GameModes
{
    // 0.2.0 : customers only pick card compartments whose card belongs to a sellable edition.
    public sealed partial class Plugin
    {
        private void InstallCardEditionHooks()
        {
            Hook("ShelfManager", "GetCardShelfToBuyCard", nameof(SelectCardShelf));
            Hook("CardShelf", "GetCustomerTargetCardCompartment", nameof(SelectCardCompartment));
        }
        private static bool CardEditionsRestricted => instance != null && ModeRules.RestrictsCardEditions(instance.active);
        // Expansion name of the first card stored in a compartment, as the binder uses it.
        private static string? CardExpansion(object compartment)
        {
            var cards = Read(compartment, "m_StoredCardList") as IList;
            if (cards == null || cards.Count == 0 || cards[0] == null) return null;
            var group = Read(cards[0]!, "m_Card3dUI");
            var ui = group == null ? null : Read(group, "m_CardUI");
            var data = ui == null ? null : Invoke(ui, "GetCardData");
            if (data == null) return null;
            var expansion = Read(data, "expansionType")!.ToString()!;
            if (expansion == "Tetramon" && Read(data, "isDestiny") is bool destiny && destiny) expansion = "Destiny";
            return expansion;
        }
        private static bool SellableCardCompartment(object compartment)
        {
            try
            {
                var expansion = CardExpansion(compartment);
                return expansion != null && ModeRules.CanSellCardEdition(instance!.active, expansion);
            }
            // An unreadable card is treated as not for sale rather than breaking the customer.
            catch (Exception) { return false; }
        }
        private static bool SelectCardShelf(ref object? __result)
        {
            if (!CardEditionsRestricted) return true;
            var shelves = (IList)AccessTools.Method(Type("ShelfManager"), "GetCardShelfList").Invoke(null, null);
            var allowed = new List<object>();
            foreach (var shelf in shelves.Cast<object>())
            {
                if (shelf == null || !(bool)Invoke(shelf, "IsValidObject") || (bool)Read(shelf, "m_ItemNotForSale")!) continue;
                var compartments = (IList)Read(shelf, "m_CardCompartmentList")!;
                if (compartments.Cast<object>().Any(SellableCardCompartment)) allowed.Add(shelf);
            }
            // Same 80 % chance as the game; otherwise the customer looks elsewhere (no shelf).
            __result = allowed.Count > 0 && UnityEngine.Random.Range(0, 100) < 80 ? allowed[UnityEngine.Random.Range(0, allowed.Count)] : null;
            return false;
        }
        private static bool SelectCardCompartment(object __instance, ref object? __result)
        {
            if (!CardEditionsRestricted) return true;
            var allowed = ((IList)Read(__instance, "m_CardCompartmentList")!).Cast<object>().Where(SellableCardCompartment).ToList();
            if (allowed.Count == 0) return true;
            __result = allowed[UnityEngine.Random.Range(0, allowed.Count)];
            return false;
        }
    }
}
