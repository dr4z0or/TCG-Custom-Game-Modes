using System;
using System.Linq;
using UnityEngine;
using TCG.GameModes.Core;
using static TCG.Shared.Lang;
namespace TCG.GameModes
{
    public sealed partial class Plugin
    {
        private string presetName="";
        private ModeSettings[] savedPresets=Array.Empty<ModeSettings>();
        private Vector2 presetScroll;
        private bool presetsLoaded;
        private void DrawPresets(object dialog,GUIStyle text,GUIStyle button)
        {
            try
            {
                if(!presetsLoaded){savedPresets=ModePresets.Load(campaigns);presetsLoaded=true;}
                GUI.Label(new Rect(105,525,145,30),T("Nom du preset"),text);
                presetName=GUI.TextField(new Rect(255,525,300,32),presetName,60,(GUIStyle)Read(dialog,"fieldStyle")!);
                var existing=savedPresets.FirstOrDefault(p=>string.Equals(p.PresetName,presetName.Trim(),StringComparison.OrdinalIgnoreCase));
                var enabled=GUI.enabled;GUI.enabled=enabled && existing!=null;
                if(GUI.Button(new Rect(765,525,130,32),new GUIContent(T("Supprimer"),T("Retire ce preset de la liste. Les boutiques déjà créées gardent leurs règles.")),button))
                {
                    if(ModePresets.Delete(campaigns,presetName)){savedPresets=ModePresets.Load(campaigns);Write(dialog,"status",F("Preset « {0} » supprimé. Les boutiques existantes ne changent pas.",presetName.Trim()));presetName="";}
                }
                GUI.enabled=enabled;
                if(GUI.Button(new Rect(565,525,190,32),existing!=null?T("Mettre à jour"):T("Enregistrer"),button))
                {
                    foreach(var id in ModeRules.ContentNames.Keys)if(!draft.Contents.Any(c=>c.Id==id))draft.Contents.Add(new EditionPermission{Id=id,Buy=draft.AllowNewContent,Sell=draft.AllowNewContent});
                    foreach(var id in ModeRules.EditionNames.Keys)if(!draft.Editions.Any(c=>c.Id==id))draft.Editions.Add(new EditionPermission{Id=id,Buy=draft.AllowNewContent,Sell=draft.AllowNewContent});
                    draft.AllowNewContent=false;
                    ModePresets.Save(campaigns,presetName,draft);draft.PresetName=presetName.Trim();savedPresets=ModePresets.Load(campaigns);Write(dialog,"status",T("Preset enregistré. Cliquez sur son nom pour le réutiliser."));}
                // Ready-made models first, then the player's own presets.
                var models=ModePresets.BuiltIn();
                var all=models.Select(p=>new{Preset=p,Model=true}).Concat(savedPresets.Select(p=>new{Preset=p,Model=false})).ToArray();
                var small=new GUIStyle(button){fontSize=12,wordWrap=false,clipping=TextClipping.Clip};
                var model=new GUIStyle(small);model.normal.textColor=new Color(.05f,.4f,.65f);model.hover.textColor=model.normal.textColor;
                presetScroll=GUI.BeginScrollView(new Rect(105,562,790,42),presetScroll,new Rect(0,0,Math.Max(765,all.Length*160),28));
                try
                {
                    for(var i=0;i<all.Length;i++)
                    {
                        var entry=all[i];
                        var label=entry.Model?F("Modèle · {0}",ModePresets.PresetLabel(entry.Preset.PresetName)):entry.Preset.PresetName;
                        if(GUI.Button(new Rect(i*160,0,152,28),new GUIContent(label,entry.Model?T("Point de départ fourni : ajustez puis enregistrez sous votre propre nom."):T("Preset enregistré")),entry.Model?model:small))
                        {
                            draft=ModePresets.Copy(entry.Preset);draft.CampaignId="";
                            presetName=entry.Model?"":draft.PresetName;
                            if(entry.Model)draft.PresetName="";
                            Write(dialog,"status",entry.Model?F("Modèle « {0} » appliqué. Consultez l’onglet Résumé.",ModePresets.PresetLabel(entry.Preset.PresetName)):F("Preset « {0} » appliqué.",entry.Preset.PresetName));
                        }
                    }
                }
                finally {GUI.EndScrollView();}
            }
            catch(Exception ex){Write(dialog,"status",F("Preset : {0}",ex.Message));}
        }
    }
}
