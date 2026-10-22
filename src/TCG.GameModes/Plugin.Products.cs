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
    public sealed partial class Plugin
    {
        private Vector2 productsScroll;
        private int configurationTab;
        private string configurationSearch="";
        private int editionView;
        private string[] summaryCache=Array.Empty<string>();
        private float summaryTime=-1;
        private readonly HashSet<string> expandedFamilies=new HashSet<string>(StringComparer.Ordinal);
        private static string ContentFamily(string label) {var split=label.IndexOf(" — ",StringComparison.Ordinal);return split<0?T("Autres"):label.Substring(0,split);}
        private static string ContentLabel(string label) {var split=label.IndexOf(" — ",StringComparison.Ordinal);return split<0?label:label.Substring(split+3);}
        [ThreadStatic] private static bool selectingCustomerProducts;
        private void DrawProductPermissions(GUIStyle text,GUIStyle toggle,GUIStyle button,GUIStyle selectedButton,GUIStyle field)
        {
            ModeRules.AddCategories(draft);
            // Base game edition names follow the interface language.
            ModeRules.RegisterNativeEditions();
            var tabs=new[]{T("Produits"),T("Packs de contenu"),T("Éditions"),T("Meubles"),T("Activités"),T("Résumé")};
            for(var i=0;i<tabs.Length;i++)
                if(GUI.Button(new Rect(105+i*132,200,127,34),tabs[i],new GUIStyle(configurationTab==i?selectedButton:button){fontSize=13}))
                {configurationTab=i;productsScroll=Vector2.zero;configurationSearch="";}
            bool Matches(string name)=>name.IndexOf(configurationSearch,StringComparison.OrdinalIgnoreCase)>=0;
            var plain=configurationTab==4 || configurationTab==5;
            var cardsView=configurationTab==2 && editionView==1;
            if(!plain)
            {
                GUI.Label(new Rect(105,240,95,28),T("Rechercher"),text);
                configurationSearch=GUI.TextField(new Rect(205,240,690,30),configurationSearch,80,field);
                if(configurationTab==2)
                {
                    var small=new GUIStyle(button){fontSize=12};var smallSelected=new GUIStyle(selectedButton){fontSize=12};
                    if(GUI.Button(new Rect(105,274,165,30),new GUIContent(T("Boosters et boîtes"),T("Achats et ventes des boosters scellés et de leurs boîtes. « Désactivé » retire l’édition de la partie : ni boosters, ni boîtes, ni cartes à l’unité vendues, ni classeur.")),editionView==0?smallSelected:small)){editionView=0;productsScroll=Vector2.zero;}
                    if(GUI.Button(new Rect(276,274,165,30),new GUIContent(T("Cartes à l’unité"),T("Éditions dont les clients achètent les cartes posées sur vos présentoirs.")),editionView==1?smallSelected:small)){editionView=1;productsScroll=Vector2.zero;}
                }
                else GUI.Label(new Rect(110,276,490,28),tabs[configurationTab],text);
                GUI.Label(new Rect(450,276,440,28),cardsView?T("Cartes de l’édition"):T("Utilisation"),text);
            }
            // The summary copies the draft: refresh it twice a second rather than every frame.
            if(configurationTab==5 && (summaryTime<0 || Time.realtimeSinceStartup-summaryTime>0.5f)){summaryCache=ModeSummary.Describe(ModePresets.Copy(draft));summaryTime=Time.realtimeSinceStartup;}
            var summary=configurationTab==5?summaryCache:Array.Empty<string>();
            var summaryStyle=new GUIStyle(text){wordWrap=true};
            var allEntries=(configurationTab==1?ModeRules.ContentNames:ModeRules.EditionNames).OrderBy(e=>e.Value,StringComparer.OrdinalIgnoreCase).ToArray();
            var families=allEntries.GroupBy(e=>ContentFamily(e.Value)).Where(g=>g.Any(e=>Matches(e.Value))).ToArray();
            bool Expanded(string family)=>configurationSearch!="" || expandedFamilies.Contains(configurationTab+":"+family);
            var catalogue=Catalogue;
            CatalogueEntry[] ItemsOf(ProductCategory category)=>catalogue.Items.Where(i=>ModeRules.Classify(i.Id)==category).OrderBy(i=>i.Name,StringComparer.OrdinalIgnoreCase).ToArray();
            bool ItemMatches(CatalogueEntry i)=>Matches(i.Name) || Matches(i.Id);
            var products=draft.Products.Where(p=>Matches(ModeRules.CategoryName(p.Category)) || configurationSearch!="" && ItemsOf(p.Category).Any(ItemMatches)).ToArray();
            bool ProductExpanded(ProductCategory c)=>configurationSearch!="" || expandedFamilies.Contains("product:"+c);
            CatalogueEntry[] ShownItems(ProductCategory c)=>ItemsOf(c).Where(i=>configurationSearch=="" || Matches(ModeRules.CategoryName(c)) || ItemMatches(i)).ToArray();
            var furniture=catalogue.Furniture.Where(f=>Matches(f.Name) || Matches(f.Id)).OrderBy(f=>f.Level).ThenBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToArray();
            var summaryHeights=summary.Select(line=>Math.Max(30f,summaryStyle.CalcHeight(new GUIContent("• "+line),750))+6).ToArray();
            bool CardsExpanded(string family)=>configurationSearch!="" || expandedFamilies.Contains("cards:"+family);
            var count=configurationTab==0?products.Length+3+products.Where(p=>ProductExpanded(p.Category)).Sum(p=>Math.Max(1,ShownItems(p.Category).Length)):
                configurationTab==3?Math.Max(2,furniture.Length+1):configurationTab==4?7:configurationTab==5?0:
                cardsView?families.Sum(g=>1+(CardsExpanded(g.Key)?g.Count(e=>Matches(e.Value)):0))+(draft.AllowSingleCardSales?0:2):
                families.Sum(g=>1+(Expanded(g.Key)?g.Count(e=>Matches(e.Value)):0));
            var contentHeight=configurationTab==5?summaryHeights.Sum():count*34;
            productsScroll=GUI.BeginScrollView(new Rect(105,plain?245:308,790,plain?269:206),productsScroll,new Rect(0,0,765,Math.Max(plain?269:206,contentHeight)));
            try
            {
                bool Permission(int row,string label,EditionPermission permission)
                {
                    var legacy=!permission.Buy && permission.Sell;
                    GUI.Label(new Rect(5,row,325,30),new GUIContent(label,legacy?label+T(" — ancien réglage : vente uniquement. Conservé jusqu’à un nouveau choix."):label),new GUIStyle(text){wordWrap=false,clipping=TextClipping.Clip});
                    var choices=new[]{T("Désactivé"),T("Achat uniquement"),T("Achat et vente")};
                    var current=legacy?-1:!permission.Buy?0:permission.Sell?2:1;
                    for(var choice=0;choice<choices.Length;choice++)
                        if(GUI.Button(new Rect(340+choice*140,row,135,30),choices[choice],new GUIStyle(choice==current?selectedButton:button){fontSize=12,wordWrap=true}))
                        {permission.Buy=choice!=0;permission.Sell=choice==2;return true;}
                    return false;

                }
                if(configurationTab==5)
                {
                    var y=0f;
                    for(var i=0;i<summary.Length;i++){GUI.Label(new Rect(5,y,750,summaryHeights[i]-6),"• "+summary[i],summaryStyle);y+=summaryHeights[i];}
                }
                else if(configurationTab==0)
                {
                    draft.AllowNativeProducts=GUI.Toggle(new Rect(5,0,750,30),draft.AllowNativeProducts,T("Produits du jeu d’origine"),toggle);
                    draft.AllowModProducts=GUI.Toggle(new Rect(5,34,750,30),draft.AllowModProducts,T("Produits ajoutés par les mods"),toggle);
                    draft.AllowShopSupplies=GUI.Toggle(new Rect(5,68,750,30),draft.AllowShopSupplies,new GUIContent(T("Fournitures toujours achetables (désodorisant)"),T("Les fournitures servent à la boutique et ne sont pas vendues : elles restent commandables même si leur catégorie ou les produits d’origine sont bloqués.")),toggle);
                    var productRow=3*34;
                    for(var i=0;i<products.Length;i++)
                    {
                        var p=products[i];var copy=new EditionPermission{Buy=p.Buy,Sell=p.Sell};
                        var exception=draft.NativeProductExceptions.FirstOrDefault(e=>e.Category==p.Category);
                        var supply=p.Category==ProductCategory.Hygiene && draft.AllowShopSupplies;
                        var expanded=ProductExpanded(p.Category);
                        var own=ItemsOf(p.Category).Count(item=>draft.ItemRules.Any(r=>r.Id==item.Id));
                        if(GUI.Button(new Rect(5,productRow,26,30),expanded?"▾":"▸",new GUIStyle(button){fontSize=12,padding=new RectOffset(0,0,0,0)}))
                        {var key="product:"+p.Category;if(!expandedFamilies.Add(key))expandedFamilies.Remove(key);}
                        var row=productRow;productRow+=34;
                        if(Permission(row,"      "+ModeRules.CategoryName(p.Category)+(own>0?(own>1?F(" ({0} réglés à part)",own):F(" ({0} réglé à part)",own)):"")+(supply?T(" (fourniture, achat toujours permis)"):exception!=null && !draft.AllowNativeProducts?T(" (exception)"):""),copy))
                        {
                            if(!draft.AllowNativeProducts)
                            {
                                if(exception==null){exception=new ProductPermission{Category=p.Category};draft.NativeProductExceptions.Add(exception);}
                                exception.Buy=copy.Buy;exception.Sell=copy.Sell;
                            }
                            if(copy.Buy)draft.AllowAccessoryPurchases=true;
                        }
                        p.Buy=copy.Buy;p.Sell=copy.Sell;
                        if(!expanded)continue;
                        var items=ShownItems(p.Category);
                        if(items.Length==0){GUI.Label(new Rect(40,productRow,715,30),catalogue.Items.Count==0?T("Lancez une partie une fois pour afficher la liste des produits."):T("Aucun produit connu dans cette catégorie."),text);productRow+=34;continue;}
                        foreach(var item in items){DrawItemRule(productRow,item,text,button,selectedButton);productRow+=34;}
                    }
                }
                else if(configurationTab==3)DrawFurnitureRules(furniture,text,button,selectedButton);
                else if(configurationTab==4)
                {
                    var row=0;
                    bool Option(bool value,string label){var result=GUI.Toggle(new Rect(5,row,750,30),value,label,toggle);row+=34;return result;}
                    draft.AllowSingleCardSales=Option(draft.AllowSingleCardSales,T("Vendre des cartes individuelles sur présentoir"));
                    draft.UnlockCardTablesAtStart=Option(draft.UnlockCardTablesAtStart,T("Tables de vente accessibles dès le départ"));
                    draft.AllowEmployees=Option(draft.AllowEmployees,T("Autoriser les employés"));
                    draft.AllowTableGames=Option(draft.AllowTableGames,T("Autoriser les parties aux tables"));
                    draft.AllowCardTrading=Option(draft.AllowCardTrading,T("Autoriser les échanges de cartes avec les clients"));
                    draft.AllowTournaments=Option(draft.AllowTournaments,T("Autoriser les tournois"));
                    draft.AllowNewContent=Option(draft.AllowNewContent,T("Autoriser automatiquement les futurs packs et éditions"));
                }
                else
                {
                    var permissions=configurationTab==1?draft.Contents:draft.Editions;
                    EditionPermission Rule(string id)
                    {
                        var rule=permissions.FirstOrDefault(p=>p.Id==id);
                        if(rule==null){rule=new EditionPermission{Id=id,Buy=draft.AllowNewContent,Sell=draft.AllowNewContent};permissions.Add(rule);}
                        return rule;
                    }
                    var row=0;
                    if(cardsView)
                    {
                        if(!draft.AllowSingleCardSales){GUI.Label(new Rect(5,row,750,60),T("La vente de cartes à l’unité est désactivée dans l’onglet Activités : ces choix s’appliqueront si vous la réactivez."),text);row+=68;}
                        foreach(var family in families)
                        {
                            var expanded=CardsExpanded(family.Key);
                            var key="cards:"+family.Key;
                            if(GUI.Button(new Rect(5,row,330,30),(expanded?"▾ ":"▸ ")+family.Key+" ("+family.Count()+")",button))
                            {if(!expandedFamilies.Add(key))expandedFamilies.Remove(key);}
                            var ids=family.Select(entry=>entry.Key).ToArray();
                            var noneSold=ids.All(id=>draft.UnsellableCardEditions.Contains(id));var allSold=ids.All(id=>!draft.UnsellableCardEditions.Contains(id));
                            if(GUI.Button(new Rect(340,row,205,30),new GUIContent(T("Non vendues"),T("Les clients n’achètent pas les cartes de toute cette famille.")),new GUIStyle(noneSold?selectedButton:button){fontSize=12}))
                                foreach(var id in ids)if(!draft.UnsellableCardEditions.Contains(id))draft.UnsellableCardEditions.Add(id);
                            if(GUI.Button(new Rect(550,row,205,30),new GUIContent(T("Vendues"),T("Les clients achètent les cartes de toute cette famille.")),new GUIStyle(allSold?selectedButton:button){fontSize=12}))
                                draft.UnsellableCardEditions.RemoveAll(id=>ids.Contains(id));
                            row+=34;
                            if(CardsExpanded(family.Key))foreach(var entry in family.Where(e=>Matches(e.Value)))
                            {
                                var blocked=draft.UnsellableCardEditions.Contains(entry.Key);
                                var off=ModeRules.EditionDisabled(draft,entry.Key);
                                GUI.Label(new Rect(5,row,325,30),new GUIContent("    "+ContentLabel(entry.Value)+(off?T(" — désactivée"):""),off?F("{0} : édition désactivée dans « Boosters et boîtes », ses cartes ne sont pas vendues.",entry.Value):entry.Value),new GUIStyle(text){wordWrap=false,clipping=TextClipping.Clip});
                                if(GUI.Button(new Rect(340,row,205,30),T("Non vendues"),new GUIStyle(blocked?selectedButton:button){fontSize=12}) && !blocked)draft.UnsellableCardEditions.Add(entry.Key);
                                if(GUI.Button(new Rect(550,row,205,30),T("Vendues"),new GUIStyle(!blocked?selectedButton:button){fontSize=12}))draft.UnsellableCardEditions.Remove(entry.Key);
                                row+=34;
                            }
                        }
                        if(families.Length==0)GUI.Label(new Rect(5,row,750,60),configurationSearch!=""?T("Aucun résultat pour cette recherche."):T("Aucune édition détectée."),text);
                        return;
                    }
                    foreach(var family in families)
                    {
                        var expanded=Expanded(family.Key);
                        var key=configurationTab+":"+family.Key;
                        if(GUI.Button(new Rect(5,row,330,30),(expanded?"▾ ":"▸ ")+family.Key+" ("+family.Count()+")",button))
                        {if(!expandedFamilies.Add(key))expandedFamilies.Remove(key);}
                        var familyRules=family.Select(entry=>Rule(entry.Key)).ToArray();
                        var allDisabled=familyRules.All(rule=>!rule.Buy && !rule.Sell);
                        var allPurchase=familyRules.All(rule=>rule.Buy && !rule.Sell);
                        var allSales=familyRules.All(rule=>rule.Buy && rule.Sell);
                        var familyChoices=new[]{T("Désactivé"),T("Achat uniquement"),T("Achat et vente")};
                        var familyChoice=allDisabled?0:allPurchase?1:allSales?2:-1;
                        for(var choice=0;choice<familyChoices.Length;choice++)
                            if(GUI.Button(new Rect(340+choice*140,row,135,30),new GUIContent(familyChoices[choice],T("Appliquer à toute cette famille, y compris les entrées masquées par la recherche.")),new GUIStyle(choice==familyChoice?selectedButton:button){fontSize=12,wordWrap=true}))
                                foreach(var rule in familyRules){rule.Buy=choice!=0;rule.Sell=choice==2;}
                        row+=34;
                        if(expanded)foreach(var entry in family.Where(e=>Matches(e.Value)))
                        {Permission(row,"    "+ContentLabel(entry.Value),Rule(entry.Key));row+=34;}
                    }
                    if(families.Length==0)GUI.Label(new Rect(5,0,750,60),configurationSearch!=""?T("Aucun résultat pour cette recherche."):T("Aucun contenu détecté dans cette catégorie."),text);
                }
            }
            finally {GUI.EndScrollView();}
        }
        // One product: follow its category, or a choice of its own.
        private void DrawItemRule(int row,CatalogueEntry item,GUIStyle text,GUIStyle button,GUIStyle selectedButton)
        {
            var rule=draft.ItemRules.FirstOrDefault(r=>r.Id==item.Id);
            var label="        "+item.Name+(item.Level>1?F(" · niv. {0}",item.Level):"");
            GUI.Label(new Rect(5,row,330,30),new GUIContent(label,item.Name+" ("+item.Id+")"),new GUIStyle(text){wordWrap=false,clipping=TextClipping.Clip,fontSize=Math.Max(11,text.fontSize-1)});
            var choices=new[]{T("Comme la catégorie"),T("Désactivé"),T("Achat"),T("Achat et vente")};
            var current=rule==null?0:!rule.Buy && !rule.Sell?1:rule.Sell && rule.Buy?3:rule.Buy?2:-1;
            for(var choice=0;choice<choices.Length;choice++)
                if(GUI.Button(new Rect(340+choice*104,row,100,30),choices[choice],new GUIStyle(choice==current?selectedButton:button){fontSize=11,wordWrap=true}))
                {
                    draft.ItemRules.RemoveAll(r=>r.Id==item.Id);
                    if(choice>0)draft.ItemRules.Add(new EditionPermission{Id=item.Id,Buy=choice>=2,Sell=choice==3});
                }
        }
        // Furniture of the phone shop: allowed or locked for this shop.
        private void DrawFurnitureRules(CatalogueEntry[] furniture,GUIStyle text,GUIStyle button,GUIStyle selectedButton)
        {
            if(furniture.Length==0)
            {
                GUI.Label(new Rect(5,0,750,60),Catalogue.Furniture.Count==0?T("Lancez une partie une fois pour afficher la liste des meubles."):T("Aucun résultat pour cette recherche."),text);
                return;
            }
            var small=new GUIStyle(button){fontSize=12};
            GUI.Label(new Rect(5,0,330,30),(configurationSearch!=""?F("{0} meuble(s) trouvés",furniture.Length):F("{0} meuble(s)",furniture.Length)),text);
            if(GUI.Button(new Rect(340,0,205,30),new GUIContent(T("Tout bloquer"),T("Bloque tous les meubles affichés (ceux de la recherche).")),small))
                foreach(var f in furniture)if(!draft.LockedFurniture.Contains(f.Id))draft.LockedFurniture.Add(f.Id);
            if(GUI.Button(new Rect(550,0,205,30),new GUIContent(T("Tout autoriser"),T("Autorise tous les meubles affichés (ceux de la recherche).")),small))
                draft.LockedFurniture.RemoveAll(id=>furniture.Any(f=>f.Id==id));
            var row=34;
            foreach(var f in furniture)
            {
                var locked=draft.LockedFurniture.Contains(f.Id);
                GUI.Label(new Rect(5,row,330,30),new GUIContent("    "+f.Name+(f.Level>1?F(" · niv. {0}",f.Level):""),f.Name+" ("+f.Id+")"),new GUIStyle(text){wordWrap=false,clipping=TextClipping.Clip});
                if(GUI.Button(new Rect(340,row,205,30),T("Bloqué"),new GUIStyle(locked?selectedButton:button){fontSize=12}) && !locked)draft.LockedFurniture.Add(f.Id);
                if(GUI.Button(new Rect(550,row,205,30),T("Autorisé"),new GUIStyle(!locked?selectedButton:button){fontSize=12}))draft.LockedFurniture.Remove(f.Id);
                row+=34;
            }
        }
        private void InstallProductHooks()
        {
            harmony!.Patch(AccessTools.Method(Type("Customer"),"AttemptFindShelf"),prefix:new HarmonyMethod(typeof(Plugin),nameof(CustomerProductSearch)),finalizer:new HarmonyMethod(typeof(Plugin),nameof(CustomerProductSearchEnd)));
            Hook("ShelfManager","GetShelfToBuyItem",nameof(SelectProductShelf));
            Hook("Shelf","GetCustomerTargetItemCompartment",nameof(SelectProductCompartment));
        }
        private static void CustomerProductSearch(object __instance,out bool __state)
        {
            __state=selectingCustomerProducts;
            selectingCustomerProducts=instance?.active.Mode==GameMode.Custom;
            if(!selectingCustomerProducts)return;
            var targets=(IList)Read(__instance,"m_TargetBuyItemList")!;
            for(var i=targets.Count-1;i>=0;i--)
                if(!ModeRules.CanSell(instance!.active,targets[i]!.ToString()!))targets.RemoveAt(i);
        }
        private static void CustomerProductSearchEnd(bool __state)=>selectingCustomerProducts=__state;
        private static bool AllowedCompartment(object compartment)=>
            (int)Invoke(compartment,"GetItemCount")>0 && ModeRules.CanSell(instance!.active,Invoke(compartment,"GetItemType").ToString()!);
        private static object? Pick(List<object> candidates)=>candidates.Count==0?null:candidates[UnityEngine.Random.Range(0,candidates.Count)];
        private static bool SelectProductShelf(object __0,int __1,bool __2,ref object? __result)
        {
            if(!selectingCustomerProducts)return true;
            var targets=__0 as IList;
            var shelves=(IList)AccessTools.Method(Type("ShelfManager"),"GetShelfList").Invoke(null,null);
            var allowed=new List<object>();var preferred=new List<object>();
            foreach(object shelf in shelves)
            {
                if(!(bool)Invoke(shelf,"IsValidObject") || (bool)Read(shelf,"m_ItemNotForSale")!)continue;
                var compartments=((IList)Invoke(shelf,"GetItemCompartmentList")).Cast<object>().Where(AllowedCompartment).ToArray();
                if(compartments.Length==0)continue;
                allowed.Add(shelf);
                if(targets==null || targets.Count==0 || compartments.Any(c=>targets.Contains(Invoke(c,"GetItemType"))))preferred.Add(shelf);
            }
            __result=preferred.Count>0 && UnityEngine.Random.Range(0,100)<80+__1?Pick(preferred):__2 && preferred.Count==0?null:Pick(allowed);
            return false;
        }
        private static bool SelectProductCompartment(object __instance,object __0,ref object? __result)
        {
            if(!selectingCustomerProducts)return true;
            var targets=__0 as IList;
            var allowed=((IList)Invoke(__instance,"GetItemCompartmentList")).Cast<object>().Where(AllowedCompartment).ToList();
            var preferred=allowed.Where(c=>targets==null || targets.Count==0 || targets.Contains(Invoke(c,"GetItemType"))).ToList();
            __result=Pick(preferred.Count>0?preferred:allowed);
            return false;
        }
    }
}

