# Upgrade All Dishes — Dave the Diver

Experimental BepInEx 6 IL2CPP plugin for Dave the Diver (Windows x64 / Steam).

Adds **Enhance all / Aprimorar todos** above the restaurant recipe list. Review a compact dish-icon grid and ingredient-icon cost list before enhancing each selected dish by one level. Recipes without enough ingredients for their next level are excluded. Automatic selection reserves shared ingredients in recipe-ID order; it does not optimize profit or recipe priorities.

The plugin calls the game's enhancement routine and consumes ingredients normally. It rechecks costs, levels and stock before applying the batch. Canceling spends nothing. Partial upgrades are not rolled back if an error interrupts a batch.

## AI Disclosure

Substantial code, UI layout, translations, tests and documentation were generated using **OpenAI Codex**, guided by JoaoPPolli. This is AI-generated code. No generated visual/audio assets or AI runtime services are included. Full disclosure: [AI_DISCLOSURE.md](AI_DISCLOSURE.md).

## Install

1. Install the official [BepInEx 6 Unity IL2CPP Windows x64 loader](https://builds.bepinex.dev/projects/bepinex_be). The local build used 6.0.0-be.764 and Unity 6000.0.52f1.
2. Start the game once to generate interop assemblies, then close it.
3. Extract the mod archive into the game folder so the DLL is at `BepInEx/plugins/UpgradeAllDishes/UpgradeAllDishes.dll`.
4. Open the restaurant recipe menu and choose **Enhance all**. Portuguese and English follow the game language automatically; other languages use English for mod UI. Dish and ingredient names come from the game.

Back up your save before testing. Confirmed upgrades and ingredient consumption persist after uninstalling. Remove only the `BepInEx/plugins/UpgradeAllDishes` folder to uninstall.

## Status and limits

Version 0.4.0 is experimental. Compiles against locally generated interop assemblies. Fifteen automated planning/cost tests pass. Previous versions' confirmation opening, stock display, selection and cancellation were observed in-game. Version 0.3.2 replaced the faulty event-based visibility gate from 0.3.1 with the native management panel's visible/transition state plus a scene-loading guard.

A complete batch with permanent ingredient spending has not been validated end to end. English detection and translation paths are implemented; a complete English gameplay pass has not been recorded. Controller support, other mods, game updates and DLC-specific menus have not been validated. The plugin suppresses repeated enhancement animations during its batch.

## Build and test

Install a .NET SDK and run the game with BepInEx once. Use .NET 9 for the test runner; the plugin targets .NET 6.

```powershell
dotnet build src/UpgradeAllDishes.csproj -c Release "-p:GamePath=C:/path/to/Dave the Diver"
dotnet run --project tests/PlanTests.csproj
```

Game and loader DLLs are not distributed. Use your own locally generated references. Do not add them to this repository.

## Credits and permissions

Created at the direction of JoaoPPolli with OpenAI Codex. Thanks to BepInEx, HarmonyX and Il2CppInterop. DaveDiverExpansion's public integration examples and compilation references were consulted during development. This is an independent plugin, not an official Mintrocket release. Native assets belong to their respective owners. See [PERMISSIONS.md](PERMISSIONS.md).

## 0.4.0 interaction polish
Native UI sounds for open, selection, cancel, confirm, completion and error, routed through the game UI sound mixer. Repeated per-dish upgrade clicks are suppressed only during the batch. Selected tiles have an outline, the footer shows the selected recipe name, ingredient rows show remaining stock, and confirmation shows pending/success/error states. Selection controls are disabled after confirmation. Audio and the new pending-state UI have been compiled but have not yet been auditioned/visually verified in-game.
