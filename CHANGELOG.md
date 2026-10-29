# Changelog

## 0.4.0 — experimental
- Add native UI audio for interaction and completion/error feedback.
- Suppress repeated per-recipe click sounds during batch execution.
- Add selected-tile outlines, full recipe name in footer, and remaining-ingredient counts.
- Render a pending state before applying the native batch; prevent repeat input.
- Keep the working 0.3.2 native panel visibility guard.

## 0.3.2 — experimental
- Replace the unreliable sheet-event visibility gate with the native management panel's actual shown and transitioning state.
- Keep the scene-loading guard and hide the overlay root while unavailable.

## 0.3.1 — superseded
- Attempted loading-screen fix using sheet lifecycle events. This could leave the button hidden; use 0.3.2 instead.

## 0.3.0
- Compact recipe icon grid and ingredient icon rows.
- Portuguese/English UI selection from game settings.
- Hide individually unaffordable recipes and reserve shared stock in automatic selection.

## 0.2.0
- Fix non-blittable ingredient delegate conversion.
- Reuse the native font and show errors on screen.

## 0.1.0
- Initial experimental batch enhancement implementation.
