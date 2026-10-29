# 0.4.0 — Experimental prerelease

Batch dish enhancement for Dave the Diver on Windows/Steam with BepInEx 6 IL2CPP x64.

- Compact native-icon recipe grid and ingredient cost review.
- Only individually affordable learned recipes are listed; automatic selection reserves shared stock.
- Portuguese/English UI follows the game's language.
- Native UI sound feedback, selected-tile outlines, remaining-stock counts and confirmation states.
- Uses the native management panel's actual visibility state to keep the button out of loading screens.

## Downloads

- **Nexus.zip**: recommended manual installation; plugin DLL and documentation, with BepInEx installed separately.
- **Source.zip**: source, tests and publication documentation; no proprietary game/loader assemblies.

## Validation limits

Compiles against the local game's generated interop assemblies. Fifteen planning tests pass. The user confirmed the 0.3.2 visibility correction works. The 0.4.0 audio/pending-state polish has not been auditioned or visually verified in-game. Complete batch spending, full English gameplay, controllers, DLC-specific menus and other-mod compatibility have not been validated end to end. Back up saves before testing. Applied upgrades persist and a partial batch is not automatically rolled back.

## AI Disclosure

Substantial implementation, UI layout, translations, tests and documentation were generated with OpenAI Codex under JoaoPPolli's direction. No AI-generated images, voices or music are bundled; the plugin uses native assets and has no AI runtime or network inference. See AI_DISCLOSURE.md for the complete disclosure. Prepared Nexus page tags: **AI-Generated Content** and **AI Media**.

This prerelease is not a Nexus Mods publication or approval. Submission material is in docs/nexus/.
