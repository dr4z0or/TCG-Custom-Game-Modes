using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using static TCG.Shared.Lang;
namespace TCG.GameModes.Core
{
    public static class ModePresets
    {
        public static ModeSettings Copy(ModeSettings settings)
        {
            var serializer=new XmlSerializer(typeof(ModeSettings));
            using(var stream=new MemoryStream()) { serializer.Serialize(stream,settings);stream.Position=0;return (ModeSettings)serializer.Deserialize(stream)!; }
        }
        private static string Folder(string campaigns)
        {
            if(!Path.IsPathRooted(campaigns) || !File.Exists(Path.Combine(campaigns,".tcg-save-lab")))throw new IOException(T("Espace de boutiques invalide."));
            var folder=Path.Combine(campaigns,"mode-presets");
            for(var d=new DirectoryInfo(folder);d!=null;d=d.Parent)if(d.Exists && (d.Attributes & FileAttributes.ReparsePoint)!=0)throw new IOException(T("Lien refusé."));
            return folder;
        }
        public static ModeSettings[] Load(string campaigns)
        {
            var folder=Folder(campaigns);if(!Directory.Exists(folder))return Array.Empty<ModeSettings>();
            return Directory.GetFiles(folder,"*.xml").Select(path=> {
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                {var value=(ModeSettings)new XmlSerializer(typeof(ModeSettings)).Deserialize(reader)!;if(!Guid.TryParseExact(value.CampaignId,"N",out _) || value.Mode!=GameMode.Custom || value.SchemaVersion!=1 || string.IsNullOrWhiteSpace(value.PresetName))throw new IOException(T("Preset invalide."));return value;}
            }).OrderBy(p=>p.PresetName,StringComparer.OrdinalIgnoreCase).ToArray();
        }
        // Ready-made starting points shown before the player's own presets. Never written to disk.
        public static ModeSettings[] BuiltIn()
        {
            ModeSettings Make(string name,Action<ModeSettings> change)
            {
                var value=ModeSettings.Preset("",GameMode.Custom);value.PresetName=name;ModeRules.AddCategories(value);change(value);return value;
            }
            return new[]
            {
                Make("Cartes à l’unité",s=>
                {
                    s.AllowProductSales=false;s.AllowAccessoryPurchases=false;s.UnlockCardTablesAtStart=true;s.AllowSingleCardSales=true;
                    foreach(var p in s.Products){p.Buy=p.Category==ProductCategory.Boosters || p.Category==ProductCategory.BoosterBoxes;p.Sell=false;}
                }),
                Make("Scellé uniquement",s=>
                {
                    s.AllowSingleCardSales=false;
                    foreach(var p in s.Products){p.Buy=true;p.Sell=p.Category!=ProductCategory.BulkCards;}
                }),
                Make("Sans employés",s=>s.AllowEmployees=false),
                Make("Boutique calme",s=>{s.AllowTableGames=false;s.AllowTournaments=false;s.AllowCardTrading=false;}),
            };
        }
        // Built-in names are stored in French (they identify the template); this is their displayed name.
        public static string PresetLabel(string name)=>
            name=="Cartes à l’unité"?T("Cartes à l’unité"):name=="Scellé uniquement"?T("Scellé uniquement"):
            name=="Sans employés"?T("Sans employés"):name=="Boutique calme"?T("Boutique calme"):name;
        public static bool IsBuiltIn(string name)=>BuiltIn().Any(p=>string.Equals(p.PresetName,name.Trim(),StringComparison.OrdinalIgnoreCase) || string.Equals(PresetLabel(p.PresetName),name.Trim(),StringComparison.OrdinalIgnoreCase));
        // The file is kept with a .deleted suffix: presets are small and a mistake stays recoverable by hand.
        public static bool Delete(string campaigns,string name)
        {
            var folder=Folder(campaigns);if(!Directory.Exists(folder))return false;
            var preset=Load(campaigns).FirstOrDefault(p=>string.Equals(p.PresetName,name.Trim(),StringComparison.OrdinalIgnoreCase));
            if(preset==null)return false;
            var path=Path.Combine(folder,preset.CampaignId+".xml");
            var target=path+"."+DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+".deleted";
            File.Move(path,target);
            return true;
        }
        public static void Save(string campaigns,string name,ModeSettings settings)
        {
            name=name.Trim();if(name.Length==0 || name.Length>60)throw new IOException(T("Nom de preset requis (60 caractères maximum)."));
            if(IsBuiltIn(name))throw new IOException(T("Ce nom est réservé à un modèle fourni. Choisissez un autre nom."));
            var folder=Folder(campaigns);Directory.CreateDirectory(folder);
            var previous=Load(campaigns).FirstOrDefault(p=>string.Equals(p.PresetName,name,StringComparison.OrdinalIgnoreCase));
            var value=Copy(settings);value.Mode=GameMode.Custom;value.PresetName=name;value.CampaignId=previous?.CampaignId??Guid.NewGuid().ToString("N");
            var path=Path.Combine(folder,value.CampaignId+".xml");var temporary=path+".pending";
            try {using(var writer=XmlWriter.Create(temporary))new XmlSerializer(typeof(ModeSettings)).Serialize(writer,value);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
            finally {if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
    // Plain-language summary of a rule set, shown in the "Résumé" tab.
    public static class ModeSummary
    {
        private static string Join(System.Collections.Generic.IEnumerable<string> items,string empty)
        {
            var list=items.ToList();
            return list.Count==0?empty:string.Join(", ",list);
        }
        public static string[] Describe(ModeSettings s)
        {
            if(s.Mode==GameMode.Classic)return new[]{T("Mode Classique : les règles habituelles du jeu.")};
            var lines=new System.Collections.Generic.List<string>();
            if(s.Mode==GameMode.CardsOnly)
            {
                lines.Add(T("Mode Cartes uniquement : boosters et boîtes à l’achat, les clients achètent vos cartes individuelles."));
                lines.Add(s.AllowShopSupplies?T("Fournitures (désodorisant) : toujours achetables."):T("Fournitures (désodorisant) : bloquées."));
                return lines.ToArray();
            }
            ModeRules.AddCategories(s);
            lines.Add(s.PresetName!=""?F("Mode Personnalisé · preset « {0} ».",ModePresets.PresetLabel(s.PresetName)):T("Mode Personnalisé."));
            var buy=s.Products.Where(p=>p.Buy && (p.Category==ProductCategory.Boosters || p.Category==ProductCategory.BoosterBoxes?s.AllowBoosterPurchases:s.AllowAccessoryPurchases)).Select(p=>ModeRules.CategoryName(p.Category));
            lines.Add(F("Achats : {0}.",Join(buy,T("aucun produit"))));
            var sell=ModeRules.CanSellProduct(s)?s.Products.Where(p=>p.Sell).Select(p=>ModeRules.CategoryName(p.Category)):Enumerable.Empty<string>();
            lines.Add(F("Ventes aux clients : {0}.",Join(sell,T("aucun produit"))));
            if(!s.AllowNativeProducts)
            {
                var exceptions=s.NativeProductExceptions.Where(e=>e.Buy || e.Sell).Select(e=>ModeRules.CategoryName(e.Category));
                lines.Add(exceptions.Any()?F("Produits du jeu d’origine : bloqués sauf {0}.",Join(exceptions,"")):T("Produits du jeu d’origine : bloqués."));
            }
            if(!s.AllowModProducts)lines.Add(T("Produits ajoutés par les mods : bloqués."));
            lines.Add(s.AllowShopSupplies?T("Fournitures (désodorisant) : toujours achetables."):T("Fournitures (désodorisant) : soumises aux règles des produits."));
            if(!s.AllowSingleCardSales)lines.Add(T("Cartes à l’unité : les clients n’en achètent pas."));
            else if(s.UnsellableCardEditions.Count==0)lines.Add(T("Cartes à l’unité : vendues, toutes éditions."));
            else
            {
                var names=s.UnsellableCardEditions.Select(id=>ModeRules.EditionNames.TryGetValue(id,out var n)?n:id).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase).ToList();
                lines.Add(F("Cartes à l’unité : vendues sauf {0} édition(s) : {1}.",names.Count,string.Join(", ",names.Take(6))+(names.Count>6?"…":"")));
            }
            var editions=ModeRules.EditionNames.Keys.ToList();
            if(editions.Count>0)
            {
                int Allowed(string id){var rule=s.Editions.FirstOrDefault(e=>e.Id==id);return rule==null?(s.AllowNewContent?1:0):rule.Buy?1:0;}
                lines.Add(F("Éditions de boosters achetables : {0} sur {1}.",editions.Sum(Allowed),editions.Count));
                var disabled=editions.Count(id=>ModeRules.EditionDisabled(s,id));
                if(disabled>0)lines.Add(F("Éditions désactivées (ni boosters, ni cartes à l’unité) : {0}.",disabled));
            }
            var contents=ModeRules.ContentNames.Keys.ToList();
            if(contents.Count>0)
            {
                int Allowed(string id){var rule=s.Contents.FirstOrDefault(e=>e.Id==id);return rule==null?(s.AllowNewContent?1:0):rule.Buy?1:0;}
                lines.Add(F("Packs de contenu achetables : {0} sur {1}.",contents.Sum(Allowed),contents.Count));
            }
            if(s.ItemRules.Count>0)
            {
                var blocked=s.ItemRules.Count(r=>!r.Buy && !r.Sell);
                lines.Add(blocked>0?F("Produits réglés un par un : {0} (dont {1} désactivé(s)).",s.ItemRules.Count,blocked):F("Produits réglés un par un : {0}.",s.ItemRules.Count));
            }
            if(s.LockedFurniture.Count>0)lines.Add(F("Meubles bloqués : {0}.",s.LockedFurniture.Count));
            var off=new System.Collections.Generic.List<string>();
            if(!s.AllowEmployees)off.Add(T("employés"));if(!s.AllowTableGames)off.Add(T("parties aux tables"));if(!s.AllowCardTrading)off.Add(T("échanges"));if(!s.AllowTournaments)off.Add(T("tournois"));
            lines.Add(off.Count==0?T("Activités : toutes autorisées."):F("Activités désactivées : {0}.",string.Join(", ",off)));
            lines.Add(s.AllowNewContent?T("Futurs packs et éditions : autorisés automatiquement."):T("Futurs packs et éditions : bloqués jusqu’à votre choix."));
            return lines.ToArray();
        }
    }
}
