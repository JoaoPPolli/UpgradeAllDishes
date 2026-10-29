using System;
using UnityEngine;

namespace UpgradeAllDishes;

internal enum UICue { Open, Select, Cancel, Confirm, Success, Error }

internal static class UIAudio
{
    private static float lastSelection = -1;
    private static bool warned;

    internal static void Play(UICue cue)
    {
        try
        {
            // Use the native UI mixer, including the player's mute/volume settings.
            // Success/error cues are never swallowed by the selection debounce.
            if (cue == UICue.Select)
            {
                if (Time.unscaledTime - lastSelection < 0.05f) return;
                lastSelection = Time.unscaledTime;
            }
            var sound = cue switch
            {
                UICue.Select => SoundConfigManager.commonFocusMove,
                UICue.Success => SoundConfigManager.commonRewardSound,
                UICue.Error => SoundConfigManager.commonAttention,
                _ => SoundConfigManager.commonButtonClick
            };
            if (sound != null) SoundManager.PlayUISound(sound);
        }
        catch (Exception ex)
        {
            // A missing sound must never prevent selection, cancel or enhancement.
            if (!warned) { Plugin.Logger.LogWarning("UI audio unavailable: " + ex.Message); warned = true; }
        }
    }
}
