# Wild Jokers — how this project works

Context document for anyone, human or agent, picking this project up cold. It describes the game,
the backend contract, the architecture, the round lifecycle, the conventions, and the traps that are
expensive to rediscover.

**Read `ToDo.md` first.** This document describes how the project works; `ToDo.md` says what is open,
blocked or decided. If the two disagree, trust `ToDo.md`.

**Wild Jokers** (game id `SL-WJ`) is a 5-reel x 3-row, **20-payline** slot. The **Wild** (id 0)
substitutes for paying symbols and pays nothing itself. Three scatters each drive a feature, and each
feature has a **Mystery Respin** on a near miss:
- **Free Spins** — a Drum on each of reels 2, 3 and 4 awards 10 free spins (`BonusFreeSpins.md`).
- **Sandbell Pick** — 3+ Sandbells; the player picks one and wins its multiplier
  (`SandbellPickBonus.md`).
- **Ball Gale Pick** — 3+ Balls; the reels slide away for a 20-gem pick bonus
  (`BallGalePickBonus.md`).

It is a **conversion of a conversion of a conversion**: *Sizzling 7s* (3x3, 27 lines) became *Golden
Dynasty* (5x3, 50 paylines, Free Games + Hold & Spin), which became *Gold of Luck* (243 ways, Genie
Wheel), which became this. Hold & Spin, Orb, Mystery and the Genie Wheel have been removed
completely. Golden Dynasty's line-by-line win walk is back, reworked for Wild Jokers' 20 paylines
(§5), and names from all three older games survive in places — see §11.

**Current state, in one paragraph:** the backend is bound and verified against live captures — the
init, line wins, the near-miss respin, both pick bonuses' results, and Free Spins' trigger and round
(captured 2026-11-02; only a round's last spin is missing). The base game spins, lands, pays and
presents wins: every winning symbol static above a dim, with an animated win box over each (§5).
**All three features are built in code and wired** (§6): Free Spins (2026-11-02), Sandbell Pick
and Ball Gale Pick (2026-11-03) — the Sandbell Pick works in the editor; Free Spins awaits a full
retest and Ball Gale Pick its first test. A Mystery Respin waits for the player's Spin press. Wild
Jokers' own sounds are in (§8). The big-win popup is switched off for now.

---

## 1. Stack and layout

| | |
|---|---|
| Unity | 6000.3.24f1 |
| Pipeline | URP, **Renderer2D** |
| Target | WebGL, **landscape only** (Wild Jokers — Gold of Luck was portrait only) |
| Scene | one — `Assets/Scenes/GameScene.unity` |
| Tweening | DOTween (no TextMeshPro module — see §10) |
| Transport | Socket.IO via Best.SocketIO, namespace `playground` |
| JSON | Newtonsoft inbound; `JsonUtility` for the spin request, Newtonsoft for the jackpot request |

The canvas is **Screen Space – Camera**, reference resolution **1920 x 1080** (landscape), on a
**perspective** camera with `planeDistance: 100`.

```
Assets/Scripts/
├── BackEnd/
│   ├── SocketIOManager.cs   transport edge: connect, auth, events, emit     (~790, PROTECTED)
│   └── JSFunctCalls.cs      browser bridge: postMessage, fullscreen, visibility
├── Config/
│   └── SL-WJ_config.json    the backend's own game config — reference only, never loaded
├── Core/
│   └── GameDataModels.cs    every wire model, every client model, InitDataConverter       (~820)
├── Managers/
│   ├── GameManager.cs       THE CONTROLLER: round state, the spin lifecycle              (~850)
│   ├── SlotView.cs          the board: reels, symbols, layers, win presentation          (~1810)
│   ├── UIManager.cs         buttons, panels, popups, autoplay, the win box, jackpots     (~1580)
│   ├── AudioManager.cs      every cue, one method per cue                                 (~435)
│   └── PopupManager.cs      connection / error / exit popups                (~720, PROTECTED)
├── Feature/
│   ├── FeaturePopupView.cs  the shared feature popup: MYSTERY RESPIN / CONGRATULATIONS / TOTAL WIN
│   ├── FreeGameView.cs      Free Spins' black fade, "FREE GAME X OF Y" panel and logo crossfade
│   ├── SandbellPickView.cs  the Sandbell Pick's pick cells: PICK ME, the X values, the explosion
│   └── BallGalePickView.cs  the Ball Gale Pick: the slide, the gems (glare, break, value), the total
├── Helper/
│   ├── ImageAnimation.cs    sprite-sequence player, single- and two-phase   (~400, PROTECTED)
│   ├── SpriteTextFormatter.cs   money / digit / X-value formatting for sprite fonts
│   ├── SymbolInfoCard.cs, SymbolButtonHandler.cs   tap-a-symbol paytable card
│   ├── SpinButtonHoldHandler.cs   hold Spin to open autoplay
│   └── OrientationChange.cs fit + -90° turn on a tall screen (UIWrapper = BG)
└── MD/                      these documents (gitignored)
```

**Build check without opening Unity.** `dotnet build` does **not** work on the owner's machine — there
is no .NET SDK installed. Instead, compile with the Roslyn compiler bundled inside Unity
(`Unity.app/Contents/Resources/Scripting/DotNetSdkRoslyn/csc.dll`, run with the bundled
`NetCoreRuntime/dotnet`), feeding it a response file built by parsing `Assembly-CSharp.csproj` for its
sources, references and `DefineConstants`. Flags: `-target:library -nostdlib -langversion:9.0
-unsafe`. Three gotchas: use **absolute** source and `HintPath` paths; add each `ProjectReference`
as `Library/ScriptAssemblies/<name>.dll` (Best.SocketIO and friends — without them, CS0246 on
`Best`); and Unity only regenerates the `.csproj` after it recompiles, so a script created since
then must be added to the list by hand. Write the response file and the output DLL to a scratch
folder, never into the project. Do this after every code change.

---

## 2. Architecture — MVC, and it is not optional

| Layer | Files |
|---|---|
| **Model** | `Core/GameDataModels.cs` — wire DTOs, client `GameConfig`/`SpinResult`, and `InitDataConverter` |
| **View** | `SlotView`, `UIManager`, `FreeGameView`, `FeaturePopupView`, `SandbellPickView`, `BallGalePickView`, `PopupManager` |
| **Controller** | `GameManager` |
| **Transport edge** | `BackEnd/SocketIOManager.cs` |

Three rules hold the whole thing together:

1. **Views never call back into game logic.** They take callbacks and report when a sequence
   finishes. `SlotView` has no idea a free-spins round exists; `FreeGameView` never requests a spin.
2. **`GameManager` owns round state and drives the views.** `isInFreeSpins`, `freeSpinsRemaining`,
   `freeSpinsRoundWin` live there and only there.
3. **`SocketIOManager` deserializes and hands off.** It holds no game rules.

**The game is server-authoritative.** The client is a renderer. Never compute a win, a balance or a
symbol outcome locally — every figure on screen comes from the server, already resolved. The one
exception is the optimistic bet deduction at spin start, which the response's balance overwrites.

**The callback convention**, used wherever a sequence takes time:

```csharp
view.PlaySomething(data, onStageComplete, onComplete);   // view reports, caller decides what next
```

**The caller never waits on a duration it computed itself.** Reel stops, quick stops and count-ups all
report completion. A hand-tuned delay against an animation is the single most repeated source of
bugs in this codebase's history.

---

## 3. The backend contract

### Connection and auth

1. `SocketIOManager.Start` calls `RequestAuthToken()`. In WebGL it asks the page for a token via
   `JSManager.SendCustomMessage("authToken")`; in the editor it falls back to `testToken` and
   `testSocketURL` serialized on the component.
2. The page answers by calling `ReceiveAuthToken(json)` on the GameObject by name. Re-auth with
   identical values is ignored; different values tear the socket down and rebuild it.
3. `InitializeSocket()` builds a `SocketManager` with `options.Auth = { token }` and takes the
   namespace socket (`/playground`).

This whole layer is **platform transport**, not game code — it needed no conversion work beyond
`GenerateRandomMatrix` and the spin request.

### Inbound events

| Event | Payload | Handled by |
|---|---|---|
| `game:init` | `InitData` — bets, paylines, line count, symbol table, features, balance | `OnInitReceived` → `InitDataConverter` → `GameManager.OnInitDataReceived` |
| `result` | `ServerSpinResponse` — one spin | `OnResultReceived` → converter → `GameManager.OnSpinResultReceived` |
| `balance:sync` | `{ balance }` | writes `playerData.balance`, refreshes the display |
| `jackpot:sync` | `{ values }` | `UIManager.UpdateJackpotDisplay` — platform jackpot values |
| `AnotherDevice` | — | popup; the session was taken over elsewhere |
| `pong` | ping echo | latency display; missed pongs raise the reconnection popup |

### Outbound

| Emit | Payload |
|---|---|
| `request` | `{"type":"SPIN","payload":{"betIndex":N}}` — `JsonUtility.ToJson(SpinRequest)` |
| `request` | `{"type":"JACKPOT_OPEN","payload":{"tier":"grand"}}` — a jackpot button; fire-and-forget |
| `ping` | every few seconds; `MAX_MISSED_PONGS` consecutive misses raises the reconnection popup |

**There is no free-spin flag in the spin request.** The server owns round state; the client sends a
bet index and reads what comes back. (The mock client the captures come from also sends
`"spins": 100`; this client deliberately does not.)

**Jackpot open** asks the platform to show its own jackpot overlay for a tier. A 1-second debounce is
shared across all tiers, since only one overlay can be open. Whether this game's backend actually
handles `JACKPOT_OPEN` or sends `jackpot:sync` is unconfirmed — the init carries no jackpot block.

### `game:init` — shape and conversion (Wild Jokers)

Confirmed against live captures; the pick bonuses' maths fields as updated by the backend on
2026-10-30. The feature `symbolId`s and the `wild` block are **sent** (captured init, 2026-11-02), so
the per-feature ids resolve to 7 / 8 / 9. The init carries no free-spins round state.

```
gameData.lines            List<List<int>>  20 paylines, one row index per reel, row 0 at the top
gameData.bets             List<double>     16 levels, 0.01 … 5 — the bet PER LINE
gameData.totalLines       int              20 — total bet = bet x this
features.wild             { enabled, symbolId 0 }
features.freeSpins        { enabled, symbolId 7, spinsCount 10, triggerReels [1,2,3],
                            nearMissReels [1,2], respinReel 3 }
features.ballPickemBonus  { enabled, symbolId 8, triggerCount 3, respinReel 4,
                            nearMissReels [0,1,2,3], nearMissCountRange {min 2, max 4},
                            availablePrizes [1 … 25], maxWinMultiplier 500,
                            triggerProbability 1, picksCount 5 (ignored, owner) }
features.sandbellPickBonus  same shape, symbolId 9, availablePrizes [2 … 50], no picksCount
uiData.paylines.symbols[] { id, name, group, description, multiplier[] }
player.balance            double
```

`InitDataConverter.ConvertToGameConfig` produces `GameConfig`:

- **Special symbols come from `group`** (`"wild"`, `"scatter"`), not from names. Wild is id 0; the
  three scatters are Drum 7, Ball 8, Sandbell 9. Which feature each scatter belongs to comes from
  the features' `symbolId` → `freeSpinsSymbolId`, `ballBonusSymbolId`, `sandbellBonusSymbolId`.
  `scatterSymbolId` is a Gold of Luck single-scatter leftover (it resolves to the Sandbell); only the
  old anticipation and trigger code still reads it.
- **`multiplier` arrives ASCENDING and ends at a full-reel match** — with 5 reels, index 0 is
  2-of-a-kind. It is reversed into `multipliers` (descending from 5-of-a-kind) and the trailing
  zeros are cut, so the list stops at `minMatch` (2 for Jack-in-the-box, 3 for the rest, 0 for the
  Wild and scatters). Values are credits; one credit is the per-line bet.
- **`activeLine` = `totalLines` (20)**: the bet multiplier and the line-count label. Gold of Luck's
  `creditDivisor` is not sent.
- **`paylines`** = `gameData.lines` as sent; a malformed line is logged, never dropped, so a win's
  `lineIndex` still points at the right one.

### `result` — shape and conversion (Wild Jokers)

```
matrix                       List<List<string>>  ROW-major, 3 rows x 5 cols — the board AFTER any respin
payload.winAmount            double   the line wins' total
payload.grandTotalWin        double   THE figure to display
payload.lineWins[]           { lineIndex, matchedPositions[{row,col}], symbolIds[], matchLength,
                               basePayout, winInCredits, lineMultiplier, winInCash }
payload.nearMissRespin       { triggered, reelIndex, originalMatrix, newMatrix?, featureName } — only on a respin;
                               newMatrix is not always sent — the final board is always `matrix`
payload.freeSpins            { triggered, active, spinsRemaining, totalSpins, totalWinCash } — on a
                               trigger and every spin of the round; absent on a base spin
payload.isRespin / respinCount, betAmountPerLine, totalBetAmount, netReturnRatio (= win ÷ total bet)
payload.sandbellPickBonus    { featureName, triggered, pickedPosition {row,col,multiplier},
                               unpickedPositions [{row,col,multiplier}], totalWinMultiplier,
                               prizeMultiplier, winInCash } — only on a trigger
payload.ballPickemBonus      { featureName, triggered, triggerPositions [{row,col}],
                               picks [{pickIndex, multiplier, revealedPrize, revealed, isEnd,
                               isCollect}], totalWinMultiplier, winInCash,
                               grid / gridDimensions / centerBox (ignore — being removed) } — only on a trigger
player.balance               double?
```

`ConvertServerResponseToSpinResult` produces `SpinResult`:

- **`resultMatrix` is transposed to COLUMN-major** (`[reel][row]`), because the view draws per reel.
- **`winAmount` = `grandTotalWin`**; the line wins alone are kept as `lineWinAmount`.
- **`winLines`** — one per winning **payline**: `lineId` = `lineIndex`, flat cell indices from
  `matchedPositions`, `matchCount` = `matchLength`, `winAmount` = `winInCash`. **`symbolId` is the
  paying symbol**: the first non-Wild in `symbolIds`, since a Wild standing in is listed as itself.
- **`freeGame`** (`FreeGameData`) from `freeSpins`: `present`, `spinsAwarded` = `triggered`,
  `active`, `spinsRemaining`, `totalSpins`, `roundWin` = `totalWinCash`. Never null; a base spin
  reads `present` false.
- **`respin`** (`RespinData`) from `nearMissRespin`: `reelIndex`, `originalMatrix` (column-major),
  `featureName`; null when there was none. `newMatrix` is unbound — `resultMatrix` is the final board.
- **Unbound for now:** `sandbellPickBonus` and `ballPickemBonus` (each bound with its feature), the
  bet echoes and `netReturnRatio`. Gold of Luck's `freeGames`, wheel and Genie-multiplier blocks are
  gone.

### Semantics worth knowing before arguing with the numbers

- **Total bet = bet x 20.** Every captured spin at bet 0.01 reports `totalBetAmount` 0.2, and the
  balance drops by exactly that plus the win.
- **`grandTotalWin` is authoritative.** Do not sum anything.
- **`winInCash` = `basePayout` x `lineMultiplier` x the per-line bet.** `lineMultiplier` has been 1 on
  every capture.
- **Paylines are zero-based** and `matchedPositions` trace `lines[lineIndex]` cell for cell, left to
  right. **Lines can share cells** — three Jack-in-the-box 2-of-a-kinds used the same two cells.
- **The Wild substitutes for paying symbols** (`[0,6,6]` pays as three 10s) and pays nothing itself.
  Whether it can stand in for a scatter is unconfirmed.
- **Two scatters can land on one reel** — captured for both the Sandbell and the Ball, and the
  backend counts them as separate cells. More than 5 triggers too (owner, for now; backend to
  confirm).
- **Near-miss respins are server-resolved.** `matrix` is already the final board; `originalMatrix`
  and `reelIndex` say what was respun. The Drum checks count Drums instead of reels — the respin fires
  with both Drums on reel 2 and none on reel 3 (1-based), and Free Spins has triggered with a reel of
  2–4 empty. A backend bug, reported; being fixed (`ToDo.md`).
- **Free spins:** requested like any spin (the server owns the round), cost nothing, and each win is
  in the result's balance at once — so holding the shown balance until TAKE is display only. The
  trigger spin is an ordinary paid spin whose win is not in the round's total.
- **The pick bonuses arrive whole on the trigger spin.** No click is ever sent: the server has
  already decided every value, and the client only chooses which Sandbell or gem shows it. Prizes are
  multipliers of the **total bet**; the money is the server's `winInCash` / `grandTotalWin`.
- **Captured samples are useless for frequencies.** The mock client may boost features. Valid for
  shape, never for rates.

---

## 4. The round lifecycle

This is the spine of the game. Everything else hangs off it.

```
UIManager button press
  └─ GameManager.RequestSpin()          guards: state == Idle, connected, balance
     └─ StartSpin()                     state = Spinning; deduct bet; UIManager.OnSpinStarted()
        ├─ SlotView.StartSpin()         reels start; DisableAllOverlays(); clears lastWinLines
        ├─ SocketIOManager.SendSpinRequest(betIndex)
        └─ SpinRoutine()                coroutine
             ├─ wait GetSpinDuration()          speed-dependent; a Stop press cuts it short
             ├─ wait until lastResult != null   the response can arrive at any time
             ├─ state = Stopping
             ├─ SlotView.QuickStop(matrix, cb)  Quick Spin, or the player pressed Stop
             └─ SlotView.StopSpin(matrix, cb)   normal: staggered stops + scatter anticipation
                  └─ per reel: write symbols, land, reel-stop cue, scatter landing cue
  └─ OnReelsStoppedComplete()           stop the spin loop sound; take the server's balance
     ├─ a Free Spins / Sandbell respin: PlayMysteryRespin() — MYSTERY RESPIN, SlotView.RespinReel, then ↓
     └─ PresentSpinOutcome()
          ├─ features: PlayFeatureSequence() — every feature the spin won, in order, 1 s apart:
          │          the Sandbell Pick, the Ball Gale Pick, then the Free Spins trigger (Drum boxes,
          │          CONGRATULATIONS, black fade, panel for logo, START → BeginFreeSpins()); then
          │          Spin live again. Inside a round: the pick bonuses only, then the round goes on ↓
          ├─ win:    UIManager.OnSpinStopping/OnSpinCompleted, then
          │          SlotView.ShowWinLineAnimation(winLines, OnWinAnimationComplete)
          └─ no win: ShowWinLineAnimation(null, ...) — the view is still told, it owns the dim
  └─ OnWinAnimationComplete() → ProcessSpecialFeaturesAfterWin()
       ├─ wait out any big-win popup (never shown during free spins)
       ├─ a retrigger: PlayRetriggerSequence() — Drum boxes, CONGRATULATIONS, counter's Y up
       └─ ResumeAfterSpecialFeature() → DelayBeforeNextRound() → ProcessSpinResult()
  └─ ProcessSpinResult()                where a round is advanced
       ├─ in free spins         → next free spin, or EndFreeSpins() — TOTAL WIN, TAKE, FreeSpinsOutro()
       └─ autoplay              → next round, or StopAutoPlay()
```

**Where the features hook in** (Free Spins built; `BonusFreeSpins.md` §10):
- **The respin** goes between the reels landing and `PresentSpinOutcome`: `SpinRoutine` lands
  `originalMatrix`, then `PlayMysteryRespin` shows the popup, respins the one reel onto the final
  `matrix`, and calls `PresentSpinOutcome` from its completion — anything that alters the board runs
  first. All three features' respins (`"freeSpins"`, `"sandbellPickBonus"`, and the Ball's
  `"ballPickem"` or `"ballPickemBonus"`); an unknown `featureName` lands straight on the final board.
- **A trigger** takes over from `PresentSpinOutcome`, which hands every feature the spin won to
  `PlayFeatureSequence` (below). The game is handed back when the last is done — after the Free
  Spins TAKE, or once the pick bonuses are over. The trigger spin's result is closed out first
  (`lastResult` nulled), so START's `StartSpin` can't hand it to `ProcessSpinResult` — except inside
  a round, where `ProcessSpinResult` needs it to carry the round on.
- **Several features on one spin** (backend, 2026-11-06: any combination can land together).
  `GameManager.PlayFeatureSequence` plays every one the spin won, in order — **Sandbell Pick, Ball
  Gale Pick, then Free Spins** — each after the first waiting `FeatureGap` (1 s, owner), with the
  Sandbell's values hidden first. The game is taken over once (`BeginFeatureSession`: ShowingWin,
  autoplay parked, buttons locked, Spin greyed, the spin closed out) and handed back once
  (`EndFeatureSession`, or the Free Spins TAKE). A spin with one feature plays exactly as before.
  - **Win box and balance (owner, option C):** GOOD LUCK while the pick bonuses play; after each
    one's TOTAL WIN the win box shows the prizes so far and the held balance steps up by the prize;
    after the last, the win box shows the spin's total (`grandTotalWin`, its line wins included) and
    the balance the server's figure. Free Spins, if won too, starts from there.
  - **Inside a Free Spins round** only the pick bonuses can be won: the round pauses for them
    (Sandbell, then Ball), then carries on its usual way — the retrigger if any, then the next spin
    or TOTAL WIN. The round's balance hold, win box (its running total), counter and music are left
    alone; the Ball fades the FREE GAME counter instead of the logo
    (`BallGalePickView.freeGamesCounterGroup`).
  - **Backend assumption, unverified:** a pick prize won in a free spin is in that spin's balance
    (confirmed) **and** in `freeSpins.totalWinCash`. If it is not, only the round's win box and
    TOTAL WIN miss it; the balance at TAKE is still right (`ToDo.md`).
- **The free-spins round** advances in `ProcessSpinResult` and ends on `freeSpinsRemaining` reaching
  0. (A round should really end on the server's own "still active" flag; `active` is bound, but what
  it reads on a round's last spin isn't captured yet.)

**`GameState`** (`Initializing` until the init arrives, then `Idle`, `Spinning`, `Stopping`,
`ShowingWin`): every entry point — spin, bet change, autoplay start, hold-for-autoplay — refuses
anything but `Idle`. `ShowingWin` is meant for a feature holding the game between the reels landing
and its own end; nothing sets it today, since the Genie Wheel was its only user.

---

## 5. SlotView — the board

The biggest file, and the one most specific to "this is a reel slot".

**Layers, bottom to top**, each a separate object tree over the same 15 cell positions:

| Layer | What it is |
|---|---|
| Reels (`SlotMask/Slot (0..4)`) | five scrolling strips; `displayImages` are the three visible icons per reel |
| `WinDimOverlay` | dark sheet, raised whenever something above it needs to pop |
| `AnimationLayer` | one slot per cell; every symbol animation plays here, never on the reels |
| `AnimationLayer/WinBoxes` | one animated box per cell, drawn over the symbol; shown only on winning cells — see the win presentation below |

**Why animations live on their own layer:** reel icons scroll and are recycled, so drawing an
animation on a moving strip means per-instance components and clipping problems. The layer slots are
static, one per visible cell, and the code hides the reel icon underneath while a slot is drawn. **Any
new per-cell visual — the win boxes, for one — belongs on a static layer like this, not on the reel
icons.**

**Symbols — already Wild Jokers.** Ids 0–9 map to named sprite and animation fields: Wild (0);
Jack-in-the-box, A, K (high); Q, J, 10 (low); Drum (7, scatter, reels 2–4 only — Free Spins), Ball
(8, scatter — Ball Pick'em), Sandbell (9, scatter — Sandbell Pick). The sprites are wired by the
owner in the scene.

**Almost no symbol animates (owner).** Unlike Gold of Luck, being part of a win never animates a
symbol. Only two have clips, and only when a feature triggers: the **Sandbell**, when its feature
triggers, and the **Wild**, only when it is part of triggering a feature (which feature is still open
— the backend gives the Wild none of its own). The Drum and Ball do not animate on their triggers.
Every other `animSprites…` list stays empty **by design**, not as missing art. `SymbolSizeOverrides`
and `SymbolAnimationSpeeds` list all ten at the defaults (200 × 200, speed 20) until tuned against the
new art; only the Sandbell's and the Wild's speeds matter. One size per symbol id serves both the reel icon and any copy drawn on
the animation layer — anything over the 245 pitch overlaps its neighbours and catches taps meant for
them.

**Per-cell data is captured at stop time.** Gold of Luck's Genie carried a per-cell multiplier sprite;
that code is gone. Its lesson stays in `StopSpinSequence`: anything per-cell a landing write needs
must be captured before any reel lands, because the controller may have cleared `lastResult` by the
time the last reel draws.

**Spinning** is a continuous DOTween loop per reel with a buffer of filler rows above the visible
window. Stopping kills the loop, snaps to a fixed pre-land position, writes the landed symbols, then
plays one overshoot-and-settle tween.

**Reel geometry (Wild Jokers).** Every strip has 25 icons **245 apart**: (−18) at 4725 down to (6) at
−1155, with the visible rows (0) / (1) / (2) at 315 / 70 / −175 inside each `Slot (n)`. Reel x is
−505 / −252.5 / 0 / 252.5 / 505.
- `symbolHeight` (245) **must equal the pitch.** The spin loop travels `bufferRowsAbove × symbolHeight`
  and then restarts; anything but a whole number of pitches shows as a jump on every loop.
- **Move icons, not the `Slot` parents.** The parents sit at y −67.4, which `InitializeReels`
  hard-codes as `middlePosition`, so a moved parent is snapped back on the first spin.
- **The animation layer does not take the same numbers.** Its parents sit 123.383 higher, so its rows
  are the reel rows minus 123.383 — 191.617 / −53.383 / −298.383 — at the same x. Re-laying out the
  reels means redoing the layer (and the win boxes inside it) to match.

**Anticipation — the Drum's and the Sandbell's.** `ComputeAnticipatedReels`: once every
`freeSpinsNearMissReels` reel (2 and 3) shows a Drum, each reel after them is held — reel 4, then
reel 5 unless reel 4 completed the trigger. `anticipationEffects[reel − 1]` comes on in green
(`anticipationGreenFrames`, handed to its `ImageAnimation`) as the reel before lands, and the held
reel spins on for an extra 2 s (0.8 s on Turbo — the scene's values; the code defaults are 2 / 1),
cumulatively, under the anticipation tension sound. A respun reel shows the same effect in orange
(`anticipationOrangeFrames`). The Sandbell's rule (`AddSandbellAnticipation`) holds every reel after
the second Sandbell cell and keeps going after the third, never during free spins; the held reels are
the union of both rules. The Ball never anticipates (each spec's §3). The Drum and the Sandbell have
landing cues.
Reels report landing from their stop tweens; `StopSpinSequence` waits for all of them.

**The win presentation — Wild Jokers design (owner).** The board darkens and the winning symbols are
copied onto the animation layer above the dim, **static**, the Wild included. On top of each winning
symbol sits an **animated box**: one clip, the same for every cell and every line, one box per winning
cell (cells shared by several lines get one box). Wild and Sandbell clips belong to feature triggers,
not to line wins.

**How it runs.** `PlayTwoPhaseWinLines` → `AnimateWinPositions` copies every winning cell onto the
animation layer, hides the reel icon beneath it, turns on that cell's box, raises the dim and plays —
twice in step with the total, then waits for the burst behind the total to finish its clip (every
spin alike, owner 2026-11-06; autoplay and free spins then clear it and move on). A manual spin walks
the lines twice and ends on the total again (`PlayWinLineCycleRoutine`, below).

**The line walk (owner, 2026-11-05; reworked 2026-11-10).** Manual spins only — autoplay and free
spins stop after the total; when autoplay stops or a Free Games round ends, `PlayWinLineCycle` starts
the walk for the last win. For each winning line, in the server's order, **two passes**
(`WalkPasses`):
- `KillWinTweens(false)` clears the previous line: symbol and box animations, boxes, amount, payline
  image — and the total, which stays hidden for the whole walk. The dim stays up.
- `ShowWinLine`: the **payline image** (`paylineImage`, one Image, its sprite swapped to
  `paylineSprites[lineId]`; no sprite → no image) and the line's **amount** (`ToSpriteMoney`) on one
  of the three `WinLineAmount (0..2)` labels on reel 3 — the row the payline crosses reel 3, read
  from `gameConfig.paylines[lineId]`, so a 2-symbol win that stops short of reel 3 still puts it on
  its line. The amount **pops in** like the total: its scene scale → 1.7× → settling at 1.3×
  (`AnimateAmountPop`; each label's own scene scale, restored when it is hidden).
- `AnimateWinPositions(line.positions, winLineRounds = 2)`: **only that line's cells and boxes**,
  played twice in step (two box loops, ≈ 2.1 s a line; slowed by the owner, 2026-11-05).

**The closing total** (owner, 2026-11-10), after the second pass — or at once if no line can be
shown: the payline and amount go; the total comes back **with its stars and its pop**, exactly as it
first showed (`ShowPhase1TotalWin`), and is hidden once the stars have played (`WaitForTotalWinBurst`;
with no star frames it stays `winSymbolLoopDuration`). **Every winning cell's symbol and box keeps
looping** until the next spin. Nothing is held on screen. No sound plays on a line change (Golden
Dynasty's clip was removed).

**The win boxes:**
- **Scene:** `AnimationLayer/WinBoxes` (at 0,0), placed after the five `AnimSlot`s and before
  `WinLineAmounts`, so every box draws over every symbol and comes and goes with the layer. Inside it
  `WinBoxColumn (0..4)` at x −505 / −252.5 / 0 / 252.5 / 505, each holding `WinBox (0..2)` at
  y 191.617 / −53.383 / −298.383 — the `AnimSlot` numbers. Boxes are 290 × 310, bigger than the 245
  pitch, so neighbouring boxes overlap (the art's glow). Each box: Image + `ImageAnimation`, **Raycast
  Target off** (taps must still reach the symbol info card), **Start On Awake / Start On Enable off**,
  empty frame list, GameObject inactive.
- **Code (`SlotView`):** `winBoxColumns` (one `WinBoxColumn` per reel, its `rows` the 3 boxes'
  `ImageAnimation`s, top first — the component finds its own Image) and **one `winBoxFrames` list**,
  handed to all 15 at runtime so the clip is set in one place. The speed is the const
  `winBoxAnimationSpeed` (33 = the frame count = 24 fps, ≈ 1.4 s a loop); tune it there.
  `AnimateWinPositions` turns a box on per winning cell and adds it to the clips it paces its rounds
  on, so the rounds follow the box clip; `HideWinSlots` stops and hides every box (every teardown
  runs through it). A box with no `winBoxFrames` stays hidden.
- **The scatter guard** in `AnimateWinPositions` skips every symbol in the init's scatter group (Drum,
  Ball, Sandbell) — read from the symbol table's scatter group, not the per-feature ids. A skipped
  scatter stays dimmed with no box.
- **The board total, with a burst behind it.** The spin's total win shows in the middle of the board.
  - **Scene:** `SpinWinText` (`phase1TotalWinText`) is a child of `SpinWinAnimation` (Image +
    `ImageAnimation`, Raycast Target off, Start On Awake / On Enable off), wired as
    `SlotView.totalWinBurst`. Save it off: the code switches it off at Start and on for each win.
  - **The first show** (`ShowPhase1TotalWin`, the start of `PlayTwoPhaseWinLines`): the burst plays
    once and then only its **Image** goes off (the object is the text's parent); the text pops from
    its scene scale to 1.7× and settles at 1.3×.
  - **During the line walk** the total is hidden. **The closing total**, after the walk, is a second
    first show: burst and pop again, then hidden when the burst ends (owner, 2026-11-10; this
    replaced the settled, burst-less hold of 2026-11-05).
  - `HidePhase1TotalWinText` (every teardown, through `KillWinTweens`) hides the text, puts it back to
    its scene scale, and stops and switches off the burst.
  - **The amount must be the server's.** `PlayTwoPhaseWinLines` builds `totalWinAmount` by **summing
    the lines' `winAmount`**, falling back to `lastResult.winAmount` only when that comes to zero.
    That breaks the never-sum rule; it should read `lastResult.winAmount` (`grandTotalWin`) — the
    figure the win box already shows. `SummariseWinLines` sums the same way for the restarted cycle.
- **Settled:** the dim stays; the Wild does not animate on a line win; one clip for every box (`SquareWin`,
  29 frames — its speed is still the old clip's 33, `ToDo.md`).
- **Scene for the walk:** `AnimationLayer/WinLineAmounts/WinLineAmount (0..2)` (top, middle, bottom
  row of reel 3; saved off; wired as `winLineAmounts`). The payline Image is the owner's to add —
  suggested in `AnimationLayer` after `WinBoxes`, before `WinLineAmounts`, Raycast Target off, saved
  off — wired as `paylineImage`, with the 20 sprites in `paylineSprites` in line order.

**Who owns the dim** is a recurring source of bugs. `dimHeld` (a per-spin claim) and `featureDimHeld`
(a feature round's claim) and their guards in `HideWinDim` / `ReleaseHeldDim` are **kept but
currently inert** — nothing sets either since Mystery and Hold & Spin were removed. They are the
pattern anything drawing over the reels mid-spin has to follow: without a hold, the win
presentation's opening teardown drops the dim a frame before raising it again, which reads as a
flicker. The features' trigger boxes and the Sandbell Pick's values on the reels will be the next
users.

---

## 6. The features

**Wild Jokers' features — all three built in code; Ball Gale Pick not yet wired or tested.** Each
has its own spec, which is the place to start:

| Feature | Trigger | Spec |
|---|---|---|
| Free Spins (Drum, id 7) | a Drum on each of reels 2, 3, 4 → 10 spins; respin reel 4 | `BonusFreeSpins.md` — **built in code**, §10 |
| Sandbell Pick (id 9) | 3+ Sandbells → pick one; respin reel 5 | `SandbellPickBonus.md` — **built in code**, §9 |
| Ball Gale Pick (Ball, id 8) | 3+ Balls → 20-gem pick bonus; respin reel 5 | `BallGalePickBonus.md` — **built in code**, §10 |

**Built with Free Spins, for the others to reuse:** the near-miss respin (`SlotView.RespinReel`,
the orange effect, `GameManager.PlayMysteryRespin` — for all three features), the feature popup
(`FeaturePopupView` — one child per message), win boxes on the trigger cells (`SlotView.ShowFeatureTriggerBoxes`), the `FeatureStart` /
`FeatureTake` button modes, `SuspendAutoPlayForFeature` / `ResumeAutoPlay`,
`UIManager.SetFeatureButtonLock`, and `GameManager.DisplayBalance`, which holds the shown balance
while a feature pays (`balanceHeld`).

**Gold of Luck's Genie Wheel is removed** (code, model and sounds). Its scene leftovers —
`GenieWheelManager` (now a missing script) and `FullScreenGenie1`/`2` — are for the owner to delete
(`ToDo.md`).

**Degradation is a rule, not a nicety:** every feature view checks its references and falls straight
through to its callback when something is unwired. A round must never stall waiting on a callback
that cannot fire.

---

## 7. UIManager

One shared button drives the whole game. `SpinButtonMode` is `Spin`, `Stop`, `AutoplayStop`,
`BigWinTake`, `FeatureStart`, `FeatureTake`, and a single click handler routes on the current
mode, so what the player sees and what the press does cannot disagree. "Explicit" modes (the feature
and popup ones) outlive the ordinary spin events and are released only by whoever set them —
`ReleaseSpinButton` returns to Spin without clearing the win box, where `SetSpinButtonMode(Spin)`
resets it. `FeatureStart` / `FeatureTake` are the features' START and TAKE (both share art with
`BigWinTake`'s TAKE but answer to `GameManager`); the old free-games round itself just greys out the
plain Spin button.

Also here: bet controls, the autoplay panel (opened by holding Spin), the speed cycle
(Normal → Turbo → Quick Spin), the settings/rules/guide/sound panels, the universal win popup, the
win box, the balance, and `SetFeatureButtonLock`, which locks everything a feature round forbids —
START / TAKE, fullscreen and the speed toggle stay live.

**The bet display shows the TOTAL bet** — `GetTotalPay()`, bet x `activeLine` (20). At the lowest bet
the player sees 0.20, not 0.01.

**`GetDisplayWin`** is the one place the win box reads its figure, and the hook for a feature that
owns its own payout: in free spins it returns the round's running total. The balance has the
matching hook, `GameManager.DisplayBalance` — today simply `playerData.balance`; the features hold it
until their TAKE / end.

**Platform jackpots.** Four buttons (`jackpotButtons`, a list pairing each `Button` with a
`JackpotTier`) send `JACKPOT_OPEN`; the four `…JackpotText` fields (`grandJackpotText` …
`miniJackpotText`) display values from `jackpot:sync`. The tier is stated in the list rather than read from the object's name, so renaming a
button cannot change what is sent.

**Every UI element exists twice, landscape and portrait.** Wild Jokers is landscape-only: the
landscape fields are wired to the `LandScapeObj` prefab instance under `MainCanvas/BG`, and the
portrait fields still point into `PortraitObj`, which is switched off. Every use is paired and
null-guarded, so either half can be empty. The portrait half is unused but kept on purpose (owner)
— don't remove it. **Trap:** a landscape
field left empty means that button simply does nothing — its sprite still swaps on press, because
that is the Button component's own transition, not code.

---

## 8. Audio

`AudioManager` is a singleton with one serialized `AudioClip` per cue and one method per cue, so call
sites never touch clips or sources. File names in the audio folder are often misleading; the field
names say what each cue does.

**Sources:** music (one track at a time, with a 0.5 s dip whenever the track switches), UI/sfx, a
reserve for when UI is busy, the **spin loop** (the reel spin), and **`anticipationSource`** for the
anticipation. **Duration sounds** — the reel spin, the anticipation — are started and stopped
explicitly on their own source; a one-shot cannot be cut, and these must stop the moment the reels
land. **MYSTERY RESPIN** and **TOTAL WIN** outlast what they go with, so they are played with
`Play()` on the UI source and cut by clip (`PlayCuttable` / `StopCuttable`); while one plays, other UI
sounds go to the reserve source.

**Wild Jokers' clips** (`Assets/Audio/WildJokers`, 2026-11-04):

| Cue | Clip | When |
|---|---|---|
| Main music | `wj` | base game; back at a free-spins round's TAKE |
| Free-spins music | `free spin bg` | from the black fade into the round until TAKE |
| Reel spin | `reels spining` | looped while reels turn |
| Win presentation | `normal win` | the winning symbols and their boxes coming up |
| Anticipation | `bonus scatter fail` | reels held, and a respun reel; cut when the last lands |
| CONGRATULATIONS | `congrats bonus game` | all three features (the popup holds 6.5 s for it) |
| MYSTERY RESPIN | `mystery respin` | the popup only — cut as it closes |
| TOTAL WIN | `total win ` | once from the popup; cut at TAKE / the Sandbell's fade |
| Gem break | `glass break in ball game` | a gem breaking (Ball Gale Pick) |
| Swoosh | `ball game swoosh` | the Ball panel's slide in, and out |

The shared UI clips (bet, max bet, spin button, buttons, popups, autoplay, turbo) are in
`Assets/Audio`. Gold of Luck's and Golden Dynasty's clips, fields and calls are gone, so a feature
trigger, a reel landing, a scatter landing, the Wild in a win and the big win are **silent** until
this game has clips for them (`ToDo.md`).

---

## 9. Conventions

**Flat index = `row * reelCount + col`**, row 0 at the top. The shared address for
`WinLine.positions` and every per-cell map. (The backend's own cells are `{row, col}` pairs.) Sorting flat indices ascending gives
reading order for free.

**Column-major layers.** The animation layer is `List<AnimSlotColumn>`, one entry per reel, each
holding 3 rows. Wire column by column, matching the hierarchy.

**Tuning values are un-serialized `private const`** (or `static readonly` for tables — the symbol
size and speed maps).
Deliberate and load-bearing: a serialized value is saved in the scene and **silently overrides**
whatever the code says, so retuning in code appears to do nothing. Values that genuinely need
eyeballing in play — the anticipation hold times, the big-win threshold — are documented serialized
exceptions; the scene's value is the one that runs.

**Money formatting** goes through `Helper/SpriteTextFormatter.cs`: `ToSpriteMoney` for amounts,
`ToSpriteDigits` for counts, `ToSpriteMultiplier` for an X value ("x" = sprite 11 — only in
`0-1ORANGE`, `1-0 GREEN` and `Artboard 1`), and `MoneyFormat` ("0.00") shared by the money. A sprite-font text renders
`<sprite=N>` tags and needs its TMP sprite asset assigned, or it shows the literal tags.

**`-1` means "no such symbol"**, never `0` — 0 is a real symbol id (the Wild).

**Comments explain WHY.** The codebase is heavily commented with the reasoning behind non-obvious
choices, especially the ones that were got wrong once. Keep it up. When removing a feature, **rewrite**
a comment that cites it rather than deleting the lesson with it.

---

## 10. Traps that cost real time

**`ImageAnimation` is the richest source of these.**
- `AnimationSpeed` is read only inside `StartAnimation()`. Components are reused across spins, so an
  unwritten speed is a stale speed from whichever symbol used the slot last. Always write it first.
- A loop lasts `frames² ÷ (24 × AnimationSpeed)` seconds — quadratic, and NOT a frame rate. To match
  two clips' lengths, solve for speed; `speed = frames² ÷ 64` gives roughly 2.9s.
- **It has no per-frame callback** (only `onLoopComplete` and, in `TWO_PHASE`, `onPhase1Complete`).
  Never sync to a specific frame with a timer: frames wait for rendered frames, so a clip runs a
  little long and a computed delay drifts by several frames. Watch the frame actually shown instead —
  `textureArray.IndexOf(rendererDelegate.sprite)` reached-or-passed, with a timeout — as
  Gold of Luck's congratulations title cue did (`PanelClipPlayback`, deleted 2026-11-04; in git history).
- **A `TWO_PHASE` start draws frame 0 twice** before moving, so a clip sits still for about two frames
  after `StartAnimation()`. Start it that much early when it must move on a beat
  (Gold of Luck's `clipEarlyStartFrames`, in `PanelClipPlayback`).
- `StopAnimation()` rewinds to frame 0 **only while the clip is playing**. For a one-shot whose last
  frame is the end state, set `doLoopAnimation = false` and let it stop itself — the last frame stays.
- **Borrowing a component is fine if you put it back.** Gold of Luck's `PanelClipPlayback` played an opening
  backwards by swapping in a reversed frame list, single-phase, and restored the original list, mode
  and speed afterwards. The views here swap frame lists the same way (`SlotView`'s anticipation, the
  Ball's glare and break).
- **Open-then-loop clips use `TWO_PHASE`** rather than two components or a timed handover. Assign
  opening and loop as ONE list, opening first, and set `phase2StartIndex` to the loop's first frame;
  `phase1LoopCount` 1 and `phase2LoopCount` -1 is the usual pair. The handover is seamless, and
  `onPhase1Complete` fires the moment the opening ends — hook follow-on work there, not on a delay.
  `SINGLE_PHASE` is the default and is the original behaviour. `doLoopAnimation` is ignored in
  `TWO_PHASE`; the loop counts decide when it stops.
- Never call `StartAnimation()` from inside `onLoopComplete`: the clip runs at double speed.
- **"Start On Enable"** in the Inspector starts the clip before any code runs, so a one-shot meant to
  fire later animates immediately and nothing in the calling code explains why.

**Sorting order beats Z.** For world renderers (`SpriteRenderer`, `TrailRenderer`) composited against
a Screen Space – Camera canvas, Z is consulted only *within* the same sorting order. To put UI in
front of a world object, give it a nested Canvas with **Override Sorting**. For plain UI Images, draw
order is simply sibling order in the hierarchy.

**The canvas unit trap.** Inside the canvas 1 unit = 1 reference pixel, but a `SpriteRenderer` sizes
as `texturePixels / pixelsPerUnit`. At Unity's default PPU of 100 a sprite comes out ~58x too small.

**Rotation and scale are around the RectTransform's pivot.** Anything that spins or pops needs its
pivot at (0.5, 0.5), or it swings or grows off-centre.

**There is no `DOTweenModuleTextMeshPro`.** `tmpText.DOFade(...)` will not compile; fade through a
`CanvasGroup`.

**`SlotView.ApplySymbol` rewrites `sizeDelta` but not `pivot`.** Anything that changes a slot's pivot
must put it back itself — nothing does today, and there is no layout cache to fall back on (it went
with Golden Dynasty's stacked Wilds, its only user).

**Renaming a serialized field drops its scene assignment.** `[FormerlySerializedAs]` is not used here,
so every rename is re-wired by hand. Removing a field is harmless: the scene keeps the orphaned value
until next saved, and Unity ignores it.

**Serialized lists hide references.** A button wired through a `List<…>` field does not show up as a
named reference when scanning the scene, and has no persistent `onClick` calls — look for
`AddListener` in code.

**`Assets/Scripts/MD/` is gitignored**, including `MD.meta`. These documents are not in version
control.

---

## 11. Inherited oddities

- **`activeLine`** holds `totalLines` (20) — both the line count and the bet multiplier. (In Gold of
  Luck it held `creditDivisor`, 50, and the label showed the 243 ways.)
- **`uiData.paylines`** holds the symbol table, not paylines; the paylines are `gameData.lines`.
- **The jackpot button `MiiniButton`** has an extra "i". Harmless — the tier is set in the list.
- **`SymbolInfoCard`'s scatter text** is Golden Dynasty's — "3 or more Bonus symbols trigger Free
  Games" — and is shown for all three scatters. Needs per-feature text.
- **`scatterSymbolId`** is Gold of Luck's single scatter; it resolves to the Sandbell and still
  drives the anticipation and the scatter clip (§5).
- **The Rules / Guide panels and `Graphics/InfoSlides`** are Golden Dynasty art with Golden Dynasty
  payouts.
- **Leftovers still in the project:** the unused win-box clip `Graphics/Animations/WildJokers/Square`
  and the `WheelSpin` audio object (its field is gone). The Genie Wheel's scene objects,
  `FreeGamesOverlay`, `PanelClipPlayback.cs` and the old games' audio were deleted (owner,
  2026-11-04).

---

## 12. Known problems — do not copy these into a new game

Full detail in `ToDo.md`; this is the short list.

- **A failed `result` (`success: false`)** is only logged, not surfaced, so the reels spin forever.
- **The board total sums the lines** — `PlayTwoPhaseWinLines` and `SummariseWinLines` add up the
  lines' `winAmount` instead of reading `grandTotalWin`. Off for now (§5); fix before it comes back.
- **The per-feature symbol ids come only from the init** (`features.<feature>.symbolId`, sent since
  2026-11-02). Don't hard-code them; a missing one logs an error and reads -1.
- **Unused portrait UI** — `PortraitObj`, every paired call site and the portrait fields. Kept on
  purpose (owner); a new game that needs only one orientation can drop the other half.
- **90 empty `ImageAnimation` components** on cell strips and reel icons that nothing uses.

---

## 13. Building a NEW game from this base

**Keep as-is.** Game-agnostic and debugged the hard way:

| Keep | Why |
|---|---|
| `SocketIOManager` + `JSFunctCalls` | connection, auth, ping/reconnect, popups, the emit format |
| `GameDataModels`' converter shape | one seam where wire shapes change; views never see a backend revision |
| `GameManager`'s spin lifecycle and state machine | §4 is the part you do not want to rewrite |
| `UIManager`'s button-mode machine, autoplay, speed, popups, jackpots | one button, one router |
| `AudioManager`, `PopupManager`, `SpriteTextFormatter`, `ImageAnimation` | plumbing |

**Expect to rework** `GameDataModels`' wire models (every game's payload differs) and much of
`SlotView` — its *shape* is worth copying: one view owning one surface, layered, driven through
callbacks.

**Lessons from this conversion:**
- **Bind the backend first, from real captures.** Get the init and a spread of spin responses —
  plain win, feature trigger, full feature round, retrigger — before touching a view.
- **Read every multiplier off the balance.** `totalLines` and `creditDivisor` both looked like "the
  thing you multiply by"; only balance deltas showed which one was real.
- **Identify specials by a stable field** (`group`), never by name.
- **Switch features off with a flag before removing them**, so the base game can be tested alone.
- **Remove dead features in one pass**, with the file deletions last — the project will not compile
  in between, and that is expected.
- **Settle the feature's face against the maths before the art is final.** Gold of Luck's wheel went
  through it twice: the backend first sent 33 slices for 18 painted wedges, and then multiplier wedges whose art
  shows a value and a badge but whose maths had only the badge. Wild Jokers' pick bonuses did too:
  the Sandbell first paid the sum of every Sandbell, and the Ball's prize list didn't match the
  reference.

---

## 14. Working rules for agents on this repo

These come from the project owner and are not negotiable:

1. **Never create or delete files**, including assets. Say what should exist and hand the step over.
2. **Git is read-only.** No `add`, `commit`, `rm`, `checkout`, `restore`, `reset`, `stash`, `mv`.
   Reading (`status`, `log`, `diff`, `show`) is fine.
3. **Never wire Unity Inspector references**, and never use `[FormerlySerializedAs]`. Scene work is
   handed over with the exact field, object and value.
4. **Three protected scripts** — `BackEnd/SocketIOManager.cs`, `Helper/ImageAnimation.cs`,
   `Managers/PopupManager.cs`. Ask explicitly by name before changing any of them. They were once
   cleaned up as "dead code" and had to be restored: unused entry points in connection, popup and
   animation plumbing are deliberate spare capacity, and a reference count cannot tell that apart
   from cruft. (The jackpot-sync path in `SocketIOManager` is exactly that kind of spare capacity.)
5. **Maintain the MVC split.**
6. **Flag, don't decide.** Report problems found next to the task, but stay inside what was asked.
   Product questions go to the owner.
7. **Verify the build after every change** — via the bundled Roslyn compiler (§1), since `dotnet
   build` is not available on the owner's machine.
8. **No public functions.** Every method is `private` or `internal` — strictly. A method another
   class needs is `internal`, never `public`.

---

## 15. The other documents

| File | What it holds |
|---|---|
| `ToDo.md` | the live working list — what is done, open, blocked on the backend or art team, and awaiting an owner decision. **Read this first** |
| `SandbellPickBonus.md` | **Wild Jokers** Sandbell Pick Bonus end to end: rules, anticipation, Mystery Spin, the pick, the total, the backend contract and its gaps, how it was built, open questions. Built and working |
| `BallGalePickBonus.md` | **Wild Jokers** Ball Gale Pick Bonus (the rules page's "Ball Pickem") end to end: rules, Mystery Respin, the gem panel and its picks, the total and TAKE, the backend contract and its gaps, how it was built, open questions. Built, not yet wired |
| `BonusFreeSpins.md` | **Wild Jokers** Free Spins (Drum) end to end: trigger, anticipation, Mystery Respin, the round, the backend contract and its gaps, how it was built, open questions. Built and wired |
| `Config/SL-WJ_config.json` | the backend's own game config — symbols, paytable, reel strips (`reelsInstance`), paylines, features. Reference only, never loaded; its shape differs from the wire `game:init`, and it may not match what the server runs |

`ToDo.md` is the one kept most current. If this document and `ToDo.md` disagree, trust `ToDo.md`.