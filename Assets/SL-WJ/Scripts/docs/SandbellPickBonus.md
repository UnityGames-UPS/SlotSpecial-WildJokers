# Sandbell Pick Bonus — Wild Jokers

The full description of Wild Jokers' Sandbell Pick Bonus: what the player sees, step by step, what
the backend sends for it, what is still unknown or wrong on the backend, and how it maps onto the
client. Written so a programmer or an agent can build it from this file alone.

**Status:** described by the owner (2026-10-28). **Built in code (2026-11-03), wired, and working
in the editor** (owner, 2026-11-03: "works for now") — §9. Two Sandbell triggers captured (2026-10-30, §8.2): the first paid the sum of every Sandbell; the
backend fixed it, and the second sends one winning value plus the missed values. Respin captures in
§8.3. Scene objects set up by the owner (2026-11-03, §7); the Sandbell clip and the explosion are
wired. The values are sprite digits (2026-11-04): `1-0 GREEN` on the clicked Sandbell, `0-1ORANGE`
on the rest (§9).

**Authority:** the game's rules page and the owner's description are the spec. Where the backend
disagrees, the **backend is wrong** and gets changed. Anything marked **(open)** is unconfirmed;
the full list is in §10.

Related: `BonusFreeSpins.md` (shares the anticipation, the respin and the feature popup),
`GameExplanation.md` (how the project works), `ToDo.md` (what is open).

---

## Terms used here

- **Reels are numbered 1–5, left to right.** The backend uses **0-based** indexes; where a backend
  value is quoted, the 0-based index is given too — e.g. "reel 5 (index 4)".
- **Rows** are 0 (top) to 2 (bottom). The board is 5 reels × 3 rows.
- **Sandbell** — symbol id **9**, group `scatter`. Lands on **every reel**, more than one per reel
  possible (3 per reel strip in the backend config). It is one of only two symbols with an animation in this game (`GameExplanation.md`
  §5) — the clip plays here, once the bonus is won.
- **Win box** — the animated square overlay drawn over a winning symbol (`GameExplanation.md` §5).
- **Feature popup** — the same popup object as in `BonusFreeSpins.md` (`FeaturePopupView`): a
  rotating backdrop, a frame, one child per message (this feature's is `CongratulationsSandbellPick`),
  a `CanvasGroup` on the root for fading.
- **Anticipation layout** — the same green Image + `ImageAnimation` around a reel as in
  `BonusFreeSpins.md`.
- **X value** — a prize shown on a Sandbell as a multiplier of the **total bet**, e.g. "X25".

---

## 1. The rules (game rules page — authoritative)

> 3, 4 or 5 Sandbell bonus symbols anywhere in view during the basic game award the Sandbell Pick
> Bonus.
> Pick a Sandbell to reveal your prize.
> The prize is based on your total bet. Max win from the bonus is total bet x500.
> 2, 3 or 4 Sandbell symbols on 1-4 reels initiate the respin feature on the fifth reel.

Read as:

- **Trigger:** **3 or more Sandbells anywhere on the board** — a count of cells, not one per reel
  (unlike the Drum).
- **More than one Sandbell can land on a reel** (the backend's strips allow 3 per reel), so more than
  5 is possible even though the rules only say "3, 4 or 5". **More than 5 triggers the bonus too**
  (owner, for now).
- **Base game and Free Spins** (backend, 2026-11-06 — the rules page said base game only). Inside a
  round it pauses the round, plays, and the round carries on; the near-miss tease runs in the round
  too.
- **It can land with the other features on one spin** (backend, 2026-11-06): it plays first, then
  the Ball Gale Pick, then Free Spins (`GameExplanation.md`, "Several features on one spin").
- **One pick.** The player picks one Sandbell; its prize is the win.
- **Prize = X value × total bet**, at most **× 500**.
- **Respin — near miss only (owner):** **exactly 2 Sandbells on reels 1–4** → **reel 5** respins,
  a chance at the third. **No respin once 3 or more Sandbells are on the board** — the bonus is
  already won. This includes 2 on reels 1–4 plus one already on reel 5: that is 3, so reel 5 is not
  respun (which could take the third away).
- **More than 3 Sandbells changes nothing** (owner): the prize is the picked value either way.

**The rules page and the backend say more than the owner's rule.** The rules page reads "2, 3 or 4
Sandbell symbols on 1-4 reels initiate the respin", and the backend's `nearMissCountRange` is 2–4.
The owner's rule is 2 only — raise both with the backend team (§8.4). Either way the client never
decides a respin: it respins only when `nearMissRespin` says so.

---

## 2. The flow at a glance

From the reference video (two Sandbells, then the respin lands the third):

```
Base spin
 ├─ the second Sandbell lands (reference: reels 1 and 2)
 │    └─ ANTICIPATION: green layout around every reel still spinning (reference: 3, 4, 5), they spin longer
 ├─ the board ends with exactly 2 Sandbells, both on reels 1–4
 │    └─ "MYSTERY RESPIN" popup → fades → reel 5 respins
 │         └─ lands a Sandbell (reference) ──────────────┐
 └─ 3+ Sandbells ────────────────────────────────────────┤
                                                         ▼
"CONGRATULATIONS! YOU WON SANDBELL PICK BONUS" popup → fades on its own
 → the Sandbells animate, with win boxes and a pulsing "PICK ME" on each
 → the player clicks one → small explosion → every Sandbell reveals its X value (the picked one larger)
 → they keep animating for a while
 → "TOTAL WIN X" popup counts up → the balance takes the amount → the popup fades on its own
 → Spin is live again; Sandbells, boxes and X values stay on screen until the next spin
```

No START and no TAKE button in this feature.

---

## 3. Anticipation

**Once the second Sandbell has landed, every reel still spinning gets the green anticipation layout
and spins longer** — the same layout and the same extended spin as Free Spins.

- Reference: Sandbells on reels 1 and 2 → reels 3, 4 and 5 anticipated.
- Unlike the Drum, every remaining reel can genuinely land a Sandbell, since they land anywhere.
- **Every reel after the one that landed the second Sandbell** is held — Sandbells on reels 1 and 4
  → reel 5 only. Two stacked on one reel count as two (owner, 2026-11-03).
- **It keeps going once the third Sandbell lands** — the remaining reels stay held (owner,
  2026-11-03). Unlike the Drum, which stops holding once the trigger is complete.

---

## 4. Mystery Respin (near miss)

**If the board ends with exactly 2 Sandbells, both on reels 1–4, reel 5 gets one more chance.**
With 3 or more there is no respin — the bonus is already won (owner, §1).

1. The reels land.
2. The **feature popup** shows **"MYSTERY RESPIN"** — the same text as Free Spins: it pops up
   immediately, animates, then fades out.
3. After the fade, **reel 5 respins** with the **orange** anticipation layout — the respin colour
   for all three features (owner).
   **The player then presses Spin to start it** (owner, 2026-11-04) — the button waits with its
   normal Spin face; on autoplay or in a free-spins round it starts by itself. The popup's sound
   plays with the popup only.
4. It lands:
   - **a Sandbell** → 3 Sandbells, the bonus is won (§5);
   - **no Sandbell** → the spin ends normally (line wins, if any).

**It is one spin, not two** — as for Free Spins, the server decides the respin inside the same
`result` (one request, one bet) and sends the board before and after it (§8.3). The client lands
the first board, shows the popup, then respins reel 5 onto the second.

The client respins when the result says so, and never decides a respin itself.

---

## 5. The bonus

1. The board has landed with 3 or more Sandbells. In the reference, the newly landed Sandbell
   showed **no win box and no animation yet**.
2. The **feature popup** shows **"CONGRATULATIONS! YOU WON SANDBELL PICK BONUS"** and **fades out
   on its own**.
3. **Every Sandbell starts animating** (the Sandbell clip), with a **win box** animating on top of
   it, and a **"PICK ME"** text on top of that, **pulsing** (scaling up and down).
4. **The player clicks one Sandbell.** Only the Sandbells are clickable. **No time limit and no
   automatic pick** (owner, 2026-11-03) — the game waits for the click. **Autoplay pauses at the
   trigger and resumes once the TOTAL WIN popup has faded** (owner, 2026-11-03).
5. A **small explosion** animation plays **on the clicked Sandbell only** (owner, 2026-11-03).
6. **Every Sandbell reveals its X value** — the "PICK ME" text gives way to the value. The picked
   one's value is in a **larger font** than the others'. **The picked Sandbell always shows the
   server's winning value**, whichever one was clicked; the others show the server's missed values
   (§8.2). The client never invents a value. **Which missed value goes where doesn't matter**
   (owner): the client does the simplest thing — the missed values in the order the server sent
   them, on the other Sandbells in board order.
7. They **keep animating for a while** — **(open)** how long.

With 4 or more Sandbells (more than 5 is possible, §1), every one gets "PICK ME" and reveals a value; there is still one pick, and
the extra Sandbells change nothing else (owner).

---

## 6. The total and the return to the base game

1. The **feature popup** shows **"TOTAL WIN X"**, X **counting up from 0** — the same popup and
   count-up as Free Spins' end of round.
2. When the count-up ends, **the amount goes into the balance**.
3. The popup **fades out on its own**. There is **no TAKE button**.
4. **Spin is live again.** The Sandbell animations, the win boxes and the X values **stay on
   screen until the player spins again**.

**Money.**
- **Prize = the winning X value × total bet** — the **server's** figure (its cash amount), never
  computed by the client from the X value it shows. Only the winning value pays; the missed values
  are for show.
- **Balance:** the shown balance waits until the count-up has finished, then shows the **server's**
  balance (the hook is `GameManager.DisplayBalance`).
- **The trigger spin's line wins are paid by the server but not presented in any way** (owner,
  2026-11-03): no line animation, and **not part of TOTAL WIN** — TOTAL WIN is the bonus prize
  alone, `sandbellPickBonus.winInCash`. The balance after the bonus is the server's, so on a board
  that also won lines it moves by more than TOTAL WIN shows; that is accepted.
- **The win box (the UI's win amount) shows GOOD LUCK until TOTAL WIN's count-up is done**, then the
  same amount — the prize (owner, 2026-11-03).
- **No big-win popup** — the big-win popup is switched off game-wide for now (owner, 2026-11-03).

---

## 7. What the client needs, by scene object

Created and placed in the editor by the owner; the code gets fields to wire. Every one is optional
in code — a missing reference skips that step, it never stalls the feature.

**Set up by the owner (2026-11-03).** Paths under `MainCanvas/BG/SlotBG/SlotObject`.

| Object | In the scene |
|---|---|
| Anticipation layouts | shared with Free Spins — `SlotView.anticipationEffects` (reels 2–5), green / orange lists on `SlotView` |
| Feature popup | shared with Free Spins (`FeaturePopupView`); this feature's message is `CongratulationsSandbellPick`, then `TotalWin` |
| Sandbell clip | `SlotView.animSpritesSandbell` — **wired**: `Graphics/Animations/WildJokers/Juggling Pins`, 60 frames (the Sandbell's sprite is its frame 0). Played on the animation layer |
| Win boxes | the 15 line-win boxes (`GameExplanation.md` §5), reused for the Sandbells |
| Pick cells | `AnimationLayer/PickCells/PickColumn (0..4)/PickCell (0..2)` — after `WinBoxes`, before `WinLineAmounts`, so they draw over the boxes; laid out exactly like the win boxes (x −505 / −252.5 / 0 / 252.5 / 505, y 191.617 / −53.383 / −298.383), 236.8 × 250.5. Each: a transparent Image (Raycast on) + Button (Transition None) — the click target — with three children: **`PickMe`** (the PICK ME image, pulses), **`ValueText`** (the X value — a plain TMP text for now, to become the sprite font; Raycast off), **`Explosion`** (Image + `ImageAnimation`, Start On Enable off, inactive, Raycast off). Code switches every cell off at startup and as each spin starts, and on only for the Sandbells during a pick. **`PickCells` itself must be saved on** — the code switches the cells, not their parent (a pick with `PickCells` off shows nothing and waits for a click that can't happen) |
| Explosion clip | `SandbellPickView.explosionFrames` — **wired**: `Graphics/Animations/WildJokers/Explosion`, 29 frames, played once at 24 fps (≈ 1.2 s) on the clicked Sandbell |

Wiring: on its own view, **`SandbellPickView`** (`Feature/SandbellPickView.cs`, created by the
owner 2026-11-03), added to `PickCells`. Explicit, the `winBoxColumns` pattern (owner's choice) —
`pickCellColumns`, 5 columns × 3 cells × 4 fields (`button`, `pickMe`, `valueText`, `explosion`).
The explosion's frames go in one list on the view (`explosionFrames`), handed to all 15 at runtime;
with no frames the explosion is skipped (its Image has no sprite and would show as a white square).
`GameManager.sandbellPickView` points at it. **All wired (2026-11-03)** — the 60 cell references by
the view's editor-only fill button (right-click the component → "Fill pick cells from children"),
checked against the scene.

Known and accepted: the cells are 250.5 tall on a 245 pitch, so stacked Sandbells overlap by ~5 px.

---

## 8. Backend

### 8.1 Init — `features.sandbellPickBonus`

Captured (the 0-indexed init, `Config/SL-WJ_config.json`, updated by the backend 2026-10-30 — the
prize list was 5 … 500 and `triggerProbability` 0.1 before):

```json
"sandbellPickBonus": {
  "enabled": true,
  "respinReel": 4,
  "triggerCount": 3,
  "nearMissReels": [0, 1, 2, 3],
  "availablePrizes": [2, 3, 4, 5, 6, 8, 10, 15, 20, 25, 50],
  "maxWinMultiplier": 500,
  "nearMissCountRange": { "max": 4, "min": 2 },
  "triggerProbability": 1
}
```

| Field | Meaning (as read) | Matches the rules? |
|---|---|---|
| `enabled` | the feature is on | — |
| `triggerCount` | Sandbells needed — 3 | yes ("3, 4 or 5"; more than 5 triggers too, owner) |
| `respinReel` | reel respun on a near miss — reel 5 | yes |
| `nearMissReels` | reels counted for the near miss — reels 1–4 | yes ("on 1-4 reels") |
| `nearMissCountRange` | Sandbells on those reels that earn the respin — 2 to 4 | the rules page, yes ("2, 3 or 4"); **the owner, no — 2 only** (§8.4) |
| `availablePrizes` | the X values a Sandbell can carry, × total bet (confirmed by the trigger capture, §8.2) | yes (rules: "based on your total bet") |
| `maxWinMultiplier` | 500 — the bonus's cap, × total bet | the rules say × 500, but with one pick and a top prize of × 50 it can't be reached (§8.4) |
| `triggerProbability` | was 0.1, now 1. Probably the chance that a board with 3+ Sandbells actually awards the bonus — which would explain a 4-Sandbell board that awarded nothing (§8.3) | the rules say 3+ Sandbells **award** the bonus, so it must be 1 or gone (§8.4) |
| `symbolId` | `9` — **deployed**, in the captured init of 2026-11-02 | — |

**`nearMissCountRange` is a question again.** It was once withdrawn because the rules page backs
2–4. The owner has since settled that there is no respin once 3 Sandbells are on the board, so the
range should be 2 to 2 — and since it counts only reels 1–4, the backend must also skip the respin
when reel 5 already holds a Sandbell. Raise both, along with the rules page's wording (§8.4).

Client binding today: `ServerPickBonusFeature` in `GameDataModels.cs` (shared with Ball Pick'em)
reads `enabled`, `symbolId`, `triggerCount`, `respinReel`, `availablePrizes`, `maxWinMultiplier`;
`nearMissReels` and `nearMissCountRange` are **unbound** — they turn out to be needed for the near
miss and should be bound. `GameConfig.sandbellBonusSymbolId` gets `symbolId` (9);
the rest is not yet copied into `GameConfig`.

### 8.2 Result — `payload.sandbellPickBonus`

**The bonus comes on the trigger spin's own `result`.** The pick is **not** sent to the server: the
prize is decided with the spin, and the click only chooses which Sandbell shows it.

**Captured (2026-10-30) — the shape before the fix.** Bet 0.01 per line, total bet 0.20:

```
matrix: [["5","6","3","4","9"],["5","9","8","5","8"],["2","4","0","9","0"]]
payload: {
  winAmount: 2.6, grandTotalWin: 2.6, lineWins: [], isRespin: false,
  sandbellPickBonus: {
    featureName: "sandbellPickBonus", triggered: true,
    positions: [ {row: 0, col: 4, multiplier: 4}, {row: 1, col: 1, multiplier: 6}, {row: 2, col: 3, multiplier: 3} ],
    items:     (identical to positions),
    totalWinMultiplier: 13, prizeMultiplier: 13, winInCash: 2.6
  }
}
player.balance: 6090.19
```

Three Sandbells — reel 5 row 0, reel 2 row 1, reel 4 row 2 — each with a value from
`availablePrizes`. **The prize was the sum of all three** (4 + 6 + 3 = 13; 13 × 0.20 = 2.60 =
`winInCash` = `winAmount` = `grandTotalWin`). That contradicts the rules page ("Pick a Sandbell to
reveal your prize") and the owner's description (one pick, one prize) — raised, and **the backend is
fixing it**.

**The fix (agreed 2026-10-30) — captured the same day.** Bet 0.01 per line, total bet 0.20:

```
matrix: [["5","2","0","5","6"],["6","2","4","9","9"],["5","7","9","4","9"]]
payload: {
  winAmount: 2, grandTotalWin: 2, netReturnRatio: 10, lineWins: [], isRespin: false, respinCount: 0,
  sandbellPickBonus: {
    featureName: "sandbellPickBonus", triggered: true,
    pickedPosition:    { row: 1, col: 3, multiplier: 10 },
    unpickedPositions: [ {row: 1, col: 4, multiplier: 8}, {row: 2, col: 2, multiplier: 2}, {row: 2, col: 4, multiplier: 3} ],
    totalWinMultiplier: 10, prizeMultiplier: 10, winInCash: 2
  }
}
player.balance: 6098.29
```

What it shows:
- **Four Sandbells** — reel 4 row 1, reel 5 rows 1 and 2 (**stacked**), reel 3 row 2. One
  `pickedPosition` + three `unpickedPositions` = all four cells, so the missed values are **one per
  other Sandbell** (count − 1), stacked ones included.
- **The prize is the one winning value:** × 10; 10 × 0.20 = 2 = `winInCash` = `winAmount` =
  `grandTotalWin`; `netReturnRatio` 10 = the win ÷ the total bet. The missed values (8, 2, 3) are
  for show only. Every value is in `availablePrizes`. **The sum bug is fixed.**
- `positions` / `items` are gone. `totalWinMultiplier` and `prizeMultiplier` are still both sent and
  equal (§8.4).
- **No respin** — 2 Sandbells on reels 1–4 and two more on reel 5. Right, as before (§8.3).
- **No line wins** — checked: none of the 20 lines pays on this board, so `lineWins: []` is right.

**The positions are the server's own layout, not the player's pick.** The server names a cell for
the winning value (here reel 4 row 1), but the player can click any Sandbell, and the winning value
always goes on the clicked one. So the client uses `pickedPosition` + `unpickedPositions` as the
list of Sandbell cells and their values, and only the **values** matter: the winning value goes on
the clicked Sandbell, the missed values on the rest.

- **(open, owner)** Which missed value goes on which unclicked Sandbell? Suggested: if the player
  clicks the server's `pickedPosition`, show everything as sent; otherwise **swap** — the clicked
  cell shows the winning value and the server's picked cell takes the value the server had put on
  the clicked cell; the rest stay as sent. Purely cosmetic either way.

**The client's side, settled:** read the Sandbell cells from the bonus (they match the matrix); on
the click, put the **winning** value (`pickedPosition.multiplier`) on the clicked Sandbell and the
**missed** values on the rest; never compute the prize — the money is `grandTotalWin` / `winInCash`.

### 8.3 Result — `nearMissRespin`

The shape is the same as for the Drum (`BonusFreeSpins.md` §9.3): `triggered`, `reelIndex`,
`originalMatrix`, `newMatrix` (= the result's `matrix`), `featureName`; plus `isRespin` /
`respinCount` on the payload.

The only respin captured with Sandbells on the board (a sample from the backend team):

```
originalMatrix: [["0","4","6","0","4"],["1","8","8","6","6"],["9","6","6","9","5"]]
newMatrix:      [["0","4","6","0","5"],["1","8","8","6","6"],["9","6","6","9","6"]]
reelIndex: 4, featureName: "ballPickem"
```

Two Sandbells (reels 1 and 4, row 2) **and** two Balls (reels 2 and 3, row 1) on reels 1–4 — a near
miss for **both** pick bonuses, which share reel 5 as their respin reel. Only `"ballPickem"` was
reported. **(open, backend)** Should `featureName` name both — or become a list? One reel-5 respin
can serve both features.

**A Sandbell respin, captured (owner, before the 2026-10-30 config update):**

```
originalMatrix: [["2","9","6","5","6"],["9","6","4","9","5"],["3","9","0","5","5"]]
newMatrix:      [["2","9","6","5","8"],["9","6","4","9","4"],["3","9","0","5","6"]]
reelIndex: 4, featureName: "sandbellPickBonus"; winAmount 0, no bonus data; balance 6088.19
```

- **`featureName` is `"sandbellPickBonus"`** — it matches the init key (the Ball's doesn't, §8.4).
- **Stacked Sandbells are real:** reel 2 has two (rows 0 and 2).
- **The respin fired with 4 Sandbells already on reels 1–4** — the bonus was already won. Against
  the owner's rule (§1).
- **And the bonus wasn't awarded** — no bonus data, nothing paid (the next spin's balance was
  6087.99, exactly one 0.20 bet less). Probably `triggerProbability` (then 0.1, §8.1).

**No respin when reel 5 already has the third — confirmed.** The trigger capture (§8.2) had 2
Sandbells on reels 1–4 and the third on reel 5, and the backend did **not** respin.

### 8.4 Known backend problems and questions

1. ~~Deploy `symbolId: 9` on `features.sandbellPickBonus`.~~ **Done** — in the init of 2026-11-02.
2. ~~The prize fix~~ — **fixed and captured:** `pickedPosition` (the win) + `unpickedPositions`
   (the missed values) (§8.2). Still wanted: a trigger capture **after a respin**. And
   `totalWinMultiplier` / `prizeMultiplier` — both sent, equal: which stays?
3. A near miss for Ball and Sandbell at once reports only one feature (§8.3).
4. **Stacked Sandbells** — confirmed they happen (§8.3). Confirm the count is of cells, and that 6+
   triggers the bonus (owner's rule for now) — the rules page only says "3, 4 or 5".
5. **The respin is for exactly 2 Sandbells (owner).** No respin once 3 or more are on the board.
   - **2 on reels 1–4 + a third on reel 5: already right** — no respin (§8.3).
   - **3 or 4 on reels 1–4: still respins** (captured, §8.3) — `nearMissCountRange` (2–4) should be
     2–2. The rules page's "2, 3 or 4" needs rewording too.
6. ~~The Sandbell's `featureName`~~ — **answered:** `"sandbellPickBonus"`, matching the init (§8.3).
7. **`triggerProbability`** — if it is the chance a 3+ Sandbell board awards the bonus, it must be 1
   (or removed): the rules say 3+ Sandbells award it. A 4-Sandbell board awarded nothing while it
   was 0.1 (§8.3).
8. **`maxWinMultiplier` 500 can't be reached** — one pick, top prize × 50. Is the cap or the rules
   page's "× 500" out of date, or the prize list?
9. Does the trigger spin's balance already include the bonus prize? (`winAmount` does; the balance
   needs the previous spin's figure to tell.)
10. Never trigger during free spins (rules: "during the basic game").

---

## 9. Client implementation — built (2026-11-03)

Built in code, wired, and working in the editor (owner, 2026-11-03). Reuses what Free Spins built
(`BonusFreeSpins.md` §10). Every piece falls through when its reference is missing.

**Model — `GameDataModels.cs`.** `payload.sandbellPickBonus` → `ServerSandbellPickBonus` (with
`ServerPickPosition`s) → `SpinResult.sandbellPick` (`SandbellPickData`): `cells` (every Sandbell,
flat, board order — by reel, then row), `winValue`, `missedValues` (in the order sent), `prize` =
`winInCash`. Null unless the block is `triggered` with a `pickedPosition`.

**Reels — `SlotView`.**
- `ComputeAnticipatedReels` is the union of each feature's rule. `AddSandbellAnticipation`: once two
  Sandbell cells have landed, every later reel is held, and it keeps going after the third — never
  during free spins. The Sandbell has its landing cue back.
- The Sandbell clip's speed is 60 (`SymbolAnimationSpeeds`) — ≈ 2.5 s a loop.
- `ShowFeatureTriggerBoxes(cells)`: the Sandbells play their clip with their win boxes, looping until
  the next spin. `SetSymbolTapsBlocked` shuts the symbol info card during the pick.

**Pick cells — `SandbellPickView`** (its own view; `SlotView` stays about the reels).
- `ShowPick(cells, reelCount, onPicked)`: each Sandbell's `PickCell` on, PICK ME pulsing; a click
  calls back once. With no cell wired to click, the first Sandbell is taken as the pick, so the
  feature can't wait forever.
- `Reveal(picked, winValue, missedValues)`: PICK ME off; "X" + value on every Sandbell — the winning
  one on the clicked cell, `PickedValueScale` larger, the missed ones on the rest in board order; the
  explosion once on the clicked cell (skipped until `explosionFrames` has frames).
- `HideImmediate`: every cell off. `GameManager` calls it at init and as every spin starts, so the
  cells only show during a pick and its reveal. No Start / Awake work: the view sits under the
  animation layer, which is switched on and off. It never switches its own object (`PickCells`) —
  that stays on in the scene (§7).
- **Editor only:** "Fill pick cells from children" (a `[ContextMenu]`, inside `#if UNITY_EDITOR`)
  fills `pickCellColumns` from the hierarchy by structure, undoable, logging anything missing.

**Popup — `FeaturePopupView`.** `congratulationsSandbellPick` + `ShowSandbellPickAwarded` (hold,
fade). TOTAL WIN reuses `ShowTotalWin` / `HideTotalWin`.

**Controller — `GameManager.PlaySandbellPick`**, run by `PlayFeatureSequence` (2026-11-06), which
takes the game over first and hands it back after — so it plays alongside the other features. From
`PresentSpinOutcome` on any spin carrying the bonus (after a Sandbell respin too —
`GetPlayableRespin` takes `"sandbellPickBonus"`). Taken over: autoplay parked, buttons locked, the
balance held at `balanceBeforeResult`, GOOD LUCK in the win box, no line wins shown, the spin closed
out → CONGRATULATIONS → the boxes (`SlotView`) and the pick (`SandbellPickView`) → the reveal,
`SandbellRevealHold` → TOTAL WIN counts up to the prize → the win box shows the prizes so far and
the held balance steps up by the prize (`ShowPickPrize`) → `TotalWinStayHold` → the popup fades.
Then the next feature, or the hand-back: the win box on the spin's total, the server's balance, Spin
live (`ReleaseSpinButton(true)`), autoplay resumed. Inside a Free Spins round the win box and the
balance are the round's and left alone. The balance hold is general (`balanceHeld` /
`heldBalance`), shared with Free Spins.

**Big win — off game-wide.** `GameManager.BigWinPopupEnabled = false`; `IsBigWin` guards both
checks.

**Values (2026-11-04).** `Reveal` writes each value with `SpriteTextFormatter.ToSpriteMultiplier`
("x" = sprite 11, then the digits) and sets the text's sprite asset first:
`SandbellPickView.pickedValueSpriteAsset` (`1-0 GREEN`) on the clicked Sandbell,
`missedValueSpriteAsset` (`0-1ORANGE`) on the rest. An empty field leaves the text's own asset. The
clicked value is still drawn 1.5× larger.

**Wiring (owner)** — done (2026-11-03), except the two value sprite assets (2026-11-04).

---

## 10. Open questions

**Settled — the rules page**
- Trigger: 3 or more Sandbells anywhere on the board. Base game only — overruled by the backend
  (2026-11-06): it can trigger in Free Spins too. One pick; prize = X value ×
  total bet, max × 500. (§1)

**Owner — answered**
- Respin only on a near miss: exactly 2 Sandbells on reels 1–4 → reel 5 respins. None once 3 or
  more are on the board. (§1, §4)
- More than one Sandbell can land on a reel; more than 5 triggers the bonus too (for now). (§1)
- More than 3 Sandbells changes nothing. (§1, §5)
- The popup reads "MYSTERY RESPIN", the same as Free Spins. (§4)
- The trigger spin's line wins are paid by the server but not presented, and not part of TOTAL WIN.
  (§6)
- Anticipation: every reel after the second Sandbell, and it keeps going after the third. (§3)
- The explosion: on the clicked Sandbell only. (§5)
- No time limit and no automatic pick. (§5)
- Which missed value goes where doesn't matter — the simplest: in the order sent. (§5)
- The win box: GOOD LUCK until the count-up is done, then the prize. (§6)
- No big-win popup — switched off game-wide for now. (§6)
- Scene objects set up; explicit wiring (the `winBoxColumns` pattern). (§7)

- Autoplay pauses at the trigger and resumes after the TOTAL WIN popup has faded. (§5)

**Owner — open**
1. A spin that wins the Sandbell Pick together with Free Spins or Ball Pick'em — thought not to
   happen; if it can, Free Spins is presented and the Sandbell Pick ignored.
2. Timings: the reveal hold, TOTAL WIN's stay, the count-up, the popup holds — placeholders in
   `GameManager` (`SandbellRevealHold`, `TotalWinStayHold`) and `FeaturePopupView`. (§5, §6)

**Backend — answered by captures**
- The bonus comes on the trigger spin's `result`; the pick is not sent. (§8.2)
- Values are × total bet. (§8.2)
- `featureName` is `"sandbellPickBonus"`. (§8.3)
- Stacked Sandbells happen. (§8.3)
- No respin with 2 on reels 1–4 and the third on reel 5. (§8.3)
- The prize fix: one winning value (`pickedPosition`) + the missed values (`unpickedPositions`),
  one per other Sandbell; prize = winning value × total bet. (§8.2)

**Backend — open** (see §8.4)
1. ~~Deploy `symbolId: 9`.~~ Done (2026-11-02).
2. A trigger capture after a respin; `totalWinMultiplier` vs `prizeMultiplier` — which stays.
3. A double near miss (Ball + Sandbell) names only one feature.
4. Stacked Sandbells: count by cell, 6+ triggers?
5. Respin with 3–4 already on reels 1–4 — `nearMissCountRange` 2–4 should be 2–2.
6. `triggerProbability` must be 1 (or gone).
7. `maxWinMultiplier` 500 unreachable with a top prize of × 50.
8. Does the trigger spin's balance include the prize?
9. Never trigger during free spins.
