using System;
using System.IO;
using System.Linq;
using TCG.GameModes.Core;
// The checked texts are the French ones.
System.AppDomain.CurrentDomain.SetData("TCG.Language", "fr");
void Check(bool value,string text){if(!value)throw new Exception(text);Console.WriteLine("OK : "+text);}
var id=Guid.NewGuid().ToString("N");var cards=ModeSettings.Preset(id,GameMode.CardsOnly);
Check(ModeRules.CanBuy(cards,"BasicCardPack") && ModeRules.CanBuy(cards,"DestinyLegendaryCardBox"),"boosters et cartons autorisés");
Check(!ModeRules.CanBuy(cards,"Toy_PiggyA") && !ModeRules.CanBuy(cards,"Playmat1") && !ModeRules.CanBuy(cards,"FutureUnknownPack"),"accessoires et types inconnus refusés en cartes uniquement");
Check(!ModeRules.CanSellProduct(cards) && ModeRules.CanSellCard(cards),"achats de boosters distincts des ventes client");
var custom=ModeSettings.Preset(id,GameMode.Custom);custom.AllowProductSales=false;
Check(ModeRules.CanBuy(custom,"BasicCardPack") && !ModeRules.CanSellProduct(custom),"autorisations personnalisées indépendantes");
var detailed=ModeSettings.Preset(id,GameMode.Custom);ModeRules.AddCategories(detailed);
var dice=detailed.Products.Find(p=>p.Category==ProductCategory.Dice)!;dice.Buy=false;
Check(!ModeRules.CanBuy(detailed,"D20DiceBox2") && ModeRules.CanSell(detailed,"D20DiceBox2"),"dés : achats interdits, ventes autorisées");
var packs=detailed.Products.Find(p=>p.Category==ProductCategory.Boosters)!;packs.Sell=false;
Check(ModeRules.CanBuy(detailed,"BasicCardPack") && !ModeRules.CanSell(detailed,"BasicCardPack") && ModeRules.CanSell(detailed,"BasicCardBox"),"boosters ouverts distincts des boîtes et des achats");
Check(ModeRules.Classify("PlayMat18")==ProductCategory.Playmats && ModeRules.Classify("UP_Playmat4")==ProductCategory.Playmats && ModeRules.Classify("Toy_PiggyEvo")==ProductCategory.Figurines && ModeRules.Classify("UP_Toploader")==ProductCategory.Sleeves,"catégories : variantes de noms et accessoires identifiés");
var legacy=ModeSettings.Preset(id,GameMode.Custom);legacy.AllowAccessoryPurchases=false;
Check(!ModeRules.CanBuy(legacy,"D20DiceBox") && ModeRules.CanBuy(legacy,"BasicCardBox") && ModeRules.CanSell(legacy,"Toy_PiggyA"),"anciens réglages sans catégories conservés");
var root=Path.Combine(Path.GetTempPath(),"TCG-game-modes-"+Guid.NewGuid().ToString("N"));var campaign=Path.Combine(root,id);
Directory.CreateDirectory(campaign);File.WriteAllText(Path.Combine(root,".tcg-save-lab"),"fixture");File.WriteAllText(Path.Combine(campaign,"campaign.xml"),"fixture");
Check(ModeStore.Load(root,id).Mode==GameMode.Classic,"boutique existante classique par défaut");
ModeStore.Create(root,cards);Check(ModeStore.Load(root,id).Mode==GameMode.CardsOnly,"mode conservé par boutique");
try{ModeStore.Create(root,custom);throw new Exception("Écrasement accepté");}catch(IOException){}
Check(ModeStore.Load(root,id).Mode==GameMode.CardsOnly,"choix existant protégé contre écrasement");
File.Delete(Path.Combine(campaign,"game-mode.xml"));ModeStore.Create(root,detailed);
var loaded=ModeStore.Load(root,id);
Check(!ModeRules.CanBuy(loaded,"D20DiceBox2") && ModeRules.CanSell(loaded,"D20DiceBox2") && !ModeRules.CanSell(loaded,"BasicCardPack"),"autorisations par catégorie conservées par boutique");
Check(ModeRules.Classify("Deodorant")==ProductCategory.Hygiene && ModeRules.Classify("BulkBox_TetramonDestinyGradeUG")==ProductCategory.BulkCards,"hygiène et cartes en vrac identifiées");
var oldCategories=ModeSettings.Preset(id,GameMode.Custom);oldCategories.AllowShopSupplies=false;
oldCategories.Products.Add(new ProductPermission{Category=ProductCategory.Other,Buy=false,Sell=false});
Check(!ModeRules.CanBuy(oldCategories,"Deodorant") && !ModeRules.CanSell(oldCategories,"BulkBox_TetramonBase"),"anciens choix Autres produits respectés après séparation");
ModeRules.AddCategories(oldCategories);
Check(!ModeRules.CanBuy(oldCategories,"Deodorant") && !ModeRules.CanSell(oldCategories,"BulkBox_TetramonBase"),"ajout des catégories conserve les anciennes interdictions");
loaded.AllowEmployees=false;loaded.AllowTableGames=false;loaded.AllowCardTrading=false;loaded.AllowTournaments=false;
File.Delete(Path.Combine(campaign,"game-mode.xml"));ModeStore.Create(root,loaded);
var activities=ModeStore.Load(root,id);
Check(!activities.AllowEmployees && !activities.AllowTableGames && !activities.AllowCardTrading && !activities.AllowTournaments,"options d'activités conservées par boutique");
ModeRules.RegisterNativeEditions();
ModeRules.RegisterItem("PokemonTestPack",ProductCategory.Boosters,"epl:Test Set","Test Set");
ModeRules.RegisterItem("PokemonTestBox",ProductCategory.BoosterBoxes,spawnedPack:"PokemonTestPack");
ModeRules.RegisterItem("PokemonTestFigure",ProductCategory.Figurines);
var editionMode=ModeSettings.Preset(id,GameMode.Custom);
editionMode.Editions.Add(new EditionPermission{Id="epl:Test Set",Buy=false,Sell=true});
Check(!ModeRules.CanBuy(editionMode,"PokemonTestPack") && !ModeRules.CanBuy(editionMode,"PokemonTestBox") && ModeRules.CanSell(editionMode,"PokemonTestPack"),"édition mod : achats interdits, ventes conservées, packs et boîtes associés");
Check(ModeRules.CanBuy(cards,"PokemonTestPack") && ModeRules.CanBuy(cards,"PokemonTestBox") && !ModeRules.CanBuy(cards,"PokemonTestFigure"),"Cartes uniquement reconnaît les boosters ajoutés par un mod");
editionMode.Editions[0].Buy=true;editionMode.Editions[0].Sell=false;
Check(ModeRules.CanBuy(editionMode,"PokemonTestPack") && !ModeRules.CanSell(editionMode,"PokemonTestPack"),"édition : booster commandable pour ouvrir sans vente du booster fermé");
editionMode.Editions.Add(new EditionPermission{Id="native:Destiny",Buy=false,Sell=false});
Check(!ModeRules.CanBuy(editionMode,"DestinyRareCardBox") && ModeRules.CanBuy(editionMode,"RareCardBox"),"éditions natives Tetramon et Destiny indépendantes");
ModeRules.AddCategories(editionMode);editionMode.Products.Find(p=>p.Category==ProductCategory.Boosters)!.Buy=false;
Check(!ModeRules.CanBuy(editionMode,"PokemonTestPack") && ModeRules.CanBuy(editionMode,"PokemonTestBox"),"catégorie interdite prioritaire sur autorisation de l'édition");
File.Delete(Path.Combine(campaign,"game-mode.xml"));ModeStore.Create(root,editionMode);
var editionLoaded=ModeStore.Load(root,id);
Check(editionLoaded.Editions.Count==2 && !ModeRules.CanBuy(editionLoaded,"DestinyBasicCardPack") && !ModeRules.CanSell(editionLoaded,"PokemonTestBox"),"choix par édition conservés par boutique");
foreach(var permission in detailed.Products)permission.Sell=false;
Check(!ModeRules.CanSellProduct(detailed) && ModeRules.CanSellCard(detailed),"toutes ventes de produits interdites, cartes individuelles indépendantes");
Check(ModeRules.CanBuy(cards,"BasicCardBox") && !ModeRules.CanSell(cards,"BasicCardBox"),"mode Cartes uniquement conservé");
// 0.3.0 : produits réglés un par un, éditions désactivées, meubles bloqués, catalogue.
var perItem=ModeSettings.Preset(id,GameMode.Custom);ModeRules.AddCategories(perItem);
perItem.Products.Find(p=>p.Category==ProductCategory.Dice)!.Buy=false;
perItem.ItemRules.Add(new EditionPermission{Id="D20DiceBox2",Buy=true,Sell=false});
perItem.ItemRules.Add(new EditionPermission{Id="Toy_PiggyA",Buy=false,Sell=false});
Check(ModeRules.CanBuy(perItem,"D20DiceBox2") && !ModeRules.CanSell(perItem,"D20DiceBox2") && !ModeRules.CanBuy(perItem,"D20DiceBox"),"produit réglé à part prioritaire sur sa catégorie");
Check(!ModeRules.CanBuy(perItem,"Toy_PiggyA") && !ModeRules.CanSell(perItem,"Toy_PiggyA") && ModeRules.CanBuy(perItem,"Toy_PiggyB"),"produit désactivé seul, les autres de la catégorie restent permis");
var classicItem=ModeSettings.Preset(id,GameMode.Classic);classicItem.ItemRules.Add(new EditionPermission{Id="Toy_PiggyA",Buy=false,Sell=false});
Check(ModeRules.CanBuy(classicItem,"Toy_PiggyA"),"mode Classique ignore les réglages par produit");
var offEdition=ModeSettings.Preset(id,GameMode.Custom);
offEdition.Editions.Add(new EditionPermission{Id="epl:Test Set",Buy=false,Sell=false});
Check(ModeRules.EditionDisabled(offEdition,"epl:Test Set") && !ModeRules.CanSellCardEdition(offEdition,"Test Set") && ModeRules.RestrictsCardEditions(offEdition),"édition désactivée : cartes à l'unité non vendues");
offEdition.Editions[0].Buy=true;
Check(!ModeRules.EditionDisabled(offEdition,"epl:Test Set") && ModeRules.CanSellCardEdition(offEdition,"Test Set"),"édition en achat seul : cartes toujours vendues");
var furniture=ModeSettings.Preset(id,GameMode.Custom);furniture.LockedFurniture.Add("PlayTable");
Check(!ModeRules.CanBuyFurniture(furniture,"PlayTable") && ModeRules.CanBuyFurniture(furniture,"CardShelf") && ModeRules.CanBuyFurniture(cards,"PlayTable"),"meubles bloqués par boutique, mode Personnalisé uniquement");
File.Delete(Path.Combine(campaign,"game-mode.xml"));ModeStore.Create(root,furniture);
Check(ModeStore.Load(root,id).LockedFurniture.SequenceEqual(new[]{"PlayTable"}),"meubles bloqués conservés par boutique");
Check(ModePresets.Copy(perItem).ItemRules.Count==2,"copie d'un preset avec ses réglages par produit");
Check(ModeSummary.Describe(furniture).Any(l=>l.Contains("Meubles bloqués : 1")) && ModeSummary.Describe(perItem).Any(l=>l.Contains("réglés un par un : 2")),"résumé : meubles et produits réglés");
var stored=GameCatalogue.Merge(root,new[]{new CatalogueEntry{Id="BasicCardPack",Name="Basic Card Pack",Level=1}},new[]{new CatalogueEntry{Id="PlayTable",Name="Play Table",Level=5}});
GameCatalogue.Merge(root,new[]{new CatalogueEntry{Id="RareCardPack",Name="Rare Card Pack",Level=3}},Array.Empty<CatalogueEntry>());
var catalogue=GameCatalogue.Load(root);
Check(catalogue.Items.Count==2 && catalogue.Furniture.Single().Name=="Play Table","catalogue : entrées des sessions précédentes conservées");
Check(GameCatalogue.Load(Path.Combine(root,"absent")).IsEmpty,"catalogue absent : liste vide sans erreur");
if(args.Length>0)
{
    using var fixture=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[0]));
    var packRows=fixture.RootElement.EnumerateArray().ToArray();
    var editions=new System.Collections.Generic.HashSet<string>();
    foreach(var row in packRows)
    {
        var edition=row.GetProperty("Edition").GetString()??"";
        if(edition!="")editions.Add("epl:"+edition);
        ModeRules.RegisterItem(row.GetProperty("Item").GetString()!,row.GetProperty("Box").GetBoolean()?ProductCategory.BoosterBoxes:ProductCategory.Boosters,edition==""?"":"epl:"+edition,edition,row.GetProperty("Spawns").ValueKind==System.Text.Json.JsonValueKind.String?row.GetProperty("Spawns").GetString()!:"");
    }
    var restricted=ModeSettings.Preset(id,GameMode.Custom);
    foreach(var edition in editions)restricted.Editions.Add(new EditionPermission{Id=edition,Buy=false,Sell=true});
    Check(packRows.All(row=>!ModeRules.CanBuy(restricted,row.GetProperty("Item").GetString()!) && ModeRules.CanSell(restricted,row.GetProperty("Item").GetString()!)),"pack réel : tous les boosters et leurs boîtes suivent les éditions interdites à l'achat");
    Check(packRows.All(row=>ModeRules.CanBuy(cards,row.GetProperty("Item").GetString()!)),"pack réel : boosters et boîtes reconnus en Cartes uniquement");
}
var pokemon=ModeSettings.Preset(id,GameMode.Custom);pokemon.AllowNativeProducts=false;
Check(!ModeRules.CanBuy(pokemon,"BasicCardPack") && !ModeRules.CanSell(pokemon,"Toy_PiggyA"),"preset : produits vanilla interdits à l’achat et à la vente");
ModeRules.RegisterItem("PokemonTestDice",ProductCategory.Dice);
Check(ModeRules.CanBuy(pokemon,"PokemonTestDice"),"preset : produits ajoutés conservés");
pokemon.AllowNewContent=false;
ModeRules.RegisterItem("KnownPokemon",ProductCategory.Boosters,"epl:Known","Pokémon — Known","","content:pokemon","Pokémon");
pokemon.Contents.Add(new EditionPermission{Id="content:pokemon"});pokemon.Editions.Add(new EditionPermission{Id="epl:Known"});
ModeRules.RegisterItem("FutureYugioh",ProductCategory.Boosters,"epl:YuGiOh","Yu-Gi-Oh!","","content:yugioh","Yu-Gi-Oh!");
Check(ModeRules.CanBuy(pokemon,"KnownPokemon") && !ModeRules.CanBuy(pokemon,"FutureYugioh") && !ModeRules.CanSell(pokemon,"FutureYugioh"),"preset fermé : Pokémon conservé, nouveau pack Yu-Gi-Oh interdit");
pokemon.Contents[0].Buy=false;
Check(!ModeRules.CanBuy(pokemon,"KnownPokemon") && ModeRules.CanSell(pokemon,"KnownPokemon"),"pack : choix achats et ventes indépendants");
ModePresets.Save(root,"100% Pokémon",pokemon);
var saved=ModePresets.Load(root).Single();var cloned=ModePresets.Copy(saved);cloned.AllowNativeProducts=true;
Check(!ModePresets.Load(root).Single().AllowNativeProducts && saved.PresetName=="100% Pokémon","preset persistant et copie indépendante");
ModePresets.Save(root,"100% Pokémon",cloned);
Check(ModePresets.Load(root).Length==1 && ModePresets.Load(root).Single().AllowNativeProducts,"mise à jour du preset sans doublon");
Directory.Delete(Path.Combine(root,"mode-presets"),true);
var edited=ModePresets.Copy(ModeStore.Load(root,id));edited.AllowEmployees=false;
ModeStore.Update(root,edited);
Check(!ModeStore.Load(root,id).AllowEmployees && File.Exists(Path.Combine(campaign,"game-mode.xml.previous")),"règles existantes modifiées avec copie des anciens réglages");
File.Delete(Path.Combine(campaign,"game-mode.xml.previous"));
File.Delete(Path.Combine(campaign,"game-mode.xml"));File.Delete(Path.Combine(campaign,"campaign.xml"));Directory.Delete(campaign);File.Delete(Path.Combine(root,".tcg-save-lab"));File.Delete(Path.Combine(root,GameCatalogue.FileName));Directory.Delete(root);

if(args.Length>1)
{
    var definitions=Directory.EnumerateFiles(args[1],"*.json",SearchOption.AllDirectories).Where(p=>p.IndexOf("_prefabloader"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)>=0).Select(TCG.GameModes.PackDefinitions.Read).ToArray();
    var items=definitions.SelectMany(d=>d.Items).ToArray();
    Check(items.Where(i=>i.IsCardPack && i.CardExpansion!="").All(i=>!string.IsNullOrWhiteSpace(i.PhoneAppId)),"association boutique présente pour tous les boosters du pack réel");
    var expansionNames=items.Where(i=>i.IsCardPack && i.CardExpansion!="").Select(i=>i.CardExpansion).Distinct().ToArray();
    Check(definitions.Length>=55 && items.Length>61 && expansionNames.Length==32,"lecture des fichiers réels : "+definitions.Length+" descripteurs, "+items.Length+" produits, "+expansionNames.Length+" éditions");
    Check(definitions.SelectMany(d=>d.CardExpansions).Any(e=>e.MenuCategory=="Pokémon Pocket" && e.CardExpansion=="Shining Revelry"),"nom de famille et édition Pokémon lus dans le descripteur réel");
}

var preserved=ModeSettings.Preset("",GameMode.Custom);preserved.AllowNewContent=false;preserved.AllowNativeProducts=false;
ModeRules.RegisterItem("Space-TimePack",ProductCategory.Boosters,"epl:Space-Time","Pokémon — Space-Time","","content:original","Pokémon");
ModeRules.RegisterItem("PreservedBox",ProductCategory.BoosterBoxes,"","","Space-TimePack","content:original","Pokémon");
preserved.Contents.Add(new EditionPermission{Id="content:original"});preserved.Editions.Add(new EditionPermission{Id="epl:Space-Time"});
Check(ModeRules.PreserveRegisteredItem("Space_TimePack") && ModeRules.CanBuy(preserved,"Space_TimePack"),"nom normalisé runtime conserve édition, source et choix du fichier original");
Check(ModeRules.PreserveRegisteredItem("PreservedBox") && ModeRules.CanBuy(preserved,"PreservedBox"),"boîte conserve édition du booster, sans remplacement par _Invalid");
Check(!ModeRules.PreserveRegisteredItem("UnknownRuntimePack"),"nom runtime inconnu reste soumis à sa détection normale");

Check(!ModeRules.CanBrowseEdition(preserved,"Tetramon") && !ModeRules.CanBrowseEdition(preserved,"Destiny"),"classeur : éditions natives masquées lorsque désactivées");
Check(ModeRules.CanBrowseEdition(preserved,"Space_Time"),"classeur : édition mod avec nom runtime normalisé disponible");
preserved.Editions[0].Buy=false;preserved.Editions[0].Sell=false;
Check(!ModeRules.CanBrowseEdition(preserved,"Space_Time"),"classeur : édition désactivée masquée");
Check(ModeRules.CanBrowseEdition(ModeSettings.Preset("",GameMode.Classic),"Tetramon"),"classeur classique conservé");

var cardsFiltered=ModeSettings.Preset("",GameMode.CardsOnly);cardsFiltered.AllowNativeProducts=false;
ModeRules.RegisterItem("AllowedCardsPack",ProductCategory.Boosters,"epl:AllowedCards","Allowed Cards","","content:allowedcards","Allowed");
Check(!ModeRules.CanBuy(cardsFiltered,"BasicCardPack") && ModeRules.CanBuy(cardsFiltered,"AllowedCardsPack"),"Cartes uniquement conserve les exclusions de contenus");

var hygieneException=ModeSettings.Preset("",GameMode.Custom);hygieneException.AllowNativeProducts=false;
hygieneException.NativeProductExceptions.Add(new ProductPermission{Category=ProductCategory.Hygiene,Buy=true,Sell=false});
Check(ModeRules.CanBuy(hygieneException,"Deodorant") && !ModeRules.CanSell(hygieneException,"Deodorant") && !ModeRules.CanBuy(hygieneException,"BasicCardPack") && !ModeRules.CanBuy(hygieneException,"Toy_PiggyA"),"exception hygiène : achat seul sans réactiver boosters ou figurines natifs");

// 0.2.0 : fournitures toujours achetables.
var cardsSupplies=ModeSettings.Preset("",GameMode.CardsOnly);
Check(ModeRules.CanBuy(cardsSupplies,"Deodorant") && !ModeRules.CanBuy(cardsSupplies,"Toy_PiggyA") && !ModeRules.CanSellProduct(cardsSupplies),"Cartes uniquement : désodorisant achetable, autres accessoires toujours refusés");
var strictSupplies=ModeSettings.Preset("",GameMode.Custom);strictSupplies.AllowNativeProducts=false;
Check(ModeRules.CanBuy(strictSupplies,"Deodorant") && !ModeRules.CanBuy(strictSupplies,"BasicCardPack"),"produits d’origine bloqués : fournitures encore achetables");
strictSupplies.AllowShopSupplies=false;
Check(!ModeRules.CanBuy(strictSupplies,"Deodorant"),"option fournitures désactivée : règles des produits appliquées");
ModeRules.RegisterItem("ModSprayCan",ProductCategory.Hygiene,"","","","content:spray","Spray");
Check(ModeRules.CanBuy(ModeSettings.Preset("",GameMode.CardsOnly),"ModSprayCan"),"spray ajouté par un mod reconnu comme fourniture");
// Dossier dédié aux tests 0.2.0 : le dossier initial est supprimé plus haut.
var root2=Path.Combine(Path.GetTempPath(),"TCG-game-modes-02-"+Guid.NewGuid().ToString("N"));var campaign2=Path.Combine(root2,id);
Directory.CreateDirectory(campaign2);File.WriteAllText(Path.Combine(root2,".tcg-save-lab"),"fixture");File.WriteAllText(Path.Combine(campaign2,"campaign.xml"),"fixture");
var legacyFile=Path.Combine(campaign2,"game-mode.xml");
File.WriteAllText(legacyFile,"<?xml version=\"1.0\"?><ModeSettings><SchemaVersion>1</SchemaVersion><CampaignId>"+id+"</CampaignId><Mode>CardsOnly</Mode></ModeSettings>");
var legacyLoaded=ModeStore.Load(root2,id);
Check(legacyLoaded.AllowShopSupplies && legacyLoaded.UnsellableCardEditions.Count==0 && ModeRules.CanBuy(legacyLoaded,"Deodorant"),"ancienne boutique sans réglage : désodorisant achetable, toutes éditions vendues");

// 0.2.0 : éditions des cartes à l’unité.
var singles=ModeSettings.Preset("",GameMode.Custom);
Check(ModeRules.CanSellCardEdition(singles,"Tetramon") && ModeRules.CanSellCardEdition(singles,"Space_Time") && !ModeRules.RestrictsCardEditions(singles),"par défaut : cartes de toutes les éditions vendues");
singles.UnsellableCardEditions.Add("native:Tetramon");singles.UnsellableCardEditions.Add("epl:Space-Time");
Check(!ModeRules.CanSellCardEdition(singles,"Tetramon") && ModeRules.CanSellCardEdition(singles,"Destiny") && !ModeRules.CanSellCardEdition(singles,"Space_Time") && ModeRules.RestrictsCardEditions(singles),"éditions retirées : cartes non vendues, nom runtime normalisé reconnu");
Check(ModeRules.EditionOf("Space_Time")=="epl:Space-Time" && ModeRules.EditionOf("Ghost")=="native:Ghost","identifiant d’édition depuis le nom du jeu");
singles.AllowSingleCardSales=false;
Check(!ModeRules.CanSellCardEdition(singles,"Destiny") && !ModeRules.RestrictsCardEditions(singles),"vente de cartes désactivée : aucune édition vendue");
Check(ModeRules.CanSellCardEdition(ModeSettings.Preset("",GameMode.Classic),"Tetramon"),"classique : toutes les cartes vendues");
var editionsFile=Path.Combine(campaign2,"game-mode.xml");File.Delete(editionsFile);
var savedSingles=ModeSettings.Preset(id,GameMode.Custom);savedSingles.UnsellableCardEditions.Add("epl:Base Set");ModeStore.Create(root2,savedSingles);
Check(ModeStore.Load(root2,id).UnsellableCardEditions.SequenceEqual(new[]{"epl:Base Set"}),"éditions non vendues conservées par boutique");

// 0.2.0 : modèles, suppression et résumé.
var builtIn=ModePresets.BuiltIn();
Check(builtIn.Length==4 && builtIn.All(p=>p.Mode==GameMode.Custom && p.PresetName!=""),"quatre modèles fournis");
var unit=builtIn.First(p=>p.PresetName=="Cartes à l’unité");
Check(ModeRules.CanBuy(unit,"BasicCardPack") && !ModeRules.CanBuy(unit,"Toy_PiggyA") && ModeRules.CanBuy(unit,"Deodorant") && !ModeRules.CanSellProduct(unit) && ModeRules.CanSellCard(unit),"modèle Cartes à l’unité équivalent à Cartes uniquement, désodorisant inclus");
var sealedOnly=builtIn.First(p=>p.PresetName=="Scellé uniquement");
Check(!ModeRules.CanSellCard(sealedOnly) && ModeRules.CanSell(sealedOnly,"BasicCardPack"),"modèle Scellé uniquement : boosters vendus, pas de cartes à l’unité");
try{ModePresets.Save(root2,"cartes à l’unité",unit);throw new Exception("nom réservé accepté");}catch(IOException){Console.WriteLine("OK : nom de modèle réservé");}
var unitCopy=ModePresets.Copy(unit);unitCopy.CampaignId=Guid.NewGuid().ToString("N");
ModePresets.Save(root2,"À supprimer",unitCopy);
Check(ModePresets.Load(root2).Any(p=>p.PresetName=="À supprimer") && ModePresets.Delete(root2,"à supprimer") && !ModePresets.Load(root2).Any(p=>p.PresetName=="À supprimer"),"preset supprimé (fichier conservé en .deleted)");
Check(Directory.GetFiles(Path.Combine(root2,"mode-presets"),"*.deleted").Length==1 && !ModePresets.Delete(root2,"inexistant"),"suppression récupérable, preset inconnu ignoré");
var summary=ModeSummary.Describe(singles.AllowSingleCardSales?singles:unit);
Check(summary.Any(l=>l.StartsWith("Achats : ")) && summary.Any(l=>l.Contains("désodorisant")) && summary.Any(l=>l.StartsWith("Cartes à l’unité")),"résumé lisible des règles");
var summarySingles=ModeSettings.Preset("",GameMode.Custom);summarySingles.UnsellableCardEditions.Add("epl:Space-Time");summarySingles.AllowEmployees=false;
var described=ModeSummary.Describe(summarySingles);
Check(described.Any(l=>l.Contains("sauf 1 édition") && l.Contains("Space-Time")) && described.Any(l=>l.Contains("employés")),"résumé : éditions non vendues et activités désactivées nommées");
Check(ModeSummary.Describe(ModeSettings.Preset("",GameMode.Classic)).Length==1,"résumé classique en une ligne");
