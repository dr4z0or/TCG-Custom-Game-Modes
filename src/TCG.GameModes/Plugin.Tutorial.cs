using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TCG.GameModes.Core;

using static TCG.Shared.Lang;
namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private sealed class TutorialOriginal
        {
            public object Data=null!;
            public float Maximum;
            public bool Adapted;
            public Dictionary<Component,string> Texts=new Dictionary<Component,string>();
        }
        private readonly Dictionary<object,TutorialOriginal> tutorialOriginals=new Dictionary<object,TutorialOriginal>();
        private static bool cardPriceEvent;
        private static bool CardsTutorial=>instance?.active.Mode==GameMode.CardsOnly;

        private void InstallTutorialHooks()
        {
            Hook("TutorialManager","OnGameDataFinishLoaded",nameof(PrepareTutorial));
            Hook("TutorialSubGroup","OpenScreen",nameof(TutorialText),true);
            Hook("TutorialSubGroup","IsTaskFinish",nameof(TutorialFinished),true);
            Hook("TutorialManager","AddTaskValue",nameof(TutorialEvent));
            var confirm=AccessTools.Method(Type("SetItemPriceScreen"),"OnPressConfirm");
            harmony!.Patch(confirm,prefix:new HarmonyMethod(typeof(Plugin),nameof(CardPriceBeforeConfirm)),postfix:new HarmonyMethod(typeof(Plugin),nameof(CardPriceConfirmed)));
        }

        private static void PrepareTutorial(object __instance)
        {
            var owner=instance!;
            foreach(object group in (IList)Read(__instance,"m_TutorialSubGroupList")!)
            {
                if(!owner.tutorialOriginals.TryGetValue(group,out var original))
                {
                    original=new TutorialOriginal {Data=Read(group,"m_TutorialData")!,Maximum=(float)Read(group,"m_MaxValue")!};
                    owner.tutorialOriginals.Add(group,original);
                }
                if(!CardsTutorial && !original.Adapted)continue;
                foreach(var text in original.Texts)
                    if(text.Key!=null)AccessTools.Property(text.Key.GetType(),"text").SetValue(text.Key,text.Value,null);
                // Each subgroup owns its copy. Native save progress is replayed after this prefix.
                var data=AccessTools.Method(typeof(object),"MemberwiseClone").Invoke(original.Data,null);
                var condition=Read(data,"tutorialTaskCondition")!.ToString();
                var mapped=CardsTutorial && condition=="PickupBox"?"OpenPack":
                    CardsTutorial && condition=="PutItemOnShelf"?"PutCardOnShelf":condition;
                Write(data,"tutorialTaskCondition",Enum.Parse(Type("ETutorialTaskCondition"),mapped!));
                Write(data,"value",0f);
                Write(group,"m_TutorialData",data);
                Write(group,"m_CurrentValue",0f);
                Write(group,"m_IsTaskFinish",false);
                Write(group,"m_MaxValue",CardsTutorial && (condition=="PickupBox" || condition=="PutItemOnShelf" || condition=="SetItemPrice")?1f:original.Maximum);
                original.Adapted=CardsTutorial;
            }
            if(CardsTutorial)Write(__instance,"m_FinishedTutorial",false);
        }

        private static void TutorialFinished(object __instance,ref bool __result)
        {
            if(!CardsTutorial && !(instance?.active.Mode==GameMode.Custom && !instance.active.AllowTableGames))return;
            var condition=Read(Read(__instance,"m_TutorialData")!,"tutorialTaskCondition")!.ToString();
            // Cards-only shops use their starter display and do not host table games.
            if(condition=="BuyPlayTable" || condition=="CustomerPlay" || CardsTutorial && condition=="BuyCardShelf")__result=true;
        }

        private static void TutorialText(object __instance)
        {
            if(!CardsTutorial || !instance!.tutorialOriginals.TryGetValue(__instance,out var original))return;
            var condition=Read(original.Data,"tutorialTaskCondition")!.ToString();
            string? message=condition=="RestockItem"?T("Achetez des boosters depuis votre téléphone."):
                condition=="PickupBox"?T("Récupérez la livraison, prenez un booster en main et ouvrez-le pour obtenir des cartes."):
                condition=="PutItemOnShelf"?T("Ouvrez votre album et placez une carte sur le présentoir de cartes."):
                condition=="SetItemPrice"?T("Fixez le prix d'une carte à l'aide de son étiquette sur le présentoir."):null;
            if(message==null)return;
            var number=Read(__instance,"m_NumberText");
            var root=(GameObject)Read(__instance,"m_ScreenGrp")!;
            foreach(var text in root.GetComponentsInChildren(Type("TMPro.TextMeshProUGUI"),true))
            {
                if(ReferenceEquals(text,number))continue;
                var property=AccessTools.Property(text.GetType(),"text");
                var previous=(string)property.GetValue(text,null);
                // Keep headings and numeric counters: only replace the task description.
                if(string.IsNullOrWhiteSpace(previous) || previous.Length<25)continue;
                if(!original.Texts.ContainsKey(text))original.Texts.Add(text,previous);
                property.SetValue(text,message,null);
            }
        }

        private static bool TutorialEvent(object __0)=>!CardsTutorial || __0.ToString()!="SetItemPrice" || cardPriceEvent;

        private static void CardPriceBeforeConfirm(object __instance,out bool __state)
        {
            __state=CardsTutorial && (bool)Read(__instance,"m_IsSetCardPrice")! && Read(__instance,"m_CardData")!=null;
        }

        private static void CardPriceConfirmed(bool __state)
        {
            if(!__state)return;
            // Confirming a card's price also counts when the chosen market price is unchanged.
            cardPriceEvent=true;
            try {AccessTools.Method(Type("TutorialManager"),"AddTaskValue").Invoke(null,new[]{Enum.Parse(Type("ETutorialTaskCondition"),"SetItemPrice"),(object)1f});}
            finally {cardPriceEvent=false;}
        }
    }
}
