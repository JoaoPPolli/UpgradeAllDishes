using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace UpgradeAllDishes;

[BepInPlugin("br.upgradealldishes", "Upgrade All Dishes", "0.4.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    public override void Load()
    {
        Logger = Log;
        new Harmony("br.upgradealldishes").PatchAll();
        ClassInjector.RegisterTypeInIl2Cpp<BatchUI>();
        AddComponent<BatchUI>();
        Log.LogInfo("UpgradeAllDishes 0.4.0 loaded.");
    }
}

internal static class Game
{
    internal static RecipeListScroller Menu;
    internal static ManagementPanel MenuPanel;
    internal static GameObject OverlayRoot;
    internal static bool Applying;
    internal static bool Locked => BatchUI.ModalOpen && !Applying;
    internal static SushiBarKitchen Kitchen => SingletonNoMono<SushiBarKitchen>.s_Instance;
    internal static IngredientsStorage Stock => SingletonNoMono<IngredientsStorage>.s_Instance;

    // Read the native owner's actual visible state. Sheet Init/OnEnable calls can
    // happen after ShowSheet and are not a reliable open/closed state machine.
    internal static bool MenuOpen => !SceneLoader.IsSceneLoading
        && Menu != null && Menu.isActiveAndEnabled && Menu.gameObject.scene.isLoaded
        && MenuPanel != null && MenuPanel.IsShow && !MenuPanel.IsTransitioning;

    internal static void HideMenu()
    {
        if (OverlayRoot != null) OverlayRoot.SetActive(false);
    }

    internal static Dish Read(CookingStudyData data)
    {
        if (data.studyLevel < 1 || data.IsMaxLevel || data.isVIPTemp || Kitchen.IsSystemRecipe(data.recipeID))
            throw new InvalidOperationException(L.T("Receita indisponível para melhoria.", "This recipe cannot be enhanced."));
        var costs = new Dictionary<int, int>();
        // GetIngredients uses a value-type callback that Il2CppInterop cannot marshal.
        // Its native predicate always returns true; indexed IngredientsList is equivalent
        // and avoids converting a managed delegate with a non-blittable struct parameter.
        var ingredients = data.Recipe.IngredientsList;
        for (int i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            int count = CookingStudyData.GetRequiredIngredientsCount(data.studyLevel, ingredient.quantity);
            if (count <= 0) throw new InvalidOperationException(L.T("O jogo retornou um custo inválido.", "The game returned an invalid cost."));
            costs.TryGetValue(ingredient.id, out int old);
            costs[ingredient.id] = checked(old + count);
        }
        if (costs.Count == 0) throw new InvalidOperationException(L.T("Receita sem ingredientes.", "Recipe has no ingredients."));
        return new Dish(data.recipeID, data.Recipe.Name, data.studyLevel, costs);
    }

    internal static List<Dish> Recipes()
    {
        var result = new List<Dish>();
        var iterator = Kitchen.m_RecipeStudyBook.GetEnumerator();
        while (iterator.MoveNext())
        {
            var data = iterator.Current.Value;
            if (data.studyLevel >= 1 && !data.IsMaxLevel && !data.isVIPTemp && !Kitchen.IsSystemRecipe(data.recipeID))
            {
                var dish = Read(data);
                if (data.CanNextLevel() && Plan.Fits(dish, Available))
                    result.Add(dish);
            }
        }
        return result.OrderBy(d => d.Id).ToList();
    }

    internal static int Available(int id) => Stock[id]?.TotalCount() ?? 0;
    internal static string IngredientName(int id) => Stock[id]?.Entity?.Name ?? L.T($"Ingrediente #{id}", $"Ingredient #{id}");

    // Calls the native upgrade, preserving taste, ingredient grading, missions and save updates.
    // No recipe level or inventory values are written by the plugin.
    internal static int Execute(IReadOnlyList<Dish> approved)
    {
        if (!MenuOpen || approved.Count == 0) throw new InvalidOperationException(L.T("Abra a lista de receitas novamente.", "Open the recipe list again."));
        foreach (var dish in approved)
            if (!Plan.Same(dish, Read(Kitchen[dish.Id])))
                throw new InvalidOperationException(L.T("Uma receita mudou. Reabra a confirmação.", "A recipe changed. Reopen the confirmation."));
        foreach (var cost in Plan.Sum(approved))
            if (Available(cost.Key) < cost.Value)
                throw new InvalidOperationException(L.T("Estoque insuficiente. Reabra a confirmação.", "Not enough ingredients. Reopen the confirmation."));

        var panel = Resources.FindObjectsOfTypeAll<CookingStudyStartPanel>()
            .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid() && !p.gameObject.activeInHierarchy);
        if (panel == null) throw new InvalidOperationException(L.T("Painel de melhoria não encontrado. Abra uma melhoria normal, feche e tente novamente.", "Enhancement panel not found. Open a normal enhancement, close it, then try again."));
        var oldData = panel.m_Data;
        int oldTaste = panel.m_NextStudyTasteValue;
        var oldDiff = panel.m_DIffResult;
        int completed = 0;
        try
        {
            Applying = true;
            foreach (var dish in approved)
            {
                var data = Kitchen[dish.Id];
                if (!Plan.Same(dish, Read(data)) || !data.CanNextLevel())
                    throw new InvalidOperationException(L.T("As condições de melhoria mudaram.", "Enhancement conditions changed."));
                foreach (var cost in dish.Costs)
                    if (Available(cost.Key) < cost.Value) throw new InvalidOperationException(L.T("O estoque mudou.", "Ingredient stock changed."));
                var before = dish.Costs.Keys.ToDictionary(id => id, Available);
                panel.m_Data = data;
                panel.m_DIffResult = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>(4);
                panel.UpdateRecipeData();
                panel.OnStartStudy();
                if (data.studyLevel != dish.Level + 1)
                    throw new InvalidOperationException(L.T("O jogo não confirmou o próximo nível.", "The game did not confirm the next level."));
                completed++;
                foreach (var cost in dish.Costs)
                    if (before[cost.Key] - Available(cost.Key) != cost.Value)
                        throw new InvalidOperationException(L.T("O consumo diferiu da previsão. Lote interrompido.", "Ingredient usage differed from the preview. Batch stopped."));
                Plugin.Logger.LogInfo($"Melhoria: {dish.Id}, nível {dish.Level} -> {data.studyLevel}");
            }
            return completed;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(L.T($"{completed}/{approved.Count} melhorias verificadas; lote interrompido. {ex.Message}", $"{completed}/{approved.Count} enhancements verified; batch stopped. {ex.Message}"), ex);
        }
        finally
        {
            Applying = false;
            panel.m_Data = oldData;
            panel.m_NextStudyTasteValue = oldTaste;
            panel.m_DIffResult = oldDiff;
            if (Menu != null)
            {
                Menu.RefreshActiveCellViews();
                Menu.ShowFocusRecipeDetail();
            }
        }
    }
}

[HarmonyPatch(typeof(RecipeListScroller), nameof(RecipeListScroller.OnEnable))]
internal static class CaptureMenu
{
    private static void Postfix(RecipeListScroller __instance)
    {
        Game.Menu = __instance;
        Game.MenuPanel = __instance.GetComponentInParent<ManagementPanel>(true);
        if (Game.MenuPanel == null)
            Game.MenuPanel = Resources.FindObjectsOfTypeAll<ManagementPanel>()
                .FirstOrDefault(p => p != null && p.gameObject.scene == __instance.gameObject.scene);
    }
}

[HarmonyPatch(typeof(RecipeListScroller), nameof(RecipeListScroller.OnDirectHandler))]
internal static class BlockMenuInput
{
    private static bool Prefix() => !Game.Locked;
}

// OnStartStudy has already committed the upgrade before requesting this animation.
// Skipping the animation also skips the individual result popup, only inside our batch.
[HarmonyPatch(typeof(SushiBarResultAnimManager), nameof(SushiBarResultAnimManager.Play))]
internal static class SkipRepeatedAnimation
{
    private static bool Prefix() => !Game.Applying;
}

// Native enhancement emits one click per recipe. The batch supplies one confirm
// cue and one completion cue rather than stacking dozens of identical clicks.
[HarmonyPatch(typeof(SushiBarMainCanvasManager), nameof(SushiBarMainCanvasManager.OnButtonClickSound))]
internal static class SkipRepeatedUpgradeClicks
{
    private static bool Prefix() => !Game.Applying;
}

