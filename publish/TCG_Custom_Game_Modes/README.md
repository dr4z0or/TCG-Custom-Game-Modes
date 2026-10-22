# TCG Custom Game Modes

**Choose the rules of each shop.** When you create a shop, pick **Classic**, **Cards only** or **Custom**, and decide freely what exists in that shop: which products can be bought and sold, which card editions, which furniture, which activities. Every shop keeps its own rules.

Requires **TCG Save Manager** (shops). *Français plus bas.*

## Modes

- **Classic** — the usual rules of the game.
- **Cards only** — buy and open boosters; customers only buy your single cards from the card displays. Card tables are available from the start and air freshener can still be ordered.
- **Custom** — every rule below, with ready-made templates (*Single cards*, *Sealed only*, *No employees*, *Quiet shop*) and your own saved presets.

## Custom rules

| Tab | What you choose |
|---|---|
| **Products** | For each category (boosters, figures, playmats, sleeves, binders, board games, manga…): *Disabled*, *Buying only* or *Buying and selling*. Expand a category (▸) to set **each product on its own**. Base game products and products added by mods can be blocked as a whole. |
| **Content packs** | Packs added by Enhanced Prefab Loader, by family. |
| **Editions** | *Boosters and boxes*: buying/selling per card edition. **Disabled** removes the edition from the game: no boosters, no boxes, no single cards sold, no binder page. *Single cards*: which editions customers buy as single cards. |
| **Furniture** | Lock any piece of furniture or table: it no longer appears in the phone furniture shop (also with Furniture Overhaul). |
| **Activities** | Single card sales, card tables from the start, employees, games at the tables, card trades, tournaments, and whether future packs and editions are allowed automatically. |
| **Summary** | The rules of the shop in plain words. |

Level-up notices only announce what the rules let you obtain. The lists of products and furniture are filled the first time you play a game with the mod.

## Use

- **New shop**: the creation window shows the modes; with *Custom*, set the rules, then *Create my shop*.
- **Existing shop**: click **Rules** on its card in *My shops* (main menu). Changes apply from the next load. Furniture already placed is kept.

## Installation

**r2modman / Thunderstore Mod Manager:** install *TCG Custom Game Modes* (TCG Save Manager is installed with it).

**Manual:** install TCG Save Manager first, then extract the archive's `BepInEx` folder into the game folder.

## Notes

- Rules are stored with each shop in `%LOCALAPPDATA%\TCGSM\campaigns\<shop>\game-mode.xml`; presets in `campaigns\mode-presets`.
- Supported mods: Enhanced Prefab Loader packs (editions and products), Furniture Overhaul (furniture shop), Collection Tracker (binder pages of disabled editions), Purchase Overhaul.
- Interface in English or French (setting `Interface.Language` of TCG Save Manager).

---

## Français

Des règles par boutique : **Classique**, **Cartes uniquement** ou **Personnalisé**. En Personnalisé, chaque catégorie ou produit (désactivé, achat, achat et vente), chaque édition (désactivée = retirée de la partie), chaque meuble (bloqué = absent du magasin), et les activités (employés, tables, échanges, tournois). Bouton **Règles** sur la carte de chaque boutique. Nécessite TCG Save Manager.
