using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SlotView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    // Number of distinct symbols the backend can send (ids 0..SymbolCount-1). Every array indexed
    // by symbol id is sized from this, so the count lives in exactly one place.
    private const int SymbolCount = 10;

    [Header("Symbol Sprites - Assign by Name")]
    // Field names follow the backend's symbol "name", so the two can be checked against each other
    // at a glance. The ids are the init's symbol ids and are what the server's matrix carries — if
    // the backend ever reorders the table, BuildSymbolSpriteArray is what to correct.
    [SerializeField] private Sprite spriteWild;               // ID: 0  (wild)
    [SerializeField] private Sprite spriteJackInTheBox;       // ID: 1  (high — top paytable)
    [SerializeField] private Sprite spriteA;                  // ID: 2  (high)
    [SerializeField] private Sprite spriteK;                  // ID: 3  (high)
    [SerializeField] private Sprite spriteQ;                  // ID: 4  (low)
    [SerializeField] private Sprite spriteJ;                  // ID: 5  (low)
    [SerializeField] private Sprite spriteTen;                // ID: 6  (low)
    [SerializeField] private Sprite spriteDrum;               // ID: 7  (scatter — reels 2-4 only, triggers Free Spins)
    [SerializeField] private Sprite spriteBall;               // ID: 8  (scatter — triggers the Ball Pick'em bonus)
    [SerializeField] private Sprite spriteSandbell;           // ID: 9  (scatter — triggers the Sandbell Pick bonus)

    // Rect size for each symbol, keyed by symbol id. Any id not listed here uses normalSymbolSize.
    //
    // Every symbol is listed, all at DefaultSymbolSize (200) until each is sized against its own
    // art. Sizing is per-symbol art, not a role: anything above the 175 pitch overlaps its vertical
    // neighbours, which is intentional bleed but also means it swallows clicks aimed at the cells
    // above and below.
    //
    // Kept next to the sprite fields on purpose: both are id-keyed maps of the same symbol table, so
    // if the backend ever reorders it they have to be corrected together.
    private static readonly Vector2 DefaultSymbolSize = new Vector2(200f, 200f);
    private static readonly Dictionary<int, Vector2> SymbolSizeOverrides = new Dictionary<int, Vector2>
    {
        {0, DefaultSymbolSize},  // Wild
        {1, DefaultSymbolSize},  // Jack-in-the-box
        {2, DefaultSymbolSize},  // A
        {3, DefaultSymbolSize},  // K
        {4, DefaultSymbolSize},  // Q
        {5, DefaultSymbolSize},  // J
        {6, DefaultSymbolSize},  // 10
        {7, DefaultSymbolSize},  // Drum
        {8, DefaultSymbolSize},  // Ball
        {9, DefaultSymbolSize}   // Sandbell
    };

    // Playback speed per symbol, applied wherever that symbol's clip is assigned.
    //
    // NOT frames per second. ImageAnimation derives its frame delay as
    // (1/24) * frameCount / AnimationSpeed, so the same value plays a long clip more slowly than a
    // short one — which is why each symbol needs its own, tuned by eye against its own art.
    //
    // Every symbol gets a value on every write. The win-layer components are reused from spin to
    // spin, so leaving one untouched would silently inherit whatever the previous symbol had set
    // on that slot.
    //
    // All 10 are listed explicitly, all at the default until each is tuned against its own clip.
    // The fallback below is only reached if the backend ever sends an id this table doesn't know
    // about.
    private const float DefaultSymbolAnimationSpeed = 20f;
    //Animation Speeds
    private static readonly Dictionary<int, float> SymbolAnimationSpeeds = new Dictionary<int, float>
    {
        { 0, DefaultSymbolAnimationSpeed },  // Wild
        { 1, DefaultSymbolAnimationSpeed },  // Jack-in-the-box
        { 2, DefaultSymbolAnimationSpeed },  // A
        { 3, DefaultSymbolAnimationSpeed },  // K
        { 4, DefaultSymbolAnimationSpeed },  // Q
        { 5, DefaultSymbolAnimationSpeed },  // J
        { 6, DefaultSymbolAnimationSpeed },  // 10
        { 7, DefaultSymbolAnimationSpeed },  // Drum
        { 8, DefaultSymbolAnimationSpeed },  // Ball
        { 9, DefaultSymbolAnimationSpeed }   // Sandbell
    };

    // Internal array built from named sprites
    private Sprite[] symbolSprites;

    [Header("Win Animation Sprite Arrays")]
    [Tooltip("Optional per-symbol win-animation frame sequences. Leave any empty until real art exists — animation playback already no-ops safely on an empty list.")]
    [SerializeField] private List<Sprite> animSpritesWild;           // ID: 0
    [SerializeField] private List<Sprite> animSpritesJackInTheBox;   // ID: 1
    [SerializeField] private List<Sprite> animSpritesA;              // ID: 2
    [SerializeField] private List<Sprite> animSpritesK;              // ID: 3
    [SerializeField] private List<Sprite> animSpritesQ;              // ID: 4
    [SerializeField] private List<Sprite> animSpritesJ;              // ID: 5
    [SerializeField] private List<Sprite> animSpritesTen;            // ID: 6
    [SerializeField] private List<Sprite> animSpritesDrum;           // ID: 7
    [SerializeField] private List<Sprite> animSpritesBall;           // ID: 8
    [SerializeField] private List<Sprite> animSpritesSandbell;       // ID: 9

    // Internal array of animation sprite lists
    private List<Sprite>[] animationSpriteArrays;

    [Header("Reel Containers")]
    [SerializeField] private Transform[] reelTransforms;

    [Header("Reel Images")]
    [SerializeField] private List<ReelImages> reelImagesList;

    // ── Symbol sizing / reel pitch ──────────────────────────────────────────────────────────────
    // Not serialized, so the code is the single source of truth — a serialized value would be
    // overridden by whatever the scene saved. The trade is that changing them needs a recompile.

    // Rect size used by every symbol not listed in SymbolSizeOverrides.
    private Vector2 normalSymbolSize = new Vector2(175f, 175f);

    // Must match the actual icon pitch in the scene. Drives the spin loop's travel distance, which
    // has to be a whole number of pitches or the loop's wrap-around is visible.
    private float symbolHeight = 245f;

    [Header("Spin Settings")]
    [SerializeField] private float spinSpeed = 6000f;
    [SerializeField] private float reelStartStagger = 0.08f;
    [SerializeField] private float reelStopStagger = 0.12f;

    [Header("Animation Settings - Casino Style")]
    [SerializeField] private float anticipationUpDistance = 20f;
    [SerializeField] private float anticipationUpDuration = 0.12f;

    [Header("Stop Animation Settings")]
    // One continuous tween using DOTween's overshoot-and-settle curve for the landing.
    [SerializeField] private Ease stopEase = Ease.OutBack;
    [Tooltip("Overshoot strength for stopEase — how far a reel bounces past its landing before it settles.")]
    [SerializeField] private float stopEaseOvershoot = 0.9f;
    [Tooltip("Length of the landing tween. Authored rather than derived from distance and spin speed, like every other stop timing in this file.")]
    [SerializeField] private float stopDuration = 0.5f;

    [Header("Quick Spin Settings")]
    [SerializeField] private float quickStopStagger = 0.06f;
    [SerializeField] private float quickStopOvershoot = 20f;
    [SerializeField] private float quickStopDuration = 0.2f;

    [Header("Scatter Anticipation")]
    [Tooltip("Effects shown around a reel that could still complete a scatter trigger. Index 0 = reel 2, index 1 = reel 3, index 2 = reel 4, index 3 = reel 5. Reel 1 can never anticipate, since two scatters must already have landed.")]
    [SerializeField] private GameObject[] anticipationEffects = new GameObject[4];
    [Tooltip("Extra time each held reel keeps spinning. Applied per held reel and cumulative, so several holds in one spin add up.")]
    [SerializeField] private float anticipationExtraTime = 2f;
    [Tooltip("Shorter hold used when the player is on Turbo.")]
    [SerializeField] private float anticipationExtraTimeTurbo = 1f;

    [Header("Continuous Spin (Tween) Settings")]
    [Tooltip("Filler image slots prepended above the visible window, giving the continuous spin loop room to travel before it has to wrap.")]
    [SerializeField] private int bufferRowsAbove = 16;
    [SerializeField] private Ease spinLoopEase = Ease.Linear;


    [Header("Win Animation Settings")]
    [SerializeField] private float winSymbolLoopDuration = 1.5f;

    // The shape of the win presentation: every winning symbol plays twice in step (once in autoplay
    // and free spins), then holds, looping, until the next spin. The per-line walk between them is
    // commented out (owner) — see PlayWinLineCycleRoutine; the two line constants
    // below belong to it.
    private const int totalWinRounds = 2;
    private const int winLinePassCount = 1;
    private const int winLineRounds = 1;
    [Tooltip("Delay between raising the win animation layer and starting the ImageAnimation - for sync timing")]
    [SerializeField] private float winLineBoxToAnimationDelay = 0.05f;

    [Header("Win Presentation Layer")]
    [Tooltip("Dark sheet covering the reel area during a win. Snaps on/off, no fade.")]
    [SerializeField] private GameObject winDimOverlay;
    [Tooltip("Root of the layer holding the bright winning symbols, drawn above the dim.")]
    [SerializeField] private GameObject winAnimationLayer;
    [Tooltip("One entry per reel column, each holding the 3 active-row slots top to bottom.")]
    [SerializeField] private List<AnimSlotColumn> animSlotColumns = new List<AnimSlotColumn>(3);
    [Tooltip("The per-line win amount, ONE PER ROW, top to bottom — element 0 is the top row. This game draws no payline graphics: a win is shown by animating its symbols and putting its payout on the middle reel, so only three positions are ever needed.")]
    [SerializeField] private TMPro.TMP_Text[] winLineAmounts = new TMPro.TMP_Text[3];

    [Header("Phase 1 Total Win Presentation")]
    [SerializeField] private TMPro.TMP_Text phase1TotalWinText;

    [Header("Symbol Info Card")]
    [SerializeField] private SymbolInfoCard symbolInfoCard;


    private float middlePosition = 0f;


    private List<Tween> spinTweens = new List<Tween>();
    private List<Tween> winTweens = new List<Tween>();
    private Coroutine winAnimationCoroutine;

    // Set when the controller asks for the line walk (PlayWinLineCycle) while a presentation is still
    // on its total, which decided at its start whether to walk the lines. Honoured when the total
    // ends; reset at the start of every presentation.
    private bool lineWalkRequested;

    // The lines from the spin that just landed, kept so the controller can start the Phase 2 cycle
    // after the fact — autoplay and free spins skip it while they run, and only the controller knows
    // when the round is actually over. Null on a losing spin: StartSpin clears it, and only a win
    // writes it again.
    private List<WinLine> lastWinLines;

    // Which reels are being held back to tease a scatter trigger this spin. Filled before the reels
    // start stopping; StopSingleReel raises and clears each effect off its own landing events.
    // More than one reel can be held in a single spin — see ComputeAnticipatedReels.
    private readonly HashSet<int> anticipatedReels = new HashSet<int>();

    // A per-spin claim on the win dim: true while something raised it before the win presentation
    // and means the presentation to inherit it, instead of dropping and re-raising it (which
    // flickers). Nothing sets it today; kept, with its guards, as the pattern to follow.
    private bool dimHeld;

    // A feature round's claim on the shared dim, alongside dimHeld above. A round that owns the
    // board holds this for its whole duration, so an ordinary win teardown inside the round cannot
    // drop the dim out from under it. Nothing sets it today; kept like dimHeld.
    private bool featureDimHeld;

    // Ids the scroll buffer is allowed to pick from. Built once and cached, since gameConfig doesn't
    // change after init and this is read on every buffer icon of every spin. See
    // EnsureFillerSymbolIds for what would justify excluding a symbol from it.
    private List<int> fillerSymbolIds;


    internal List<List<int>> currentDisplayMatrix;

    private bool isSpinning;

    // Config-driven, not Inspector-array-length-driven: reelTransforms/reelImagesList may still
    // have leftover unused slots from an older game's layout, so this must
    // reflect the real backend's reel count, not the serialized array size.
    internal int ReelCount => (gameManager != null && gameManager.gameConfig != null)
        ? gameManager.gameConfig.reelCount
        : (reelTransforms != null ? reelTransforms.Length : 3);

    // Row count (3). Every row the server sends is live and pays, so a row index means the same thing
    // in the server payload, in currentDisplayMatrix, and in each reel's displayImages list.
    internal int RowCount => (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

    // -1 rather than 0 when unknown, deliberately: 0 is a real symbol (the Wild itself), so a literal
    // fallback would silently match it before init.
    private int WildSymbolId => (gameManager != null && gameManager.gameConfig != null)
        ? gameManager.gameConfig.wildSymbolId
        : -1;

    #region Initialization

    
    private void Awake()
    {
        BuildSymbolSpriteArray();
        InitializeReels();
    }

    private void Start()
    {
        if (symbolSprites == null || symbolSprites.Length == 0)
        {
            BuildSymbolSpriteArray();
        }
        DisableAllOverlays();
        SetupSymbolButtons();
    }

    private void DisableAllOverlays()
    {
        HidePhase1TotalWinText();
        HideAnticipationEffects();
        HideWinSlots();
        HideAllWinLines();
        // Release rather than Hide: this is the full teardown, so no claim on the dim survives it.
        ReleaseHeldDim();
        HideWinDim();
        if (symbolInfoCard) symbolInfoCard.HideCard();
    }

    private void HideAnticipationEffects()
    {
        anticipatedReels.Clear();
        AudioManager.Instance?.StopAnticipation();
        if (anticipationEffects == null) return;
        foreach (var effect in anticipationEffects)
        {
            if (effect != null) effect.SetActive(false);
        }
    }

    private void SetupSymbolButtons()
    {
        if (reelImagesList == null) return;
        for (int col = 0; col < reelImagesList.Count; col++)
        {
            var reel = reelImagesList[col];
            if (reel == null || reel.displayImages == null) continue;
            int rowCount = RowCount;
            for (int row = 0; row < rowCount; row++)
            {
                if (row < reel.displayImages.Count && reel.displayImages[row] != null)
                {
                    Image img = reel.displayImages[row];
                    SymbolButtonHandler btnHandler = img.GetComponent<SymbolButtonHandler>();
                    if (btnHandler == null)
                    {
                        btnHandler = img.gameObject.AddComponent<SymbolButtonHandler>();
                    }
                    btnHandler.Init(col, row, this);
                }
            }
        }
    }

    internal void OnBetChanged()
    {
        if (symbolInfoCard != null && symbolInfoCard.gameObject.activeSelf)
        {
            symbolInfoCard.RefreshCard(gameManager);
        }
    }

    internal void OnSymbolClicked(int col, int row, RectTransform symbolRect)
    {
        if (isSpinning)
        {
            if (symbolInfoCard != null) symbolInfoCard.HideCard();
            return;
        }

        int matrixRow = row;
        if (currentDisplayMatrix == null || col >= currentDisplayMatrix.Count || matrixRow < 0 || matrixRow >= currentDisplayMatrix[col].Count)
        {
            return;
        }

        int symbolId = currentDisplayMatrix[col][matrixRow];

        if (symbolInfoCard != null)
        {
            symbolInfoCard.ShowCard(symbolId, col, row, symbolRect, gameManager);
        }
    }

    private void BuildSymbolSpriteArray()
    {
        // Build the symbol sprite array from named sprite fields
        symbolSprites = new Sprite[SymbolCount];
        symbolSprites[0] = spriteWild;
        symbolSprites[1] = spriteJackInTheBox;
        symbolSprites[2] = spriteA;
        symbolSprites[3] = spriteK;
        symbolSprites[4] = spriteQ;
        symbolSprites[5] = spriteJ;
        symbolSprites[6] = spriteTen;
        symbolSprites[7] = spriteDrum;
        symbolSprites[8] = spriteBall;
        symbolSprites[9] = spriteSandbell;

        // Validate
        for (int i = 0; i < symbolSprites.Length; i++)
        {
            if (symbolSprites[i] == null)
            {
                Debug.LogError($"[SlotView] Symbol sprite at index {i} is not assigned in inspector!");
            }
        }

        // Build the animation sprite arrays (any entry left empty simply won't animate)
        animationSpriteArrays = new List<Sprite>[SymbolCount];
        animationSpriteArrays[0] = animSpritesWild;
        animationSpriteArrays[1] = animSpritesJackInTheBox;
        animationSpriteArrays[2] = animSpritesA;
        animationSpriteArrays[3] = animSpritesK;
        animationSpriteArrays[4] = animSpritesQ;
        animationSpriteArrays[5] = animSpritesJ;
        animationSpriteArrays[6] = animSpritesTen;
        animationSpriteArrays[7] = animSpritesDrum;
        animationSpriteArrays[8] = animSpritesBall;
        animationSpriteArrays[9] = animSpritesSandbell;
    }

    private void InitializeReels()
    {
        middlePosition = -67.4f;

        int rowCount = RowCount;

        currentDisplayMatrix = new List<List<int>>();
        for (int col = 0; col < ReelCount; col++)
        {
            var defaultCol = new List<int>();
            for (int r = 0; r < rowCount; r++)
            {
                defaultCol.Add(0);
            }
            currentDisplayMatrix.Add(defaultCol);
        }
    }

    internal void SetInitialMatrix(List<List<int>> matrix)
    {
        if (matrix == null || matrix.Count != ReelCount) return;

        int rowCount = RowCount;

        for (int col = 0; col < ReelCount; col++)
        {
            if (matrix[col].Count != rowCount) return;
        }

        currentDisplayMatrix = matrix;

        for (int col = 0; col < ReelCount; col++)
        {
            SetReelSymbols(col, matrix[col], true);
        }
    }

    #endregion

    #region Symbol Display

    private void SetReelSymbols(int columnIndex, List<int> visibleSymbolIds, bool isInitial = false)
    {
        if (columnIndex >= reelImagesList.Count)
        {
            Debug.LogError($"SetReelSymbols: Invalid column index {columnIndex}, max is {reelImagesList.Count - 1}");
            return;
        }

        int rowCount = RowCount;

        if (visibleSymbolIds == null || visibleSymbolIds.Count != rowCount)
        {
            Debug.LogError($"SetReelSymbols: Invalid visibleSymbolIds count {visibleSymbolIds?.Count}, expected {rowCount}");
            return;
        }

        var reel = reelImagesList[columnIndex];

        if (reel.images == null)
        {
            Debug.LogError($"SetReelSymbols: Reel {columnIndex} has no images assigned");
            return;
        }

        WriteDisplayBlockSprites(columnIndex, visibleSymbolIds);
        RandomizeBufferSprites(columnIndex);

        if (isInitial && reelTransforms[columnIndex] != null)
        {
            reelTransforms[columnIndex].localPosition = new Vector3(
                reelTransforms[columnIndex].localPosition.x,
                middlePosition,
                0
            );
        }
    }

    // Writes only the display-block sprites (no buffer reshuffle, no position touch). Called from
    // SetReelSymbols above.
    private void WriteDisplayBlockSprites(int columnIndex, List<int> visibleSymbolIds)
    {
        if (columnIndex >= reelImagesList.Count) return;

        int rowCount = RowCount;
        if (visibleSymbolIds == null || visibleSymbolIds.Count != rowCount) return;

        var reel = reelImagesList[columnIndex];
        if (reel.displayImages == null) return;

        for (int row = 0; row < rowCount; row++)
        {
            if (row < reel.displayImages.Count && reel.displayImages[row] != null)
            {
                int symbolId = visibleSymbolIds[row];

                ApplySymbol(reel.displayImages[row], symbolId, manageRaycast: true);
            }
        }
    }

    // Falls back rather than throwing on an unlisted id: only symbols being retuned need an entry,
    // and an id with no entry is the normal case, not an error.
    private static float GetSymbolAnimationSpeed(int symbolId)
    {
        return SymbolAnimationSpeeds.TryGetValue(symbolId, out float speed)
            ? speed
            : DefaultSymbolAnimationSpeed;
    }

    private Sprite GetSymbolSprite(int symbolId)
    {
        // Validate symbolId range (0..SymbolCount-1)
        if (symbolId < 0 || symbolId >= symbolSprites.Length)
        {
            Debug.LogWarning($"[SlotView] Invalid symbolId {symbolId}, using default sprite 0. Total sprites: {symbolSprites.Length}");
            return symbolSprites[0];
        }

        if (symbolSprites[symbolId] == null)
        {
            Debug.LogError($"[SlotView] Symbol sprite for ID {symbolId} is null!");
            return symbolSprites[0];
        }

        return symbolSprites[symbolId];
    }

    // Single place that puts a symbol onto an icon. Sprite and size are set together on purpose:
    // symbols can be drawn at different scales (see SymbolSizeOverrides), so each needs its own
    // rect. Because every write goes through here and always sets a size, an icon that showed an
    // oversized symbol is snapped back as soon as it's given any other — no reset pass to maintain
    // and no way for an icon to get stuck oversized.
    private void ApplySymbol(Image image, int symbolId, bool manageRaycast = false)
    {
        if (image == null) return;

        image.sprite = GetSymbolSprite(symbolId);

        // Sizing is art-driven, not role-driven, which is why this reads an id-keyed map. Anything
        // above 175 overlaps its vertical neighbours, which is intentional art bleed, but also means
        // those symbols swallow clicks aimed at the ones above and below them.
        image.rectTransform.sizeDelta = SymbolSizeOverrides.TryGetValue(symbolId, out Vector2 size)
            ? size
            : normalSymbolSize;

        // Display icons must catch clicks so the symbol info card can open on them. This is set
        // here rather than left to the scene so it can't be lost by an icon being re-authored.
        //
        // Opt-in rather than unconditional, because the other two callers must not get it: the
        // win-animation layer's slots are authored raycast-off and have to stay that way (they sit
        // above the reels during a win), and the scroll buffer has no info card to open.
        //
        // Oversized symbols (see the size table) overlap their neighbours, so they can steal clicks
        // aimed at the cells above and below.
        if (manageRaycast)
        {
            image.raycastTarget = true;
        }
    }

    // Randomizes the pure spin-loop scroll buffer. images now holds only buffer icons (the 5
    // real display-block icons live in displayImages instead), so no start/end boundary math
    // is needed — every entry here is fair game for random filler.
    private void RandomizeBufferSprites(int columnIndex)
    {
        if (columnIndex >= reelImagesList.Count) return;
        var reel = reelImagesList[columnIndex];
        if (reel.images == null) return;

        EnsureFillerSymbolIds();

        for (int i = 0; i < reel.images.Count; i++)
        {
            // Held in a variable so ApplySymbol can size it — the pool spans every symbol id, so a
            // special that needs its own rect size resizes as it scrolls past just like a landed one.
            int symbolId = fillerSymbolIds.Count > 0
                ? fillerSymbolIds[Random.Range(0, fillerSymbolIds.Count)]
                : 0;
            ApplySymbol(reel.images[i], symbolId);
        }
    }

    // Builds the filler pool once and reuses it — gameConfig is fixed for the session, and this is
    // read on every buffer icon of every spin.
    //
    // Every id is in the pool. Exclude one here if a symbol should never appear as random filler —
    // anything the backend has to PLACE deliberately, because it carries a value or only means
    // something inside a sequence. Seeing one scroll past would be showing something the server
    // never sent.
    private void EnsureFillerSymbolIds()
    {
        if (fillerSymbolIds != null) return;

        fillerSymbolIds = new List<int>(SymbolCount);
        for (int id = 0; id < SymbolCount; id++)
        {
            fillerSymbolIds.Add(id);
        }
    }

    #endregion

    #region Spin Animation

    internal void StartSpin()
    {
        if (isSpinning) return;

        if (symbolInfoCard != null) symbolInfoCard.HideCard();

        isSpinning = true;

        // Cleared at every spin start: only a winning spin writes it, so a losing spin must not leave
        // the previous win for PlayWinLineCycle to replay over the new board.
        lastWinLines = null;

        KillAllTweens();

        DisableAllOverlays();

        for (int col = 0; col < ReelCount; col++)
        {
            RandomizeBufferSprites(col);
            StartReelCycleWithDelay(col, col * reelStartStagger);
        }
    }

    private void StartReelCycleWithDelay(int columnIndex, float delay)
    {
        if (columnIndex >= reelTransforms.Length) return;

        Transform slotTransform = reelTransforms[columnIndex];

        Sequence startSequence = DOTween.Sequence();

        if (delay > 0)
        {
            startSequence.AppendInterval(delay);
        }

        startSequence.Append(
            slotTransform.DOLocalMoveY(middlePosition + anticipationUpDistance, anticipationUpDuration)
                .SetEase(Ease.OutQuad)
        );

        startSequence.Append(
            slotTransform.DOLocalMoveY(middlePosition, anticipationUpDuration * 0.5f)
                .SetEase(Ease.InQuad)
        );

        startSequence.OnComplete(() => {
            if (isSpinning)
            {
                StartContinuousLoop(columnIndex);
            }
        });

        startSequence.Play();

        if (spinTweens.Count <= columnIndex)
            spinTweens.Add(startSequence);
        else
            spinTweens[columnIndex] = startSequence;
    }

    // One continuous loop tween per column. The strip's sprites are set once at StartSpin() and left
    // alone for the rest of the spin — reshuffling on each wrap shows as symbols popping mid-scroll.
    private void StartContinuousLoop(int columnIndex)
    {
        if (columnIndex >= reelTransforms.Length) return;
        if (!isSpinning) return;

        Transform slotTransform = reelTransforms[columnIndex];

        slotTransform.localPosition = new Vector3(slotTransform.localPosition.x, middlePosition, 0);

        float loopDistance = bufferRowsAbove * symbolHeight;
        float loopDuration = loopDistance / spinSpeed;

        Tween loopTween = slotTransform.DOLocalMoveY(middlePosition - loopDistance, loopDuration)
            .SetEase(spinLoopEase)
            .SetLoops(-1, LoopType.Restart);

        loopTween.Play();

        if (spinTweens.Count <= columnIndex)
            spinTweens.Add(loopTween);
        else
            spinTweens[columnIndex] = loopTween;
    }

    #endregion

    #region Stop Spin

    internal void StopSpin(List<List<int>> resultMatrix, System.Action onComplete)
    {
        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < ReelCount; col++)
            {
                SetReelSymbols(col, resultMatrix[col], false);
            }
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(StopSpinSequence(resultMatrix, onComplete, false));
    }

    private IEnumerator StopSpinSequence(List<List<int>> resultMatrix, System.Action onComplete, bool isQuickStop)
    {
        currentDisplayMatrix = resultMatrix;

        // Anything per-cell that a reel's landing write needs from the spin result has to be
        // captured here, before any reel lands, and never read at DRAW time: the last reel lands on
        // the same frame this sequence reports completion, and the controller nulls lastResult a
        // frame or two later, so a draw-time read is a race the last reel routinely loses. (Gold of
        // Luck captured its Genie multipliers here for exactly that reason.)

        // GameManager.GetSpinDuration() already enforces the minimum spin time before this is
        // ever called, so there's no need for a separate discrete-cycle-count gate here.
        float stagger = isQuickStop ? quickStopStagger : reelStopStagger;

        // Skipped entirely on a quick stop — that path covers both QuickSpin mode and the player
        // hitting Stop, and neither should sit through the hold. StopSingleReel reads this set to
        // know when to raise and clear each effect.
        anticipatedReels.Clear();
        if (!isQuickStop) ComputeAnticipatedReels(resultMatrix, anticipatedReels);
        float anticipationHold = GetAnticipationHold();

        // Each held reel spins on for anticipationHold, which pushes itself and everything after
        // it back by that much — so the delays are cumulative and the reels still land left to
        // right. Four held reels really do add four holds; that is the intended drama.
        int holdsSoFar = 0;
        for (int col = 0; col < ReelCount; col++)
        {
            if (anticipatedReels.Contains(col)) holdsSoFar++;

            float delay = col * stagger + anticipationHold * holdsSoFar;
            StartCoroutine(StopSingleReel(col, resultMatrix[col], delay, isQuickStop));
        }

        float lastColumnDelay = (ReelCount - 1) * stagger;
        float longestStopTime;
        if (isQuickStop)
        {
            longestStopTime = lastColumnDelay + quickStopDuration;
        }
        else
        {
            longestStopTime = lastColumnDelay + stopDuration;
        }

        // The whole hand-off waits for every hold too, so the win presentation can't start while a
        // reel is still spinning on.
        longestStopTime += anticipationHold * anticipatedReels.Count;

        yield return new WaitForSeconds(longestStopTime);

        isSpinning = false;

        // Cut the spin loop here rather than in the controller's OnReelsStoppedComplete: this is the
        // real moment the last reel lands, on both the normal and the quick stop. Anticipation is
        // already accounted for, since the hold is folded into longestStopTime above — a teased
        // reel keeps the loop running while it spins on.
        AudioManager.Instance?.StopSpinLoop();

        // Safety net only — the tension normally stops as the last held reel lands, below.
        AudioManager.Instance?.StopAnticipation();

        onComplete?.Invoke();
    }

    /// <summary>
    /// Which reels are held back to tease a scatter trigger. Any number of reels can be held in one
    /// spin, and the hold starts as soon as a 2nd scatter is on the board — whether or not a 3rd
    /// ever turns up.
    ///
    /// The whole rule reduces to one condition: <b>a reel is held iff exactly 2 scatters have
    /// landed in the reels before it.</b> Once a 3rd lands the running count passes 2 and the holds
    /// stop by themselves; while only 1 has landed it never starts. That single test also produces
    /// every "no anticipation" case without special-casing any of them:
    /// <list type="bullet">
    /// <item>3 scatters on reel 1 — the count is already 3 by reel 2, so nothing is held.</item>
    /// <item>2nd scatter on the last reel — no reels follow it to hold.</item>
    /// <item>All 3 on the last reel — the count is 0 everywhere before it.</item>
    /// </list>
    /// The backend allows at most one Lamp per reel, and only on reels 3–5, so "two have landed"
    /// always means the next reel is the one that can complete the trigger.
    /// </summary>
    private void ComputeAnticipatedReels(List<List<int>> resultMatrix, HashSet<int> results)
    {
        if (resultMatrix == null || ReelCount < 2) return;

        int scattersBefore = 0;
        for (int col = 0; col < ReelCount; col++)
        {
            if (scattersBefore == 2) results.Add(col);
            scattersBefore += CountScattersInColumn(resultMatrix, col);
        }
    }

    // Counts scatters in one reel column, bounded to the rows the grid actually shows.
    private int CountScattersInColumn(List<List<int>> matrix, int col)
    {
        if (matrix == null || col < 0 || col >= matrix.Count || matrix[col] == null) return 0;

        int bonusId = gameManager?.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : -1;
        if (bonusId < 0) return 0;

        int rowEnd = Mathf.Min(RowCount, matrix[col].Count);

        int count = 0;
        for (int row = 0; row < rowEnd; row++)
        {
            if (matrix[col][row] == bonusId) count++;
        }
        return count;
    }

    private float GetAnticipationHold()
    {
        bool isTurbo = gameManager != null && gameManager.currentSpinSpeed == SpinSpeed.Turbo;
        return isTurbo ? anticipationExtraTimeTurbo : anticipationExtraTime;
    }

    // effects[0] belongs to reel index 1, effects[1] to reel index 2, and so on — reel 0 can never
    // anticipate, since two scatters have to have landed before it.
    private void SetAnticipationEffect(int reelIndex, bool visible)
    {
        int effectIndex = reelIndex - 1;
        if (anticipationEffects == null || effectIndex < 0 || effectIndex >= anticipationEffects.Length) return;

        GameObject effect = anticipationEffects[effectIndex];
        if (effect != null) effect.SetActive(visible);
    }

    private IEnumerator StopSingleReel(int columnIndex, List<int> targetSymbols, float delay, bool isQuickStop)
    {
        if (delay > 0)
        {
            yield return new WaitForSeconds(delay);
        }

        if (columnIndex < spinTweens.Count && spinTweens[columnIndex] != null)
        {
            spinTweens[columnIndex].Kill();
        }

        Transform slotTransform = reelTransforms[columnIndex];

        SetReelSymbols(columnIndex, targetSymbols, false);

        // Snap to a fixed pre-land reference point so the overshoot/settle distance below is
        // consistent regardless of where in its continuous loop the reel was stopped.
        slotTransform.localPosition = new Vector3(
            slotTransform.localPosition.x,
            middlePosition + symbolHeight,
            0
        );

        // ── Play reel-stop sound immediately when symbols lock in ──────────
        AudioManager.Instance?.PlayReelStop();

        // Special-symbol landing cues for this column. A cue fires at most once per REEL, not once
        // per symbol — three Scatters in one column is one cue, not three. A quick stop lands every
        // reel on the same frame, so up to five can overlap there; that is accepted.
        //
        // The Wild deliberately has no landing cue in this game. It is announced when it ANIMATES,
        // which only happens if it is part of a win.
        if (currentDisplayMatrix != null && columnIndex < currentDisplayMatrix.Count)
        {
            bool hasScatter = false;

            // No literal fallback: -1 before init can never match a real symbol id.
            int scatterId = gameManager != null && gameManager.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : -1;

            var column = currentDisplayMatrix[columnIndex];
            int rowEnd = Mathf.Min(RowCount, column.Count);

            for (int r = 0; r < rowEnd; r++)
            {
                if (column[r] == scatterId) hasScatter = true;

                if (hasScatter) break;
            }

            if (hasScatter) AudioManager.Instance?.PlayScatterLand();
        }
        // ──────────────────────────────────────────────────────────────────

        // If the next reel is being held, its effect comes in on this reel's landing slam. Driven
        // off the actual event rather than a computed timestamp so it can't drift out of sync with
        // the staggers or the holds — which matters more now that several reels can be held and
        // the delays accumulate.
        if (anticipatedReels.Contains(columnIndex + 1))
        {
            SetAnticipationEffect(columnIndex + 1, true);

            // Starts with the first held reel and runs across any that follow it.
            AudioManager.Instance?.PlayAnticipation();
        }

        if (isQuickStop)
        {
            Sequence quickStopSequence = DOTween.Sequence();

            quickStopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition - quickStopOvershoot, quickStopDuration * 0.3f)
                    .SetEase(Ease.OutQuad)
            );

            quickStopSequence.Append(
                slotTransform.DOLocalMoveY(middlePosition, quickStopDuration * 0.7f)
                    .SetEase(Ease.InOutQuad)
            );

            spinTweens[columnIndex] = quickStopSequence;
        }
        else
        {
            // Single tween — the ease's own overshoot-and-settle does the bounce.
            Tween stopTween = slotTransform.DOLocalMoveY(middlePosition, stopDuration)
                .SetEase(stopEase, stopEaseOvershoot)
                .OnComplete(() =>
                {
                    // This reel was being teased and has now landed — clear its effect whether or
                    // not the scatter actually turned up. Other reels keep their own holds.
                    if (anticipatedReels.Remove(columnIndex))
                    {
                        SetAnticipationEffect(columnIndex, false);

                        // The last held reel has landed: the tension is over.
                        if (anticipatedReels.Count == 0) AudioManager.Instance?.StopAnticipation();
                    }
                });

            spinTweens[columnIndex] = stopTween;
        }
    }

    #endregion

    #region Quick Spin

    internal void QuickStop(List<List<int>> resultMatrix, System.Action onComplete = null)
    {
        if (!isSpinning)
        {
            currentDisplayMatrix = resultMatrix;
            for (int col = 0; col < ReelCount; col++)
            {
                if (col < reelTransforms.Length)
                {
                    SetReelSymbols(col, resultMatrix[col], false);
                    reelTransforms[col].localPosition = new Vector3(
                        reelTransforms[col].localPosition.x,
                        middlePosition,
                        0
                    );
                }
            }

            onComplete?.Invoke();
            return;
        }

        StartCoroutine(StopSpinSequence(resultMatrix, onComplete, true));
    }

    #endregion

    #region Stop Symbol Animations

    // loopCount <= 0 means "animate indefinitely", until something calls KillWinTweens — normally the
    // next StartSpin. Used by the free-spins retrigger, which passes a count.
    internal void AnimateAllScatters(int loopCount)
    {
        AnimateScatters(loopCount, playOnce: false);
    }

    // playOnce runs each clip once to its end and holds the last frame (see AnimateSymbolSingleLoop).
    // Gold of Luck's Genie Wheel used it for its trigger; nothing does now, but a feature trigger that
    // should play its scatters' clip exactly once can.
    private void AnimateScatters(int loopCount, bool playOnce)
    {
        if (currentDisplayMatrix == null) return;

        // Clear any individual hit animations before starting the collective one
        KillWinTweens();

        int actualScatterId = gameManager?.gameConfig != null ? gameManager.gameConfig.scatterSymbolId : -1;
        if (actualScatterId < 0) return;

        int rowCount = RowCount;

        for (int col = 0; col < ReelCount; col++)
        {
            if (col >= currentDisplayMatrix.Count) continue;
            for (int localRow = 0; localRow < rowCount; localRow++)
            {
                if (localRow >= currentDisplayMatrix[col].Count) continue;

                if (currentDisplayMatrix[col][localRow] == actualScatterId)
                {
                    AnimateSymbolSingleLoop(col, localRow, loopCount, playOnce);
                }
            }
        }
    }

    // Plays one symbol's clip on the animation layer, the same surface AnimateWinPositions uses, so
    // it draws above the win dim rather than on the reel beneath it.
    private void AnimateSymbolSingleLoop(int column, int row, int loopCount = 1, bool playOnce = false)
    {
        if (currentDisplayMatrix == null) return;
        if (column < 0 || column >= ReelCount || row < 0 || row >= RowCount) return;
        if (column >= currentDisplayMatrix.Count || row >= currentDisplayMatrix[column].Count) return;

        if (animSlotColumns == null || column >= animSlotColumns.Count) return;
        var slotColumn = animSlotColumns[column];
        if (slotColumn == null || slotColumn.rows == null || row >= slotColumn.rows.Count) return;

        AnimSlot slot = slotColumn.rows[row];
        if (slot == null || slot.image == null) return;

        ImageAnimation imageAnim = slot.animation;
        if (imageAnim == null) return;

        int symbolId = currentDisplayMatrix[column][row];
        if (symbolId < 0 || symbolId >= animationSpriteArrays.Length) return;

        List<Sprite> animSprites = animationSpriteArrays[symbolId];
        if (animSprites == null || animSprites.Count == 0) return;

        Image symbolImage = slot.image;

        // Draw the symbol on the layer and take the reel icon out from under it — otherwise the
        // resting icon shows through as a ghost, and an oversized neighbour can poke into the cell.
        // HideWinSlots puts every icon back when the layer comes down.
        symbolImage.DOKill();
        ApplySymbol(symbolImage, symbolId);
        symbolImage.transform.localScale = Vector3.one;
        Color c = symbolImage.color;
        symbolImage.color = new Color(c.r, c.g, c.b, 1f);
        symbolImage.gameObject.SetActive(true);
        SetDisplayIconActive(column, row, false);

        // The dim goes up with the layer. The animation layer is meant to be read against a
        // darkened board — that pairing is what makes a symbol pop — and AnimateAllScatters lowers
        // the dim on its way in via KillWinTweens, so raising it here is what puts it back.
        //
        // It stays up until KillWinTweens takes it down — the next StartSpin after a free-spins
        // retrigger.
        if (winAnimationLayer != null) winAnimationLayer.SetActive(true);
        if (winDimOverlay != null) winDimOverlay.SetActive(true);

        imageAnim.textureArray = animSprites;
        // Play-once runs the clip to its end and leaves the last frame up: with looping off,
        // ImageAnimation stops itself there, and a later StopAnimation does not rewind a clip that
        // has already finished.
        imageAnim.doLoopAnimation = !playOnce;
        imageAnim.onLoopComplete = null;
        // Only read inside StartAnimation, and these slots are reused every spin, so an unwritten
        // speed is whichever symbol used this slot last.
        imageAnim.AnimationSpeed = GetSymbolAnimationSpeed(symbolId);

        Sequence seq = DOTween.Sequence();

        seq.AppendCallback(() => imageAnim.StartAnimation());

        // A timed stop only for a counted run (the retrigger's scatterTriggerLoops). loopCount <= 0
        // runs until whatever kills winTweens ends it, and play-once ends on its own last frame.
        if (!playOnce && loopCount > 0)
        {
            seq.AppendInterval(winSymbolLoopDuration * loopCount);

            seq.AppendCallback(() => {
                if (imageAnim != null) imageAnim.StopAnimation(); // reverts to textureArray[0], which equals the resting sprite
            });
        }

        winTweens.Add(seq);
    }

    #endregion

    #region Win Line Animation

    internal void ShowWinLineAnimation(List<WinLine> winLines, System.Action onComplete)
    {
        if (winLines == null || winLines.Count == 0)
        {
            lastWinLines = null;
            // No win presentation is coming to inherit the dim, so anything that raised it earlier
            // in the spin has to let it go here — otherwise the board stays dark until the next spin.
            ReleaseHeldDim();
            onComplete?.Invoke();
            return;
        }

        lastWinLines = winLines;

        KillWinTweens();
        winAnimationCoroutine = StartCoroutine(PlayTwoPhaseWinLines(winLines, onComplete));
    }

    /// <summary>
    /// Starts the Phase 2 line-by-line cycle for the spin that just landed. Autoplay and free spins
    /// skip Phase 2 while they're running — a round ends with the presentation parked after Phase 1
    /// — so the controller calls this once the round is genuinely over. Loops until the next
    /// StartSpin kills it, same as an ordinary manual spin.
    ///
    /// If that spin's presentation is still running, the request is left for it rather than acted on
    /// here: it walks the lines itself once its total ends.
    /// </summary>
    internal void PlayWinLineCycle()
    {
        if (lastWinLines == null || lastWinLines.Count == 0) return;

        // Still presenting — the player stopped autoplay while the total was on screen. Starting a
        // second coroutine would double up and strobe the lines, and simply returning would drop the
        // request, because that presentation decided before its total to skip the walk. So it is
        // recorded instead, and PlayTwoPhaseWinLines honours it when the total ends.
        if (winAnimationCoroutine != null)
        {
            lineWalkRequested = true;
            return;
        }

        KillWinTweens();

        SummariseWinLines(lastWinLines, out HashSet<int> allWinPositions, out double totalWinAmount);
        winAnimationCoroutine = StartCoroutine(PlayWinLineCycleRoutine(lastWinLines, allWinPositions, totalWinAmount));
    }

    private IEnumerator PlayTwoPhaseWinLines(List<WinLine> winLines, System.Action onComplete)
    {
        // A request left for a previous presentation must not carry into this one.
        lineWalkRequested = false;

        int rowLimit = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        // ==========================================
        // PHASE 1: Show all winning icons at once
        // ==========================================
        HashSet<int> allWinPositions = new HashSet<int>();
        foreach (var winLine in winLines)
        {
            if (winLine.positions != null)
            {
                foreach (int flatIndex in winLine.positions)
                {
                    allWinPositions.Add(flatIndex);
                }
            }
        }

        Debug.Log($"[PlayTwoPhaseWinLines] Phase 1: Showing all {allWinPositions.Count} winning icons at once for {winLines.Count} win lines");

        // Calculate Phase 1 Total Win Amount
        double totalWinAmount = 0;
        foreach (var winLine in winLines)
        {
            totalWinAmount += winLine.winAmount;
        }
        if (totalWinAmount <= 0 && gameManager != null && gameManager.lastResult != null)
        {
            totalWinAmount = gameManager.lastResult.winAmount;
        }

        // Decided BEFORE the total plays, because it picks the total's round count. It also skips the
        // line walk afterwards — unless the controller asks for the walk while the total is still
        // playing, which is checked once the total ends (see lineWalkRequested).
        bool skipPhase2 = gameManager != null
            && (gameManager.isInFreeSpins || gameManager.isAutoPlaying);

        // SpinWinText switched off (owner): a win shows its symbols animating, with no total on the
        // board. The win box still shows the amount.
        // ShowPhase1TotalWin(totalWinAmount);

        AudioManager.Instance?.PlayWinPresentationStart();

        // The total: every winning symbol, twice, in step. Autoplay and Free Games get a single
        // round and end here — a full sequence on every spin would make a round crawl.
        //
        // Yielded directly, NOT through StartCoroutine. A nested StartCoroutine is its own coroutine:
        // stopping this one (the next spin's KillWinTweens) left it running, waiting forever for
        // loop-complete callbacks that teardown had already cleared — one stuck coroutine per
        // interrupted win. Run inline, it stops with this one.
        yield return AnimateWinPositions(
            allWinPositions, rounds: skipPhase2 ? 1 : totalWinRounds, announceWilds: true);

        KillWinTweens(false);
        HidePhase1TotalWinText();

        // Invoke onComplete immediately after the total so game logic (Free Spins / Autoplay / Win
        // complete) can proceed while the win lines are still being walked.
        onComplete?.Invoke();

        // The round count above had to be fixed before the total played; the walk did not. If
        // autoplay was stopped while the total was on screen, the controller has asked for the walk
        // by now, and this spin gets it the same way a manual spin would.
        if (skipPhase2 && !lineWalkRequested)
        {
            // Take the presentation down on the way out: after the last autoplay spin, or at the end
            // of a free-games round, there is no next spin to clear it. The controller restarts the
            // cycle via PlayWinLineCycle when the round is genuinely over.
            winAnimationCoroutine = null;

            // Presentation is genuinely over here, so any dim still being held is released before
            // the teardown rather than surviving it.
            ReleaseHeldDim();

            KillWinTweens();
            yield break;
        }

        yield return PlayWinLineCycleRoutine(winLines, allWinPositions, totalWinAmount);
    }

    // ==========================================
    // PHASE 2: Individual Win Line presentation loop
    // ==========================================
    // Split out of PlayTwoPhaseWinLines so the controller can start it on its own once an autoplay
    // or free-games round ends. Loops until something kills the coroutine — normally the next
    // StartSpin.
    private IEnumerator PlayWinLineCycleRoutine(List<WinLine> winLines, HashSet<int> allWinPositions, double totalWinAmount)
    {
        // The per-line walk is commented out (owner): Gold of Luck pays ways, not paylines, and a win
        // just shows every winning symbol animating together. What is left of this routine is the
        // closing hold below. Kept rather than deleted while the ways win presentation is designed.
        //
        // // Once through every line, each line playing its symbols once in step. Bounded, unlike the
        // // old cycle: what loops at the end is the TOTAL, not the lines. Kept as a loop rather than a
        // // straight pass so the count stays a single constant to change.
        // for (int pass = 0; pass < winLinePassCount; pass++)
        // {
        //     foreach (var winLine in winLines)
        //     {
        //         if (winLine.positions == null || winLine.positions.Count == 0) continue;
        //
        //         KillWinTweens(false);
        //
        //         // Lines are a Phase 2 thing only — the total shows every winning symbol at once with
        //         // no line drawn, then this walks them one at a time.
        //         AudioManager.Instance?.PlayWinLineChange();
        //         ShowWinLine(winLine);
        //
        //         yield return AnimateWinPositions(winLine.positions, rounds: winLineRounds);
        //     }
        // }

        // Every winning symbol, looping, and hold. rounds: 0 starts everything looping and returns at
        // once, so this coroutine ends here with the board still animating — the next spin's teardown
        // is what stops it. Nulling the handle lets PlayWinLineCycle start a fresh sequence if a round
        // ends.
        KillWinTweens(false);
        HideAllWinLines();
        // ShowPhase1TotalWin(totalWinAmount);   // SpinWinText switched off (owner)

        // Inline for the same reason as the total above.
        yield return AnimateWinPositions(allWinPositions, rounds: 0);

        winAnimationCoroutine = null;
    }

    // The total and the set of winning cells, rebuilt from the lines. Used by PlayWinLineCycle,
    // which restarts the presentation for a spin whose own run finished long ago.
    private static void SummariseWinLines(List<WinLine> winLines, out HashSet<int> allWinPositions, out double totalWinAmount)
    {
        allWinPositions = new HashSet<int>();
        totalWinAmount = 0;

        if (winLines == null) return;

        foreach (var winLine in winLines)
        {
            totalWinAmount += winLine.winAmount;

            if (winLine.positions == null) continue;
            foreach (int flatIndex in winLine.positions) allWinPositions.Add(flatIndex);
        }
    }

    /// <param name="announceWilds">
    /// True only from Phase 1. The Wild cue is once per spin, and Phase 2 cycles its lines forever
    /// until the player spins again — so firing it there would replay the cue on every pass, for as
    /// long as the player sat looking at the result.
    /// </param>
    /// <param name="rounds">
    /// How many times every symbol here plays, IN STEP: they all start together, and the group waits
    /// for the slowest before going again. Left to their own loop counts they drift apart at once,
    /// since a symbol's loop length is its frame count over its speed and no two match.
    /// Zero or less means the closing hold — start them looping and do not wait at all.
    /// </param>
    private IEnumerator AnimateWinPositions(IEnumerable<int> flatPositions, int rounds = 1, bool announceWilds = false)
    {
        if (flatPositions == null) yield break;

        bool wildAnnounced = false;

        int rowLimit = (gameManager != null && gameManager.gameConfig != null) ? gameManager.gameConfig.rowCount : 3;

        List<ImageAnimation> activeAnims = new List<ImageAnimation>();

        bool anyShown = false;

        foreach (int flatIndex in flatPositions)
        {
            int row = flatIndex / ReelCount;
            int col = flatIndex % ReelCount;

            if (col < 0 || col >= ReelCount || row < 0 || row >= rowLimit) continue;

            // Image lookup goes to the animation layer, which holds one slot per visible cell.
            if (animSlotColumns == null || col >= animSlotColumns.Count) continue;
            var column = animSlotColumns[col];
            if (column == null || column.rows == null || row >= column.rows.Count) continue;

            AnimSlot slot = column.rows[row];
            if (slot == null || slot.image == null) continue;

            Image slotImage = slot.image;

            int matrixRow = row;
            if (col >= currentDisplayMatrix.Count || matrixRow >= currentDisplayMatrix[col].Count) continue;
            int symbolId = currentDisplayMatrix[col][matrixRow];

            // A guard: a scatter never takes part in a line win, so if the server ever lists one
            // among a win's positions it is left dimmed. Scatters get their own presentation when
            // they trigger a feature. (Still checks only scatterSymbolId — ToDo.md.)
            int winBonusId = (gameManager != null && gameManager.gameConfig != null)
                ? gameManager.gameConfig.scatterSymbolId
                : -1;
            if (symbolId == winBonusId) continue;

            // Once per spin, however many Wilds are winning.
            if (announceWilds && !wildAnnounced && symbolId == WildSymbolId)
            {
                wildAnnounced = true;
                AudioManager.Instance?.PlayWildAnimate();
            }

            // Show the symbol first, unconditionally. Some symbols have no animation frames at all
            // (their anim list is left empty), and under the dim a skipped slot would leave a
            // winning symbol sitting dark while its neighbours light up.
            slotImage.DOKill();

            ApplySymbol(slotImage, symbolId);

            slotImage.transform.localScale = Vector3.one;
            Color c = slotImage.color;
            slotImage.color = new Color(c.r, c.g, c.b, 1f);
            slotImage.gameObject.SetActive(true);
            anyShown = true;

            // Take the reel icon underneath out of the picture entirely. It sits below the dim but
            // is still faintly visible through it, and an oversized neighbour can poke into this
            // cell — either way it reads as a ghost behind the bright copy. Hidden per-cell rather
            // than blanket-hiding the display block, so the scatter (skipped above) correctly stays
            // on screen and dimmed. HideWinSlots puts every icon back.
            SetDisplayIconActive(col, row, false);

            // Animate on top of that only if this symbol actually has frames.
            if (symbolId < 0 || symbolId >= animationSpriteArrays.Length) continue;

            List<Sprite> animSprites = animationSpriteArrays[symbolId];

            if (animSprites == null || animSprites.Count == 0) continue;

            ImageAnimation imageAnim = slot.animation;
            if (imageAnim == null) continue;

            imageAnim.textureArray = animSprites;
            imageAnim.doLoopAnimation = true;
            // Must be set before StartAnimation() below — that is the only place ImageAnimation
            // reads it, so a later change would not take effect until the next start.
            imageAnim.AnimationSpeed = GetSymbolAnimationSpeed(symbolId);

            activeAnims.Add(imageAnim);
        }

        // Only raise the dim once something is actually on the layer — otherwise an empty or
        // fully-invalid position set would darken the reels with nothing shown on top.
        if (anyShown)
        {
            if (winDimOverlay != null) winDimOverlay.SetActive(true);
            if (winAnimationLayer != null) winAnimationLayer.SetActive(true);
        }

        if (winLineBoxToAnimationDelay > 0)
        {
            yield return new WaitForSeconds(winLineBoxToAnimationDelay);
        }

        // Nothing of our own to animate. Still hold the beat so the sequence keeps its pacing.
        if (activeAnims.Count == 0)
        {
            if (rounds > 0) yield return new WaitForSeconds(winSymbolLoopDuration);
            yield break;
        }

        // The closing hold: set everything looping and leave it. Nothing waits on this — the next
        // spin's teardown is what ends it.
        if (rounds <= 0)
        {
            foreach (var anim in activeAnims)
            {
                anim.doLoopAnimation = true;
                anim.onLoopComplete = null;
                anim.StartAnimation();
            }

            yield break;
        }

        for (int round = 0; round < rounds; round++)
        {
            int finished = 0;

            foreach (var anim in activeAnims)
            {
                ImageAnimation a = anim;

                // One pass each; the round below is what restarts them, together.
                a.doLoopAnimation = false;
                a.onLoopComplete = (loop) =>
                {
                    a.onLoopComplete = null;

                    // Reverts to frame 0, so a symbol that finishes early rests on its resting sprite
                    // instead of freezing on its last frame while it waits for the others.
                    a.StopAnimation();
                    finished++;
                };

                a.StartAnimation();
            }

            yield return new WaitUntil(() => finished >= activeAnims.Count);
        }
    }

    private void ShowPhase1TotalWin(double totalWinAmount)
    {
        if (phase1TotalWinText != null)
        {
            // Sprite digits, drawn from the text's sprite asset — the same one the per-line amounts
            // use, so the total and the lines always match each other.
            phase1TotalWinText.text = SpriteTextFormatter.ToSpriteMoney(totalWinAmount);
            AnimateTextScaleAppear(phase1TotalWinText.transform);
        }
    }

    private void HidePhase1TotalWinText()
    {
        if (phase1TotalWinText != null)
        {
            phase1TotalWinText.transform.DOKill();
            phase1TotalWinText.transform.localScale = Vector3.one;
            phase1TotalWinText.gameObject.SetActive(false);
        }
    }

    private void AnimateTextScaleAppear(Transform textTransform, float popScale = 1.2f, float durationUp = 0.15f, float durationDown = 0.10f)
    {
        if (textTransform == null) return;
        textTransform.DOKill();
        textTransform.localScale = Vector3.zero;
        textTransform.gameObject.SetActive(true);

        Sequence seq = DOTween.Sequence();
        seq.Append(textTransform.DOScale(popScale, durationUp).SetEase(Ease.OutQuad));
        seq.Append(textTransform.DOScale(1.0f, durationDown).SetEase(Ease.InQuad));
        winTweens.Add(seq);
    }

    private void KillWinTweens(bool stopCoroutine = true)
    {
        foreach (var tween in winTweens)
        {
            tween?.Kill();
        }
        winTweens.Clear();

        if (stopCoroutine && winAnimationCoroutine != null)
        {
            StopCoroutine(winAnimationCoroutine);
            winAnimationCoroutine = null;
        }

        HidePhase1TotalWinText();

        // Stop all in-flight win animations and restore alpha for every icon — covers both the
        // buffer (images) and the display block (displayImages). ImageAnimation lives directly on
        // each display icon's SlotIcon root (sharing that same Image), so GetComponent finds it
        // there; buffer icons simply have none and are skipped.
        void RestoreImageList(List<Image> imageList)
        {
            if (imageList == null) return;
            foreach (var image in imageList)
            {
                if (image != null)
                {
                    image.DOKill();
                    image.transform.localScale = Vector3.one;
                    Color c = image.color;
                    image.color = new Color(c.r, c.g, c.b, 1f);

                    ImageAnimation imageAnim = image.GetComponent<ImageAnimation>();
                    if (imageAnim != null)
                    {
                        imageAnim.onLoopComplete = null;
                        imageAnim.StopAnimation();
                    }
                }
            }
        }

        foreach (var reel in reelImagesList)
        {
            RestoreImageList(reel.images);
            RestoreImageList(reel.displayImages);
        }

        // The win-layer slots are cleared on *every* call, including the between-cycle reset in
        // Phase 2 — each cycle shows one win line, so the previous line's symbols have to go
        // before the next line's appear. Its Image and ImageAnimation are separate explicit
        // references, so this can't reuse RestoreImageList's GetComponent-based pass.

        if (animSlotColumns != null)
        {
            foreach (var column in animSlotColumns)
            {
                if (column == null || column.rows == null) continue;
                foreach (var slot in column.rows)
                {
                    if (slot == null) continue;

                    if (slot.image != null)
                    {
                        slot.image.DOKill();
                        slot.image.transform.localScale = Vector3.one;
                        Color c = slot.image.color;
                        slot.image.color = new Color(c.r, c.g, c.b, 1f);
                    }

                    if (slot.animation != null)
                    {
                        slot.animation.onLoopComplete = null;
                        slot.animation.StopAnimation();
                    }
                }
            }
        }
        HideWinSlots();

        // Lines clear on every call too — Phase 2 shows one at a time, so the previous line has
        // to go before the next is raised.
        HideAllWinLines();

        // The dim itself only comes down on a full teardown. Hiding it on the between-cycle reset
        // would make it strobe once per win line.
        if (stopCoroutine) HideWinDim();
    }

    // Shows or hides the reel icon sitting behind one win-layer slot. Row indices need no
    // translation — displayImages holds one entry per visible row, in the same order as the
    // server matrix and the animation layer.
    private void SetDisplayIconActive(int col, int row, bool active)
    {
        if (reelImagesList == null || col < 0 || col >= reelImagesList.Count) return;

        var reel = reelImagesList[col];
        if (reel == null || reel.displayImages == null) return;
        if (row < 0 || row >= reel.displayImages.Count) return;

        Image icon = reel.displayImages[row];
        if (icon != null) icon.gameObject.SetActive(active);
    }

    // Takes the whole win layer down and restores every reel icon underneath it. The restore is
    // deliberately unconditional and paired with the hide in this one method: AnimateWinPositions
    // hides icons per winning cell, and if any of them were missed here that cell would stay blank
    // for the rest of the session. Every teardown path runs through here — between the total and
    // the closing hold, on the next StartSpin, and on Start.
    private void HideWinSlots()
    {
        if (animSlotColumns != null)
        {
            foreach (var column in animSlotColumns)
            {
                if (column == null || column.rows == null) continue;
                foreach (var slot in column.rows)
                {
                    if (slot == null || slot.image == null) continue;

                    slot.image.gameObject.SetActive(false);
                }
            }
        }

        if (reelImagesList == null) return;
        for (int col = 0; col < reelImagesList.Count; col++)
        {
            var reel = reelImagesList[col];
            if (reel == null || reel.displayImages == null) continue;

            for (int row = 0; row < reel.displayImages.Count; row++)
            {
                if (reel.displayImages[row] != null) reel.displayImages[row].gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Shows one win's payout. There is no payline graphic in this game — a win is presented by
    /// animating its symbols, and this is the only thing drawn on top of them.
    ///
    /// The amount sits on the MIDDLE REEL, at the first row this win occupies there, which is why
    /// three labels cover every win. Read from the win's own positions, so it follows what is
    /// actually animating.
    ///
    /// A win that never reaches the middle reel falls back to the middle row.
    /// </summary>
    private void ShowWinLine(WinLine winLine)
    {
        if (winLineAmounts == null || winLine == null) return;

        int middleReel = ReelCount / 2;
        int row = RowCount / 2;

        if (winLine.positions != null)
        {
            foreach (int flatIndex in winLine.positions)
            {
                if (flatIndex % ReelCount != middleReel) continue;

                row = flatIndex / ReelCount;
                break;
            }
        }

        if (row < 0 || row >= winLineAmounts.Length)
        {
            Debug.LogWarning($"[SlotView] Win line row {row} is outside winLineAmounts ({winLineAmounts.Length} entries) — no amount shown.");
            return;
        }

        TMPro.TMP_Text label = winLineAmounts[row];
        if (label == null)
        {
            Debug.LogWarning($"[SlotView] No win amount label assigned for row {row} — the line will show without its payout.");
            return;
        }

        // Sprite digits, matching the total win. ToSpriteMoney goes through MoneyFormat, so the two
        // can never disagree about how an amount is written.
        label.text = SpriteTextFormatter.ToSpriteMoney(winLine.winAmount);
        label.gameObject.SetActive(true);
    }

    // All three, not just the one showing: the cycle moves between rows, and only the row that is
    // about to be shown gets switched on again.
    private void HideAllWinLines()
    {
        if (winLineAmounts == null) return;

        foreach (var label in winLineAmounts)
        {
            if (label != null) label.gameObject.SetActive(false);
        }
    }

    // The win layer always comes down; the dim itself is skipped while something is holding it up.
    //
    // Both holds below are inert today (nothing sets either flag). Anything that raises the dim before
    // the win presentation and means the presentation to inherit it needs one: without it,
    // ShowWinLineAnimation's opening KillWinTweens drops the dim a frame before it is raised again,
    // which reads as a flicker.
    private void HideWinDim()
    {
        if (winAnimationLayer != null) winAnimationLayer.SetActive(false);

        // A feature round's hold is the same kind of guard: a round owning the dim for its whole
        // duration means a win teardown inside the round cannot take it down.
        if (dimHeld || featureDimHeld) return;
        if (winDimOverlay != null) winDimOverlay.SetActive(false);
    }

    // Releases the per-spin claim on the dim and takes it down. Called at the end of the whole
    // presentation, so a spin that raised the dim and then produced no win still clears correctly.
    private void ReleaseHeldDim()
    {
        dimHeld = false;

        // Releasing the per-spin claim does not release a feature round's.
        if (featureDimHeld) return;
        if (winDimOverlay != null) winDimOverlay.SetActive(false);
    }

    #endregion


    private void KillAllTweens()
    {
        foreach (var tween in spinTweens)
        {
            tween?.Kill();
        }
        spinTweens.Clear();

        KillWinTweens();
    }

    #region Cleanup

    private void OnDestroy()
    {
        KillAllTweens();
    }

    #endregion
}

// One win-animation slot: the Image that shows the symbol and the ImageAnimation that plays it.
// Both are wired explicitly rather than found with GetComponent — a missing component would
// otherwise just silently no-op, and these icons must have one while the reel icons must not.
// Kept in a single struct so the two can never drift out of step with each other.
[System.Serializable]
public class AnimSlot
{
    public Image image;
    public ImageAnimation animation;
}

// One reel column's worth of win-animation slots. These live on a layer above the dim overlay,
// so a winning symbol can be shown bright while the real reel icon stays dimmed underneath.
// One slot per visible cell — 3 rows, matching the grid exactly.
[System.Serializable]
public class AnimSlotColumn
{
    public List<AnimSlot> rows = new List<AnimSlot>(3);   // index 0 = top active row
}

[System.Serializable]
public class ReelImages
{
    // Pure scroll buffer — everything except the real display-block icons below.
    public List<Image> images = new List<Image>(16);
    // Direct references to the real display-block icons, top row first — one per visible row, so
    // a row index here means the same thing it does in the server matrix. Wired manually per reel
    // in the Inspector, not derived from bufferRowsAbove, so each reel's buffer icon count can
    // differ without breaking which icons show the real backend result.
    public List<Image> displayImages = new List<Image>(3);
}