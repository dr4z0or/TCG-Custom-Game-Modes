# TCG Custom Game Modes — prototype 0.1

Plugin séparé TCG.GameModes.dll, indépendant des DLL stables Save Manager. Le mode se choisit lors de la création de la boutique dans le dialogue du gestionnaire : Classique, Cartes uniquement, Personnalisé. La configuration game-mode.xml reste propre à la boutique ; les boutiques existantes et les imports sans configuration sont Classique.

## Test dans la copie isolée

1. Lancer Lancer-test-extensions.cmd puis ouvrir Charger la partie et Nouvelle boutique.
2. Saisir un nom, choisir Cartes uniquement, puis Créer ma boutique et Commencer cette boutique.
3. Vérifier le catalogue fournisseur : boosters/cartons accessibles, accessoires et licences associées masqués.
4. Vérifier le catalogue de meubles : tables de vente de cartes accessibles sans niveau préalable. Elles gardent leur prix normal.
5. Acheter et ouvrir des boosters, disposer les cartes sur une table, ouvrir la boutique et vérifier les achats clients.
6. Revenir dans une boutique Classique pour vérifier que les catalogues et clients retrouvent les règles habituelles.

Le mode Personnalisé propose un tableau avec autorisations indépendantes Achats / Ventes : boosters, boîtes de boosters, figurines, dés, tapis de jeu, decks préconstruits, pochettes et protections, albums et classeurs, boîtes de rangement, jeux de société, mangas et autres produits. Les cartes individuelles ont une option de vente séparée ; elles s'obtiennent en ouvrant des boosters. Le carton de livraison n'est pas une catégorie de produit. Les anciens réglages des boutiques restent respectés, sans réécriture de leur fichier de mode. Économie, expérience et frais restent inchangés.

Les clients du mode Personnalisé ne recherchent et ne prennent que des produits dont la vente est autorisée. Les sélections d'étagères et de compartiments sont filtrées uniquement pendant la recherche d'un client ; les employés conservent leur réapprovisionnement habituel. Le contrôle des ventes est indépendant du catalogue d'achat et couvre aussi les produits déjà présents en rayon.

Test conseillé : créer une boutique Personnalisé, garder Achats des boosters sur Oui et mettre leurs Ventes sur Non ; les boosters restent commandables pour les ouvrir, mais les clients ne les achètent pas. Mettre Achats des dés sur Non pour vérifier leur absence du catalogue. Faire défiler le tableau pour les autres catégories. Le tri par édition n'est pas implémenté ; les produits ajoutés par des mods utilisant un type natif suivent sa catégorie, les types supplémentaires non reconnus relèvent d'Autres produits en Personnalisé.

## Intégration

Le plugin complète le dialogue de création et observe CampaignWorkspace.Create/PrepareLaunch en mémoire avec Harmony. Il filtre les listes du catalogue avant tri et contrôle l'ajout au panier et la livraison, y compris le réapprovisionnement au scanner. Les clients dont les achats de produits sont interdits sont orientés vers les cartes et évitent la pénalité native pour produits absents. En Cartes uniquement, les intentions de jeu aux tables sont également orientées vers les cartes.

Les données de meubles nécessaires aux tables de vente sont copiées avant de réduire leur exigence de niveau, pour préserver les assets communs lorsque l'on revient à une boutique Classique. Aucun fichier natif du jeu ou sauvegarde originale n'est modifié.

Le plugin de modes est indispensable dans les profils de mods lorsqu'il est installé : une sélection de mods ne doit pas pouvoir contourner les règles de la boutique. Les versions de laboratoire de Mod Profiles et de son préchargeur prennent cette protection en charge.

## Validation et limites

### Éditions de boosters (11 octobre 2026)

Dans Personnalisé, faire défiler les produits puis ouvrir **Éditions de boosters**. Les colonnes Achats / Ventes s'appliquent aux boosters et aux boîtes de boosters de l'édition. Une catégorie interdite garde priorité sur une édition autorisée. Les cartes individuelles sont indépendantes de ces choix. Les choix sont enregistrés par identifiant d'édition dans game-mode.xml ; les boutiques anciennes sans cette liste conservent leurs autorisations.

Les éditions natives sont disponibles, et les définitions JSON d'Enhanced Prefab Loader installé et actif fournissent ses produits/éditions. Le traitement ItemProcessor confirme leurs catégories à l'exécution. Les boîtes sans CardExpansion suivent SpawnsPackType pour retrouver leur édition. Les nouveaux boosters EPL sont ainsi reconnus en Cartes uniquement. Les types ajoutés par d'autres chargeurs restent à intégrer.

La boutique EPL filtre sa liste après tri ; Purchase Overhaul filtre les résultats du catalogue, l'éligibilité aux achats et licences, et l'ajout/modification du panier. Sa validation de commande utilise l'éligibilité du produit. Les filtres de ventes clients existants utilisent aussi la nouvelle classification et les autorisations d'édition.

Compilation sans avertissement, tests de persistance et d'indépendance des permissions réussis. Tests sur les 61 produits boosters/boîtes des définitions réelles du pack Pokémon réussis. Les hooks et le parcours interactif de cette nouvelle version restent à vérifier au prochain lancement de Lancer-test-pokemon.cmd ; la partie utilisateur ouverte n'a pas été interrompue.

Test : créer une nouvelle boutique Personnalisé, laisser les catégories Boosters / Boîtes de boosters autorisées, puis mettre Base Set en Achats Non / Ventes Oui dans Éditions de boosters. Vérifier Base Set et sa boîte absents du catalogue EPL et de Purchase Overhaul tandis que Jungle reste commandable. Autre choix possible : Base Set Achats Oui / Ventes Non, pour pouvoir ouvrir ses boosters sans vendre les boosters fermés.

Compilation sans avertissement, tests d'achats/ventes et stockage réussis. Installation de tous les hooks confirmée au lancement avec Shop C et Upstairs. Le dialogue, catalogues, commandes, clients et changement de boutique doivent encore être vérifiés en mode interactif. Les modes ne sont pas encore distribués pour Steam. Les catalogues ajoutés par d'autres mods et les objets de vente supplémentaires ne sont pas tous couverts par ce prototype.

Dans une nouvelle partie Cartes uniquement, le prototype remplace désormais l'unique étagère de départ vide par un présentoir natif de cartes au même emplacement. Il conserve le mobilier déjà garni ou modifié. Changement compilé, encore à vérifier visuellement sur une nouvelle boutique ; aucun remplacement dans la partie actuellement ouverte.

Le tutoriel Cartes uniquement suit désormais : acheter des boosters, récupérer la livraison et ouvrir un booster, placer une carte sur le présentoir depuis l'album, confirmer son prix, puis ouvrir la boutique. Les étapes de tables de jeu et d'achat du présentoir sont ignorées dans ce mode. Les autres objectifs natifs, dont le passage en caisse, sont conservés. Le parcours Classique est conservé et les conditions des groupes de tutoriel sont copiées avant adaptation. La progression utilise les événements natifs d'ouverture et de pose de cartes ; la confirmation du prix d'une carte valide l'objectif de prix, même si le montant reste identique.

Validation du 11 octobre 2026 : compilation sans avertissement, tests des règles réussis et installation des hooks confirmée au lancement isolé avec Shop C et Upstairs. Le parcours du tutoriel et le remplacement du mobilier restent à vérifier visuellement dans une nouvelle boutique Cartes uniquement. Le lanceur Lancer-test-extensions.cmd installe la nouvelle DLL de modes au lancement ; les DLL stables de Save Manager sont inchangées.
`nPersonnalisé ajoute Hygiène (désodorisants) et Cartes en vrac (boîtes BulkBox, y compris grades). Les anciens choix Autres produits sont conservés par héritage pour ces catégories si elles étaient absentes. En bas de la liste défilante, les options Autoriser les employés / les parties aux tables / les échanges de cartes / les tournois sont indépendantes. Employés désactivés : recrutement natif bloqué avec message ; échanges désactivés : aucune caisse éligible pour les clients ; tournois désactivés : ouverture depuis le téléphone et confirmation de programmation bloquées avec message. Parties aux tables désactivées : clients redirigés vers les achats et objectifs de tutoriel de parties ignorés. Ces règles sont celles des nouvelles boutiques Personnalisé ; intégrations de mods supplémentaires de personnel/échanges à vérifier. Compilation et tests des règles/persistance réussis ; test interactif encore nécessaire. La DLL de laboratoire sera actualisée au prochain lancement, le jeu actuellement ouvert est laissé intact.


## Presets personnalisés (11 octobre 2026)
Dans Personnalisé, régler les catégories, les éditions et les activités puis saisir un nom et cliquer sur « Enregistrer ce preset ». Les boutons portant les noms enregistrés permettent de reprendre ces choix pour une nouvelle boutique. Enregistrer à nouveau le même nom met à jour le preset ; les boutiques déjà créées conservent leurs propres réglages.

Les presets sont conservés dans `campaigns/mode-presets`, propres à chaque espace de boutiques. Le choix « Autoriser les produits du jeu d’origine » filtre leurs achats et ventes ; le choix des éditions affine les boosters et leurs boîtes. Pour un preset Pokémon avec le pack actuel, désactiver les produits d’origine et les éditions non souhaitées, puis enregistrer « 100 % Pokémon ». Ce nom ne sélectionne pas automatiquement les packs : les autres contenus doivent être désactivés dans Mods. Les cartes individuelles, les décors et les ressources remplacées par TextureReplacer ne sont pas filtrés par cette option.

Les packs JSON EPL disposant d’un BundleId apparaissent désormais dans Mods sous « Pack · nom ». Leur activation entraîne celle d’EnhancedPrefabLoader et de son API. Le choix est appliqué au redémarrage en filtrant les descripteurs du chargeur runtime ; les fichiers restent en place et le prépatch conserve les déclarations des types pour préserver leurs identifiants. Nouveau pack ou descripteur modifié : nouvelle décision. Cette prise en charge vise EPL ; un autre chargeur nécessite son propre adaptateur. Désactiver un contenu utilisé dans une sauvegarde n’est pas une conversion de cette sauvegarde.

Validation : compilation des deux plugins sans avertissement ; tests GameModes (dont fixtures Pokémon, exclusion des produits natifs, copie et mise à jour des presets), tests ModProfiles réussis ; signature du point de filtrage EPL vérifiée sur la DLL installée. Affichage et filtrage effectif à valider dans le laboratoire au prochain lancement. Aucun remplacement de DLL dans la session actuellement ouverte ni dans Steam.


### Précision des contenus et éditions
Les familles EPL (MenuCategory) précèdent maintenant les noms d’éditions, par exemple Pokémon Pocket — Shining Revelry. Les packs de produits sont sélectionnables individuellement dans « Packs de contenu », avec achats et ventes distincts. Lors de l’enregistrement d’un preset, les choix des contenus et éditions actuellement connus sont enregistrés et les futurs ajouts sont interdits par défaut. Il est possible de les autoriser ensuite en personnalisant le preset. Les anciens presets restent compatibles mais doivent être enregistrés à nouveau pour adopter cette règle.
Le contrôle des noms EItemType au démarrage empêchait l’inventaire préalable des éditions EPL ; il est retiré pour que les descripteurs soient visibles au menu. Les entrées natives anticipées (Mégabot, CatJob…) ne sont plus présentées comme des éditions disponibles. Les entrées Tetramon et Destiny désignent leur origine technique, même si un pack remplace leurs textures.
Validation : tests de non-activation automatique d’un futur pack Yu-Gi-Oh!, indépendance achats/ventes par contenu, persistance des presets et fixtures Pokémon réussis. Affichage au menu à vérifier au prochain lancement.


La personnalisation est répartie en quatre onglets : Produits (catégories et origines), Packs de contenu (produits de chaque pack), Éditions (boosters et boîtes), Activités (employés, tables, échanges, tournois et futurs ajouts). Une recherche filtre les trois listes. Les commandes d’enregistrement et de sélection des presets restent sous la zone de réglages.


Lecture EPL corrigée : désérialisation Newtonsoft.Json (assembly déjà fourni par le jeu). La suite de tests peut recevoir en deuxième argument le dossier plugins pour lire directement ses descripteurs : `dotnet run --project src/TCG.GameModes.Tests -c Release -- docs/pokemon-editions-audit.json lab/instance-005/game/BepInEx/plugins`. Résultat actuel : 55 fichiers, 112 produits, 32 éditions.


Les onglets Éditions et Packs de contenu affichent désormais des familles repliables. Cliquer sur une famille montre ses entrées ; chacune conserve ses choix Achats / Ventes. Tout autoriser et Tout désactiver modifient les deux permissions pour toute la famille. La recherche déplie les résultats automatiquement ; les actions de famille concernent aussi ses entrées masquées par la recherche.


Présentation validée : les permissions se règlent avec Désactivé, Achat uniquement ou Achat et vente. Ces trois choix sont disponibles sur chaque ligne et sur les familles entières. Une famille dont les membres ont des réglages différents n’affiche aucun choix global sélectionné ; les choix individuels restent conservés.


Depuis le menu principal, le bouton Règles de chaque boutique ouvre ses réglages. Enregistrer les règles les applique au prochain chargement. Annuler conserve le fichier existant. La précédente configuration est copiée dans game-mode.xml.previous. Modifier un preset ne modifie pas automatiquement les boutiques. Les meubles installés et employés déjà recrutés ne sont pas supprimés par un changement de règles.


## Version 0.2.0 (18 octobre 2026)

**Fournitures.** Le désodorisant (et les sprays ajoutés par EPL) est une fourniture de la boutique : il reste commandable quel que soit le mode, y compris Cartes uniquement et les boutiques existantes. Option « Fournitures toujours achetables (désodorisant) » dans l'onglet Produits du mode Personnalisé.

**Cartes à l'unité par édition.** Onglet Éditions → « Cartes à l'unité » : chaque édition ou famille peut être « Vendues » ou « Non vendues ». Les clients ne choisissent que les présentoirs et compartiments dont la carte appartient à une édition vendue ; le jeu garde sa probabilité de 80 % pour trouver un présentoir. Par défaut toutes les éditions sont vendues. Les boosters et boîtes gardent leurs propres choix dans « Boosters et boîtes ».

**Résumé et modèles.** Un onglet Résumé décrit les règles en clair. La barre des presets propose quatre modèles (Cartes à l'unité, Scellé uniquement, Sans employés, Boutique calme) avant les presets enregistrés ; un modèle s'ajuste puis s'enregistre sous un autre nom. Supprimer retire un preset de la liste (fichier conservé en `.deleted`) sans toucher aux boutiques existantes. La carte de boutique affiche le nom du preset.
