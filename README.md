# TCG Custom Game Modes

A BepInEx mod for **TCG Card Shop Simulator**. Interface in English, or in French when the game is in French.

**Choose the rules of each shop.** When you create a shop, pick **Classic**, **Cards only** or **Custom**, and decide freely what exists in that shop: which products can be bought and sold, which card editions, which furniture, which activities. Every shop keeps its own rules.

Full description, settings and FAQ: [mod page](publish/TCG_Custom_Game_Modes/README.md) · changes: [CHANGELOG](publish/TCG_Custom_Game_Modes/CHANGELOG.md).

## Install

Players: install it from Thunderstore (r2modman) or Nexus Mods. Requires BepInEx 5 and [TCG Save Manager](https://github.com/dr4z0or/TCG-Save-Manager).

## Building from source (Windows, .NET SDK 8)

1. Clone [TCG-Save-Manager](https://github.com/dr4z0or/TCG-Save-Manager) **next to this repository** (same parent folder): the plugin is built against its core library.
2. Copy `local.props.example` to `local.props` and set `GameDir` to your game folder (BepInEx 5 installed).
3. `dotnet build src/TCG.GameModes -c Release`.
4. Offline tests: `dotnet run --project src/TCG.GameModes.Tests -- docs/pokemon-editions-audit.json`.

Install: `TCG.GameModes.dll` in `BepInEx/plugins/TCGGameModes`.

## Other mods by dr4z0or

- [TCG Save Manager](https://github.com/dr4z0or/TCG-Save-Manager)
- [TCG Mod Profiles](https://github.com/dr4z0or/TCG-Mod-Profiles)
- [TCG Card Translations](https://github.com/dr4z0or/TCG-Card-Translations)
- [Mods Translator](https://github.com/dr4z0or/Mods-Translator)

## License

[MIT](LICENSE). Not affiliated with OPNeon Games.

---

**En français :** Règles par boutique : Classique, Cartes uniquement ou Personnalisé (produits, éditions de cartes, meubles, activités). Nécessite TCG Save Manager. Les joueurs l'installent depuis Thunderstore ou Nexus ; ce dépôt contient le code source.
