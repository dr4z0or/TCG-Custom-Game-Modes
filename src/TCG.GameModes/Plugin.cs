using System;
using System.IO;
using System.Linq;
using System.Collections;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using TCG.SaveManager.Core;
using TCG.GameModes.Core;
using static TCG.Shared.Lang;
namespace TCG.GameModes
{
    [BepInPlugin("local.tcg.gamemodes", "TCG Custom Game Modes", "1.0.0")]
    [BepInDependency("local.tcg.savemanager.diagnostics")]
    public sealed partial class Plugin : BaseUnityPlugin
    {
        private static Plugin? instance;
        private Harmony? harmony;
        private string campaigns="";
        private ModeSettings active=ModeSettings.Preset("",GameMode.Classic);
        private GameMode selected=GameMode.Classic;
        private ModeSettings draft=ModeSettings.Preset("",GameMode.Custom);
        private bool creating;
        private bool importTab;
        private string editingCampaign="",editingName="";
        public void OpenCampaignRules(object dialog,Campaign campaign)
        {
            if(!(bool)Invoke(dialog,"IsAtTitle"))throw new InvalidOperationException(T("Retournez au menu principal pour modifier les règles."));
            draft=ModePresets.Copy(ModeStore.Load(campaigns,campaign.Id));selected=draft.Mode;
            editingCampaign=campaign.Id;editingName=campaign.Name;importTab=false;configurationSearch="";productsScroll=Vector2.zero;
            Write(dialog,"createDialog",true);Write(dialog,"status",T("Les règles seront appliquées au prochain chargement de cette boutique."));
        }
        // A "Rules" button on each shop card of Save Manager, at the place Mod Profiles uses.
        private static void DrawRulesButtons(object __instance,Campaign? current)
        {
            var self=instance;
            if(self==null || !GUI.enabled)return;
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("local.tcg.modprofiles",out var profiles) && profiles.Instance!=null && profiles.Instance.enabled)return;
            try
            {
                var search=((string?)Read(__instance,"search")??"").Trim();
                var entries=((Campaign[]?)Read(__instance,"campaigns")??Array.Empty<Campaign>())
                    .Where(c=>search=="" || c.Name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                    .OrderByDescending(c=>c.Id==current?.Id).ThenBy(c=>c.Name,StringComparer.OrdinalIgnoreCase).ToArray();
                var scroll=(Vector2)(Read(__instance,"scroll")??Vector2.zero);
                var style=new GUIStyle((GUIStyle)Read(__instance,"quietButton")!){fontSize=12};
                GUI.BeginGroup(new Rect(28,218,920,344));
                try
                {
                    for(var i=0;i<entries.Length;i++)
                    {
                        var rect=new Rect(i%2*466+342-scroll.x,i/2*180+45-scroll.y,96,30);
                        if(rect.yMax<0 || rect.y>344)continue;
                        var campaign=entries[i];
                        if(GUI.Button(rect,T("Règles"),style))
                        {
                            try{self.OpenCampaignRules(__instance,campaign);}
                            catch(Exception ex){Write(__instance,"status",F("Règles indisponibles : {0}",ex.Message));}
                        }
                    }
                }
                finally{GUI.EndGroup();}
            }
            catch(Exception ex){self.Logger.LogWarning(ex.Message);}
        }
        private static object? Read(object obj,string name)=>AccessTools.Field(obj.GetType(),name).GetValue(obj);
        private static void Write(object obj,string name,object value)=>AccessTools.Field(obj.GetType(),name).SetValue(obj,value);
        private static object Invoke(object obj,string name,params object[] args)=>AccessTools.Method(obj.GetType(),name).Invoke(obj,args);
        private static Type Type(string name)=>AccessTools.TypeByName(name) ?? throw new MissingMemberException(name);
        private void Hook(string type,string method,string callback,bool postfix=false)
        {
            var target=AccessTools.Method(Type(type),method) ?? throw new MissingMethodException(type,method);
            var patch=new HarmonyMethod(typeof(Plugin),callback);
            harmony!.Patch(target,prefix:postfix?null:patch,postfix:postfix?patch:null);
        }
        private void Awake()
        {
            instance=this; campaigns=Path.Combine(Directory.GetParent(Application.persistentDataPath)!.FullName,"campaigns");
            harmony=new Harmony("local.tcg.gamemodes");
            try
            {
                Hook("TCG.SaveManager.Diagnostics.Plugin","DrawNameDialog",nameof(CreateDialog));
                Hook("TCG.SaveManager.Core.CampaignWorkspace","Create",nameof(Created),true);
                Hook("TCG.SaveManager.Core.CampaignWorkspace","PrepareLaunch",nameof(Prepared),true);
                Hook("Customer","DetermineShopAction",nameof(CustomerChoice));
                Hook("Customer","ThinkWantToBuyItem",nameof(BuyProduct));
                Hook("Customer","ThinkWantToBuyCard",nameof(BuyCard));
                Hook("Customer","TakeItemFromShelf",nameof(TakeProduct));
                Hook("Customer","ThinkWantToPlayTable",nameof(PlayTable));
                Hook("Customer","ThinkWantToGetCardFromBulkBox",nameof(BuyCard));
                Hook("RestockItemScreen","EvaluateSorting",nameof(FilterCatalogue));
                Hook("RestockItemScreen","AddToCartForCheckout",nameof(AddToCart));
                Hook("RestockItemScreen","SpawnPackageItemBox",nameof(SpawnProduct));
                Hook("ScannerRestockScreen","AddToCartForCheckout",nameof(AddToCart));
                Hook("RestockCheckoutItemBar","UpdateData",nameof(ScannerBar),true);
                foreach(var method in Type("InventoryBase").GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Where(m=>m.Name=="GetFurniturePurchaseData"))
                    harmony.Patch(method,postfix:new HarmonyMethod(typeof(Plugin),nameof(FurnitureData)));
                Hook("FurnitureShopPanelUI","Init",nameof(FurniturePanel),true);
                InstallEditionHooks();
                InstallBinderHooks();
                InstallTrackerHooks();
                InstallActivityHooks();
                InstallProductHooks();
                InstallCardEditionHooks();
                InstallTutorialHooks();
                InstallUnlockHooks();
                // Mod Profiles draws its own Rules button next to Mods; without it, Game Modes adds one.
                Hook("TCG.SaveManager.Diagnostics.Plugin","DrawCampaignScreen",nameof(DrawRulesButtons),true);
                LoadActive(); Logger.LogInfo("Modes de jeu : création et règles installées.");
            }
            catch(Exception ex){harmony.UnpatchSelf();enabled=false;Logger.LogError(ex);}
        }
        private void OnDestroy(){harmony?.UnpatchSelf();if(instance==this)instance=null;}
        private void LoadActive()
        {
            var workspace=new CampaignWorkspace(Application.persistentDataPath);
            active=workspace.Active==null?ModeSettings.Preset("",GameMode.Classic):ModeStore.Load(campaigns,workspace.Active.CampaignId);
            Logger.LogInfo("Mode actif : "+active.Mode+" / "+active.CampaignId);
        }
        private static void Prepared(){instance!.LoadActive();instance.ArmStarterDisplay();}
        private static void Created(Campaign __result)
        {
            if(!instance!.creating)return;
            var settings=instance.selected==GameMode.Custom?instance.draft:ModeSettings.Preset(__result.Id,instance.selected);
            settings.CampaignId=__result.Id;ModeStore.Create(instance.campaigns,settings);
        }
        private static bool Restricted=>instance!=null && instance.active.Mode!=GameMode.Classic;
        private static void CustomerChoice(object __instance)
        {
            if(Restricted && !ModeRules.CanSellProduct(instance!.active))Write(__instance,"m_LookingForCardOnly",true);
        }
        private static bool BuyProduct(object __instance)
        {
            if(!Restricted || ModeRules.CanSellProduct(instance!.active))return true;
            Invoke(__instance,ModeRules.CanSellCard(instance.active)?"ThinkWantToBuyCard":"ExitShop");return false;
        }
        private static bool BuyCard(object __instance)
        {
            if(!Restricted || ModeRules.CanSellCard(instance!.active))return true;
            Invoke(__instance,ModeRules.CanSellProduct(instance.active)?"ThinkWantToBuyItem":"ExitShop");return false;
        }
        private static bool TakeProduct(object __instance)
        {
            if(!BuyProduct(__instance))return false;
            if(!Restricted)return true;
            var compartment=Read(__instance,"m_CurrentItemCompartment");
            if(compartment==null || ModeRules.CanSell(instance!.active,Invoke(compartment,"GetItemType").ToString()!))return true;
            var hasBag=((IList)Read(__instance,"m_ItemInBagList")!).Count>0 || ((IList)Read(__instance,"m_CardInBagList")!).Count>0;
            Invoke(__instance,hasBag?"ThinkWantToPay":ModeRules.CanSellCard(instance.active)?"ThinkWantToBuyCard":"ExitShop");
            return false;
        }
        private static object Restock(int index)=>AccessTools.Method(Type("InventoryBase"),"GetRestockData",new[]{typeof(int)}).Invoke(null,new object[]{index});
        private static bool AllowedIndex(int index)=>!Restricted || ModeRules.CanBuy(instance!.active,Read(Restock(index),"itemType")!.ToString()!);
        private static bool AddToCart(int __0)=>__0>=0 && AllowedIndex(__0);
        private static bool PlayTable(object __instance)
        {
            if(instance?.active.Mode!=GameMode.CardsOnly && !(instance?.active.Mode==GameMode.Custom && !instance.active.AllowTableGames))return true;
            Invoke(__instance,"ThinkWantToBuyCard");return false;
        }
        private static void ScannerBar(object __instance)
        {
            if(Restricted && !ModeRules.CanBuy(instance!.active,Invoke(__instance,"GetItemType").ToString()!)) ((Component)__instance).gameObject.SetActive(false);
        }
        private static bool SpawnProduct(object __0)=>!Restricted || ModeRules.CanBuy(instance!.active,__0.ToString()!);
        private static void FilterCatalogue(object __instance)
        {
            if(!Restricted)return;
            var indices=(IList)Read(__instance,"m_CurrentRestockDataIndexList")!;
            for(var i=indices.Count-1;i>=0;i--)if(!AllowedIndex((int)indices[i]))indices.RemoveAt(i);
        }
        private static bool CardFurniture(object data)=>new[]{"CardShelf","VintageCardTable","DisplayCardTableA"}.Contains(Read(data,"objectType")!.ToString());
        private static void FurnitureData(ref object __result)
        {
            if(__result==null || !Restricted || !instance!.active.UnlockCardTablesAtStart || !CardFurniture(__result))return;
            __result=AccessTools.Method(typeof(object),"MemberwiseClone").Invoke(__result,null);
            Write(__result,"levelRequirement",0);
        }
        private static void FurniturePanel(object __instance,int __1)
        {
            if(!Restricted || !instance!.active.UnlockCardTablesAtStart)return;
            var data=AccessTools.Method(Type("InventoryBase"),"GetFurniturePurchaseData",new[]{typeof(int)}).Invoke(null,new object[]{__1});
            if(!CardFurniture(data))return;
            Write(__instance,"m_LevelRequired",0);
            foreach(var field in new[]{"m_CompanyTitle","m_UIGrp"})((GameObject)Read(__instance,field)!).SetActive(true);
            foreach(var field in new[]{"m_LockPurchaseBtn","m_PrologueUIGrp"})((GameObject)Read(__instance,field)!).SetActive(false);
            ((Component)Read(__instance,"m_LevelRequirementText")!).gameObject.SetActive(false);
        }
        private static bool CreateDialog(object __instance)
        {
            if(!(bool)Read(__instance,"createDialog")!)return true;
            var self=instance!;
            var card=(GUIStyle)Read(__instance,"cardStyle")!;var text=(GUIStyle)Read(__instance,"bodyStyle")!;
            var button=(GUIStyle)Read(__instance,"quietButton")!;var gold=(GUIStyle)Read(__instance,"goldButton")!;
            var old=GUI.color;GUI.color=new Color(0,0,0,.72f);GUI.DrawTexture(new Rect(0,0,1000,700),Texture2D.whiteTexture);GUI.color=old;
            GUI.Box(new Rect(80,20,840,660),GUIContent.none,card);
            GUI.Label(new Rect(105,40,790,40),self.editingCampaign!=""?F("Règles : {0}",self.editingName):self.importTab?T("Importer une sauvegarde"):T("Une nouvelle boutique"),(GUIStyle)Read(__instance,"nameStyle")!);
            if(self.editingCampaign!="")GUI.Label(new Rect(105,88,790,42),T("Modifiez les règles puis enregistrez. Les meubles déjà installés sont conservés."),text);
            else if(!self.importTab)Write(__instance,"newName",GUI.TextField(new Rect(105,88,790,42),(string)Read(__instance,"newName")!,80,(GUIStyle)Read(__instance,"fieldStyle")!));
            foreach(var mode in new[]{GameMode.Classic,GameMode.CardsOnly,GameMode.Custom})
            {
                var label=mode==GameMode.Classic?T("Classique"):mode==GameMode.CardsOnly?T("Cartes uniquement"):T("Personnalisé");
                if(GUI.Button(new Rect(105+(int)mode*200,145,190,44),label,new GUIStyle(!self.importTab && self.selected==mode?gold:button){fontSize=14,wordWrap=true})){self.selected=mode;self.importTab=false;}
            }
            if(self.editingCampaign=="" && GUI.Button(new Rect(705,145,190,44),T("Importer"),self.importTab?gold:button))self.importTab=true;
            if(self.importTab)
            {
                GUI.Label(new Rect(205,290,590,130),T("Reprendre une sauvegarde du jeu d'origine, conservée avant l'installation du gestionnaire de sauvegardes. Choisissez ensuite l'emplacement à importer et vérifiez son aperçu."),text);
                GUI.Label(new Rect(205,420,590,85),T("L'import crée une nouvelle boutique à partir d'une copie. La sauvegarde source est conservée. Si elle utilisait déjà des mods, ceux-ci peuvent être nécessaires pour la charger correctement."),text);
                if(GUI.Button(new Rect(105,630,170,40),T("Annuler"),button)){Write(__instance,"createDialog",false);self.importTab=false;}
                if(GUI.Button(new Rect(295,630,600,40),T("Choisir la sauvegarde à importer"),gold))
                {
                    try {Invoke(__instance,"OpenImportDialog");self.importTab=false;}
                    catch(Exception ex){Write(__instance,"status",F("Import impossible : {0}",ex.InnerException?.Message ?? ex.Message));self.Logger.LogError(ex);}
                }
                GUI.Label(new Rect(205,540,590,60),(string)Read(__instance,"status")!,text);
                return false;
            }
            var toggle=new GUIStyle(GUI.skin.toggle) { fontSize=text.fontSize, fontStyle=FontStyle.Normal };
            foreach(var state in new[]{toggle.normal,toggle.hover,toggle.active,toggle.focused,toggle.onNormal,toggle.onHover,toggle.onActive,toggle.onFocused})state.textColor=text.normal.textColor;
            if(self.selected==GameMode.Custom)
            {
                self.DrawProductPermissions(text,toggle,button,gold,(GUIStyle)Read(__instance,"fieldStyle")!);
                self.DrawPresets(__instance,text,button);
            }
            else GUI.Label(new Rect(205,290,590,110),self.selected==GameMode.Classic?T("Les règles habituelles du jeu."):T("Achetez et ouvrez des boosters. Les clients achètent uniquement vos cartes individuelles. Les tables de vente sont accessibles dès le départ et le désodorisant reste commandable. Pour choisir les éditions vendues, partez du modèle « Cartes à l’unité » en Personnalisé."),text);
            GUI.Label(new Rect(105,607,790,22),(string)Read(__instance,"status")!,text);
            if(GUI.Button(new Rect(105,630,170,40),T("Annuler"),button)){Write(__instance,"createDialog",false);self.selected=GameMode.Classic;self.editingCampaign="";}
            if(GUI.Button(new Rect(295,630,600,40),self.editingCampaign!=""?T("Enregistrer les règles"):T("Créer ma boutique"),gold))
            {
                try
                {
                    if(self.editingCampaign!="")
                    {
                        var settings=ModePresets.Copy(self.draft);
                        if(self.selected==GameMode.CardsOnly){settings.AllowProductSales=false;settings.AllowAccessoryPurchases=false;settings.UnlockCardTablesAtStart=true;}
                        else if(self.selected==GameMode.Classic)settings=ModeSettings.Preset(self.editingCampaign,self.selected);
                        settings.CampaignId=self.editingCampaign;settings.Mode=self.selected;ModeStore.Update(self.campaigns,settings);
                        // 0.4.1 : the shop already prepared in the game's slots can also be resumed with the
                        // game's own Continue button, which does not go through Save Manager: apply its rules now.
                        if(((CampaignWorkspace)Read(__instance,"workspace")!).Active?.CampaignId==self.editingCampaign)self.LoadActive();
                        self.editingCampaign="";self.selected=GameMode.Classic;Write(__instance,"createDialog",false);Invoke(__instance,"Refresh");
                        Write(__instance,"status",T("Règles enregistrées. Elles seront appliquées au prochain chargement."));return false;
                    }
                    var workspace=(CampaignWorkspace)Read(__instance,"workspace")!;workspace.ArchiveCurrent();
                    self.creating=true;try{workspace.Create((string)Read(__instance,"newName")!);}finally{self.creating=false;}
                    Invoke(__instance,"Refresh");Write(__instance,"createDialog",false);Write(__instance,"status",T("Boutique créée. Choisissez Commencer cette boutique."));self.selected=GameMode.Classic;
                }
                catch(Exception ex){self.Logger.LogError(ex);Write(__instance,"status",F("Création impossible : {0}",ex.Message));}
            }
            return false;
        }
    }
}



