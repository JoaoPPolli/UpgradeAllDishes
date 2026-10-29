using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TextMeshProUGUI;

namespace UpgradeAllDishes;

public sealed class BatchUI : MonoBehaviour
{
    public BatchUI(IntPtr pointer) : base(pointer) { }
    internal static bool ModalOpen;
    private BatchUIController controller;
    public void Update()
    {
        controller ??= new BatchUIController();
        controller.Tick();
    }
    public void LateUpdate()
    {
        if (SceneLoader.IsSceneLoading) Game.HideMenu();
        if (Game.OverlayRoot != null) Game.OverlayRoot.SetActive(Game.MenuOpen);
    }
}

// Keep managed callbacks, local functions and helper types outside the injected type.
// Only the small Update bridge above is exposed to IL2CPP.
internal sealed class BatchUIController
{
    private static bool ModalOpen { get => BatchUI.ModalOpen; set => BatchUI.ModalOpen = value; }
    private GameObject canvas;
    private GameObject openButton;
    private GameObject modal;
    private Text status;
    private Button confirm;
    private TMP_FontAsset font;
    private Material fontMaterial;
    private CookingStudyStartPanel nativeUpgrade;
    private static readonly Color Paper = new Color(0.94f, 0.86f, 0.67f, 1);
    private static readonly Color Wood = new Color(0.22f, 0.17f, 0.14f, 1);
    private static readonly Color Gold = new Color(1f, 0.72f, 0.12f, 1);
    private List<Dish> dishes = new();
    private readonly HashSet<int> selected = new();
    private readonly Dictionary<int, Text> labels = new();
    private bool done;
    private int executeAtFrame = -1;
    private string selectedName = "";
    private readonly List<Button> selectionButtons = new();
    private readonly Dictionary<int, Outline> outlines = new();

    public void Tick()
    {
        try
        {
            if (SceneLoader.IsSceneLoading) Game.HideMenu();
            if (canvas != null && language != L.Portuguese)
            { Close(); UnityEngine.Object.Destroy(canvas); canvas = null; }
            if (canvas == null && Game.MenuOpen) Build();
            if (canvas == null) return;
            canvas.SetActive(Game.MenuOpen);
            openButton!.SetActive(Game.MenuOpen && !ModalOpen && (nativeUpgrade == null || !nativeUpgrade.gameObject.activeInHierarchy));
            if (ModalOpen && !Game.MenuOpen) Close();
            if (ModalOpen && Input.GetKeyDown(KeyCode.Escape)) { UIAudio.Play(UICue.Cancel); Close(); }
            if (executeAtFrame >= 0 && Time.frameCount >= executeAtFrame)
            {
                executeAtFrame = -1;
                FinishApply();
            }
        }
        catch (Exception ex) { Plugin.Logger.LogError(ex); Close(); }
    }

    private GameObject Node(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Rect(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void Fill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private Text Text(string name, Transform parent, string value, int size = 20)
    {
        var go = Node(name, parent);
        var text = go.AddComponent<Text>();
        text.font = font;
        if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
        text.fontSize = size; text.text = value;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.color = Paper; text.richText = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.raycastTarget = false;
        return text;
    }

    private Button Button(Transform parent, string value, float x, float y, float w, float h, Action action, UICue cue = UICue.Select)
    {
        var go = Node(value, parent); Rect(go, x, y, w, h);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.58f, 0.32f, 0.12f, 1);
        // Solid pixel bevel; native button children include animated glow strips,
        // so selecting an arbitrary large child Image is not a usable button skin.
        Bevel(go.transform, w, h);
        var colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(1f, 0.88f, 0.55f, 1);
        colors.pressedColor = new Color(0.75f, 0.59f, 0.31f, 1);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image; button.colors = colors;
        var label = Text("Label", go.transform, value, 15);
        Fill(label.gameObject); label.alignment = TextAlignmentOptions.Center;
        button.onClick.AddListener((UnityAction)(() =>
        {
            try { UIAudio.Play(cue); action(); }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
                UIAudio.Play(UICue.Error); ShowError(ex);
                if (confirm != null) confirm.interactable = false;
            }
        }));
        return button;
    }

    private Transform Scroll(Transform parent, string name, float x, float y, float w, float h)
    {
        var root = Node(name, parent); Rect(root, x, y, w, h);
        root.AddComponent<Image>().color = new Color(0.17f, 0.13f, 0.11f, 1);
        var scroll = root.AddComponent<ScrollRect>();
        var viewport = Node("Viewport", root.transform); Fill(viewport);
        viewport.AddComponent<RectMask2D>();
        var content = Node("Content", viewport.transform); Rect(content, 8, 8, w - 32, 0);
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content.GetComponent<RectTransform>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        var bar = Node("ScrollBar", root.transform); Rect(bar, w - 16, 0, 16, h);
        bar.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.07f, 1);
        var handle = Node("Handle", bar.transform); Fill(handle);
        var handleImage = handle.AddComponent<Image>(); handleImage.color = new Color(0.66f, 0.45f, 0.25f, 1);
        var scrollbar = bar.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage; scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        return content.transform;
    }

    private void Build()
    {
        var native = Resources.FindObjectsOfTypeAll<MenuRecipeDetailBottom>()
            .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
        var nativeLabel = native != null ? native.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (nativeLabel == null) nativeLabel = Game.Menu.GetComponentInChildren<TextMeshProUGUI>(true);
        if (nativeLabel == null) nativeLabel = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
            .FirstOrDefault(t => t != null && t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && t.font != null);
        font = nativeLabel != null ? nativeLabel.font : TMP_Settings.defaultFontAsset;
        fontMaterial = nativeLabel != null ? nativeLabel.fontSharedMaterial : null;
        nativeUpgrade = Resources.FindObjectsOfTypeAll<CookingStudyStartPanel>().FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
        language = L.Portuguese;
        canvas = new GameObject("UpgradeAllDishes.Canvas");
        Game.OverlayRoot = canvas;
        canvas.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(canvas);
        var c = canvas.AddComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 28000;
        var scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvas.AddComponent<GraphicRaycaster>();
        openButton = Button(canvas.transform, L.T("Aprimorar todos", "Enhance all"), 0, 0, 260, 28, Open, UICue.Open).gameObject;
        // Tab above the recipe column, matching the native menu width and pixel font.
        var rt = openButton.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(-24, -70);
    }

    private Transform ingredientContent;
    private readonly Dictionary<int, Image> tiles = new();
    private readonly Dictionary<int, Text> checks = new();
    private bool language;
    private Sprite frameSprite, rowSprite, patternSprite;

    private void Skin()
    {
        var native = Resources.FindObjectsOfTypeAll<CookingStudyStartPanel>()
            .FirstOrDefault(p => p != null && p.gameObject.scene.IsValid());
        if (native == null) return;
        var images = native.GetComponentsInChildren<Image>(true);
        var frames = images.Where(i => i.sprite != null && i.type == Image.Type.Sliced
            && i.sprite.border != Vector4.zero).ToArray();
        var frame = frames.OrderByDescending(i =>
            (i.name.ToLowerInvariant().Contains("frame") || i.name.ToLowerInvariant().Contains("border") ? 10000000 : 0)
            + i.rectTransform.rect.width * i.rectTransform.rect.height).FirstOrDefault();
        if (frame != null) frameSprite = frame.sprite;
        var row = frames.Where(i => i != frame && i.rectTransform.rect.width > 150 && i.rectTransform.rect.height < 100)
            .OrderByDescending(i => i.rectTransform.rect.width).FirstOrDefault();
        if (row != null) rowSprite = row.sprite;
        var pattern = images.FirstOrDefault(i => i.sprite != null && i.type == Image.Type.Tiled);
        if (pattern != null) patternSprite = pattern.sprite;
        Plugin.Logger.LogInfo($"Native skin: frame={frameSprite?.name}, row={rowSprite?.name}, pattern={patternSprite?.name}");
    }

    private Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, Sprite sprite = null)
    {
        var go = Node(name, parent); Rect(go, x, y, w, h);
        var image = go.AddComponent<Image>(); image.color = color;
        if (sprite != null) { image.sprite = sprite; image.type = Image.Type.Sliced; }
        return image;
    }

    private void Icon(Transform parent, string spriteName, float x, float y, float size)
    {
        var icon = Box(parent, "Icon", x, y, size, size, Color.white);
        icon.raycastTarget = false; icon.preserveAspect = true;
        if (string.IsNullOrEmpty(spriteName)) { icon.enabled = false; return; }
        SpriteCollectionExtension.SetSprite_IfNotEqual(icon, spriteName);
    }

    private Text Label(Transform parent, string value, float x, float y, float w, float h, int size = 14)
    {
        var text = Text("Label", parent, value, size); Rect(text.gameObject, x, y, w, h);
        text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = size;
        return text;
    }

    private void Open()
    {
        if (!Game.MenuOpen || ModalOpen) return;
        Skin();
        selected.Clear(); labels.Clear(); tiles.Clear(); checks.Clear(); outlines.Clear(); selectionButtons.Clear();
        selectedName = ""; executeAtFrame = -1; done = false;
        modal = Node("UpgradeAll.Confirmation", canvas.transform); Fill(modal);
        modal.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);
        var panel = Box(modal.transform, "Panel", 0, 0, 860, 520, Wood).gameObject;
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero;
        if (frameSprite != null)
        {
            var frame = Box(panel.transform, "NativeFrame", 0, 0, 860, 520, Color.white, frameSprite);
            frame.raycastTarget = false;
        }
        else Frame(panel.transform, 860, 520);
        var header = Box(panel.transform, "Header", 8, 8, 844, 42, new Color(0.17f, 0.13f, 0.11f, 1));
        if (patternSprite != null) { header.sprite = patternSprite; header.type = Image.Type.Tiled; }
        Label(panel.transform, L.T("Aprimorar pratos", "Enhance dishes"), 22, 14, 650, 30, 22);
        Label(panel.transform, L.T("RECEITAS", "RECIPES"), 22, 60, 200, 24, 15);
        Label(panel.transform, L.T("INGREDIENTES", "INGREDIENTS"), 464, 60, 200, 24, 15);
        Label(panel.transform, L.T("Estoque / gasto", "Stock / cost"), 680, 61, 156, 24, 13).alignment = TextAlignmentOptions.TopRight;
        var recipeContent = Scroll(panel.transform, "Recipes", 20, 90, 420, 322);
        ingredientContent = Scroll(panel.transform, "Ingredients", 458, 90, 382, 322);
        status = Label(panel.transform, L.T("Carregando...", "Loading..."), 22, 423, 816, 40, 14);
        ModalOpen = true;
        Button(panel.transform, L.T("Fechar", "Close"), 654, 472, 184, 30, Close, UICue.Cancel);
        selectionButtons.Add(Button(panel.transform, L.T("Selecionar possíveis", "Select affordable"), 22, 472, 180, 30, () => SetAll(true)));
        selectionButtons.Add(Button(panel.transform, L.T("Limpar", "Clear"), 212, 472, 100, 30, () => SetAll(false)));
        confirm = Button(panel.transform, L.T("Aprimorar", "Enhance"), 458, 472, 184, 30, Apply, UICue.Confirm);
        confirm.GetComponent<Image>().color = new Color(0.88f, 0.015f, 0.36f, 1);
        dishes = Game.Recipes();
        for (int i = 0; i < dishes.Count; i++)
        {
            var dish = dishes[i]; int col = i % 4, row = i / 4;
            var b = Button(recipeContent, "", col * 98, row * 100, 92, 94, () =>
            {
                if (done) return;
                selectedName = dish.Name;
                if (!selected.Add(dish.Id)) selected.Remove(dish.Id);
                Refresh();
            });
            UnityEngine.Object.Destroy(b.GetComponentInChildren<Text>().gameObject);
            var bg = b.GetComponent<Image>(); bg.color = new Color(0.52f, 0.47f, 0.42f, 1);
            tiles[dish.Id] = bg;
            selectionButtons.Add(b);
            var outline = b.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.84f, 0.25f, 1);
            outline.effectDistance = new Vector2(1, -1);
            outline.useGraphicAlpha = false;
            outlines[dish.Id] = outline;
            Icon(b.transform, Game.Kitchen[dish.Id].Recipe.Icon, 20, 16, 52);
            labels[dish.Id] = Label(b.transform, dish.Name, 4, 66, 84, 26, 12);
            labels[dish.Id].alignment = TextAlignmentOptions.Center;
            checks[dish.Id] = Label(b.transform, L.T("Nv.", "Lv.") + dish.Level + " → " + (dish.Level + 1), 4, 1, 84, 17, 11);
        }
        recipeContent.GetComponent<RectTransform>().sizeDelta = new Vector2(388, Math.Max(306, ((dishes.Count + 3) / 4) * 100));
        SetAll(true);
    }

    private void SetAll(bool value)
    {
        if (done) return;
        selected.Clear(); selectedName = "";
        if (value)
        {
            foreach (var dish in Plan.Affordable(dishes, Game.Available)) selected.Add(dish.Id);
        }
        Refresh();
    }

    private void Refresh()
    {
        foreach (var dish in dishes)
        {
            bool active = selected.Contains(dish.Id);
            outlines[dish.Id].enabled = active;
            tiles[dish.Id].color = active ? new Color(0.65f, 0.57f, 0.43f, 1) : new Color(0.4f, 0.35f, 0.31f, 1);
            checks[dish.Id].color = active ? new Color(0.65f, 1, 0.25f, 1) : Paper;
            labels[dish.Id].color = active ? Paper : new Color(0.65f, 0.6f, 0.54f, 1);
        }
        // Rebuild only the ingredient column; deferred-destroy rows are disabled immediately.
        for (int i = ingredientContent.childCount - 1; i >= 0; i--)
        {
            var old = ingredientContent.GetChild(i).gameObject; old.SetActive(false); UnityEngine.Object.Destroy(old);
        }
        var totals = Plan.Sum(dishes.Where(d => selected.Contains(d.Id)));
        bool enough = true; int index = 0;
        foreach (var cost in totals.OrderBy(p => Game.IngredientName(p.Key)))
        {
            int have = Game.Available(cost.Key); bool lacks = have < cost.Value; enough &= !lacks;
            var row = Box(ingredientContent, "Ingredient", 0, index * 51, 346, 46, new Color(0.45f, 0.4f, 0.36f, 1), rowSprite);
            Icon(row.transform, Game.Stock[cost.Key]?.Entity?.Icon, 3, 3, 40);
            Label(row.transform, Game.IngredientName(cost.Key), 50, 3, 278, 20, 14);
            var count = Label(row.transform, $"{have} / {cost.Value}" + (lacks
                ? L.T($"   faltam {cost.Value - have}", $"   need {cost.Value - have} more")
                : L.T($"   sobra {have - cost.Value}", $"   left {have - cost.Value}")), 50, 24, 278, 19, 13);
            count.color = lacks ? new Color(1, 0.36f, 0.2f, 1) : Paper;
            index++;
        }
        if (index == 0) Label(ingredientContent, L.T("Nenhum ingrediente selecionado.", "No ingredients selected."), 4, 6, 330, 55, 14);
        ingredientContent.GetComponent<RectTransform>().sizeDelta = new Vector2(350, Math.Max(306, index * 51));
        status.text = dishes.Count == 0
            ? L.T("Nenhum prato com ingredientes suficientes para aprimorar.", "No dishes have enough ingredients to enhance.")
            : L.T($"{selected.Count}/{dishes.Count} selecionados • +1 nível por prato", $"{selected.Count}/{dishes.Count} selected • +1 level per dish")
                + (enough ? "" : L.T(" • Ingredientes compartilhados insuficientes.", " • Not enough shared ingredients."));
        if (!string.IsNullOrEmpty(selectedName)) status.text += "\n" + selectedName;
        status.color = enough ? Paper : new Color(1, 0.43f, 0.25f, 1);
        confirm.interactable = selected.Count > 0 && enough && !done;
    }

    private void Apply()
    {
        if (done) return;
        done = true; confirm.interactable = false;
        foreach (var button in selectionButtons) button.interactable = false;
        confirm.GetComponentInChildren<Text>().text = L.T("Aprimorando...", "Enhancing...");
        status.text = L.T("Aplicando os aprimoramentos selecionados...", "Applying the selected enhancements...");
        // Allow the pending state to render before entering the native synchronous batch.
        executeAtFrame = Time.frameCount + 2;
    }

    private void FinishApply()
    {
        if (!ModalOpen || !Game.MenuOpen) return;
        try
        {
            int count = Game.Execute(dishes.Where(d => selected.Contains(d.Id)).ToList());
            status.text = L.T($"Concluído: {count} pratos aprimorados. Ingredientes gastos conforme a lista.",
                $"Done: {count} dishes enhanced. Ingredients spent as listed.");
            status.color = new Color(0.65f, 1, 0.25f, 1);
            confirm.GetComponentInChildren<Text>().text = L.T("Concluído", "Done");
            UIAudio.Play(UICue.Success);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError(ex);
            UIAudio.Play(UICue.Error);
            ShowError(ex);
            confirm.GetComponentInChildren<Text>().text = L.T("Interrompido", "Stopped");
        }
    }

    private void Bevel(Transform parent, float width, float height)
    {
        void Edge(string name, float x, float y, float w, float h, Color color)
        {
            var go = Node(name, parent); Rect(go, x, y, w, h);
            var image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }
        var light = new Color(0.83f, 0.56f, 0.29f, 1);
        var dark = new Color(0.31f, 0.16f, 0.07f, 1);
        Edge("TopBevel", 0, 0, width, 2, light);
        Edge("LeftBevel", 0, 0, 2, height, light);
        Edge("BottomBevel", 0, height - 3, width, 3, dark);
        Edge("RightBevel", width - 3, 0, 3, height, dark);
    }

    private void Frame(Transform parent, float width, float height)
    {
        void Edge(string name, float x, float y, float w, float h, Color color)
        {
            var edge = Node(name, parent); Rect(edge, x, y, w, h);
            var image = edge.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        }
        var trim = new Color(0.68f, 0.42f, 0.2f, 1);
        var light = new Color(0.89f, 0.73f, 0.49f, 1);
        Edge("Top", 0, 0, width, 5, trim); Edge("Bottom", 0, height - 5, width, 5, trim);
        Edge("Left", 0, 0, 5, height, trim); Edge("Right", width - 5, 0, 5, height, trim);
        Edge("TopInlay", 6, 6, width - 12, 1, light); Edge("BottomInlay", 6, height - 7, width - 12, 1, light);
        Edge("HeaderRule", 24, 50, width - 48, 1, trim);
    }

    private void ShowError(Exception ex)
    {
        // Errors before the old modal existed used to be logged without any visible feedback.
        if (modal == null)
        {
            modal = Node("Erro", canvas.transform); Fill(modal);
            modal.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            var panel = Node("Mensagem", modal.transform); Rect(panel, 190, 180, 900, 300);
            panel.AddComponent<Image>().color = Wood;
            status = Text("Mensagem", panel.transform, "", 20); Rect(status.gameObject, 28, 32, 844, 180);
            Button(panel.transform, L.T("Fechar", "Close"), 620, 232, 250, 42, Close, UICue.Cancel);
        }
        ModalOpen = true;
        if (status != null) { status.text = L.T("Não foi possível aprimorar. ", "Unable to enhance. ") + ex.Message; status.color = Gold; }
    }

    private void Close()
    {
        executeAtFrame = -1;
        ModalOpen = false;
        if (modal != null) UnityEngine.Object.Destroy(modal);
        modal = null;
        selected.Clear(); labels.Clear();
    }
}


