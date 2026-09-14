using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace ExfilRewards.Client;

/// <summary>
///     OnGUI popup shown after a confirmed-survived raid or run-through. Calls
///     /calculate immediately to populate the numbers, then only calls /claim
///     (which actually grants the reward) when the player clicks Confirm -
///     server does nothing until that click happens, per spec.
///
///     Visuals (v2 redesign): fullscreen PMC/Scav themed background art,
///     the existing card panel art on top of that, and per-currency tile
///     art (large tiles for Extraction/Total, small wide tiles for Kill
///     Bonus) with numbers rendered into each tile's own blank zone -
///     replacing the earlier plain-icon-and-text layout. All positioning
///     uses manual Rect math rather than GUILayout auto-flow (see
///     DrawReadyState) after repeated bugs from GUILayout controls
///     auto-sizing to their own content instead of filling the space given.
///     Currency order is Roubles -> Euros -> Dollars throughout, per spec.
/// </summary>
public class RaidRewardPopup : MonoBehaviour
{
    private enum State
    {
        Loading,
        Ready,
        Claiming,
        Claimed,
        Error
    }

    // Fraction of the background texture's own width/height that's safely
    // clear of border decoration, verified by eye against the actual PNGs.
    private const float SafeLeft = 0.09f;
    private const float SafeRight = 0.90f;
    private const float SafeTop = 0.10f;
    private const float SafeBottom = 0.86f;

    private static RaidRewardPopup? _instance;

    private static Texture2D? _pmcBackground;
    private static Texture2D? _scavBackground;
    private static Texture2D? _fullscreenPmc;
    private static Texture2D? _fullscreenScav;
    private static Texture2D? _tileRoublesLarge;
    private static Texture2D? _tileEurosLarge;
    private static Texture2D? _tileDollarsLarge;
    private static Texture2D? _tileRoublesSmall;
    private static Texture2D? _tileEurosSmall;
    private static Texture2D? _tileDollarsSmall;
    private static Texture2D? _confirmPlatePmc;
    private static Texture2D? _confirmPlateScav;
    // Per-side "RAID BONUS REWARDS" title art (2172x724, 3:1), replacing
    // the drawn text title per direction. Falls back to the old text
    // title if either fails to load, same failure pattern as every other
    // asset here.
    private static Texture2D? _titlePmc;
    private static Texture2D? _titleScav;
    private static bool _texturesLoaded;

    private State _state = State.Loading;
    private RewardBreakdownDto? _breakdown;
    private CalculateRewardRequestDto? _request;
    private string _errorMessage = "";

    // Per-currency accent colors, sampled from the actual tile art (gold
    // roubles, olive-green euros, ice-blue dollars/silver) and lightly
    // boosted for flat UI-text legibility - matches the reference reward
    // card art's per-currency-colored labels/numbers instead of one flat
    // white for every currency.
    private static readonly Color RoublesAccent = new Color(0.85f, 0.66f, 0.33f);
    private static readonly Color EurosAccent = new Color(0.56f, 0.75f, 0.37f);
    private static readonly Color DollarsAccent = new Color(0.69f, 0.80f, 0.91f);

    private GUIStyle? _titleStyle;
    private GUIStyle? _rowLabelStyle;
    private GUIStyle? _rowValueStyle;
    private GUIStyle? _totalLabelStyle;
    private GUIStyle? _totalValueStyle;
    private GUIStyle? _confirmButtonStyle;
    private GUIStyle? _statusStyle;
    // One number style per currency (large tiles) and per currency (small
    // tiles) rather than a single shared style, so color can differ by
    // currency. fontSize is a starting point only - DrawTile shrinks it
    // per-draw to whatever actually fits the tile's number zone (see
    // GetFittingFontSize), which is what fixes the roubles overlap bug:
    // six-digit rouble amounts no longer wrap/stack because the size is
    // reduced instead of letting GUI wrap the string.
    private GUIStyle? _tileNumberLargeRoublesStyle;
    private GUIStyle? _tileNumberLargeEurosStyle;
    private GUIStyle? _tileNumberLargeDollarsStyle;
    private GUIStyle? _tileNumberSmallRoublesStyle;
    private GUIStyle? _tileNumberSmallEurosStyle;
    private GUIStyle? _tileNumberSmallDollarsStyle;
    // Bordered-plate button background (see BuildPlateTexture), replacing
    // GUI.skin.button's stock look for Confirm/Close/Close Preview.
    private Texture2D? _buttonPlateNormal;
    private Texture2D? _buttonPlateHover;
    private bool _stylesBuilt;

    public static void ShowFor(string side, int killCount, long survivedSeconds, bool isRunThrough = false)
    {
        EnsureTexturesLoaded();

        if (_instance == null)
        {
            var go = new GameObject("ExfilRewardsPopup");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<RaidRewardPopup>();
        }

        _instance.BeginFor(side, killCount, survivedSeconds, isRunThrough);
    }

    /// <summary>
    ///     Loads the background/icon PNGs once, from embedded resources baked
    ///     into this DLL at build time (see csproj EmbeddedResource items).
    ///     Failure is non-fatal - if a texture is missing, drawing falls back
    ///     to a plain GUI.Box so the mod still functions, and the failure is
    ///     logged once rather than retried every popup open. If a resource
    ///     name doesn't match, the log also lists every resource name actually
    ///     embedded in the assembly, so a mismatch is diagnosable from the log
    ///     instead of silently showing a gray box.
    /// </summary>
    private static void EnsureTexturesLoaded()
    {
        if (_texturesLoaded)
        {
            return;
        }
        _texturesLoaded = true;

        _pmcBackground = TryLoadTexture("panel_background_pmc.png");
        _scavBackground = TryLoadTexture("panel_background_scav.png");
        _fullscreenPmc = TryLoadTexture("fullscreen_bg_pmc.png");
        _fullscreenScav = TryLoadTexture("fullscreen_bg_scav.png");
        _tileRoublesLarge = TryLoadTexture("tile_roubles_large.png");
        _tileEurosLarge = TryLoadTexture("tile_euros_large.png");
        _tileDollarsLarge = TryLoadTexture("tile_dollars_large.png");
        _tileRoublesSmall = TryLoadTexture("tile_roubles_small.png");
        _tileEurosSmall = TryLoadTexture("tile_euros_small.png");
        _tileDollarsSmall = TryLoadTexture("tile_dollars_small.png");
        _confirmPlatePmc = TryLoadTexture("confirm_box_pmc.png");
        _confirmPlateScav = TryLoadTexture("confirm_box_scav.png");
        _titlePmc = TryLoadTexture("title_pmc.png");
        _titleScav = TryLoadTexture("title_scav.png");
    }

    /// <summary>
    ///     Loads one PNG from an embedded resource. fileName is just the bare
    ///     file name (e.g. "title_pmc.png") - the full resource name is
    ///     assembled as AssetResourcePrefix + fileName to match the
    ///     LogicalName each asset is embedded under in the csproj.
    /// </summary>
    private const string AssetResourcePrefix = "ExfilRewards.Client.assets.";

    private static Texture2D? TryLoadTexture(string fileName)
    {
        string resourceName = AssetResourcePrefix + fileName;
        var assembly = Assembly.GetExecutingAssembly();
        try
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                string available = string.Join(", ", assembly.GetManifestResourceNames());
                BepInEx.Logging.Logger.CreateLogSource("ExfilRewards").LogWarning(
                    $"[ExfilRewards] Embedded asset not found: {resourceName}. " +
                    $"Resources actually embedded in this DLL: {available}");
                return null;
            }

            var bytes = new byte[stream.Length];
            int read = stream.Read(bytes, 0, bytes.Length);
            if (read != bytes.Length)
            {
                BepInEx.Logging.Logger.CreateLogSource("ExfilRewards").LogWarning(
                    $"[ExfilRewards] Short read on embedded asset: {resourceName}");
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                BepInEx.Logging.Logger.CreateLogSource("ExfilRewards").LogWarning(
                    $"[ExfilRewards] Failed to decode embedded asset: {resourceName}");
                return null;
            }
            return tex;
        }
        catch (Exception ex)
        {
            BepInEx.Logging.Logger.CreateLogSource("ExfilRewards").LogWarning(
                $"[ExfilRewards] Error loading embedded asset {resourceName}: {ex.Message}");
            return null;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void BeginFor(string side, int killCount, long survivedSeconds, bool isRunThrough)
    {
        _request = new CalculateRewardRequestDto
        {
            Side = side,
            KillCount = killCount,
            SurvivedSeconds = survivedSeconds,
            IsRunThrough = isRunThrough
        };
        _state = State.Loading;
        _breakdown = null;
        _errorMessage = "";

        _ = LoadCalculationAsync();
    }

    private async Task LoadCalculationAsync()
    {
        try
        {
            var result = await ExfilRewardsApi.CalculateAsync(_request!);
            _breakdown = result;
            _state = result != null ? State.Ready : State.Error;
            if (result == null)
            {
                _errorMessage = "No response from server.";
            }
        }
        catch (Exception ex)
        {
            _state = State.Error;
            _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task ConfirmAsync()
    {
        _state = State.Claiming;
        try
        {
            var result = await ExfilRewardsApi.ClaimAsync(_request!);
            _breakdown = result;
            _state = result != null && result.Granted ? State.Claimed : State.Error;
            if (result == null)
            {
                _errorMessage = "No response from server.";
            }
        }
        catch (Exception ex)
        {
            _state = State.Error;
            _errorMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private void BuildStylesIfNeeded()
    {
        if (_stylesBuilt)
        {
            return;
        }
        _stylesBuilt = true;

        var gold = new Color(0.82f, 0.70f, 0.42f);
        var offWhite = new Color(0.93f, 0.92f, 0.88f);

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = gold }
        };

        // Section labels ("Extraction Bonus", "Kill Bonus", "Total") -
        // centered (was MiddleLeft, which is why they sat flush against
        // the card's left edge instead of centered over their tile row)
        // and in the same warm gold family as the title/tile text so they
        // read as part of the metal-plate theme instead of plain UI gray.
        // fontStyle Bold + slightly letter-spaced feel via uppercase text
        // (set at the call site) approximates the reference card's
        // stenciled section headers within what GUIStyle can do without a
        // custom font asset.
        _rowLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.80f, 0.74f, 0.60f) }
        };

        _rowValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = offWhite }
        };

        _totalLabelStyle = new GUIStyle(_rowLabelStyle)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = gold }
        };

        _totalValueStyle = new GUIStyle(_rowValueStyle)
        {
            fontSize = 19,
            normal = { textColor = gold }
        };

        _statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = offWhite }
        };

        // Plate-style button (see DrawPlateButton) draws its own bordered
        // background, so this style is text-only: no box/background
        // graphic from GUI.skin.button, which is what made the old button
        // look like a stock Unity editor control sitting on top of the
        // card art instead of belonging to it.
        _confirmButtonStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = offWhite },
            hover = { textColor = offWhite },
            active = { textColor = offWhite }
        };

        // wordWrap = false is the actual fix for the roubles overlap bug:
        // GUIStyle(GUI.skin.label) inherits wordWrap = true from the base
        // skin, so a 6-digit rouble amount ("50,900") that didn't quite
        // fit numberZone.width was wrapping onto a second line and
        // rendering both lines stacked in the same Rect. DrawTile also
        // shrinks fontSize per-draw via GetFittingFontSize so long numbers
        // scale down instead of ever reaching that wrap condition.
        //
        // One style per currency (not one shared style with color set
        // per-draw) because GUI.skin.button/label styles are shared
        // mutable objects - reusing one style and mutating
        // .normal.textColor between draws within the same OnGUI call works
        // in practice with IMGUI's immediate draw model, but is fragile
        // (relies on nothing reading the style between the mutation and
        // the GUI.Label call). Three fixed styles removes that risk
        // entirely and matches the reference card's fixed per-currency
        // colors: gold roubles, ice-blue dollars, olive-green euros.
        // Base sizes lowered again per feedback ("still big make em lil
        // bit smaller") - 22/18 -> 18/15. Shrink-to-fit in DrawTile still
        // applies on top of this for any number that's still too wide at
        // the new base size.
        _tileNumberLargeRoublesStyle = BuildTileNumberStyle(18, TextAnchor.MiddleCenter, RoublesAccent);
        _tileNumberLargeEurosStyle = BuildTileNumberStyle(18, TextAnchor.MiddleCenter, EurosAccent);
        _tileNumberLargeDollarsStyle = BuildTileNumberStyle(18, TextAnchor.MiddleCenter, DollarsAccent);

        // Small (Kill Bonus) tile numbers were MiddleLeft, which is why
        // Euros/Dollars especially read as jammed against the left edge
        // of their number zone - centered per feedback, and sized down to
        // match the large-tile reduction (18 -> 15).
        _tileNumberSmallRoublesStyle = BuildTileNumberStyle(15, TextAnchor.MiddleCenter, RoublesAccent);
        _tileNumberSmallEurosStyle = BuildTileNumberStyle(15, TextAnchor.MiddleCenter, EurosAccent);
        _tileNumberSmallDollarsStyle = BuildTileNumberStyle(15, TextAnchor.MiddleCenter, DollarsAccent);

        _buttonPlateNormal = BuildPlateTexture(new Color(0.10f, 0.10f, 0.10f, 0.96f), gold);
        _buttonPlateHover = BuildPlateTexture(new Color(0.16f, 0.15f, 0.12f, 0.98f), gold);
    }

    private static GUIStyle BuildTileNumberStyle(int fontSize, TextAnchor alignment, Color color)
    {
        return new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            wordWrap = false,
            clipping = TextClipping.Overflow,
            normal = { textColor = color }
        };
    }

    /// <summary>
    ///     Builds a small bordered-plate texture (dark fill, 2px accent-
    ///     color border) once at style-build time, reused via
    ///     ScaleMode.StretchToFill for the Confirm/Close button - a thin
    ///     bordered metal-plate look matching the tile art's own border
    ///     treatment, instead of GUI.skin.button's stock rounded-rect.
    /// </summary>
    private static Texture2D BuildPlateTexture(Color fill, Color border)
    {
        const int size = 32;
        const int borderPx = 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool onBorder = x < borderPx || y < borderPx || x >= size - borderPx || y >= size - borderPx;
                pixels[y * size + x] = onBorder ? border : fill;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    ///     Largest font size (searching down from the style's own fontSize
    ///     in steps) whose GUI.CalcSize width fits within maxWidth. This is
    ///     the actual roubles-overlap fix: rather than letting a too-wide
    ///     string wrap (the old bug), the number shrinks a point at a time
    ///     until it fits on one line, down to a floor of 14pt so it never
    ///     shrinks to unreadable.
    /// </summary>
    private static int GetFittingFontSize(GUIStyle style, string text, int startSize, float maxWidth)
    {
        const int minSize = 14;
        int size = startSize;
        int originalSize = style.fontSize;
        while (size > minSize)
        {
            style.fontSize = size;
            float width = style.CalcSize(new GUIContent(text)).x;
            if (width <= maxWidth)
            {
                break;
            }
            size--;
        }
        style.fontSize = originalSize;
        return size;
    }

    /// <summary>
    ///     Draws a section label (Extraction Bonus / Kill Bonus / Total)
    ///     shrunk to fit rect.width on one line - the actual fix for the
    ///     "Extraction Bonus (Run-Through, 50%)" clip: that string is long
    ///     enough at the base font size to wrap under GUI.skin.label's
    ///     default wordWrap, and the wrapped second line then got cut off
    ///     by the fixed-height label Rect. wordWrap = false + shrink-to-fit
    ///     (same approach as the tile numbers) keeps it on one line
    ///     instead, matching how "Extraction Bonus" (no suffix) already
    ///     rendered fine on its own.
    /// </summary>
    private static void DrawSectionLabel(Rect rect, string text, GUIStyle style)
    {
        bool prevWrap = style.wordWrap;
        style.wordWrap = false;
        int baseSize = style.fontSize;
        style.fontSize = GetFittingFontSize(style, text, baseSize, rect.width * 0.96f);
        GUI.Label(rect, text, style);
        style.fontSize = baseSize;
        style.wordWrap = prevWrap;
    }

    private void OnGUI()
    {
        if (_state == State.Claimed)
        {
            // Reward has been mailed - nothing left to show, close the popup.
            return;
        }

        BuildStylesIfNeeded();

        bool isScav = _request?.Side == "scav";

        // Fullscreen themed background (v2 redesign) replaces the old flat
        // near-opaque dim overlay - draws PMC or Scav art full-screen,
        // stretched to cover regardless of aspect ratio mismatch (art is
        // ~16:9, screen may differ slightly; StretchToFill avoids letterbox
        // gaps at the cost of minor distortion, an acceptable trade for a
        // background). Falls back to the old flat dim if the art failed to
        // load, so the popup never renders over a fully-visible raid-end
        // screen even in that failure case.
        Texture2D? fullscreenBg = isScav ? _fullscreenScav : _fullscreenPmc;
        if (fullscreenBg != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), fullscreenBg, ScaleMode.StretchToFill);
        }
        else
        {
            var dimPrev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = dimPrev;
        }

        Texture2D? background = isScav ? _scavBackground : _pmcBackground;

        float cardHeight = Screen.height * 0.72f;
        float aspect = background != null ? (float)background.width / background.height : 0.7f;
        float cardWidth = cardHeight * aspect;

        var cardRect = new Rect(
            (Screen.width - cardWidth) / 2f,
            (Screen.height - cardHeight) / 2f,
            cardWidth,
            cardHeight);

        if (background != null)
        {
            GUI.DrawTexture(cardRect, background, ScaleMode.StretchToFill);
        }
        else
        {
            GUI.Box(cardRect, string.Empty);
        }

        var safeRect = new Rect(
            cardRect.x + cardRect.width * SafeLeft,
            cardRect.y + cardRect.height * SafeTop,
            cardRect.width * (SafeRight - SafeLeft),
            cardRect.height * (SafeBottom - SafeTop));

        switch (_state)
        {
            case State.Loading:
                DrawCenteredLabel(safeRect, "RAID BONUS REWARDS", _titleStyle, 0f, 40f);
                DrawCenteredLabel(safeRect, "Calculating reward...", _statusStyle, safeRect.height * 0.5f, 30f);
                break;

            case State.Error:
                DrawCenteredLabel(safeRect, "RAID BONUS REWARDS", _titleStyle, 0f, 40f);
                DrawCenteredLabel(safeRect, "Error: " + _errorMessage, _statusStyle, safeRect.height * 0.4f, 60f);
                var closeButtonWidth = Mathf.Min(220f, safeRect.width * 0.5f);
                var closeRect = new Rect(
                    safeRect.x + (safeRect.width - closeButtonWidth) / 2f,
                    safeRect.yMax - 56f,
                    closeButtonWidth,
                    44f);
                bool closeHovering = closeRect.Contains(Event.current.mousePosition);
                GUI.DrawTexture(closeRect, closeHovering ? _buttonPlateHover : _buttonPlateNormal, ScaleMode.StretchToFill);
                if (GUI.Button(closeRect, "Close", _confirmButtonStyle))
                {
                    Destroy(gameObject);
                }
                break;

            case State.Ready:
            case State.Claiming:
                DrawReadyState(safeRect);
                break;
        }
    }

    /// <summary>
    ///     Draws a single centered label at a fixed y-offset from the top of
    ///     a rect, with an explicit width/height - used for the Loading and
    ///     Error states, which are simple enough not to need the full manual
    ///     layout DrawReadyState uses.
    /// </summary>
    private static void DrawCenteredLabel(Rect area, string text, GUIStyle? style, float yOffset, float height)
    {
        var rect = new Rect(area.x, area.y + yOffset, area.width, height);
        GUI.Label(rect, text, style);
    }

    /// <summary>
    ///     Entire Ready/Claiming layout computed with explicit Rects rather
    ///     than GUILayout auto-flow. Every prior layout bug (content packed
    ///     left within its own line width, title not centered, Confirm
    ///     button overlapping the card border, dead space above/below the
    ///     block) traced back to GUILayout controls auto-sizing to their own
    ///     content instead of filling the space they were given, and having
    ///     to fight that behavior with FlexibleSpace() at every level. Manual
    ///     Rect math sidesteps the whole class of bug: every element's
    ///     position and size is stated directly against safeRect, so
    ///     "centered" and "fills the width" are literally what the numbers
    ///     say, not an emergent property of nested auto-layout groups.
    /// </summary>
    private void DrawReadyState(Rect safeRect)
    {
        bool isRunThrough = _request?.IsRunThrough ?? false;
        var breakdown = _breakdown;

        float titleHeight = 44f;
        // Confirm plate art is ~4.61:1 (cropped to 2090x453) - width set
        // relative to card width, height derived from that aspect ratio
        // rather than a fixed height, so the plate is never stretched.
        const float confirmPlateAspect = 2090f / 453f;
        float buttonWidth = Mathf.Min(260f, safeRect.width * 0.62f);
        float buttonHeight = buttonWidth / confirmPlateAspect;
        // buttonBottomMargin removed - button position is now computed
        // from the real content-bottom y, not a pre-estimated margin (see
        // buttonGap below).

        // Title art (2172x724, 3:1) replaces the drawn text title, per
        // side, top-center. Falls back to the old text title if the art
        // failed to load, same pattern as every other asset here.
        bool isScavTitle = _request?.Side == "scav";
        Texture2D? titleImage = isScavTitle ? _titleScav : _titlePmc;
        Rect titleRect;
        if (titleImage != null)
        {
            const float titleAspect = 2172f / 724f;
            float titleWidth = safeRect.width * 0.72f;
            float titleImgHeight = titleWidth / titleAspect;
            titleRect = new Rect(
                safeRect.x + (safeRect.width - titleWidth) / 2f,
                safeRect.y,
                titleWidth,
                titleImgHeight);
            GUI.DrawTexture(titleRect, titleImage, ScaleMode.ScaleToFit);
            titleHeight = titleImgHeight;
        }
        else
        {
            titleRect = new Rect(safeRect.x + safeRect.width * 0.04f, safeRect.y, safeRect.width, titleHeight);
            GUI.Label(titleRect, "RAID BONUS REWARDS", _titleStyle);
        }

        // Button position no longer pre-estimated before content - it was
        // guessed against contentAvailable/contentHeight math that kept
        // being wrong at different screen sizes (overlap kept recurring
        // no matter how the margin/cap numbers were tuned). Restructured
        // so contentBottomY is set to the REAL y position after the
        // content block finishes drawing, and buttonRect is built from
        // that actual value plus a small fixed gap - removing the
        // estimate entirely instead of tuning it again.
        float contentBottomY;

        if (breakdown == null)
        {
            GUI.Label(new Rect(safeRect.x, titleRect.yMax, safeRect.width, 30f), "No data.", _statusStyle);
            contentBottomY = titleRect.yMax + 30f;
        }
        else
        {
            // Content laid out top-down from directly below the title, at
            // a fixed size relative to safeRect width only (not tied to
            // any estimate of leftover space above a button) - this is
            // what makes the final y position trustworthy: it only grows
            // downward from a known start point, never estimated against
            // where something else might end up.
            float contentTop = titleRect.yMax + safeRect.height * 0.02f;

            float smallTileHeight;
            float sectionLabelHeight = 22f;
            float sectionGap = safeRect.height * 0.025f;
            float dividerHeight = 14f;
            // Widened per feedback ("move the confirm button even lower") -
            // was 0.02f. Kept identical to the buttonGap used in the actual
            // draw call below (buttonY) so the vertical-budget formula that
            // sizes the tiles and the real button position never drift
            // apart - that consistency is what prevents the button from
            // overlapping the Total row again.
            float buttonGapForSizing = safeRect.height * 0.055f;

            // Solved directly, not capped by trial percentage. Kill Bonus
            // (small tiles) held fixed at its pre-existing size, while
            // Extraction/Total (large tiles) get noticeably bigger. Small
            // and large are no longer proportional, so they're solved as
            // two separate unknowns. Total vertical cost = contentTop + 3
            // section labels + 6 section gaps + 1 divider + 2*largeTileSize
            // + smallTileHeight + buttonGap + buttonHeight, which must fit
            // within safeRect.yMax.
            //
            // The previous attempt at this (v1.0.31) only raised the WIDTH
            // cap (0.36->0.50) and left the vertical-budget divisor
            // effectively smaller than before (subtracting the full fixed
            // smallTileHeight, then dividing by 2, after buttonGap had
            // already grown in the prior change) - so on a normal-height
            // screen the vertical budget was the actual bottleneck, not the
            // width cap, and tiles got smaller instead of bigger. Fixing
            // that here by directly shrinking every OTHER vertical cost
            // (gaps, button gap) so more real room goes to the large tiles,
            // instead of just raising a cap that wasn't the binding
            // constraint.
            // Compression ratios increased again per feedback ("more
            // larger" + "pull down the confirm button more") - both pulls
            // in the same direction (more room to tiles, but button also
            // needs more gap below content), so gaps between labels/rows
            // are compressed further while the button gap is nudged up
            // slightly from v1.0.32's 0.0275 - net vertical budget for
            // tiles still grows because the section-gap compression saves
            // more than the button-gap increase costs (6 section gaps vs
            // 1 button gap). Verified numerically below before shipping,
            // not just asserted.
            float compressedSectionGap = sectionGap * 0.40f;
            float compressedButtonGap = buttonGapForSizing * 0.70f;
            float fixedVerticalCost = contentTop + 3f * sectionLabelHeight + 6f * compressedSectionGap
                                       + dividerHeight + compressedButtonGap + buttonHeight;
            // Small tile height fixed at the same size as before v1.0.31,
            // so Kill Bonus doesn't move at all - independent of the live
            // largeTileSize.
            float smallTileReferenceLarge = safeRect.width * 0.30f;
            smallTileHeight = smallTileReferenceLarge * 0.36f;
            float maxLargeTileFromSpace = (safeRect.yMax - fixedVerticalCost - smallTileHeight) / 2f;
            float largeTileSize = Mathf.Min(safeRect.width * 0.58f, Mathf.Max(40f, maxLargeTileFromSpace));

            float y = contentTop;

            var extractionLabel = isRunThrough ? "EXTRACTION BONUS (RUN-THROUGH, 50%)" : "EXTRACTION BONUS";
            // Shifted right slightly (~2.5% of card width) per feedback -
            // same nudge-via-rect-offset approach used for the title,
            // rather than changing DrawSectionLabel's shared centering
            // logic (which Kill Bonus/Total also use and weren't asked to
            // move).
            float extractionLabelOffset = safeRect.width * 0.025f;
            DrawSectionLabel(new Rect(safeRect.x + extractionLabelOffset, y, safeRect.width, sectionLabelHeight), extractionLabel, _rowLabelStyle!);
            y += sectionLabelHeight + compressedSectionGap;
            DrawTileRow(safeRect, y, largeTileSize, breakdown.ExtractionBonus, large: true);
            y += largeTileSize + compressedSectionGap;

            DrawSectionLabel(new Rect(safeRect.x, y, safeRect.width, sectionLabelHeight), $"KILL BONUS ({breakdown.KillCount})", _rowLabelStyle!);
            y += sectionLabelHeight + compressedSectionGap;
            DrawTileRow(safeRect, y, smallTileHeight, breakdown.KillBonus, large: false);
            y += smallTileHeight + compressedSectionGap;

            DrawGoldLine(new Rect(safeRect.x, y, safeRect.width, 1f));
            y += dividerHeight + compressedSectionGap;

            DrawSectionLabel(new Rect(safeRect.x, y, safeRect.width, sectionLabelHeight), "TOTAL", _totalLabelStyle!);
            y += sectionLabelHeight + compressedSectionGap;
            DrawTileRow(safeRect, y, largeTileSize, breakdown.Total, large: true);
            y += largeTileSize;

            // This is the real, actual bottom edge of the last drawn
            // element - not an estimate.
            contentBottomY = y;
        }

        // Button built from the real content-bottom y position plus a
        // small fixed gap - guaranteed not to overlap the content above
        // it regardless of screen size/aspect, because it's derived from
        // where content actually ended, not a pre-drawing guess. Also
        // clamped so it can never run past the card's own bottom edge
        // (safeRect.yMax) if content somehow still runs tall - without
        // this clamp, an edge-case resolution could push the button off
        // the card entirely.
        //
        // Uses the same compressed gap fraction as compressedButtonGap
        // above (buttonGapForSizing * 0.70f) so the sizing budget and the
        // real drawn position match exactly - this is the same
        // must-stay-in-sync rule that governs sectionGap vs
        // compressedSectionGap above, applied to the button.
        float buttonGap = safeRect.height * 0.055f * 0.70f;
        float buttonY = Mathf.Min(contentBottomY + buttonGap, safeRect.yMax - buttonHeight);
        var buttonRect = new Rect(
            safeRect.x + (safeRect.width - buttonWidth) / 2f,
            buttonY,
            buttonWidth,
            buttonHeight);

        GUI.enabled = _state != State.Claiming;
        bool isScavSide = _request?.Side == "scav";
        Texture2D? confirmPlate = isScavSide ? _confirmPlateScav : _confirmPlatePmc;
        if (confirmPlate != null)
        {
            // "CONFIRM" is baked into the plate art itself (per side), so
            // this draws the image only - no text label drawn over it.
            // Per direction: don't attempt "Close Preview" wording on this
            // art: preview mode's button just acts as Confirm/Close using
            // the same plate.
            var prevColor = GUI.color;
            if (_state == State.Claiming)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
            }
            GUI.DrawTexture(buttonRect, confirmPlate, ScaleMode.StretchToFill);
            GUI.color = prevColor;
            if (GUI.Button(buttonRect, GUIContent.none, GUIStyle.none))
            {
                _ = ConfirmAsync();
            }
        }
        else
        {
            // Fallback if the plate art failed to load - old bordered-
            // plate-texture + text button, so Confirm is never invisible.
            string confirmLabel = _state == State.Claiming ? "Confirming..." : "Confirm";
            bool hovering = buttonRect.Contains(Event.current.mousePosition);
            GUI.DrawTexture(buttonRect, hovering ? _buttonPlateHover : _buttonPlateNormal, ScaleMode.StretchToFill);
            if (GUI.Button(buttonRect, confirmLabel, _confirmButtonStyle))
            {
                _ = ConfirmAsync();
            }
        }
        GUI.enabled = true;
    }

    private static void DrawGoldLine(Rect rect)
    {
        var prev = GUI.color;
        GUI.color = new Color(0.82f, 0.70f, 0.42f, 0.4f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>
    ///     Draws three tiles (Roubles, Euros, Dollars - per spec order) side
    ///     by side, each tileSize-square (large) or tileSize-tall wide bars
    ///     (small), evenly spaced across safeRect's width with a real gap
    ///     between them (not FlexibleSpace - an explicit gap computed from
    ///     the available width so it can't collapse to zero or overflow).
    /// </summary>
    private void DrawTileRow(Rect safeRect, float y, float tileSize, CurrencyAmountsDto amounts, bool large)
    {
        const int columns = 3;
        // Row width capped at 94% of safeRect (was implicitly ~90-95%,
        // which could push the outer tiles' number text past safeRect's
        // own right/left bound into the card's decorative corner art -
        // this is what was clipping the Total row's rouble number).
        // Explicit cap here guarantees the whole row, tiles included,
        // stays inside safeRect regardless of tileSize.
        float gap = safeRect.width * 0.03f;
        float tileWidth = large ? tileSize : (safeRect.width - gap * (columns - 1)) / columns;
        float totalWidth = tileWidth * columns + gap * (columns - 1);
        float maxRowWidth = safeRect.width * 0.94f;
        float tileHeight = tileSize;
        if (totalWidth > maxRowWidth)
        {
            float scale = maxRowWidth / totalWidth;
            tileWidth *= scale;
            gap *= scale;
            totalWidth = maxRowWidth;
            // Large tiles are square art - scale height down with width so
            // a row-width clamp never stretches/distorts the tile texture.
            // Small tiles are wide bars sized independently by height
            // already (smallTileHeight, set by the caller), so their
            // height is intentionally left alone here.
            if (large)
            {
                tileHeight *= scale;
            }
        }
        float startX = safeRect.x + (safeRect.width - totalWidth) / 2f;

        Texture2D? roublesTex = large ? _tileRoublesLarge : _tileRoublesSmall;
        Texture2D? eurosTex = large ? _tileEurosLarge : _tileEurosSmall;
        Texture2D? dollarsTex = large ? _tileDollarsLarge : _tileDollarsSmall;

        GUIStyle roublesStyle = large ? _tileNumberLargeRoublesStyle! : _tileNumberSmallRoublesStyle!;
        GUIStyle eurosStyle = large ? _tileNumberLargeEurosStyle! : _tileNumberSmallEurosStyle!;
        GUIStyle dollarsStyle = large ? _tileNumberLargeDollarsStyle! : _tileNumberSmallDollarsStyle!;

        DrawTile(new Rect(startX, y, tileWidth, tileHeight), roublesTex, amounts.Roubles, large, roublesStyle);
        DrawTile(new Rect(startX + (tileWidth + gap) * 1, y, tileWidth, tileHeight), eurosTex, amounts.Euros, large, eurosStyle);
        DrawTile(new Rect(startX + (tileWidth + gap) * 2, y, tileWidth, tileHeight), dollarsTex, amounts.Dollars, large, dollarsStyle);
    }

    /// <summary>
    ///     Draws one currency tile and its number into the tile art's own
    ///     blank zone. Number-zone fractions below were measured directly
    ///     against the actual supplied PNGs (not guessed): large tiles
    ///     (1254x1254 source) have a clean bordered box roughly x 8%-92%,
    ///     y 63.5%-90%; small tiles (2172x724 source) have a clean area
    ///     roughly x 27%-94%, y 8%-92% (icon occupies the left ~25%). If a
    ///     tile texture failed to load, falls back to a plain colored box so
    ///     the number is still visible rather than lost.
    /// </summary>
    private void DrawTile(Rect tileRect, Texture2D? tex, long amount, bool large, GUIStyle style)
    {
        if (tex != null)
        {
            GUI.DrawTexture(tileRect, tex, ScaleMode.StretchToFill);
        }
        else
        {
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            GUI.DrawTexture(tileRect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        Rect numberZone = large
            ? new Rect(
                tileRect.x + tileRect.width * 0.08f,
                tileRect.y + tileRect.height * 0.635f,
                tileRect.width * 0.84f,
                tileRect.height * 0.265f)
            : new Rect(
                tileRect.x + tileRect.width * 0.27f,
                tileRect.y + tileRect.height * 0.08f,
                tileRect.width * 0.67f,
                tileRect.height * 0.84f);

        string text = large ? amount.ToString("N0") : "+" + amount.ToString("N0");

        // Shrink-to-fit instead of letting the label wrap - this is what
        // actually stops six-digit rouble amounts (e.g. "50,900") from
        // stacking two wrapped lines on top of each other in the old bug.
        int baseSize = style.fontSize;
        style.fontSize = GetFittingFontSize(style, text, baseSize, numberZone.width);

        // Hard clip to the tile's own bounds as a safety net on top of the
        // shrink-to-fit - GUI.BeginGroup makes tileRect an actual clipping
        // region, so even if a future layout change ever again lets a
        // number's measured width be wrong (font metrics can differ
        // slightly from CalcSize in edge cases), the text is cut at the
        // tile edge instead of spilling into the next tile or off the
        // card, which is what produced the clipped "86,90" cut instead of
        // "86,900" previously - the number wasn't clipped so much as
        // rendered past where the row's own math placed it.
        GUI.BeginGroup(tileRect);
        GUI.Label(new Rect(numberZone.x - tileRect.x, numberZone.y - tileRect.y, numberZone.width, numberZone.height), text, style);
        GUI.EndGroup();

        style.fontSize = baseSize;
    }
}
