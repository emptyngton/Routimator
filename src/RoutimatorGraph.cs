using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.EventSystems;
using System.Collections;

namespace Routimator
{
    public class RoutimatorGraph
    {
        private Routimator owner;
        private GameObject graphCanvas;
        private Canvas canvasComponent;
        private RectTransform viewportRectangle;
        private RectTransform contentPane;
        private Image panelImage;
        private Dictionary<string, RectTransform> stateNodes = new Dictionary<string, RectTransform>();
        private List<GameObject> connections = new List<GameObject>();
        private bool isGraphVisible = false;

        private bool isLinking = false;
        private RoutimatorState.State linkSourceState = null;
        private Text linkButtonText = null;
        private Color linkSourceHighlightColor = new Color(1f, 0.8f, 0f, 0.8f);
        private const float LINK_HIGHLIGHT_THICKNESS = 2f;

        private GameObject dragArrow = null;
        private RectTransform dragArrowRect = null;
        private bool isDraggingLink = false;
        private string dragLinkSourceNode = null;
        private Coroutine dragArrowUpdateCoroutine = null;

        private const float ARROW_GAP = 8f;
        private const float NODE_WIDTH = 160f;
        private const float NODE_HEIGHT = 60f;
        // Usunięto normalStateColor, ponieważ nie jest już używane do określania ikonki ludzika
        // private Color normalStateColor = new Color(0.2f, 0.2f, 0.2f, 0.9f); 
        private Color vsStateColor = new Color(0.0f, 0.537f, 0.706f, 0.9f);
        private Color vtStateColor = new Color(0.788f, 0.345f, 0.012f, 0.9f);
        private Color navStateColor = new Color(0.298f, 0.686f, 0.314f, 0.9f);
        private Color defaultNodeColor = new Color(0.2f, 0.2f, 0.2f, 0.9f); // Kolor dla stanów bez specjalnego prefiksu

        private Color lineColor = new Color(1f, 1f, 1f, 0.9f);
        private Color lineHoverColor = new Color(1f, 0.5f, 0.5f, 1f);
        private const float LINE_THICKNESS_HOVER_MULTIPLIER = 1.8f;
        private const float CONNECTION_INTERACT_PADDING = 5f;
        private Color currentStateOutlineColor = new Color(0.1f, 0.9f, 0.1f, 1f);
        private const float OUTLINE_THICKNESS = 2f;

        private const float HORIZONTAL_SPACING = 220f;
        private const float VERTICAL_SPACING = 80f;
        private const float LEVEL_HEIGHT = 120f;
        private const float CURVE_STRENGTH = 0.2f;
        private const float LINE_THICKNESS = 2.5f;
        private const float ARROW_SIZE = 20f;

        private float zoomLevel = 1f;
        private Vector2 panOffset = Vector2.zero;
        private bool isDragging = false;
        private Vector2 dragStartPosition;
        private Vector2 offsetAtDragStart;
        private RectTransform dotBackgroundRect = null;

        private const float ZOOM_SENSITIVITY_MULTIPLIER = 0.002f;
        private const float MIN_ZOOM = 0.2f;
        private const float MAX_ZOOM = 3.0f;
        // Pola do zarządzania mruganiem
        private bool isBlinkingContinuous = false;
        private int blinkCount = 0; // Dla trzykrotnego mrugania
                                    // private float blinkTimer = 0f; // Niepotrzebne przy korutynach
        private const float BLINK_INTERVAL_ON_CHANGE = 0.15f; // Szybsze mruganie przy zmianie
        private const float BLINK_INTERVAL_CONTINUOUS = 0.5f; // Wolniejsze, ciągłe mruganie
        private bool blinkOutlineVisible = true;
        private Coroutine blinkOnChangeCoroutine = null;
        private Coroutine continuousBlinkCoroutine = null;
        private Transform currentBlinkOutlineTransform = null; // Przechowuje referencję do aktualnie mrugającej ramki

        private Dictionary<string, Color> groupColors = new Dictionary<string, Color>();
        private Color[] predefinedColors = new Color[]
        {
            new Color(0.8f, 0.2f, 0.2f, 0.7f), new Color(0.2f, 0.6f, 0.8f, 0.7f),
            new Color(0.2f, 0.8f, 0.2f, 0.7f), new Color(0.8f, 0.8f, 0.2f, 0.7f),
            new Color(0.8f, 0.4f, 0.8f, 0.7f), new Color(0.8f, 0.6f, 0.2f, 0.7f),
            new Color(0.4f, 0.8f, 0.8f, 0.7f), new Color(0.6f, 0.4f, 0.2f, 0.7f),
        };
        private Sprite nodeRoundedSprite = null;
        private Sprite graphTitleIconSprite = null;

        private GameObject contextMenuPanel = null;
        private Color contextMenuBackgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.95f);
        private Color contextMenuButtonColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        private Color contextMenuButtonHighlightColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private const float CONTEXT_MENU_WIDTH = 180f;
        private const float CONTEXT_MENU_BUTTON_HEIGHT = 30f;

        private Sprite manIconSpriteField = null;

        private class ConnectionPair { public string Source { get; private set; } public string Target { get; private set; } public ConnectionPair(string source, string target) { if (string.Compare(source, target) <= 0) { Source = source; Target = target; } else { Source = target; Target = source; } } public override bool Equals(object obj) { ConnectionPair other = obj as ConnectionPair; return other != null && Source == other.Source && Target == other.Target; } public override int GetHashCode() { return Source.GetHashCode() ^ Target.GetHashCode(); } }
        private Dictionary<GameObject, KeyValuePair<string, string>> connectionData = new Dictionary<GameObject, KeyValuePair<string, string>>();

        private bool isDraggingNode = false;
        private RectTransform draggedNodeRect = null;
        private RoutimatorState.State draggedState = null;
        private Vector2 dragNodeStartPosition;
        private Vector2 dragMouseOffset;
        private Camera canvasCamera;
        private bool isGraphMinimized = false;
        private GameObject legend;
        private Text minimizeButtonText;
        private Vector2 originalPanelSize;
        private readonly Vector2 minimizedPanelSize = new Vector2(450, 450);
        private GameObject resetButton;
        private GameObject linkButton;

        public RoutimatorGraph(Routimator owner) { this.owner = owner; }

        public void ToggleGraphVisibility() { if (isGraphVisible) HideGraph(); else ShowGraph(); }

        public void ShowGraph()
        {
            bool firstTimeSetup = (graphCanvas == null);

            if (firstTimeSetup)
            {
                CreateGraphCanvas();
            }
            else
            {
                graphCanvas.SetActive(true);
            }

            GenerateGraph();
            isGraphVisible = true;

            if (owner != null)
            {
                string currentOpState = owner.GetCurrentOperationState();
                bool shouldBeContinuousBlinking = (currentOpState == RoutimatorOperationStates.NAVIGATING ||
                                                   currentOpState == RoutimatorOperationStates.WAITING_FOR_WALK_FINISH);
                SetContinuousBlinking(shouldBeContinuousBlinking);
            }

            ApplyGraphWindowState();
            UpdateHighlights();
        }

        public void HideGraph()
        {
            DestroyContextMenu();
            if (graphCanvas != null) graphCanvas.SetActive(false);
            isGraphVisible = false;
            isGraphMinimized = false; // Resetuj stan minimalizacji przy zamykaniu
            if (isLinking) ToggleLinkingMode();

            // Zatrzymaj korutyny mrugania
            if (blinkOnChangeCoroutine != null) { owner.StopCoroutine(blinkOnChangeCoroutine); blinkOnChangeCoroutine = null; }
            if (continuousBlinkCoroutine != null) { owner.StopCoroutine(continuousBlinkCoroutine); continuousBlinkCoroutine = null; }
            currentBlinkOutlineTransform = null; // Usuń referencję
            isBlinkingContinuous = false; // Zresetuj flagę
        }

        public bool IsVisible() { return isGraphVisible; }

        public void UpdateGraph()
        {
            if (isGraphVisible && graphCanvas != null && graphCanvas.activeInHierarchy)
            {
                bool wasLinking = isLinking;
                RoutimatorState.State savedLinkSource = linkSourceState;

                GenerateGraph();
                UpdateContentPaneTransform();

                if (wasLinking && savedLinkSource != null)
                {
                    isLinking = true;
                    linkSourceState = savedLinkSource;
                    Transform linkButtonTransform = graphCanvas?.transform.Find("GraphPanel/LinkButton/Text");
                    if (linkButtonTransform != null) linkButtonText = linkButtonTransform.GetComponent<Text>();
                    if (linkButtonText != null) linkButtonText.text = "Finish Linking";
                    HighlightLinkSourceNode(true);
                }
            }
        }

        private Sprite CreateArrowSprite() { Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point; Color[] pixels = new Color[32 * 32]; for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear; int startX = 8, endX = 24, arrowLength = endX - startX, maxHalfWidthAtBase = 8; for (int y = 0; y < 32; y++) { for (int x = startX; x <= endX; x++) { int distFromCenterY = Mathf.Abs(y - 16); float progress = (float)(x - startX) / arrowLength; int currentHalfWidth = Mathf.FloorToInt(maxHalfWidthAtBase * (1.0f - progress)); if (distFromCenterY <= currentHalfWidth && y >= 0 && y < 32 && x >= 0 && x < 32) { pixels[y * 32 + x] = Color.white; } } } texture.SetPixels(pixels); texture.Apply(false, false); return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.zero, false); }
        private Sprite CreateNodeRoundedSprite() { if (nodeRoundedSprite == null) nodeRoundedSprite = CreateRoundedRectSprite(32, 6, Color.white); return nodeRoundedSprite; }
        private Sprite CreateRoundedRectSprite(int size = 32, int radius = 8, Color? fillColor = null) { if (fillColor == null) fillColor = new Color(0.15f, 0.15f, 0.15f, 0.99f); Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point; Color[] pixels = new Color[size * size]; Color col = fillColor.Value; int r2 = radius * radius; for (int y = 0; y < size; y++) { for (int x = 0; x < size; x++) { bool inCorner = (x < radius && y < radius && (radius - x) * (radius - x) + (radius - y) * (radius - y) > r2) || (x < radius && y >= size - radius && (radius - x) * (radius - x) + (y - (size - radius - 1)) * (y - (size - radius - 1)) > r2) || (x >= size - radius && y < radius && (x - (size - radius - 1)) * (x - (size - radius - 1)) + (radius - y) * (radius - y) > r2) || (x >= size - radius && y >= size - radius && (x - (size - radius - 1)) * (x - (size - radius - 1)) + (y - (size - radius - 1)) * (y - (size - radius - 1)) > r2); pixels[y * size + x] = inCorner ? new Color(0, 0, 0, 0) : col; } } tex.SetPixels(pixels); tex.Apply(false, false); return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, (uint)radius, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius), false); }
        private Sprite CreateCircleSprite(int size = 16, Color? fillColor = null) { if (fillColor == null) fillColor = Color.white; Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Bilinear; Color[] pixels = new Color[size * size]; float radius = size / 2f; Vector2 center = new Vector2(radius, radius); for (int y = 0; y < size; y++) { for (int x = 0; x < size; x++) { float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center); pixels[y * size + x] = distance <= radius ? fillColor.Value : Color.clear; } } tex.SetPixels(pixels); tex.Apply(false, false); return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f); }
        private Sprite CreateGraphTitleIconSprite(int size = 20) { if (graphTitleIconSprite != null) return graphTitleIconSprite; Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false); Color[] pixels = new Color[size * size]; for (int i = 0; i < pixels.Length; ++i) pixels[i] = Color.clear; Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f); float outerRadius = (size - 1) * 0.45f; float ringThickness = Mathf.Max(1.5f, size * 0.1f); float innerHalfDim = size * 0.22f; float innerCornerRad = innerHalfDim * 0.4f; Color ringCol = new Color(0.55f, 0.55f, 0.55f, 0.9f); Color innerCol = new Color(0.4f, 0.4f, 0.4f, 0.9f); for (int y = 0; y < size; y++) { for (int x = 0; x < size; x++) { Vector2 currentPixel = new Vector2(x, y); float distToCenter = Vector2.Distance(currentPixel, center); if (distToCenter <= outerRadius && distToCenter >= outerRadius - ringThickness) { pixels[y * size + x] = ringCol; } else { float cdx = Mathf.Abs(x - center.x); float cdy = Mathf.Abs(y - center.y); float effectiveInnerHalfDimX = innerHalfDim; float effectiveInnerHalfDimY = innerHalfDim; bool isInBoundingBox = cdx <= effectiveInnerHalfDimX && cdy <= effectiveInnerHalfDimY; if (isInBoundingBox) { bool isCornerRegion = cdx > effectiveInnerHalfDimX - innerCornerRad && cdy > effectiveInnerHalfDimY - innerCornerRad; if (isCornerRegion) { float cornerDistSq = (cdx - (effectiveInnerHalfDimX - innerCornerRad)) * (cdx - (effectiveInnerHalfDimX - innerCornerRad)) + (cdy - (effectiveInnerHalfDimY - innerCornerRad)) * (cdy - (effectiveInnerHalfDimY - innerCornerRad)); if (cornerDistSq <= innerCornerRad * innerCornerRad) { pixels[y * size + x] = innerCol; } } else { pixels[y * size + x] = innerCol; } } } } } tex.SetPixels(pixels); tex.Apply(false, false); graphTitleIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f); return graphTitleIconSprite; }
        private Sprite CreateDotSprite() { int texSize = 256; Texture2D dotTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false, true); dotTexture.filterMode = FilterMode.Point; dotTexture.wrapMode = TextureWrapMode.Repeat; Color[] pixels = new Color[texSize * texSize]; Color backgroundColor = Color.clear; Color dotColor = new Color(0.4f, 0.4f, 0.4f, 0.3f); for (int i = 0; i < pixels.Length; i++) pixels[i] = backgroundColor; int gridSpacing = 8; for (int y = 0; y < texSize; y += gridSpacing) { for (int x = 0; x < texSize; x += gridSpacing) { if (x < texSize && y < texSize) { pixels[y * texSize + x] = dotColor; } } } dotTexture.SetPixels(pixels); dotTexture.Apply(false, false); float pixelsPerUnit = 64f; return Sprite.Create(dotTexture, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false); }
        private void CreateDotBackground() { Transform oldStaticDots = viewportRectangle?.Find("ViewportBackgroundDots"); if (oldStaticDots != null) GameObject.Destroy(oldStaticDots.gameObject); Transform oldMovingDots = contentPane?.Find("ContentBackgroundDots"); if (oldMovingDots != null) GameObject.Destroy(oldMovingDots.gameObject); GameObject movingDotsGO = new GameObject("ContentBackgroundDots"); movingDotsGO.transform.SetParent(contentPane.transform, false); movingDotsGO.transform.SetAsFirstSibling(); dotBackgroundRect = movingDotsGO.AddComponent<RectTransform>(); dotBackgroundRect.anchorMin = new Vector2(-2f, -2f); dotBackgroundRect.anchorMax = new Vector2(3f, 3f); dotBackgroundRect.offsetMin = new Vector2(-2000, -2000); dotBackgroundRect.offsetMax = new Vector2(2000, 2000); Image movingDotsImage = movingDotsGO.AddComponent<Image>(); movingDotsImage.sprite = CreateDotSprite(); movingDotsImage.type = Image.Type.Tiled; movingDotsImage.color = new Color(1f, 1f, 1f, 0.3f); movingDotsImage.raycastTarget = false; dotBackgroundRect.localScale = Vector3.one; }
        private void CreateGraphCanvas() { graphCanvas = new GameObject("RoutimatorGraphCanvas"); canvasComponent = graphCanvas.AddComponent<Canvas>(); canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay; canvasCamera = null; canvasComponent.sortingOrder = 10000; CanvasScaler scaler = graphCanvas.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); if (graphCanvas.GetComponent<GraphicRaycaster>() == null) { graphCanvas.AddComponent<GraphicRaycaster>(); } GameObject panel = new GameObject("GraphPanel"); panel.transform.SetParent(graphCanvas.transform, false); RectTransform panelRect = panel.AddComponent<RectTransform>(); float panelWidth = scaler.referenceResolution.x * 0.8f; float panelHeight = scaler.referenceResolution.y * 0.8f; panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f); panelRect.pivot = new Vector2(0.5f, 0.5f); panelRect.sizeDelta = new Vector2(panelWidth, panelHeight); panelRect.anchoredPosition = Vector2.zero; originalPanelSize = panelRect.sizeDelta; panelImage = panel.AddComponent<Image>(); Color panelBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f); panelImage.sprite = CreateRoundedRectSprite(64, 10, panelBackgroundColor); panelImage.type = Image.Type.Sliced; panelImage.color = Color.white; panelImage.raycastTarget = true; GameObject viewportGO = new GameObject("GraphViewport"); viewportGO.transform.SetParent(panel.transform, false); viewportRectangle = viewportGO.AddComponent<RectTransform>(); viewportRectangle.anchorMin = Vector2.zero; viewportRectangle.anchorMax = Vector2.one; viewportRectangle.pivot = new Vector2(0.5f, 0.5f); float hMargin = 5f; float topMargin = 45f; float bottomMargin = 5f; viewportRectangle.offsetMin = new Vector2(hMargin, bottomMargin); viewportRectangle.offsetMax = new Vector2(-hMargin, -topMargin); Mask mask = viewportGO.AddComponent<Mask>(); mask.showMaskGraphic = false; Image maskImage = viewportGO.AddComponent<Image>(); maskImage.color = Color.white; maskImage.raycastTarget = false; GameObject contentPaneGO = new GameObject("GraphContentPane"); contentPaneGO.transform.SetParent(viewportRectangle.transform, false); contentPane = contentPaneGO.AddComponent<RectTransform>(); contentPane.anchorMin = Vector2.zero; contentPane.anchorMax = Vector2.one; contentPane.pivot = new Vector2(0.5f, 0.5f); contentPane.offsetMin = Vector2.zero; contentPane.offsetMax = Vector2.zero; contentPane.localScale = Vector3.one; EventTrigger trigger = panel.AddComponent<EventTrigger>(); EventTrigger.Entry beginDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag }; beginDragEntry.callback.AddListener(delegate (BaseEventData data) { OnBeginDrag((PointerEventData)data); }); trigger.triggers.Add(beginDragEntry); EventTrigger.Entry dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag }; dragEntry.callback.AddListener(delegate (BaseEventData data) { OnDrag((PointerEventData)data); }); trigger.triggers.Add(dragEntry); EventTrigger.Entry endDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag }; endDragEntry.callback.AddListener(delegate (BaseEventData data) { OnEndDrag((PointerEventData)data); }); trigger.triggers.Add(endDragEntry); EventTrigger.Entry scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll }; scrollEntry.callback.AddListener(delegate (BaseEventData data) { OnScroll((PointerEventData)data); }); trigger.triggers.Add(scrollEntry); EventTrigger canvasTrigger = graphCanvas.GetComponent<EventTrigger>(); if (canvasTrigger == null) canvasTrigger = graphCanvas.AddComponent<EventTrigger>(); AddBackgroundClickListener(); GameObject closeButton = new GameObject("CloseButton"); closeButton.transform.SetParent(panel.transform, false); RectTransform closeRect = closeButton.AddComponent<RectTransform>(); closeRect.anchorMin = new Vector2(1, 1); closeRect.anchorMax = new Vector2(1, 1); closeRect.pivot = new Vector2(1, 1); closeRect.sizeDelta = new Vector2(24, 24); closeRect.anchoredPosition = new Vector2(-8, -8); Image closeImage = closeButton.AddComponent<Image>(); closeImage.color = new Color(0.8f, 0.2f, 0.2f, 0.0f); closeImage.sprite = CreateRoundedRectSprite(32, 5, new Color(0.8f, 0.2f, 0.2f, 0.8f)); closeImage.type = Image.Type.Sliced; closeImage.color = Color.white; closeImage.raycastTarget = true; Button closeButtonComponent = closeButton.AddComponent<Button>(); closeButtonComponent.targetGraphic = closeImage; ColorBlock cb = closeButtonComponent.colors; cb.highlightedColor = new Color(0.9f, 0.3f, 0.3f, 1f); closeButtonComponent.colors = cb; closeButtonComponent.onClick.AddListener(delegate { HideGraph(); }); GameObject closeText = new GameObject("Text"); closeText.transform.SetParent(closeButton.transform, false); RectTransform textRectClose = closeText.AddComponent<RectTransform>(); textRectClose.anchorMin = Vector2.zero; textRectClose.anchorMax = Vector2.one; textRectClose.offsetMin = Vector2.zero; textRectClose.offsetMax = Vector2.zero; Text buttonTextClose = closeText.AddComponent<Text>(); buttonTextClose.text = "X"; buttonTextClose.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); buttonTextClose.fontSize = 16; buttonTextClose.alignment = TextAnchor.MiddleCenter; buttonTextClose.color = Color.white; buttonTextClose.raycastTarget = false; GameObject minimizeButton = new GameObject("MinimizeButton"); minimizeButton.transform.SetParent(panel.transform, false); RectTransform minimizeRect = minimizeButton.AddComponent<RectTransform>(); minimizeRect.anchorMin = new Vector2(1, 1); minimizeRect.anchorMax = new Vector2(1, 1); minimizeRect.pivot = new Vector2(1, 1); minimizeRect.sizeDelta = new Vector2(24, 24); minimizeRect.anchoredPosition = new Vector2(-8 - 24 - 4, -8); Image minimizeImage = minimizeButton.AddComponent<Image>(); minimizeImage.sprite = CreateRoundedRectSprite(32, 5, new Color(0.2f, 0.5f, 0.8f, 0.8f)); minimizeImage.type = Image.Type.Sliced; minimizeImage.color = Color.white; minimizeImage.raycastTarget = true; Button minimizeButtonComponent = minimizeButton.AddComponent<Button>(); minimizeButtonComponent.targetGraphic = minimizeImage; ColorBlock cbMinimize = minimizeButtonComponent.colors; cbMinimize.highlightedColor = new Color(0.3f, 0.6f, 0.9f, 1f); minimizeButtonComponent.colors = cbMinimize; minimizeButtonComponent.onClick.AddListener(ToggleMinimizeGraph); GameObject minimizeTextObj = new GameObject("Text"); minimizeTextObj.transform.SetParent(minimizeButton.transform, false); RectTransform textRectMinimize = minimizeTextObj.AddComponent<RectTransform>(); textRectMinimize.anchorMin = Vector2.zero; textRectMinimize.anchorMax = Vector2.one; textRectMinimize.offsetMin = Vector2.zero; textRectMinimize.offsetMax = Vector2.zero; minimizeButtonText = minimizeTextObj.AddComponent<Text>(); minimizeButtonText.text = "-"; minimizeButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); minimizeButtonText.fontSize = 16; minimizeButtonText.alignment = TextAnchor.MiddleCenter; minimizeButtonText.color = Color.white; minimizeButtonText.raycastTarget = false; GameObject titleContainer = new GameObject("TitleContainer"); titleContainer.transform.SetParent(panel.transform, false); RectTransform titleContainerRect = titleContainer.AddComponent<RectTransform>(); titleContainerRect.anchorMin = new Vector2(0, 1); titleContainerRect.anchorMax = new Vector2(0, 1); titleContainerRect.pivot = new Vector2(0, 1); titleContainerRect.anchoredPosition = new Vector2(12, -9); titleContainerRect.sizeDelta = new Vector2(0, 22); HorizontalLayoutGroup titleLayout = titleContainer.AddComponent<HorizontalLayoutGroup>(); titleLayout.spacing = 6f; titleLayout.childAlignment = TextAnchor.MiddleLeft; titleLayout.childControlWidth = false; titleLayout.childControlHeight = true; titleLayout.childForceExpandWidth = false; titleLayout.childForceExpandHeight = false; ContentSizeFitter titleSizeFitter = titleContainer.AddComponent<ContentSizeFitter>(); titleSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; titleSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; GameObject iconObj = new GameObject("TitleIcon"); iconObj.transform.SetParent(titleContainer.transform, false); RectTransform iconRect = iconObj.AddComponent<RectTransform>(); iconRect.sizeDelta = new Vector2(20, 20); Image iconImage = iconObj.AddComponent<Image>(); iconImage.sprite = CreateGraphTitleIconSprite(20); iconImage.preserveAspect = true; iconImage.raycastTarget = false; GameObject titleTextObj = new GameObject("TitleText"); titleTextObj.transform.SetParent(titleContainer.transform, false); Text titleTextComponent = titleTextObj.AddComponent<Text>(); titleTextComponent.text = "State Graph"; titleTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); titleTextComponent.fontSize = 16; titleTextComponent.fontStyle = FontStyle.Normal; titleTextComponent.alignment = TextAnchor.MiddleLeft; titleTextComponent.color = new Color(0.8f, 0.8f, 0.8f, 0.9f); titleTextComponent.raycastTarget = false; LayoutElement textLayoutElement = titleTextObj.AddComponent<LayoutElement>(); textLayoutElement.preferredHeight = 18; CreateLegend(panel.transform); CreateResetButton(panel.transform, new Vector2(-10, 180)); CreateLinkButton(panel.transform, new Vector2(-10, 140)); CreateDragArrow(); }
        private void CreateDragArrow() { if (contentPane == null) return; dragArrow = new GameObject("DragArrow"); dragArrow.transform.SetParent(contentPane.transform, false); dragArrowRect = dragArrow.AddComponent<RectTransform>(); dragArrowRect.anchorMin = new Vector2(0.5f, 0.5f); dragArrowRect.anchorMax = new Vector2(0.5f, 0.5f); dragArrowRect.pivot = new Vector2(0f, 0.5f); dragArrowRect.sizeDelta = new Vector2(100f, LINE_THICKNESS); Image arrowLineImage = dragArrow.AddComponent<Image>(); arrowLineImage.color = new Color(1f, 1f, 1f, 0.7f); arrowLineImage.raycastTarget = false; GameObject arrowHead = new GameObject("ArrowHead"); arrowHead.transform.SetParent(dragArrow.transform, false); RectTransform arrowHeadRect = arrowHead.AddComponent<RectTransform>(); arrowHeadRect.anchorMin = new Vector2(1f, 0.5f); arrowHeadRect.anchorMax = new Vector2(1f, 0.5f); arrowHeadRect.pivot = new Vector2(0.5f, 0.5f); arrowHeadRect.sizeDelta = new Vector2(ARROW_SIZE, ARROW_SIZE); arrowHeadRect.anchoredPosition = Vector2.zero; Image arrowHeadImage = arrowHead.AddComponent<Image>(); arrowHeadImage.sprite = CreateArrowSprite(); arrowHeadImage.color = new Color(1f, 1f, 1f, 0.7f); arrowHeadImage.raycastTarget = false; dragArrow.SetActive(false); }
        private void CreateLinkButton(Transform parent, Vector2 anchoredPosition) { linkButton = new GameObject("LinkButton"); linkButton.transform.SetParent(parent, false); RectTransform buttonRect = linkButton.AddComponent<RectTransform>(); buttonRect.anchorMin = new Vector2(1, 0); buttonRect.anchorMax = new Vector2(1, 0); buttonRect.pivot = new Vector2(1, 0); buttonRect.sizeDelta = new Vector2(120, 30); buttonRect.anchoredPosition = anchoredPosition; Image buttonImage = linkButton.AddComponent<Image>(); buttonImage.sprite = CreateRoundedRectSprite(32, 5, new Color(0.3f, 0.3f, 0.7f, 0.8f)); buttonImage.type = Image.Type.Sliced; buttonImage.color = Color.white; buttonImage.raycastTarget = true; Button buttonComponent = linkButton.AddComponent<Button>(); buttonComponent.targetGraphic = buttonImage; buttonComponent.onClick.AddListener(ToggleLinkingMode); GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(linkButton.transform, false); RectTransform textRect = textObj.AddComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero; linkButtonText = textObj.AddComponent<Text>(); linkButtonText.text = "Link States"; linkButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); linkButtonText.fontSize = 14; linkButtonText.alignment = TextAnchor.MiddleCenter; linkButtonText.color = Color.white; linkButtonText.raycastTarget = false; }
        private void CreateResetButton(Transform parent, Vector2 anchoredPosition) { resetButton = new GameObject("ResetLayoutButton"); resetButton.transform.SetParent(parent, false); RectTransform buttonRect = resetButton.AddComponent<RectTransform>(); buttonRect.anchorMin = new Vector2(1, 0); buttonRect.anchorMax = new Vector2(1, 0); buttonRect.pivot = new Vector2(1, 0); buttonRect.sizeDelta = new Vector2(120, 30); buttonRect.anchoredPosition = anchoredPosition; Image buttonImage = resetButton.AddComponent<Image>(); buttonImage.sprite = CreateRoundedRectSprite(32, 5, new Color(0.8f, 0.3f, 0.3f, 0.8f)); buttonImage.type = Image.Type.Sliced; buttonImage.color = Color.white; buttonImage.raycastTarget = true; Button buttonComponent = resetButton.AddComponent<Button>(); buttonComponent.targetGraphic = buttonImage; buttonComponent.onClick.AddListener(ResetLayout); GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(resetButton.transform, false); RectTransform textRect = textObj.AddComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero; Text resetButtonText = textObj.AddComponent<Text>(); resetButtonText.text = "Reset Layout"; resetButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); resetButtonText.fontSize = 14; resetButtonText.alignment = TextAnchor.MiddleCenter; resetButtonText.color = Color.white; resetButtonText.raycastTarget = false; }
        private void CreateLegend(Transform parent) { legend = new GameObject("Legend"); legend.transform.SetParent(parent, false); RectTransform legendRect = legend.AddComponent<RectTransform>(); legendRect.anchorMin = new Vector2(1, 0); legendRect.anchorMax = new Vector2(1, 0); legendRect.pivot = new Vector2(1, 0); legendRect.sizeDelta = new Vector2(180, 120); legendRect.anchoredPosition = new Vector2(-10, 10); Image legendBg = legend.AddComponent<Image>(); legendBg.sprite = CreateRoundedRectSprite(32, 5, new Color(0.15f, 0.15f, 0.15f, 0.8f)); legendBg.type = Image.Type.Sliced; legendBg.color = Color.white; legendBg.raycastTarget = false; AddLegendItem(legend.transform, "Default State", defaultNodeColor, 0); AddLegendItem(legend.transform, "VS State", vsStateColor, 1); AddLegendItem(legend.transform, "VT State", vtStateColor, 2); AddLegendItem(legend.transform, "NAV State", navStateColor, 3); GameObject titleObj = new GameObject("Title"); titleObj.transform.SetParent(legend.transform, false); RectTransform titleRect = titleObj.AddComponent<RectTransform>(); titleRect.anchorMin = new Vector2(0, 1); titleRect.anchorMax = new Vector2(1, 1); titleRect.pivot = new Vector2(0.5f, 1); titleRect.sizeDelta = new Vector2(-10, 25); titleRect.anchoredPosition = new Vector2(0, -5); Text titleText = titleObj.AddComponent<Text>(); titleText.text = "Legend"; titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); titleText.fontSize = 14; titleText.alignment = TextAnchor.MiddleCenter; titleText.color = Color.white; titleText.raycastTarget = false; }
        private void AddLegendItem(Transform parent, string label, Color color, int position) { float itemHeight = 20f, spacing = 2f, topPadding = 30f; GameObject item = new GameObject("LegendItem_" + label); item.transform.SetParent(parent, false); RectTransform itemRect = item.AddComponent<RectTransform>(); itemRect.anchorMin = new Vector2(0, 1); itemRect.anchorMax = new Vector2(1, 1); itemRect.pivot = new Vector2(0.5f, 1); itemRect.sizeDelta = new Vector2(-10, itemHeight); itemRect.anchoredPosition = new Vector2(0, -topPadding - (position * (itemHeight + spacing))); GameObject colorBox = new GameObject("ColorBox"); colorBox.transform.SetParent(item.transform, false); RectTransform boxRect = colorBox.AddComponent<RectTransform>(); boxRect.anchorMin = new Vector2(0, 0.5f); boxRect.anchorMax = new Vector2(0, 0.5f); boxRect.pivot = new Vector2(0, 0.5f); boxRect.sizeDelta = new Vector2(16, 16); boxRect.anchoredPosition = new Vector2(5, 0); Image boxImage = colorBox.AddComponent<Image>(); boxImage.sprite = CreateRoundedRectSprite(16, 3, color); boxImage.type = Image.Type.Sliced; boxImage.color = Color.white; boxImage.raycastTarget = false; GameObject labelObj = new GameObject("Label"); labelObj.transform.SetParent(item.transform, false); RectTransform labelRect = labelObj.AddComponent<RectTransform>(); labelRect.anchorMin = new Vector2(0, 0); labelRect.anchorMax = new Vector2(1, 1); labelRect.pivot = new Vector2(0, 0.5f); labelRect.offsetMin = new Vector2(26, 0); labelRect.offsetMax = new Vector2(-5, 0); Text labelText = labelObj.AddComponent<Text>(); labelText.text = label; labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); labelText.fontSize = 12; labelText.alignment = TextAnchor.MiddleLeft; labelText.color = Color.white; labelText.raycastTarget = false; }
        private void OnBeginDrag(PointerEventData data) { DestroyContextMenu(); if (isDraggingNode || isDraggingLink) { isDragging = false; return; } if (panelImage != null && data.pointerPressRaycast.gameObject == panelImage.gameObject) { if (data.button != PointerEventData.InputButton.Left) return; isDragging = true; dragStartPosition = data.position; offsetAtDragStart = contentPane.anchoredPosition; } else { isDragging = false; } }
        private void OnDrag(PointerEventData data) { if (isDragging) { if (data.button != PointerEventData.InputButton.Left) return; Vector2 dragDelta = data.position - dragStartPosition; panOffset = offsetAtDragStart + dragDelta; UpdateContentPaneTransform(); } }
        private void OnEndDrag(PointerEventData data) { if (data.button != PointerEventData.InputButton.Left) return; isDragging = false; }
        private void OnScroll(PointerEventData data) { if (panelImage == null || canvasComponent == null || contentPane == null) return; RectTransform panelRect = panelImage.rectTransform; if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, data.position, canvasCamera)) return; float scroll = data.scrollDelta.y; if (Mathf.Abs(scroll) > 0.01f) { float oldZoomLevel = zoomLevel; float scaleFactor = 1.0f + scroll * ZOOM_SENSITIVITY_MULTIPLIER; zoomLevel *= scaleFactor; zoomLevel = Mathf.Clamp(zoomLevel, MIN_ZOOM, MAX_ZOOM); if (Mathf.Approximately(oldZoomLevel, zoomLevel)) return; float actualZoomFactor = zoomLevel / oldZoomLevel; Vector2 mousePosInContentLocal; bool isInsideContentPane = RectTransformUtility.ScreenPointToLocalPointInRectangle(contentPane, data.position, canvasCamera, out mousePosInContentLocal); if (isInsideContentPane) { Vector2 pivotOffset = mousePosInContentLocal * (1f - actualZoomFactor); panOffset += pivotOffset; } UpdateContentPaneTransform(); } }
        private void ResetPanAndZoom() { zoomLevel = 1f; panOffset = Vector2.zero; UpdateContentPaneTransform(); }
        private void UpdateContentPaneTransform() { if (contentPane != null) { contentPane.localScale = new Vector3(zoomLevel, zoomLevel, 1f); contentPane.anchoredPosition = panOffset; if (dotBackgroundRect != null) { float inverseScale = 1f / zoomLevel; dotBackgroundRect.localScale = new Vector3(inverseScale, inverseScale, 1f); float expandFactor = 3f * inverseScale; dotBackgroundRect.anchorMin = new Vector2(-expandFactor, -expandFactor); dotBackgroundRect.anchorMax = new Vector2(1f + expandFactor, 1f + expandFactor); } } }
        private void GenerateGraph() { ClearGraph(); if (owner == null || owner.stateManager == null) { AddEmptyGraphMessage("Owner or StateManager is null."); return; } List<RoutimatorState.State> allStates = owner.stateManager.GetStates(); if (allStates == null || allStates.Count == 0) { AddEmptyGraphMessage("No states found."); return; } if (contentPane == null) { SuperController.LogError("RoutimatorGraph: contentPane is null in GenerateGraph."); return; } AssignGroupColors(); Dictionary<string, Vector2> layoutPositions = CalculateHierarchicalLayout(allStates); HashSet<string> nodesToProcess = new HashSet<string>(); foreach (var s_state in allStates) if (s_state != null && !string.IsNullOrEmpty(s_state.Name)) nodesToProcess.Add(s_state.Name); foreach (RoutimatorState.State state_node_loop in allStates.ToList()) { if (state_node_loop == null || !nodesToProcess.Contains(state_node_loop.Name)) continue; Vector2 pos = layoutPositions.ContainsKey(state_node_loop.Name) ? layoutPositions[state_node_loop.Name] : Vector2.zero; Vector2? savedPos = owner?.GetSavedNodePosition(state_node_loop.Name); CreateStateNode(state_node_loop, savedPos ?? pos); } Dictionary<ConnectionPair, bool> tempProcessedConnections = new Dictionary<ConnectionPair, bool>(); foreach (RoutimatorState.State state_loop in allStates.ToList()) { if (state_loop == null || !nodesToProcess.Contains(state_loop.Name)) continue; if (state_loop.Transitions != null) { foreach (RoutimatorState.State targetState in state_loop.Transitions.ToList()) { if (targetState == null || !nodesToProcess.Contains(targetState.Name)) continue; ConnectionPair pair = new ConnectionPair(state_loop.Name, targetState.Name); if (tempProcessedConnections.ContainsKey(pair)) continue; CreateConnection(state_loop.Name, targetState.Name); tempProcessedConnections[pair] = true; } } if (state_loop.Transitions != null && state_loop.Transitions.Contains(state_loop)) { ConnectionPair pair = new ConnectionPair(state_loop.Name, state_loop.Name); if (!tempProcessedConnections.ContainsKey(pair)) { CreateConnection(state_loop.Name, state_loop.Name); tempProcessedConnections[pair] = true; } } } CreateDotBackground(); foreach (var nodeRect in stateNodes.Values.ToArray()) { if (nodeRect != null) nodeRect.SetAsLastSibling(); } UpdateHighlights(); }
        private void AddEmptyGraphMessage(string message = "No states found.") { if (contentPane == null) return; GameObject messageObj = new GameObject("EmptyMessage"); messageObj.transform.SetParent(contentPane.transform, false); RectTransform messageRect = messageObj.AddComponent<RectTransform>(); messageRect.anchorMin = new Vector2(0.5f, 0.5f); messageRect.anchorMax = new Vector2(0.5f, 0.5f); messageRect.pivot = new Vector2(0.5f, 0.5f); messageRect.sizeDelta = new Vector2(400, 100); messageRect.anchoredPosition = Vector2.zero; Text messageText = messageObj.AddComponent<Text>(); messageText.text = message; messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); messageText.fontSize = 18; messageText.alignment = TextAnchor.MiddleCenter; messageText.color = Color.white; messageText.raycastTarget = false; }
        private void AssignGroupColors() { groupColors.Clear(); if (owner == null) return; List<string> groups = owner.GetGroups(); if (groups == null || predefinedColors.Length == 0) return; for (int i = 0; i < groups.Count; i++) { if (!string.IsNullOrEmpty(groups[i])) { groupColors[groups[i]] = predefinedColors[i % predefinedColors.Length]; } } }
        private Dictionary<string, Vector2> CalculateHierarchicalLayout(List<RoutimatorState.State> states) { Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>(); if (states == null) return positions; int nodesPerRow = Mathf.CeilToInt(Mathf.Sqrt(states.Count)); if (nodesPerRow == 0) nodesPerRow = 1; float startX = -(nodesPerRow - 1) * HORIZONTAL_SPACING / 2f; float startY = (Mathf.CeilToInt((float)states.Count / nodesPerRow) - 1) * LEVEL_HEIGHT / 2f; for (int i = 0; i < states.Count; ++i) { if (states[i] == null || string.IsNullOrEmpty(states[i].Name)) continue; float x = startX + (i % nodesPerRow) * HORIZONTAL_SPACING; float y = startY - (i / nodesPerRow) * LEVEL_HEIGHT; positions[states[i].Name] = new Vector2(x, y); } return positions; }

        private void CreateStateNode(RoutimatorState.State state, Vector2 position)
        {
            if (contentPane == null || state == null) return;
            GameObject nodeObj = new GameObject("Node_" + state.Name);
            nodeObj.transform.SetParent(contentPane.transform, false);
            RectTransform nodeRect = nodeObj.AddComponent<RectTransform>();
            nodeRect.anchorMin = new Vector2(0.5f, 0.5f);
            nodeRect.anchorMax = new Vector2(0.5f, 0.5f);
            nodeRect.pivot = new Vector2(0.5f, 0.5f);
            nodeRect.sizeDelta = new Vector2(NODE_WIDTH, NODE_HEIGHT);
            nodeRect.anchoredPosition = position;
            Shadow shadow = nodeObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(4, -4);
            Image nodeImage = nodeObj.AddComponent<Image>();
            nodeImage.sprite = CreateNodeRoundedSprite();
            nodeImage.type = Image.Type.Sliced;
            nodeImage.raycastTarget = true;

            // Ustawianie koloru noda
            if (state.Name.StartsWith("VS_")) nodeImage.color = vsStateColor;
            else if (state.Name.StartsWith("VT_")) nodeImage.color = vtStateColor;
            else if (state.Name.StartsWith("NAV_")) nodeImage.color = navStateColor;
            else nodeImage.color = defaultNodeColor; // Użyj domyślnego koloru

            // --- ZMIENIONY KOD DLA IKONKI LUDZIKA ---
            Transform existingManIcon = nodeObj.transform.Find("ManIcon");
            if (existingManIcon != null)
            {
                GameObject.Destroy(existingManIcon.gameObject);
            }

            if (state.IsWalkingEnabled) // Wyświetlaj ikonkę tylko jeśli IsWalkingEnabled jest true
            {
                GameObject manIconObj = new GameObject("ManIcon");
                manIconObj.transform.SetParent(nodeObj.transform, false);
                RectTransform iconRect = manIconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(1, 1);
                iconRect.anchorMax = new Vector2(1, 1);
                iconRect.pivot = new Vector2(1, 1);
                float iconSize = 12f;
                iconRect.sizeDelta = new Vector2(iconSize, iconSize);
                iconRect.anchoredPosition = new Vector2(-4f, -4f);

                Image iconImage = manIconObj.AddComponent<Image>();
                iconImage.sprite = CreateManIconSprite((int)iconSize);
                iconImage.color = Color.white;
                iconImage.raycastTarget = false;
                manIconObj.transform.SetAsLastSibling();
            }
            // --- KONIEC ZMIENIONEGO KODU DLA IKONKI LUDZIKA ---

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(nodeObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 2);
            textRect.offsetMax = new Vector2(-5, -2);
            Text text = textObj.AddComponent<Text>();
            text.text = state.Name;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 12;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            textObj.transform.SetAsLastSibling();

            Button button = nodeObj.AddComponent<Button>();
            button.targetGraphic = nodeImage;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            Color baseColor = nodeImage.color;
            colors.normalColor = baseColor;
            colors.highlightedColor = new Color(Mathf.Min(1f, baseColor.r * 1.2f), Mathf.Min(1f, baseColor.g * 1.2f), Mathf.Min(1f, baseColor.b * 1.2f), baseColor.a);
            colors.pressedColor = new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f, baseColor.a);
            colors.disabledColor = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, baseColor.a * 0.7f);
            colors.colorMultiplier = 1.0f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            EventTrigger nodeTrigger = nodeObj.AddComponent<EventTrigger>();
            EventTrigger.Entry nodeBeginDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            nodeBeginDragEntry.callback.AddListener((data) => { OnNodeBeginDrag((PointerEventData)data, nodeRect, state); });
            nodeTrigger.triggers.Add(nodeBeginDragEntry);
            EventTrigger.Entry nodeDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            nodeDragEntry.callback.AddListener((data) => { OnNodeDrag((PointerEventData)data); });
            nodeTrigger.triggers.Add(nodeDragEntry);
            EventTrigger.Entry nodeEndDragEntry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            nodeEndDragEntry.callback.AddListener((data) => { OnNodeEndDrag((PointerEventData)data); });
            nodeTrigger.triggers.Add(nodeEndDragEntry);
            EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            clickEntry.callback.AddListener((data) => { DestroyContextMenu(); OnNodePointerClick((PointerEventData)data, state); });
            nodeTrigger.triggers.Add(clickEntry);

            // --- NOWY KOD NAPRAWIAJĄCY BUG ---
            EventTrigger.Entry scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scrollEntry.callback.AddListener((data) => { OnScroll((PointerEventData)data); });
            nodeTrigger.triggers.Add(scrollEntry);
            // --- KONIEC NOWEGO KODU ---

            GameObject linkCircle = new GameObject("LinkCircle");
            linkCircle.transform.SetParent(nodeObj.transform, false);
            RectTransform circleRect = linkCircle.AddComponent<RectTransform>();
            circleRect.anchorMin = new Vector2(0.5f, 0f);
            circleRect.anchorMax = new Vector2(0.5f, 0f);
            circleRect.pivot = new Vector2(0.5f, 0.5f);
            circleRect.sizeDelta = new Vector2(9, 9);
            circleRect.anchoredPosition = Vector2.zero;

            Image circleImage = linkCircle.AddComponent<Image>();
            circleImage.sprite = CreateCircleSprite(16, Color.white);
            circleImage.color = Color.white;
            circleImage.raycastTarget = true;

            Button circleButton = linkCircle.AddComponent<Button>();
            circleButton.targetGraphic = circleImage;
            circleButton.transition = Selectable.Transition.ColorTint;
            ColorBlock circleColors = circleButton.colors;
            circleColors.normalColor = Color.white;
            circleColors.highlightedColor = new Color(0.8f, 0.8f, 1f, 1f);
            circleColors.pressedColor = new Color(0.6f, 0.6f, 0.8f, 1f);
            circleButton.colors = circleColors;

            EventTrigger circleTrigger = linkCircle.AddComponent<EventTrigger>();
            EventTrigger.Entry circlePointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            circlePointerDownEntry.callback.AddListener((data) =>
            {
                if (!isDraggingLink)
                    OnLinkCirclePointerDown((PointerEventData)data, state);
            });
            circleTrigger.triggers.Add(circlePointerDownEntry);
            stateNodes[state.Name] = nodeRect;
        }

        private void OnLinkCirclePointerDown(PointerEventData data, RoutimatorState.State sourceState) { if (data.button != PointerEventData.InputButton.Left) return; if (isDraggingLink) return; isDraggingLink = true; dragLinkSourceNode = sourceState.Name; linkSourceState = sourceState; if (dragArrow != null && stateNodes.ContainsKey(sourceState.Name)) { RectTransform sourceNodeRect = stateNodes[sourceState.Name]; Vector2 startPos = sourceNodeRect.anchoredPosition + new Vector2(0, -NODE_HEIGHT / 2f); dragArrowRect.anchoredPosition = startPos; dragArrow.SetActive(true); dragArrow.transform.SetAsLastSibling(); UpdateDragArrow(startPos); if (dragArrowUpdateCoroutine != null) owner.StopCoroutine(dragArrowUpdateCoroutine); dragArrowUpdateCoroutine = owner.StartCoroutine(UpdateDragArrowCoroutine()); } }
        private void CancelLinkDragging() { if (dragArrowUpdateCoroutine != null) { owner.StopCoroutine(dragArrowUpdateCoroutine); dragArrowUpdateCoroutine = null; } isDraggingLink = false; dragLinkSourceNode = null; linkSourceState = null; if (dragArrow != null) dragArrow.SetActive(false); }
        private void UpdateDragArrow(Vector2 mousePos) { if (dragArrowRect == null || !isDraggingLink) return; Vector2 startPos = dragArrowRect.anchoredPosition; Vector2 direction = mousePos - startPos; float distance = direction.magnitude; if (distance > 0.1f) { dragArrowRect.sizeDelta = new Vector2(distance, LINE_THICKNESS); float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; dragArrowRect.localEulerAngles = new Vector3(0, 0, angle); } }
        public void UpdateHighlights()
        {
            if (owner == null) return;
            RoutimatorState.State currentState = owner.GetCurrentState();

            foreach (var kvp in stateNodes.ToArray()) // Iteruj po kopii, jeśli modyfikujesz stateNodes wewnątrz
            {
                string nodeName = kvp.Key;
                RectTransform nodeRect = kvp.Value;
                if (nodeRect == null) continue;

                RoutimatorState.State nodeState = owner.stateManager?.GetStateGlobal(nodeName);
                if (nodeState == null) continue;

                Transform currentOutline = nodeRect.transform.Find("InternalOutline");
                bool shouldHaveCurrentOutline = (nodeState == currentState);

                if (shouldHaveCurrentOutline && currentOutline == null)
                {
                    GameObject parent = new GameObject("InternalOutline");
                    parent.transform.SetParent(nodeRect.transform, false);
                    RectTransform pr = parent.AddComponent<RectTransform>();
                    pr.anchorMin = Vector2.zero;
                    pr.anchorMax = Vector2.one;
                    pr.offsetMin = Vector2.zero;
                    pr.offsetMax = Vector2.zero;
                    CreateOutlineBar(parent.transform, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, OUTLINE_THICKNESS), Vector2.zero, currentStateOutlineColor);
                    CreateOutlineBar(parent.transform, "BottomBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, OUTLINE_THICKNESS), Vector2.zero, currentStateOutlineColor);
                    CreateOutlineBar(parent.transform, "LeftBar", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(OUTLINE_THICKNESS, 0), Vector2.zero, currentStateOutlineColor);
                    CreateOutlineBar(parent.transform, "RightBar", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(OUTLINE_THICKNESS, 0), Vector2.zero, currentStateOutlineColor);
                    parent.transform.SetAsFirstSibling();
                    currentOutline = parent.transform; // Zapisz nowo utworzoną ramkę
                }
                else if (!shouldHaveCurrentOutline && currentOutline != null)
                {
                    GameObject.Destroy(currentOutline.gameObject);
                }

                // Zarządzanie currentBlinkOutlineTransform dla aktywnego mrugania
                if (shouldHaveCurrentOutline)
                {
                    currentBlinkOutlineTransform = currentOutline;
                }

                if (isLinking && nodeState == linkSourceState)
                {
                    HighlightLinkSourceNode(true);
                }
                else
                {
                    HighlightLinkSourceNode(false, nodeRect);
                }
            }
            EnsureTextOnTop();

            // Po pętli, zaktualizuj referencję do ramki dla bieżącego stanu, jeśli istnieje
            if (currentState != null && stateNodes.ContainsKey(currentState.Name))
            {
                currentBlinkOutlineTransform = stateNodes[currentState.Name].transform.Find("InternalOutline");
            }
            else
            {
                // Jeśli nie ma aktualnego stanu, upewnij się, że nie ma referencji do mrugania
                currentBlinkOutlineTransform = null;
            }

            // Jeśli żadna korutyna mrugania nie jest aktywna, a mamy główną ramkę, upewnij się, że jest widoczna.
            // Korutyny same zadbają o swoją logikę włączania/wyłączania.
            if (blinkOnChangeCoroutine == null && continuousBlinkCoroutine == null)
            {
                if (currentBlinkOutlineTransform != null)
                {
                    currentBlinkOutlineTransform.gameObject.SetActive(true);
                    blinkOutlineVisible = true;
                }
            }
        }
        private void EnsureTextOnTop() { foreach (var nodeRect in stateNodes.Values.ToArray()) { if (nodeRect != null) { Transform textTransform = nodeRect.transform.Find("Text"); if (textTransform != null) textTransform.SetAsLastSibling(); } } }
        private void OnNodeClick(RoutimatorState.State state) { if (state == null) return; if (isLinking) { HandleNodeClickInLinkingMode(state); } else { if (owner != null) { JSONStorableStringChooser stateChooser = owner.GetStateChooser(); if (stateChooser != null) stateChooser.val = state.Name; JSONStorableStringChooser groupChooser = owner.GetGroupChooser(); if (groupChooser != null) groupChooser.val = state.Group ?? ""; UpdateHighlights(); } } }
        private void CreateOutlineBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition, Color color) { GameObject barObj = new GameObject(name); barObj.transform.SetParent(parent, false); RectTransform barRect = barObj.AddComponent<RectTransform>(); barRect.anchorMin = anchorMin; barRect.anchorMax = anchorMax; barRect.pivot = pivot; barRect.sizeDelta = sizeDelta; barRect.anchoredPosition = anchoredPosition; Image barImage = barObj.AddComponent<Image>(); barImage.color = color; barImage.raycastTarget = false; }
        private void CreateConnection(string sourceState, string targetState) { if (contentPane == null || !stateNodes.ContainsKey(sourceState) || !stateNodes.ContainsKey(targetState) || stateNodes[sourceState] == null || stateNodes[targetState] == null) return; GameObject connectionObj = new GameObject($"Connection_{sourceState}_to_{targetState}"); connectionObj.transform.SetParent(contentPane.transform, false); connectionObj.transform.SetAsFirstSibling(); DrawConnectionGraphics(connectionObj, sourceState, targetState); connections.Add(connectionObj); connectionData[connectionObj] = new KeyValuePair<string, string>(sourceState, targetState); }
        private void DrawConnectionGraphics(GameObject connectionObj, string sourceState, string targetState) { if (connectionObj == null || contentPane == null || owner?.stateManager == null || !stateNodes.ContainsKey(sourceState) || !stateNodes.ContainsKey(targetState) || stateNodes[sourceState] == null || stateNodes[targetState] == null) return; Vector2 startNodePos = stateNodes[sourceState].anchoredPosition; Vector2 endNodePos = stateNodes[targetState].anchoredPosition; RoutimatorState.State source = owner.stateManager.GetStateGlobal(sourceState); RoutimatorState.State target = owner.stateManager.GetStateGlobal(targetState); bool sourceToTarget = source?.Transitions.Contains(target) ?? false; bool targetToSource = target?.Transitions.Contains(source) ?? false; if (sourceState == targetState) { if (sourceToTarget) CreateSelfConnection(connectionObj, startNodePos); return; } Vector2 startEdgePos = CalculateEdgeIntersection(startNodePos, endNodePos, NODE_WIDTH, NODE_HEIGHT); Vector2 endEdgePos = CalculateEdgeIntersection(endNodePos, startNodePos, NODE_WIDTH, NODE_HEIGHT); Vector2 dir = (endEdgePos - startEdgePos).normalized; if (dir == Vector2.zero) dir = Vector2.right; Vector2 visualTipEnd = endEdgePos; Vector2 visualTipStart = startEdgePos; Vector2 lineEnd = endEdgePos - dir * ARROW_GAP; Vector2 lineStart = startEdgePos + dir * ARROW_GAP; if ((lineEnd - lineStart).sqrMagnitude > 1f && (sourceToTarget || targetToSource)) { List<Vector2> pathPoints = CalculateBezierPath(lineStart, lineEnd); CreateBezierLine(connectionObj, pathPoints, LINE_THICKNESS); if (pathPoints.Count >= 2) { if (sourceToTarget) { CreateArrow(connectionObj, lineEnd, visualTipEnd, false); } if (targetToSource) { CreateArrow(connectionObj, lineStart, visualTipStart, true); } } } }
        private void ClearConnectionGraphics(GameObject connectionObj) { if (connectionObj == null) return; for (int i = connectionObj.transform.childCount - 1; i >= 0; i--) { GameObject.Destroy(connectionObj.transform.GetChild(i).gameObject); } }
        private void OnNodeBeginDrag(PointerEventData data, RectTransform node, RoutimatorState.State state) { DestroyContextMenu(); if (isDragging || isDraggingNode || isLinking || isDraggingLink) return; if (data.button != PointerEventData.InputButton.Left) return; isDraggingNode = true; draggedNodeRect = node; draggedState = state; draggedNodeRect.SetAsLastSibling(); Vector2 localMousePos; RectTransformUtility.ScreenPointToLocalPointInRectangle(contentPane, data.position, canvasCamera, out localMousePos); dragNodeStartPosition = draggedNodeRect.anchoredPosition; dragMouseOffset = dragNodeStartPosition - localMousePos; }
        private void OnNodeDrag(PointerEventData data) { if (!isDraggingNode || draggedNodeRect == null) return; if (data.button != PointerEventData.InputButton.Left) return; Vector2 localMousePos; RectTransformUtility.ScreenPointToLocalPointInRectangle(contentPane, data.position, canvasCamera, out localMousePos); draggedNodeRect.anchoredPosition = localMousePos + dragMouseOffset; UpdateConnectionsForNode(draggedState.Name); }
        private void OnNodeEndDrag(PointerEventData data) { if (!isDraggingNode) return; if (data.button != PointerEventData.InputButton.Left) return; if (owner != null && draggedState != null && draggedNodeRect != null) { owner.SaveNodePosition(draggedState.Name, draggedNodeRect.anchoredPosition); } isDraggingNode = false; draggedNodeRect = null; draggedState = null; }
        private void UpdateConnectionsForNode(string nodeName) { if (string.IsNullOrEmpty(nodeName)) return; foreach (var kvp in connectionData.ToArray()) { GameObject connectionObj = kvp.Key; string sourceName = kvp.Value.Key; string targetName = kvp.Value.Value; if (sourceName == nodeName || targetName == nodeName) { if (connectionObj != null) { ClearConnectionGraphics(connectionObj); DrawConnectionGraphics(connectionObj, sourceName, targetName); } } } }
        private Vector2 CalculateEdgeIntersection(Vector2 nodeCenter, Vector2 otherNodeCenter, float nodeWidth, float nodeHeight) { Vector2 direction = (otherNodeCenter - nodeCenter); if (direction == Vector2.zero) return nodeCenter; float halfWidth = nodeWidth / 2f; float halfHeight = nodeHeight / 2f; if (Mathf.Abs(direction.x) < 0.001f) { return nodeCenter + new Vector2(0, Mathf.Sign(direction.y) * halfHeight); } if (Mathf.Abs(direction.y) < 0.001f) { return nodeCenter + new Vector2(Mathf.Sign(direction.x) * halfWidth, 0); } float tanTheta = Mathf.Abs(direction.y / direction.x); float tanNode = halfHeight / halfWidth; Vector2 edgePoint; if (tanTheta <= tanNode) { float sign = Mathf.Sign(direction.x); edgePoint = nodeCenter + new Vector2(sign * halfWidth, sign * halfWidth * direction.y / direction.x); } else { float sign = Mathf.Sign(direction.y); edgePoint = nodeCenter + new Vector2(sign * halfHeight * direction.x / direction.y, sign * halfHeight); } return edgePoint; }
        private List<Vector2> CalculateBezierPath(Vector2 start, Vector2 end) { List<Vector2> points = new List<Vector2>(); points.Add(start); Vector2 direction = end - start; float distance = direction.magnitude; if (distance > HORIZONTAL_SPACING * 0.4f && Mathf.Abs(direction.x) > 20f && Mathf.Abs(direction.y) > 20f) { float curveOffset = Mathf.Clamp(distance * CURVE_STRENGTH, 20f, 80f); Vector2 perpendicular; if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) { perpendicular = new Vector2(0, Mathf.Sign(direction.x) * Mathf.Sign(direction.y) * curveOffset * 0.7f); } else { perpendicular = new Vector2(-Mathf.Sign(direction.y) * Mathf.Sign(direction.x) * curveOffset * 0.7f, 0); } Vector2 midPoint = start + direction * 0.5f; Vector2 control1 = midPoint + perpendicular; int segments = 10; for (int i = 1; i < segments; i++) { float t = (float)i / segments; float u = 1 - t; Vector2 p = u * u * start + 2 * u * t * control1 + t * t * end; points.Add(p); } } points.Add(end); return points; }
        private void CreateBezierLine(GameObject parentConnectionObject, List<Vector2> points, float initialThickness) { if (parentConnectionObject == null || points == null || points.Count < 2) return; for (int i = 0; i < points.Count - 1; i++) { Vector2 start = points[i]; Vector2 end = points[i + 1]; Vector2 direction = end - start; float length = direction.magnitude; if (length < 0.1f) continue; GameObject segmentGO = new GameObject("Segment_" + i); segmentGO.transform.SetParent(parentConnectionObject.transform, false); RectTransform segmentRect = segmentGO.AddComponent<RectTransform>(); segmentRect.anchorMin = new Vector2(0.5f, 0.5f); segmentRect.anchorMax = new Vector2(0.5f, 0.5f); segmentRect.pivot = new Vector2(0f, 0.5f); Image segmentImage = segmentGO.AddComponent<Image>(); segmentImage.color = lineColor; segmentImage.raycastTarget = true; segmentRect.sizeDelta = new Vector2(length, initialThickness); segmentRect.anchoredPosition = start; float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg; segmentRect.localEulerAngles = new Vector3(0, 0, angle); EventTrigger trigger = segmentGO.AddComponent<EventTrigger>(); EventTrigger.Entry entryPointerEnter = new EventTrigger.Entry(); entryPointerEnter.eventID = EventTriggerType.PointerEnter; entryPointerEnter.callback.AddListener((data) => { HandleConnectionGroupPointerEnter(parentConnectionObject); }); trigger.triggers.Add(entryPointerEnter); EventTrigger.Entry entryPointerExit = new EventTrigger.Entry(); entryPointerExit.eventID = EventTriggerType.PointerExit; entryPointerExit.callback.AddListener((data) => { HandleConnectionGroupPointerExit(parentConnectionObject); }); trigger.triggers.Add(entryPointerExit); EventTrigger.Entry entryPointerClick = new EventTrigger.Entry(); entryPointerClick.eventID = EventTriggerType.PointerClick; entryPointerClick.callback.AddListener((data) => { HandleConnectionPointerClick((PointerEventData)data, parentConnectionObject); }); trigger.triggers.Add(entryPointerClick); } }
        private void CreateArrow(GameObject parentConnectionObject, Vector2 positionNearTip, Vector2 tipPosition, bool reversed) { if (parentConnectionObject == null) return; GameObject arrowGO = new GameObject("Arrow_" + (reversed ? "Start" : "End")); arrowGO.transform.SetParent(parentConnectionObject.transform, false); RectTransform arrowRect = arrowGO.AddComponent<RectTransform>(); arrowRect.anchorMin = new Vector2(0.5f, 0.5f); arrowRect.anchorMax = new Vector2(0.5f, 0.5f); arrowRect.pivot = new Vector2(0.5f, 0.5f); arrowRect.sizeDelta = new Vector2(ARROW_SIZE, ARROW_SIZE); arrowRect.anchoredPosition = tipPosition; Image arrowImage = arrowGO.AddComponent<Image>(); arrowImage.sprite = CreateArrowSprite(); arrowImage.color = lineColor; arrowImage.raycastTarget = true; Vector2 rotDir = tipPosition - positionNearTip; if (rotDir.sqrMagnitude < 0.001f) rotDir = reversed ? Vector2.left : Vector2.right; float angle = Mathf.Atan2(rotDir.y, rotDir.x) * Mathf.Rad2Deg; arrowRect.localEulerAngles = new Vector3(0, 0, angle); EventTrigger trigger = arrowGO.AddComponent<EventTrigger>(); EventTrigger.Entry entryPointerEnter = new EventTrigger.Entry(); entryPointerEnter.eventID = EventTriggerType.PointerEnter; entryPointerEnter.callback.AddListener((data) => { HandleConnectionGroupPointerEnter(parentConnectionObject); }); trigger.triggers.Add(entryPointerEnter); EventTrigger.Entry entryPointerExit = new EventTrigger.Entry(); entryPointerExit.eventID = EventTriggerType.PointerExit; entryPointerExit.callback.AddListener((data) => { HandleConnectionGroupPointerExit(parentConnectionObject); }); trigger.triggers.Add(entryPointerExit); EventTrigger.Entry entryPointerClick = new EventTrigger.Entry(); entryPointerClick.eventID = EventTriggerType.PointerClick; entryPointerClick.callback.AddListener((data) => { HandleConnectionPointerClick((PointerEventData)data, parentConnectionObject); }); trigger.triggers.Add(entryPointerClick); }
        private void CreateSelfConnection(GameObject parentConnectionObject, Vector2 nodePosition) { if (parentConnectionObject == null) return; float loopRadius = 25f; float loopWidth = LINE_THICKNESS; int segments = 20; Vector2 loopCenter = nodePosition + new Vector2(0, NODE_HEIGHT * 0.5f + loopRadius); List<Vector2> loopPoints = new List<Vector2>(); float startAngleRad = -Mathf.PI * 0.45f; float endAngleRad = Mathf.PI * 1.45f; float angleStep = (endAngleRad - startAngleRad) / segments; for (int i = 0; i <= segments; i++) { float currentAngle = startAngleRad + (i * angleStep); loopPoints.Add(loopCenter + new Vector2(Mathf.Cos(currentAngle) * loopRadius, Mathf.Sin(currentAngle) * loopRadius)); } CreateBezierLine(parentConnectionObject, loopPoints, loopWidth); if (loopPoints.Count >= 2) { Vector2 arrowPos = loopPoints[loopPoints.Count - 1]; Vector2 beforeArrowPos = loopPoints[loopPoints.Count - 2]; CreateArrow(parentConnectionObject, beforeArrowPos, arrowPos, false); } }
        private void ClearGraph()
        {
            stateNodes.Clear();
            foreach (GameObject connection in connections)
            {
                if (connection != null) GameObject.Destroy(connection);
            }
            connections.Clear();
            connectionData.Clear();
            isDraggingNode = false;
            draggedNodeRect = null;
            draggedState = null;
            isDraggingLink = false;
            dragLinkSourceNode = null;
            if (dragArrow != null) dragArrow.SetActive(false);

            // Zatrzymaj korutyny mrugania, ponieważ obiekty grafu są niszczone
            if (blinkOnChangeCoroutine != null) { owner.StopCoroutine(blinkOnChangeCoroutine); blinkOnChangeCoroutine = null; }
            if (continuousBlinkCoroutine != null) { owner.StopCoroutine(continuousBlinkCoroutine); continuousBlinkCoroutine = null; }
            currentBlinkOutlineTransform = null; // Usuń referencję
            isBlinkingContinuous = false; // Zresetuj flagę

            if (contentPane != null)
            {
                for (int i = contentPane.childCount - 1; i >= 0; i--)
                {
                    Transform child = contentPane.GetChild(i);
                    if (child != null && child.gameObject != null && child.gameObject != dragArrow) // Nie niszcz dragArrow, jeśli istnieje
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                }
            }
        }
        private void ToggleLinkingMode() { if (isDraggingNode) return; isLinking = !isLinking; if (isLinking) { string selectedStateName = owner?.GetStateChooser()?.val; if (!string.IsNullOrEmpty(selectedStateName)) { linkSourceState = owner.stateManager.GetStateGlobal(selectedStateName); if (linkSourceState == null) { SuperController.LogError("RoutimatorGraph: Cannot start linking, selected state not found."); isLinking = false; return; } HighlightLinkSourceNode(true); } else { SuperController.LogError("RoutimatorGraph: Cannot start linking, no state selected."); isLinking = false; return; } if (linkButtonText != null) linkButtonText.text = "Finish Linking"; } else { HighlightLinkSourceNode(false); linkSourceState = null; if (linkButtonText != null) linkButtonText.text = "Link States"; } }
        private void HandleNodeClickInLinkingMode(RoutimatorState.State targetState) { if (linkSourceState == null || targetState == null) return; if (linkSourceState == targetState) return; if (linkSourceState.Transitions.Contains(targetState)) { owner.RemoveTransitionFromGraph(linkSourceState, targetState); } else { owner.AddTransitionFromGraph(linkSourceState, targetState); } UpdateGraph(); }
        private void HighlightLinkSourceNode(bool highlight, RectTransform specificNodeRect = null) { RectTransform nodeRect = specificNodeRect; if (nodeRect == null) { if (linkSourceState == null || !stateNodes.TryGetValue(linkSourceState.Name, out nodeRect) || nodeRect == null) { if (!highlight) return; return; } } Transform outlineTransform = nodeRect.transform.Find("LinkSourceOutline"); if (highlight) { if (outlineTransform == null) { GameObject parent = new GameObject("LinkSourceOutline"); parent.transform.SetParent(nodeRect.transform, false); RectTransform pr = parent.AddComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; float lo = OUTLINE_THICKNESS + 1f; pr.offsetMin = new Vector2(lo, lo); pr.offsetMax = new Vector2(-lo, -lo); CreateOutlineBar(parent.transform, "LinkTopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, LINK_HIGHLIGHT_THICKNESS), Vector2.zero, linkSourceHighlightColor); CreateOutlineBar(parent.transform, "LinkBottomBar", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, LINK_HIGHLIGHT_THICKNESS), Vector2.zero, linkSourceHighlightColor); CreateOutlineBar(parent.transform, "LinkLeftBar", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(LINK_HIGHLIGHT_THICKNESS, 0), Vector2.zero, linkSourceHighlightColor); CreateOutlineBar(parent.transform, "LinkRightBar", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(LINK_HIGHLIGHT_THICKNESS, 0), Vector2.zero, linkSourceHighlightColor); parent.transform.SetAsLastSibling(); } else { outlineTransform.gameObject.SetActive(true); outlineTransform.SetAsLastSibling(); } Transform textTransform = nodeRect.transform.Find("Text"); if (textTransform != null) textTransform.SetAsLastSibling(); } else { if (outlineTransform != null) { GameObject.Destroy(outlineTransform.gameObject); } } }
        private void ResetLayout() { if (owner != null) { owner.ClearSavedNodePositions(); UpdateGraph(); Logger.Log("RoutimatorGraph: Layout Reset."); } else { Logger.Log("RoutimatorGraph: Owner is null, cannot reset layout positions."); } }
        private void OnNodePointerClick(PointerEventData data, RoutimatorState.State state) { if (state == null || isDraggingNode) return; if (data.button == PointerEventData.InputButton.Left) { OnNodeClick(state); } else if (data.button == PointerEventData.InputButton.Right) { ShowContextMenu(state, data.position); } }

        private void ShowContextMenu(RoutimatorState.State state, Vector2 screenPosition)
        {
            DestroyContextMenu(); if (graphCanvas == null || state == null || contentPane == null) { SuperController.LogError("RoutimatorGraph: Cannot show context menu - canvas, state, or contentPane is null."); return; }
            contextMenuPanel = new GameObject("NodeContextMenu"); contextMenuPanel.transform.SetParent(contentPane.transform, false); contextMenuPanel.transform.SetAsLastSibling();
            RectTransform panelRect = contextMenuPanel.AddComponent<RectTransform>(); Image panelImageCtx = contextMenuPanel.AddComponent<Image>(); panelImageCtx.color = contextMenuBackgroundColor; panelImageCtx.sprite = CreateRoundedRectSprite(16, 4, contextMenuBackgroundColor); panelImageCtx.type = Image.Type.Sliced; panelImageCtx.color = Color.white;
            VerticalLayoutGroup layoutGroup = contextMenuPanel.AddComponent<VerticalLayoutGroup>(); layoutGroup.padding = new RectOffset(5, 5, 5, 5); layoutGroup.spacing = 3f; layoutGroup.childAlignment = TextAnchor.UpperLeft; layoutGroup.childControlWidth = true; layoutGroup.childControlHeight = true; layoutGroup.childForceExpandWidth = true; layoutGroup.childForceExpandHeight = false;
            ContentSizeFitter sizeFitter = contextMenuPanel.AddComponent<ContentSizeFitter>(); sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; panelRect.sizeDelta = new Vector2(CONTEXT_MENU_WIDTH, 0);
            AddContextMenuButton(panelRect, "Activate this state", () => ContextMenuAction_Activate(state));
            AddContextMenuButton(panelRect, "Navigate to this state", () => ContextMenuAction_Navigate(state));
            AddContextMenuButton(panelRect, "Link state to...", () => ContextMenuAction_Link(state));
            AddContextMenuButton(panelRect, "Toggle Walking", () => ContextMenuAction_ToggleWalking(state)); // NOWA OPCJA W MENU

            Vector2 localPointInContent; bool conversionSuccess = RectTransformUtility.ScreenPointToLocalPointInRectangle(contentPane, screenPosition, canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvasComponent.worldCamera, out localPointInContent);
            if (conversionSuccess) { panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f); panelRect.pivot = new Vector2(0f, 1f); panelRect.anchoredPosition = localPointInContent; }
            else { panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f); panelRect.pivot = new Vector2(0.5f, 0.5f); panelRect.anchoredPosition = Vector2.zero; SuperController.LogError("RoutimatorGraph: Nie udało się przekonwertować punktu ekranu na punkt lokalny dla menu kontekstowego."); }
        }

        private void ContextMenuAction_ToggleWalking(RoutimatorState.State state)
        {
            if (owner == null || state == null) return;

            state.IsWalkingEnabled = !state.IsWalkingEnabled;
            owner.MarkAsModified();

            UpdateGraph();

            if (owner.GetStateChooser().val == state.Name && owner.GetGroupChooser().val == state.Group)
            {
                owner.GetStateIsWalkingEnabled().valNoCallback = state.IsWalkingEnabled;
                // owner.ui.UIRefresh(); // Można rozważyć, jeśli samo ustawienie valNoCallback nie odświeży UI
            }
            Logger.Log($"RoutimatorGraph: Toggled walking for state '{state.Name}' to {state.IsWalkingEnabled} via context menu.");
        }

        public void NotifyStateChanged()
        {
            if (!isGraphVisible || owner == null) return;

            RoutimatorState.State currentState = owner.GetCurrentState();
            if (currentState != null && stateNodes.ContainsKey(currentState.Name))
            {
                currentBlinkOutlineTransform = stateNodes[currentState.Name].transform.Find("InternalOutline");
            }
            else
            {
                currentBlinkOutlineTransform = null;
            }

            if (blinkOnChangeCoroutine != null) // Zatrzymaj poprzednie mruganie przy zmianie
            {
                owner.StopCoroutine(blinkOnChangeCoroutine);
                blinkOnChangeCoroutine = null;
            }

            // Jeśli ciągłe mruganie jest aktywne, zatrzymaj je na chwilę
            bool wasContinuousBlinking = isBlinkingContinuous && continuousBlinkCoroutine != null;
            if (wasContinuousBlinking)
            {
                owner.StopCoroutine(continuousBlinkCoroutine);
                continuousBlinkCoroutine = null;
                // Upewnij się, że ramka jest widoczna przed rozpoczęciem szybkiego mrugania
                if (currentBlinkOutlineTransform != null) currentBlinkOutlineTransform.gameObject.SetActive(true);
                blinkOutlineVisible = true;
            }


            if (currentBlinkOutlineTransform != null) // Tylko jeśli mamy co mrugać
            {
                // Przekaż informację o poprzednim stanie ciągłego mrugania
                blinkOnChangeCoroutine = owner.StartCoroutine(BlinkOnChangeRoutine(wasContinuousBlinking));
            }
            else if (wasContinuousBlinking) // Jeśli nie ma celu, ale było ciągłe mruganie, spróbuj je wznowić od razu
            {
                SetContinuousBlinking(true); // Wznowi ciągłe mruganie, jeśli nadal isBlinkingContinuous jest true
            }
        }

        public void SetContinuousBlinking(bool isBlinking)
        {
            if (!isGraphVisible || owner == null) return;

            isBlinkingContinuous = isBlinking; // Ustaw flagę globalną

            RoutimatorState.State currentState = owner.GetCurrentState();
            if (currentState != null && stateNodes.ContainsKey(currentState.Name))
            {
                currentBlinkOutlineTransform = stateNodes[currentState.Name].transform.Find("InternalOutline");
            }
            else
            {
                currentBlinkOutlineTransform = null;
            }

            // Jeśli mruganie przy zmianie jest aktywne, pozwól mu dokończyć.
            // Ciągłe mruganie zostanie (lub nie) wznowione przez BlinkOnChangeRoutine.
            if (blinkOnChangeCoroutine != null)
            {
                return;
            }

            // Jeśli korutyna ciągłego mrugania już działa i chcemy ją kontynuować, nic nie rób
            if (isBlinkingContinuous && continuousBlinkCoroutine != null)
            {
                return;
            }

            // Jeśli korutyna ciągłego mrugania działa, a chcemy ją zatrzymać
            if (!isBlinkingContinuous && continuousBlinkCoroutine != null)
            {
                owner.StopCoroutine(continuousBlinkCoroutine);
                continuousBlinkCoroutine = null;
                if (currentBlinkOutlineTransform != null)
                {
                    currentBlinkOutlineTransform.gameObject.SetActive(true); // Upewnij się, że jest widoczne
                }
                blinkOutlineVisible = true;
                return;
            }

            // Jeśli chcemy włączyć ciągłe mruganie, a nie jest ono jeszcze aktywne
            if (isBlinkingContinuous && continuousBlinkCoroutine == null)
            {
                if (currentBlinkOutlineTransform != null)
                {
                    continuousBlinkCoroutine = owner.StartCoroutine(ContinuousBlinkRoutine());
                }
            }
        }

        private IEnumerator BlinkOnChangeRoutine(bool resumeContinuousAfter = false) // Nowy parametr
        {
            if (currentBlinkOutlineTransform == null || currentBlinkOutlineTransform.gameObject == null)
            {
                RoutimatorState.State _currentState = owner.GetCurrentState();
                if (_currentState != null && stateNodes.ContainsKey(_currentState.Name))
                {
                    Transform newOutline = stateNodes[_currentState.Name].transform.Find("InternalOutline");
                    if (newOutline != null && newOutline.gameObject != null) currentBlinkOutlineTransform = newOutline;
                    else
                    {
                        blinkOnChangeCoroutine = null;
                        if (resumeContinuousAfter && isBlinkingContinuous) SetContinuousBlinking(true); // Wznów jeśli trzeba
                        yield break;
                    }
                }
                else
                {
                    blinkOnChangeCoroutine = null;
                    if (resumeContinuousAfter && isBlinkingContinuous) SetContinuousBlinking(true); // Wznów jeśli trzeba
                    yield break;
                }
            }

            blinkCount = 0;
            int maxBlinks = 3 * 2; // 3 pełne cykle (włącz/wyłącz)

            while (blinkCount < maxBlinks)
            {
                if (currentBlinkOutlineTransform == null || currentBlinkOutlineTransform.gameObject == null)
                {
                    // Próba odzyskania referencji w trakcie
                    RoutimatorState.State _currentState = owner.GetCurrentState();
                    if (_currentState != null && stateNodes.ContainsKey(_currentState.Name))
                    {
                        Transform newOutline = stateNodes[_currentState.Name].transform.Find("InternalOutline");
                        if (newOutline != null && newOutline.gameObject != null) currentBlinkOutlineTransform = newOutline;
                        else
                        {
                            blinkOnChangeCoroutine = null;
                            if (resumeContinuousAfter && isBlinkingContinuous) SetContinuousBlinking(true); // Wznów jeśli trzeba
                            yield break;
                        }
                    }
                    else
                    {
                        blinkOnChangeCoroutine = null;
                        if (resumeContinuousAfter && isBlinkingContinuous) SetContinuousBlinking(true); // Wznów jeśli trzeba
                        yield break;
                    }
                }

                blinkOutlineVisible = !blinkOutlineVisible;
                currentBlinkOutlineTransform.gameObject.SetActive(blinkOutlineVisible);
                blinkCount++;
                yield return new WaitForSeconds(BLINK_INTERVAL_ON_CHANGE);
            }

            // Upewnij się, że ramka jest widoczna na końcu szybkiego mrugania
            if (currentBlinkOutlineTransform != null)
            {
                currentBlinkOutlineTransform.gameObject.SetActive(true);
            }
            blinkOutlineVisible = true;
            blinkOnChangeCoroutine = null;

            // Wznów ciągłe mruganie, jeśli było aktywne wcześniej i flaga isBlinkingContinuous nadal jest true
            if (resumeContinuousAfter && isBlinkingContinuous)
            {
                SetContinuousBlinking(true);
            }
        }

        private IEnumerator ContinuousBlinkRoutine()
        {
            // Sprawdzenie na początku, czy mamy cel
            if (currentBlinkOutlineTransform == null || currentBlinkOutlineTransform.gameObject == null)
            {
                RoutimatorState.State _currentState = owner.GetCurrentState();
                if (_currentState != null && stateNodes.ContainsKey(_currentState.Name))
                {
                    Transform newOutline = stateNodes[_currentState.Name].transform.Find("InternalOutline");
                    if (newOutline != null && newOutline.gameObject != null) currentBlinkOutlineTransform = newOutline;
                    else { continuousBlinkCoroutine = null; yield break; }
                }
                else { continuousBlinkCoroutine = null; yield break; }
            }

            if (currentBlinkOutlineTransform != null) currentBlinkOutlineTransform.gameObject.SetActive(true); // Widoczna na start
            blinkOutlineVisible = true;

            while (isBlinkingContinuous) // Flaga kontrolowana z zewnątrz
            {
                if (currentBlinkOutlineTransform == null || currentBlinkOutlineTransform.gameObject == null)
                {
                    RoutimatorState.State _currentState = owner.GetCurrentState();
                    if (_currentState != null && stateNodes.ContainsKey(_currentState.Name))
                    {
                        Transform newOutline = stateNodes[_currentState.Name].transform.Find("InternalOutline");
                        if (newOutline != null && newOutline.gameObject != null) currentBlinkOutlineTransform = newOutline;
                        else { continuousBlinkCoroutine = null; yield break; }
                    }
                    else { continuousBlinkCoroutine = null; yield break; }
                }

                blinkOutlineVisible = !blinkOutlineVisible;
                currentBlinkOutlineTransform.gameObject.SetActive(blinkOutlineVisible);
                yield return new WaitForSeconds(BLINK_INTERVAL_CONTINUOUS);
            }

            if (currentBlinkOutlineTransform != null)
            {
                currentBlinkOutlineTransform.gameObject.SetActive(true); // Upewnij się, że jest widoczna po zakończeniu
            }
            blinkOutlineVisible = true;
            continuousBlinkCoroutine = null;
        }

        public bool IsContinuousBlinkingActive() { return isBlinkingContinuous && continuousBlinkCoroutine != null; }

        private void AddContextMenuButton(Transform parent, string text, UnityEngine.Events.UnityAction action) { GameObject buttonObj = new GameObject("ContextMenuButton_" + text.Replace(" ", "")); buttonObj.transform.SetParent(parent, false); RectTransform buttonRect = buttonObj.AddComponent<RectTransform>(); Image buttonImage = buttonObj.AddComponent<Image>(); buttonImage.sprite = CreateRoundedRectSprite(16, 3, contextMenuButtonColor); buttonImage.type = Image.Type.Sliced; buttonImage.color = Color.white; buttonImage.raycastTarget = true; Button button = buttonObj.AddComponent<Button>(); button.targetGraphic = buttonImage; ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white; Image img = button.targetGraphic as Image; if (img != null) { Color baseSpriteColor = contextMenuButtonColor; colors.normalColor = Color.white; colors.highlightedColor = Color.Lerp(Color.white, contextMenuButtonHighlightColor, 0.5f); colors.pressedColor = Color.Lerp(Color.white, Color.Lerp(contextMenuButtonHighlightColor, Color.black, 0.2f), 0.5f); } colors.disabledColor = Color.gray; colors.colorMultiplier = 1.0f; colors.fadeDuration = 0.1f; button.colors = colors; button.onClick.AddListener(() => { DestroyContextMenu(); action?.Invoke(); }); LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>(); layoutElement.minHeight = CONTEXT_MENU_BUTTON_HEIGHT; layoutElement.preferredHeight = CONTEXT_MENU_BUTTON_HEIGHT; GameObject textObj = new GameObject("Text"); textObj.transform.SetParent(buttonRect, false); RectTransform textRect = textObj.AddComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(8, 0); textRect.offsetMax = new Vector2(-8, 0); Text buttonText = textObj.AddComponent<Text>(); buttonText.text = text; buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); buttonText.fontSize = 14; buttonText.color = Color.white; buttonText.alignment = TextAnchor.MiddleLeft; buttonText.raycastTarget = false; }
        private void DestroyContextMenu() { if (contextMenuPanel != null) { GameObject.Destroy(contextMenuPanel); contextMenuPanel = null; } }
        private void ContextMenuAction_Link(RoutimatorState.State sourceState) { if (owner == null || sourceState == null) return; if (isLinking && linkSourceState != sourceState) { ToggleLinkingMode(); } if (!isLinking) { linkSourceState = sourceState; isLinking = true; HighlightLinkSourceNode(true); if (linkButtonText != null) linkButtonText.text = "Finish Linking"; Logger.Log($"RoutimatorGraph: Started linking from '{sourceState.Name}' via context menu."); } }
        private void ContextMenuAction_Activate(RoutimatorState.State state) { if (owner == null || state == null) return; owner.SwitchState(state); Logger.Log($"RoutimatorGraph: Activated state '{state.Name}' via context menu."); }
        private void ContextMenuAction_Navigate(RoutimatorState.State state) { if (owner == null || state == null) return; Logger.Log($"RoutimatorGraph: Navigating to state '{state.Name}' via context menu."); owner.RouteSwitchStateAction(state.Name); }
        private void AddBackgroundClickListener() { if (panelImage == null) return; EventTrigger trigger = panelImage.gameObject.GetComponent<EventTrigger>(); if (trigger == null) trigger = panelImage.gameObject.AddComponent<EventTrigger>(); bool entryExists = false; foreach (var entry in trigger.triggers) { if (entry.eventID == EventTriggerType.PointerDown) { entryExists = true; break; } } if (!entryExists) { EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown }; pointerDownEntry.callback.AddListener(delegate (BaseEventData data) { PointerEventData pointerData = (PointerEventData)data; if (pointerData.pointerPressRaycast.gameObject == panelImage.gameObject) { DestroyContextMenu(); } }); trigger.triggers.Add(pointerDownEntry); } }
        private IEnumerator UpdateDragArrowCoroutine() { RectTransform currentlyHoveredNodeRect = null; string currentlyHoveredNodeName = null; Image hoveredNodeImage = null; Button hoveredNodeButton = null; Color actualOriginalImageColor = Color.white; Selectable.Transition originalButtonTransition = Selectable.Transition.ColorTint; while (isDraggingLink && dragArrow != null && dragArrowRect != null) { Vector2 localMousePos; Vector3 mousePos = Input.mousePosition; RectTransformUtility.ScreenPointToLocalPointInRectangle(contentPane, mousePos, canvasCamera, out localMousePos); UpdateDragArrow(localMousePos); if (Input.GetMouseButtonDown(1)) { if (currentlyHoveredNodeRect != null && hoveredNodeImage != null) { hoveredNodeImage.color = actualOriginalImageColor; if (hoveredNodeButton != null) { hoveredNodeButton.transition = originalButtonTransition; } } CancelLinkDragging(); yield break; } if (Input.GetMouseButtonUp(0)) { RoutimatorState.State sourceStateForLink = owner.stateManager.GetStateGlobal(dragLinkSourceNode); RoutimatorState.State targetStateForLink = null; if (!string.IsNullOrEmpty(currentlyHoveredNodeName)) { targetStateForLink = owner.stateManager.GetStateGlobal(currentlyHoveredNodeName); } if (currentlyHoveredNodeRect != null && hoveredNodeImage != null) { hoveredNodeImage.color = actualOriginalImageColor; if (hoveredNodeButton != null) { hoveredNodeButton.transition = originalButtonTransition; } } CancelLinkDragging(); if (sourceStateForLink != null && targetStateForLink != null && sourceStateForLink != targetStateForLink) { if (sourceStateForLink.Transitions.Contains(targetStateForLink)) { owner.RemoveTransitionFromGraph(sourceStateForLink, targetStateForLink); } else { owner.AddTransitionFromGraph(sourceStateForLink, targetStateForLink); } UpdateGraph(); } yield break; } RectTransform foundNodeUnderMouse = null; string foundNodeNameUnderMouse = null; foreach (var kvp in stateNodes) { if (kvp.Key == dragLinkSourceNode) continue; RectTransform nodeRect = kvp.Value; if (nodeRect != null && RectTransformUtility.RectangleContainsScreenPoint(nodeRect, mousePos, canvasCamera)) { foundNodeUnderMouse = nodeRect; foundNodeNameUnderMouse = kvp.Key; break; } } if (foundNodeUnderMouse != currentlyHoveredNodeRect) { if (currentlyHoveredNodeRect != null && hoveredNodeImage != null) { hoveredNodeImage.color = actualOriginalImageColor; if (hoveredNodeButton != null) { hoveredNodeButton.transition = originalButtonTransition; } } currentlyHoveredNodeRect = foundNodeUnderMouse; currentlyHoveredNodeName = foundNodeNameUnderMouse; if (currentlyHoveredNodeRect != null) { hoveredNodeImage = currentlyHoveredNodeRect.GetComponent<Image>(); hoveredNodeButton = currentlyHoveredNodeRect.GetComponent<Button>(); if (hoveredNodeImage != null) { actualOriginalImageColor = hoveredNodeImage.color; if (hoveredNodeButton != null) { originalButtonTransition = hoveredNodeButton.transition; hoveredNodeButton.transition = Selectable.Transition.None; } Color highlightColor; if (actualOriginalImageColor.r < 0.25f && actualOriginalImageColor.g < 0.25f && actualOriginalImageColor.b < 0.25f) { highlightColor = new Color(0.5f, 0.5f, 0.5f, actualOriginalImageColor.a); } else { highlightColor = new Color(Mathf.Min(1f, actualOriginalImageColor.r * 1.5f), Mathf.Min(1f, actualOriginalImageColor.g * 1.5f), Mathf.Min(1f, actualOriginalImageColor.b * 1.5f), actualOriginalImageColor.a); } hoveredNodeImage.color = highlightColor; } } else { hoveredNodeImage = null; hoveredNodeButton = null; } } yield return null; } if (currentlyHoveredNodeRect != null && hoveredNodeImage != null) { hoveredNodeImage.color = actualOriginalImageColor; if (hoveredNodeButton != null) { hoveredNodeButton.transition = originalButtonTransition; } } }
        private void HandleConnectionPointerEnter(Image imageElement, RectTransform lineRect) { if (imageElement != null) { imageElement.color = lineHoverColor; if (lineRect != null) { Vector2 currentSize = lineRect.sizeDelta; lineRect.sizeDelta = new Vector2(currentSize.x, LINE_THICKNESS * LINE_THICKNESS_HOVER_MULTIPLIER); } } }
        private void HandleConnectionPointerExit(Image imageElement, RectTransform lineRect) { if (imageElement != null) { imageElement.color = lineColor; if (lineRect != null) { Vector2 currentSize = lineRect.sizeDelta; lineRect.sizeDelta = new Vector2(currentSize.x, LINE_THICKNESS); } } }
        private void HandleConnectionGroupPointerEnter(GameObject parentConnectionObject) { if (parentConnectionObject == null) return; foreach (Transform child in parentConnectionObject.transform) { Image image = child.GetComponent<Image>(); RectTransform rectTransform = child.GetComponent<RectTransform>(); if (image != null) { image.color = lineHoverColor; } if (rectTransform != null && child.name.StartsWith("Segment_")) { Vector2 currentSize = rectTransform.sizeDelta; rectTransform.sizeDelta = new Vector2(currentSize.x, LINE_THICKNESS * LINE_THICKNESS_HOVER_MULTIPLIER); } } }
        private void HandleConnectionGroupPointerExit(GameObject parentConnectionObject) { if (parentConnectionObject == null) return; foreach (Transform child in parentConnectionObject.transform) { Image image = child.GetComponent<Image>(); RectTransform rectTransform = child.GetComponent<RectTransform>(); if (image != null) { image.color = lineColor; } if (rectTransform != null && child.name.StartsWith("Segment_")) { Vector2 currentSize = rectTransform.sizeDelta; rectTransform.sizeDelta = new Vector2(currentSize.x, LINE_THICKNESS); } } }
        private void HandleConnectionPointerClick(PointerEventData eventData, GameObject parentConnectionObject) { if (eventData.button == PointerEventData.InputButton.Right) { DestroyContextMenu(); if (parentConnectionObject != null && connectionData.ContainsKey(parentConnectionObject)) { KeyValuePair<string, string> connectionInfo = connectionData[parentConnectionObject]; string sourceName = connectionInfo.Key; string targetName = connectionInfo.Value; RoutimatorState.State sourceState = owner.stateManager.GetStateGlobal(sourceName); RoutimatorState.State targetState = owner.stateManager.GetStateGlobal(targetName); if (sourceState != null && targetState != null) { bool changed = false; if (sourceState.Transitions.Contains(targetState)) { owner.RemoveTransitionFromGraph(sourceState, targetState); changed = true; } if (sourceState != targetState && targetState.Transitions.Contains(sourceState)) { owner.RemoveTransitionFromGraph(targetState, sourceState); changed = true; } if (changed) { Logger.Log("RoutimatorGraph: Removed connection between " + sourceName + " and " + targetName + " via right-click."); UpdateGraph(); } } } } }
        private Sprite CreateManIconSprite(int size = 12) { if (manIconSpriteField != null && manIconSpriteField.texture.width == size) { return manIconSpriteField; } Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false); Color[] pixels = new Color[size * size]; for (int i = 0; i < pixels.Length; ++i) pixels[i] = Color.clear; Color manColor = new Color(0.85f, 0.85f, 0.85f, 0.9f); int centerX = size / 2; bool isThick = size > 8; System.Action<int, int, Color> setPixel = (x, conceptual_y, color) => { if (x >= 0 && x < size && conceptual_y >= 0 && conceptual_y < size) { int texture_y = (size - 1) - conceptual_y; pixels[texture_y * size + x] = color; } }; int conceptualHeadTopY = Mathf.FloorToInt(size * 0.1f); int headLeftX = centerX - 1; setPixel(headLeftX, conceptualHeadTopY, manColor); setPixel(headLeftX + 1, conceptualHeadTopY, manColor); setPixel(headLeftX, conceptualHeadTopY + 1, manColor); setPixel(headLeftX + 1, conceptualHeadTopY + 1, manColor); int conceptualBodyTopY = conceptualHeadTopY + 2; int conceptualBodyBottomY = Mathf.FloorToInt(size * 0.60f); for (int y_concept = conceptualBodyTopY; y_concept <= conceptualBodyBottomY; y_concept++) { if (isThick) { setPixel(centerX - 1, y_concept, manColor); } setPixel(centerX, y_concept, manColor); } int conceptualArmStartY = conceptualBodyTopY + Mathf.Max(0, Mathf.FloorToInt(size * 0.05f)); int armLength = Mathf.Max(1, Mathf.FloorToInt(size * 0.20f)); for (int i = 0; i < armLength; i++) { int currentConceptualArmY = conceptualArmStartY + i; int leftArmX = centerX - (isThick ? 1 : 0) - 1 - i; setPixel(leftArmX, currentConceptualArmY, manColor); int rightArmX = centerX + (isThick ? 0 : 0) + 1 + i; setPixel(rightArmX, currentConceptualArmY, manColor); } int conceptualLegStartY = conceptualBodyBottomY + 1; int legLength = Mathf.Max(1, Mathf.FloorToInt(size * 0.28f)); for (int i = 0; i < legLength; i++) { int currentConceptualLegY = conceptualLegStartY + i; int leftLegX = centerX - (isThick ? 1 : 0) - i; setPixel(leftLegX, currentConceptualLegY, manColor); int rightLegX = centerX + (isThick ? 0 : 0) + i; setPixel(rightLegX, currentConceptualLegY, manColor); } tex.SetPixels(pixels); tex.Apply(false, false); manIconSpriteField = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f); return manIconSpriteField; }

        private void ToggleMinimizeGraph()
        {
            isGraphMinimized = !isGraphMinimized;
            ApplyGraphWindowState();
        }

        private void ApplyGraphWindowState()
        {
            if (panelImage == null || legend == null || minimizeButtonText == null || resetButton == null || linkButton == null) return;

            RectTransform panelRect = panelImage.rectTransform;
            if (isGraphMinimized)
            {
                // Zminimalizuj
                panelRect.anchorMin = new Vector2(1, 0);
                panelRect.anchorMax = new Vector2(1, 0);
                panelRect.pivot = new Vector2(1, 0);
                panelRect.sizeDelta = minimizedPanelSize;
                panelRect.anchoredPosition = new Vector2(-20, 20);

                legend.SetActive(false);
                resetButton.SetActive(false);
                linkButton.SetActive(false);
                minimizeButtonText.text = "□";
                FitGraphToView();
            }
            else
            {
                // Zmaksymalizuj
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = originalPanelSize;
                panelRect.anchoredPosition = Vector2.zero;

                legend.SetActive(true);
                resetButton.SetActive(true);
                linkButton.SetActive(true);
                minimizeButtonText.text = "-";
                FitGraphToView();
            }
        }

        public void FitGraphToView()
        {
            if (stateNodes == null || stateNodes.Count == 0 || contentPane == null || viewportRectangle == null)
            {
                ResetPanAndZoom();
                return;
            }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var nodeRect in stateNodes.Values.ToArray())
            {
                Vector2 pos = nodeRect.anchoredPosition;
                minX = Mathf.Min(minX, pos.x - NODE_WIDTH / 2f);
                minY = Mathf.Min(minY, pos.y - NODE_HEIGHT / 2f);
                maxX = Mathf.Max(maxX, pos.x + NODE_WIDTH / 2f);
                maxY = Mathf.Max(maxY, pos.y + NODE_HEIGHT / 2f);
            }

            float boundsWidth = maxX - minX;
            float boundsHeight = maxY - minY;

            if (boundsWidth <= 0 || boundsHeight <= 0)
            {
                ResetPanAndZoom();
                return;
            }

            float viewportWidth = viewportRectangle.rect.width;
            float viewportHeight = viewportRectangle.rect.height;

            float zoomX = viewportWidth / boundsWidth;
            float zoomY = viewportHeight / boundsHeight;

            zoomLevel = Mathf.Min(zoomX, zoomY) * 0.9f;
            zoomLevel = Mathf.Clamp(zoomLevel, MIN_ZOOM, MAX_ZOOM);

            float centerX = (minX + maxX) / 2f;
            float centerY = (minY + maxY) / 2f;

            panOffset = new Vector2(-centerX * zoomLevel, -centerY * zoomLevel);
            UpdateContentPaneTransform();
        }

        public GameObject GetCanvas()
        {
            return graphCanvas;
        }
    }
}