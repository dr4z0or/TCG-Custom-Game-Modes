using UnityEngine;
using TCG.GameModes.Core;

using static TCG.Shared.Lang;
namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private string activityMessage="";
        private float activityMessageUntil;
        private static bool CustomActivity=>instance?.active.Mode==GameMode.Custom;
        private static bool RefuseActivity(string message)
        {
            instance!.activityMessage=message;
            instance.activityMessageUntil=Time.realtimeSinceStartup+5f;
            return false;
        }
        private void InstallActivityHooks()
        {
            Hook("UI_PhoneScreen","OnPressHireWorkerBtn",nameof(RecruitmentAllowed));
            Hook("HireWorkerPanelUI","OnPressHireButton",nameof(RecruitmentAllowed));
            Hook("CPlayerData","SetIsWorkerHired",nameof(HireAllowed));
            Hook("UI_PhoneScreen","OnPressHostTournamentBtn",nameof(TournamentAllowed));
            Hook("HostTournamentScreen","OnPressConfirm",nameof(TournamentAllowed));
            Hook("InteractableCashierCounter","CanTradeCard",nameof(TradingAllowed),true);
            Hook("ShelfManager","GetCashierCounterToTradeCard",nameof(TradingCounter),true);
        }
        private static bool RecruitmentAllowed()=>!CustomActivity || instance!.active.AllowEmployees || RefuseActivity(T("Les employés sont désactivés pour cette boutique."));
        private static bool HireAllowed(bool __1)=>!__1 || RecruitmentAllowed();
        private static bool TournamentAllowed()=>!CustomActivity || instance!.active.AllowTournaments || RefuseActivity(T("Les tournois sont désactivés pour cette boutique."));
        private static void TradingAllowed(ref bool __result){if(CustomActivity && !instance!.active.AllowCardTrading)__result=false;}
        private static void TradingCounter(ref object? __result){if(CustomActivity && !instance!.active.AllowCardTrading)__result=null;}
        private void OnGUI()
        {
            if(Time.realtimeSinceStartup>=activityMessageUntil)return;
            var style=new GUIStyle(GUI.skin.box){fontSize=18,wordWrap=true};
            GUI.Box(new Rect(Mathf.Max(10,(Screen.width-560)/2),Screen.height-110,560,70),activityMessage,style);
        }
    }
}
