using System;
using System.Collections;
using TCG.GameModes.Core;
using TCG.SaveManager.Core;
using UnityEngine;
using HarmonyLib;
namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private string starterCampaign="";
        private float starterDeadline, starterReadyAfter;
        private void ArmStarterDisplay()
        {
            starterCampaign="";
            var workspace=new CampaignWorkspace(Application.persistentDataPath);
            if(active.Mode!=GameMode.CardsOnly || workspace.Active?.LoadedKind!="new")return;
            starterCampaign=active.CampaignId;starterReadyAfter=0;starterDeadline=Time.realtimeSinceStartup+45;
        }
        private void Update()
        {
            if(starterCampaign=="")return;
            if(starterCampaign!=active.CampaignId || Time.realtimeSinceStartup>starterDeadline){starterCampaign="";return;}
            var game=UnityEngine.Object.FindObjectOfType(Type("CGameManager"));
            if(game==null || !(bool)Read(game,"m_IsGameLevel")! || !(bool)Read(game,"m_InitLoaded")!){starterReadyAfter=0;return;}
            if(starterReadyAfter==0){starterReadyAfter=Time.realtimeSinceStartup+.5f;return;}
            if(Time.realtimeSinceStartup<starterReadyAfter)return;
            var manager=UnityEngine.Object.FindObjectOfType(Type("ShelfManager"));
            if(manager==null)return;
            starterCampaign="";
            try
            {
                var cards=(IList)AccessTools.Method(Type("ShelfManager"),"GetCardShelfList").Invoke(null,null);
                var shelves=(IList)AccessTools.Method(Type("ShelfManager"),"GetShelfList").Invoke(null,null);
                if(cards.Count!=0 || shelves.Count!=1){Logger.LogInfo("Présentoir de départ : mobilier existant conservé.");return;}
                var shelf=(Component)shelves[0];
                var compartments=(IList)Invoke(shelf,"GetItemCompartmentList");
                foreach(var compartment in compartments)
                    if((int)Invoke(compartment!,"GetItemCount")!=0){Logger.LogInfo("Présentoir de départ : étagère contenant des produits conservée.");return;}
                if((bool)Read(shelf,"m_IsBoxedUp")!){Logger.LogInfo("Présentoir de départ : étagère emballée conservée.");return;}
                var displayType=Enum.Parse(Type("EObjectType"),"CardShelf");
                var spawn=AccessTools.Method(Type("ShelfManager"),"SpawnInteractableObject");
                var display=(Component)spawn.Invoke(null,new[]{displayType});
                try
                {
                    display.transform.position=shelf.transform.position;display.transform.rotation=shelf.transform.rotation;
                    Invoke(display,"Init");
                }
                catch {Invoke(display,"OnDestroyed");throw;}
                Invoke(shelf,"OnDestroyed");
                // Native bookkeeping; no disk save and no changes to unrelated objects.
                Invoke(manager,"SaveInteractableObjectData",false);
                Logger.LogInfo("Cartes uniquement : étagère de départ remplacée par un présentoir de cartes.");
            }
            catch(Exception ex){Logger.LogError("Présentoir de départ : "+ex);}
        }
    }
}
