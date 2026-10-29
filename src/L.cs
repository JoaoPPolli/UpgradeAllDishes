using UnityEngine;

namespace UpgradeAllDishes;

internal static class L
{
    internal static bool Portuguese
    {
        get
        {
            var save = Singleton<DR.Save.SaveSystem>._instance;
            if (save != null && save.UserOptionManager != null)
                return save.UserOptionManager.CurrentLanguage == DR.Save.Languages.Portuguese;
            return Application.systemLanguage == SystemLanguage.Portuguese;
        }
    }
    internal static string T(string pt, string en) => Portuguese ? pt : en;
}
