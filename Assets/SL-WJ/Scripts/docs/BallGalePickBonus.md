# Ball Gale Pick Bonus — Wild Jokers

The full description of Wild Jokers' Ball Gale Pick Bonus: what the player sees, step by step, what
the backend sends for it, what is still unknown or wrong on the backend, and how it maps onto the
client. Written so a programmer or an agent can build it from this file alone.

**Status:** described by the owner (2026-10-29). **Built in code (2026-11-03) and wired
(2026-11-04); not yet tested in the editor** — §10. One trigger captured (2026-10-30):
the whole pick sequence comes with the spin (§9.2). It also sends a per-gem `grid`: the backend says
ignore it, it will be removed later. One sample shows a reel-5 respin reported as `"ballPickem"` (§9.3). The glare and break
art, the gem borders and the glow are in; the COLLECT image and the gems' "x" sprite are still to come (§8).

**Authority:** the game's rules page and the owner's description are the spec. Where the backend
disagrees, the **backend is wrong** and gets changed. Anything marked **(open)** is unconfirmed;
the full list is in §11. The owner's original notes are kept word for word at the end.

Related: `SandbellPickBonus.md` (the other pick bonus — same trigger rules and respin reel),
`BonusFreeSpins.md` (shares the feature popup, the TAKE button and the logo), `GameExplanation.md`
(how the project works), `ToDo.md` (what is open).

---

## Terms used here

- **One feature, three names.** The rules page calls it the **"Ball Pickem Bonus"**, the screen says
  **"BALL GALE PICK"**, the backend's key is **`ballPickemBonus`** (and its near miss says
  `"ballPickem"`, §9.3). This file uses "Ball Gale Pick"; on-screen text is quoted exactly.
- **Reels are numbered 1–5, left to right.** The backend uses **0-based** indexes; where a backend
  value is quoted, the 0-based index is given too — e.g. "reel 5 (index 4)".
- **Rows** are 0 (top) to 2 (bottom). The board is 5 reels × 3 rows.
- **Ball** — symbol id **8**, group `scatter`. Lands on **every reel**, and the backend's strips allow
  3 per reel, so more than one can land on a reel. **It does not animate** (`GameExplanation.md` §5).
- **Win box** — the animated square overlay drawn over a cell (`GameExplanation.md` §5).
- **Feature popup** — the same popup object as in `BonusFreeSpins.md` (`FeaturePopupView`): a
  rotating backdrop, a frame, one child per message (this feature's is `CongratulationsBallGale`),
  a `CanvasGroup` on the root for fading.
- **Anticipation layout** — the Image + `ImageAnimation` around a reel, as in `BonusFreeSpins.md`.
- **Bonus panel** — the frame of gems that replaces the reels during the bonus (§6).
- **Gem** — one of the 20 pickable jewels on the bonus panel. The rules page calls them "jewels".
- **Multiplier box** — the purple box in the middle of the bonus panel holding the running total.
- **X value** — what a picked gem reveals: a multiplier of the **total bet**, e.g. "X2" — or
  **COLLECT**, which ends the picking.

---

## 1. The rules (game rules page — authoritative)

> 3, 4 or 5 Ball bonus symbols anywhere in view during the basic game award the Ball Pickem Bonus.
> Pick jewels to reveal prizes. keep picking until you reveal collect or reach the maximum win for
> the bonus.
> 2, 3 or 4 Ball symbils on 1-4 reels initiate the respin feature on the fifth reel. The prize is
> based on your total bet. Max win from the bonus is total bet x500.

Read as:

- **Trigger:** **3 or more Balls anywhere on the board** — a count, not one per reel (unlike the
  Drum).
- **Base game and Free Spins** (backend, 2026-11-06 — the rules page said base game only). Inside a
  round it pauses the round, plays, and the round carries on; the slide fades the FREE GAME counter
  instead of the logo (`freeGamesCounterGroup`).
- **It can land with the other features on one spin** (backend, 2026-11-06): after the Sandbell
  Pick, before Free Spins (`GameExplanation.md`, "Several features on one spin").
- **Many picks.** The player keeps picking gems; each reveals an X value, added to a running total.
  Picking ends when a gem reveals **COLLECT** (worth nothing). **The server decides when picking
  ends**; the client never tracks a cap itself.
- **Prize = the total multiplier × total bet.**
- **× 500 — two readings.** The owner reads it as the **highest prize one gem can show**, not a cap
  on the total. The rules page says "Max win from the bonus is total bet x500" and "keep picking
  until you reveal collect or reach the maximum win for the bonus", which reads as a cap on the
  total. It doesn't change the client — the server says when picking ends — but it matters for the
  backend's prize list (§9.4). Since the 2026-10-30 config the top gem is × 25, so the per-gem
  reading no longer fits; 20 gems × 25 = 500 exactly, which suggests 500 is the most a round can
  show (§9.4).
- **Respin:** Balls on reels 1–4 → **reel 5** respins, a chance at the third. Whether that is 2 only
  or 2–4 is the same open question as the Sandbell's (§4).

---

## 2. The flow at a glance

From the reference video (two Balls on reels 1 and 2, the respin lands the third):

```
Base spin
 ├─ 2 Balls land (reference: reels 1 and 2) — NO anticipation on the remaining reels
 ├─ the board ends with 2 Balls on reels 1–4
 │    └─ "MYSTERY RESPIN" popup → fades → reel 5 respins with the ORANGE anticipation layout
 │         └─ lands a Ball (reference) ─────────────────┐
 └─ 3+ Balls ───────────────────────────────────────────┤
                                                        ▼
win boxes on every Ball
 → "CONGRATULATIONS! YOU WON BALL GALE PICK BONUS" popup → fades on its own
 → no button: the reels slide down off screen, the logo fades out, the bonus panel slides up
 → 20 gems around the multiplier box; random groups of gems glare, in sync
 → the player clicks a gem → it breaks and falls → its border stays, showing an X value
      → the X value is added to the multiplier box's total
      → repeat until a gem shows COLLECT (the server decides)
 → a glow plays behind the box's total
 → "TOTAL WIN X" popup counts up → TAKE
 → on TAKE: the popup fades out → the bonus panel slides down → the logo fades in
   → the reels slide back up → normal spins
```

No START button. TAKE only at the end.

---

## 3. Anticipation

**None before the respin.** In the reference, two Balls landed on reels 1 and 2 and the remaining
reels showed **no anticipation layout and no other effect** — unlike the Sandbell, whose second
landing anticipates every reel still spinning.

- **Answered (owner, 2026-11-03):** the Ball never anticipates.

---

## 4. Mystery Respin (near miss)

**If reels 1–4 end with 2 Balls, reel 5 gets one more chance.**

1. The reels land.
2. The **feature popup** shows **"MYSTERY RESPIN"** — the same text and behaviour as the other
   features: it pops up immediately, animates, then fades out.
3. After the fade, **reel 5 respins** with the **orange anticipation layout** on it.
   **The player then presses Spin to start it** (owner, 2026-11-04) — the button waits with its
   normal Spin face; on autoplay or in a free-spins round it starts by itself. The popup's sound
   plays with the popup only.
4. It lands:
   - **a Ball** → 3 Balls, the bonus is won (§5);
   - **no Ball** → the spin ends normally (line wins, if any).

**It is one spin, not two** — as for the other features, the server decides the respin inside the
same `result` (one request, one bet) and sends the board before and after it (§9.3). The client lands
the first board, shows the popup, then respins reel 5 onto the second. The client respins when the
result says so, and never decides a respin itself.

**With 3 or 4 Balls already on reels 1–4** — the same open question as the Sandbell
(`SandbellPickBonus.md` §1, §8.4): the rules page and the backend (`nearMissCountRange` 2–4) say the
respin still happens, possibly because each extra scatter adds to what the player picks from. For
this feature that reading doesn't apply directly — the gem count is fixed at 20 (§6), not set by the
Balls. Waiting on the backend's answer for the Sandbell; **(open)** the same rule for the Ball?

**Orange is the respin colour for all three features** (owner) — the same orange layout on whichever
reel respins.

---

## 5. The trigger

1. The board has landed with 3 or more Balls.
2. **A win box shows on every Ball** — the Balls themselves stay static.
3. The **feature popup** shows **"CONGRATULATIONS! YOU WON BALL GALE PICK BONUS"** and **fades out
   on its own**. No button.
4. The transition in (§6) starts by itself.

- **Answered (owner, 2026-11-03):** the trigger spin's line wins are paid by the server but not
  shown — as for the Sandbell.

---

## 6. The bonus panel

### 6.1 Transition in

With no button press, in this order:

1. **The reels slide down** below the bottom of the screen — the **`SlotObject`** object (§8).
2. **The logo above the reels fades out.**
3. **The bonus panel slides up** from below the screen into the reels' place.

Everything under `SlotObject` goes with it — the board, the dim and the win boxes included — so
the trigger board slides away as it was left. **Not `SlotBG`** (owner, 2026-11-03): the gem panel and
the feature popup sit under `SlotBG` too, and must stay on screen.

- **Answered (owner, 2026-11-03):** one after another — reels down, logo out, panel up; the return
  runs in reverse. Durations are placeholder consts (§10).

### 6.2 Layout

Four rows of gems, 20 in all, with the multiplier box in the middle:

```
 ◆  ◆  ◆  ◆  ◆  ◆        row 1 — 6 gems
 ◆  ◆ ┌──────┐ ◆  ◆      row 2 — 4 gems
 ◆  ◆ └──────┘ ◆  ◆      row 3 — 4 gems
 ◆  ◆  ◆  ◆  ◆  ◆        row 4 — 6 gems
```

The **multiplier box** is purple and reads, top to bottom: **"BALL GALE PICK" / "TOTAL MULTIPLIER" /
*the total* / "BONUS"**. Before the first pick the total's line is empty (owner: once a gem is
picked "the number takes up the space in the middle").

- The box spans both middle rows, in place of the two middle gems of each — the backend's board is
  4 × 6 with a 2 × 2 `centerBox` there (§9.2). The art may still differ; the owner places it.

### 6.3 Idle — the glare

Unpicked gems play a **glare animation**: a **random group of gems plays it together, in sync** —
all starting on the same frame, never staggered — then another random group, and so on.

- **Answered (owner, 2026-11-03):** 3–5 gems a group, one group every 1.5 s (placeholders).
- Picked gems are broken (§6.4), so they never glare.

### 6.4 A pick

1. **The player clicks a gem.** Only unpicked gems are clickable.
2. **Every other gem stops taking clicks** until this pick has finished (owner, until confirmed —
   see below).
3. The **X value** (e.g. "X2") goes up on the gem's border, behind the gem (owner, 2026-11-09).
4. The gem plays its **break animation** over it — it shatters and **falls down**, leaving the
   **border** in its place with the value inside it.
5. The **multiplier box's total** adds that value: reference — X2 → total X2; X1 → X3; X2 → X5.
6. The other gems take clicks again; the player picks again.

Picking ends when a gem shows **"COLLECT"** — reference: the 4th pick. **COLLECT is worth nothing**:
the total stays as it was. When picking ends is the **server's** call: the pick with **`isEnd`** is the
last, and a pick with **`isCollect`** shows the COLLECT gem (§9.2). The backend says the two are
always the same; the client reads each for its own job. (The server sends `revealed: "end"`.)

**Unpicked gems are not revealed** at the end — they stay as gems.

- **Answered (owner, 2026-11-03):** every gem is locked until the pick has finished (break, value,
  total); the fall is part of the break frames (`GemStone blast`); no time limit and no automatic
  pick; autoplay is paused at the trigger and resumes after TAKE.
- **COLLECT** is written in the gem's value text until its image arrives (owner).

### 6.5 The end of the picking

1. A **glow animation** plays **behind the total** in the multiplier box.
2. When the glow finishes, the **feature popup** shows **"TOTAL WIN X"**, X **counting up from 0**
   to the win — the same popup and count-up as Free Spins' end of round.
3. The game **waits for TAKE** — the same TAKE as Free Spins (§7).

- **Answered (owner, 2026-11-03):** the glow loops until TAKE. Its art is still to come; until then
  it is skipped.

---

## 7. TAKE and the return to the base game

1. **The player presses TAKE.**
2. **The popup fades out** — fully — before anything else moves.
3. **The bonus panel slides down** below the screen.
4. **The logo fades back in.**
5. **The reels (`SlotBG`) slide back up** into place.
6. **The balance updates** to the server's figure. Back to normal spins.

**Money.**
- **Prize = the total multiplier × total bet** — reference: X5 → 5 × total bet. Always the
  **server's** figure, never computed by the client from the values it shows.
- **Balance:** **frozen until TAKE** (owner), as in Free Spins — the shown balance updates only once
  TAKE is pressed. The hook is `GameManager.DisplayBalance`.
- **Answered (owner, 2026-11-03):** the reels come back showing the trigger board, its win boxes
  cleared. The win box shows GOOD LUCK during the bonus (as in the reference) and the prize after
  TAKE. No big-win popup — it is off game-wide for now.

---

## 8. What the client needs, by scene object

Created and placed in the editor by the owner; the code gets fields to wire. Every one is optional
in code — a missing reference skips that step, it never stalls the feature.

| Object | What it is |
|---|---|
| Anticipation layout, reel 5 | the existing reel-5 layout in `SlotView.anticipationEffects`, with an **orange** frame list for the respin |
| Feature popup | shared with the other features; texts MYSTERY RESPIN, CONGRATULATIONS! YOU WON BALL GALE PICK BONUS, TOTAL WIN |
| Win boxes | the 15 line-win boxes (`GameExplanation.md` §5), reused on the Balls |
| `MainCanvas/BG/SlotBG/SlotObject` | what slides down and up (reel background, `SlotMask` with the reels, `WinDimOverlay`, `AnimationLayer` with the win boxes, `AnticipationAnimations`, `SpinWinText`, `InfoCard`). **Not `SlotBG`**: it also holds `FreeGames`, the gem panel and `FeaturePopupView`. The logo is not under either |
| Logo | the existing logo above the reels, faded out and in — shared with Free Spins |
| Bonus panel | **`SlotBG/BallGalePick`** (owner, 2026-11-03), a sibling of `SlotObject`; holds `BallGalePickView`. Its saved position is where it rests; the code switches it off at init and slides it up from below. Children: `SlotShed` (the frame, `Panel_2.0`) → `BallGaleBoard` and `Gems` |
| Gems (20) | `Gems/GemRow (0..3)/Gem (n)` — 6/4/4/6, the middle rows named 0, 1, 4, 5. Each `Gem (n)`: Image (`GemStone rays_00000`, Raycast **off**) + `ImageAnimation` (no frames — the view hands over the glare, `GemStone rays` 26, and the break, `GemStone blast` 20); one child, **`GaleButton`** (alpha-0 Image + Button, 180×180 — the click target; the gem's own 368×500 rect overlaps its neighbours). **Borders:** `SlotShed/GemBorders/BorderRow (0..3)/Border (n)` (`Gemstone_2`), one per gem in the same place, a sibling **before** `Gems` so every border draws behind every gem. Always on: the gem covers it until the break turns the gem's Image off. **What a gem reveals sits on its border** (owner, 2026-11-05): **`GemValueText`** (TMP, sprite asset `GoldText`, Raycast off — X value in sprite digits, "x" = sprite 11) and **`CollectImage`** (`collect.png`, Preserve Aspect, Raycast off, saved off — shown instead of the value on COLLECT). A border must carry no Button (the fill menu would take it for a gem). Being behind every gem, a revealed value has the neighbours' glare and blast drawn over it |
| Multiplier box | `BallGaleBoard` (`bonusbg`) with `BallGalePick`, `TotalMultiplierImage`, `Bonus` images and **`TotalMultiplierValue`** (TMP, sprite asset `0-1ORANGE` — "X" + total in sprite digits; the code empties it). **Glow:** an Image + `ImageAnimation` behind the total — to add, no art yet |
| CONGRATULATIONS | `FeaturePopupView/PopupImage/Popup/CongratulationsBallGale` — four images (its last child is still named `FreeSpinsText`; only the name) |
| TAKE | the Spin button in its `FeatureTake` mode (`UIManager`) — the same as Free Spins |

---

## 9. Backend

### 9.1 Init — `features.ballPickemBonus`

Captured (the 0-indexed init, `Config/SL-WJ_config.json`, updated by the backend 2026-10-30 — the
prize list was 5 … 100, `triggerProbability` 0.1 and there was no `picksCount` before):

```json
"ballPickemBonus": {
  "enabled": true,
  "picksCount": 5,
  "respinReel": 4,
  "triggerCount": 3,
  "nearMissReels": [0, 1, 2, 3],
  "availablePrizes": [1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 25],
  "maxWinMultiplier": 500,
  "nearMissCountRange": { "max": 4, "min": 2 },
  "triggerProbability": 1
}
```

| Field | Meaning (as read) | Matches the rules / the reference? |
|---|---|---|
| `enabled` | the feature is on | — |
| `picksCount` | **ignored** (owner, 2026-10-30) — not needed by the client. (Not the number of picks: the captured round ended on its 3rd pick.) | — |
| `triggerCount` | Balls needed — 3 | yes ("3, 4 or 5"); the trigger capture had 5 |
| `respinReel` | reel respun on a near miss — reel 5 | yes |
| `nearMissReels` | reels counted for the near miss — reels 1–4 | yes ("on 1-4 reels") |
| `nearMissCountRange` | Balls on those reels that earn the respin — 2 to 4 | the rules page, yes; open, as for the Sandbell (§4) |
| `availablePrizes` | the X values a gem can carry, × total bet (confirmed by the trigger capture, §9.2) | yes — has the reference's X1 and X2 |
| `maxWinMultiplier` | 500 — × total bet | see §1 and §9.4: with 20 gems topping at × 25, 500 is exactly 20 × 25 |
| `triggerProbability` | was 0.1, now 1 — probably the chance a board with 3+ Balls awards the bonus (`SandbellPickBonus.md` §8.1) | the rules say 3+ **award** it, so it must be 1 or gone |
| `symbolId` | `8` — **deployed**, in the captured init of 2026-11-02 | — |

Client binding today: `ServerPickBonusFeature` in `GameDataModels.cs` (shared with the Sandbell)
reads `enabled`, `symbolId`, `triggerCount`, `respinReel`, `availablePrizes`, `maxWinMultiplier`;
`nearMissReels` and `nearMissCountRange` are unbound; `picksCount` stays unbound (ignored). `GameConfig.ballBonusSymbolId`
gets `symbolId` (8).

### 9.2 Result — `payload.ballPickemBonus`

**The whole bonus comes on the trigger spin's own `result`.** No pick is sent to the server: the
sequence of picks is decided with the spin, and each click only chooses **which gem shows the next
pick** — as for the Sandbell.

**Captured (2026-10-30).** Bet 0.01 per line, total bet 0.20:

```
matrix: [["5","0","8","5","4"],["8","5","3","2","8"],["8","8","3","3","5"]]
payload: {
  winAmount: 8, grandTotalWin: 8, netReturnRatio: 40, lineWins: [], isRespin: false, respinCount: 0,
  ballPickemBonus: {
    featureName: "ballPickemBonus", triggered: true,
    triggerPositions: [ {row:0,col:2}, {row:1,col:0}, {row:1,col:4}, {row:2,col:0}, {row:2,col:1} ],
    gridDimensions: { rows: 4, cols: 6 },
    centerBox: [ {row:1,col:2}, {row:1,col:3}, {row:2,col:2}, {row:2,col:3} ],
    picks: [
      { pickIndex: 0, multiplier: 15, revealedPrize: 15, revealed: "x15", isEnd: false, isCollect: false },
      { pickIndex: 1, multiplier: 25, revealedPrize: 25, revealed: "x25", isEnd: false, isCollect: false },
      { pickIndex: 2, multiplier: 0,  revealedPrize: 0,  revealed: "end", isEnd: true,  isCollect: true  }
    ],
    grid: 4 rows × 6 cells, each { row, col, isCenterBox, picked, revealed, multiplier, isEnd }:
      row 0:  x6   x8   x25  x20  x15  x2
      row 1:  x5   x6   [box][box]  x25  x1
      row 2:  x5   x8   [box][box]  end  x25
      row 3:  x3   x2   x25  x5   x5   x5
      — every cell "picked": false; the box cells "CENTER_BOX", multiplier 0
    totalWinMultiplier: 40, winInCash: 8
  }
}
player.balance: 6097.99
```

What it shows:
- **The trigger.** Five Balls — reel 3 row 0, reel 1 rows 1 and 2 (**stacked**), reel 5 row 1, reel 2
  row 2. `triggerPositions` lists exactly the Ball cells — the win boxes' cells (§5).
- **No respin.** 4 Balls on reels 1–4 and one on reel 5: no `nearMissRespin`, the bonus awarded.
- **The picks, in order:** × 15, × 25, then **COLLECT** (`revealed: "end"`, `isEnd` and `isCollect`
  both true, worth 0). **The total is the sum** — 15 + 25 = 40 = `totalWinMultiplier` — which is right
  for this bonus (a running total), unlike the Sandbell's.
- **Money:** 40 × 0.20 = 8 = `winInCash` = `winAmount` = `grandTotalWin`; `netReturnRatio` 40 = the
  win ÷ the total bet. Every value is in `availablePrizes`.
- **No line wins** — checked: none of the 20 lines pays on this board, so `lineWins: []` is right.
  Whether a trigger spin's line wins are shown and paid is still open (§5).
- **The board layout.** `gridDimensions` 4 × 6 with `centerBox` the middle two cells of rows 2 and 3
  — the same 6 / 4 / 4 / 6 layout as the reference, the box spanning both middle rows (§6.2).

**The client's side, settled (owner):** on the *n*-th click, show `picks[n]` on the clicked gem and add its
multiplier to the box's total; show the COLLECT gem when a pick has `isCollect`;
stop when a pick has `isEnd` (the backend says they are always equal; owner's choice of which does
what). The money is the server's
`grandTotalWin` (or `winInCash`), never the client's sum.

**`grid` — ignore it (backend, 2026-10-30: use only `picks` / `pickIndex`; `grid` will be removed
from the responses later).** It gives every gem a value of
its own — one of them "end" — but the picks carry no position and every cell says `picked: false`.
Read literally, the player's first click would have had to land on the only × 15, at row 0 col 4.
The owner's call: **the backend sends only the picks, and the client places each one under the gem
the player picks.** The client never reads `grid`, so its removal changes nothing. `gridDimensions` and
`centerBox` aren't needed either — the panel's layout is fixed in the scene.

### 9.3 Result — `nearMissRespin`

The shape is the same as for the Drum (`BonusFreeSpins.md` §9.3): `triggered`, `reelIndex`,
`originalMatrix`, `newMatrix` (= the result's `matrix`), `featureName`; plus `isRespin` /
`respinCount` on the payload.

The only respin captured for this feature (a sample from the backend team):

```
originalMatrix: [["0","4","6","0","4"],["1","8","8","6","6"],["9","6","6","9","5"]]
newMatrix:      [["0","4","6","0","5"],["1","8","8","6","6"],["9","6","6","9","6"]]
reelIndex: 4, featureName: "ballPickem"
```

Two Balls (reels 2 and 3, row 1) **and** two Sandbells (reels 1 and 4, row 2) on reels 1–4 — a near
miss for both pick bonuses; only the Ball was reported. Note `"ballPickem"` ≠ the init's key
`"ballPickemBonus"` — which the bonus itself does use (§9.2) — while the Sandbell's near miss does
say `"sandbellPickBonus"`.

### 9.4 Known backend problems and questions

**Answered by the captures**
- The bonus comes on the trigger spin; no pick requests; the picks arrive in order, ending in
  COLLECT; total = their sum; cash = total × total bet. (§9.2)
- The gem values are from `availablePrizes` (now 1–25), × total bet. (§9.1, §9.2)
- Stacked Balls happen (two on reel 1) and count as separate Balls. (§9.2)
- The board is 4 × 6 with a 2 × 2 centre box. (§9.2)
- **`isEnd` and `isCollect` are always the same** (backend). The client uses `isEnd` to end the
  feature and `isCollect` for the COLLECT gem (owner). (§6.4, §9.2)
- **`picksCount`** — ignored; it doesn't matter to the client (owner). (§9.1)
- **`grid`** — ignore it, use only `picks` / `pickIndex`; the backend removes it later. Nothing
  waits on that. (`gridDimensions` / `centerBox` are unused too.) (§9.2)

**Open**
1. ~~Deploy `symbolId: 8` on `features.ballPickemBonus`.~~ **Done** — in the init of 2026-11-02.
2. **× 500.** The top gem is × 25 and there are 20 gems, so 20 × 25 = 500 is the most any round can
   show — reachable only if all 20 picks reveal × 25 and none is COLLECT. Is that what 500 means (a
   theoretical cap, and the owner's "top prize of one gem" reading falls away), and what does the
   server send if the player picks all 20 without a COLLECT?
3. **Duplicate fields** — `multiplier` / `revealedPrize` / `revealed`, and `winInCash` /
   `winAmount` / `grandTotalWin`: always equal? Which ones stay?
4. **`featureName`** — the respin's `"ballPickem"` should be `"ballPickemBonus"`, as the bonus says.
5. A near miss for Ball and Sandbell at once reports only one feature (§9.3).
6. **6+ Balls** — 5 captured (stacked). Does 6+ trigger? (For the Sandbell the owner says yes.)
7. Is the respin sent with 3 or 4 Balls already on reels 1–4 **and none on reel 5**? The capture
   had 4 plus one on reel 5 and didn't respin — right, but not that case.
8. Does the trigger spin's `player.balance` already include the bonus prize? (Likely — check
   against the spin before's balance. Either way the client holds it until TAKE, §7.)
9. Never trigger during free spins (rules: "during the basic game").

---

## 10. How it was built (2026-11-03)

Built in code; **not yet wired or tested in the editor**. Reuses what Free Spins and Sandbell Pick
built (`BonusFreeSpins.md` §10, `SandbellPickBonus.md` §9). Every piece falls through when its
reference is missing, so nothing stalls a round.

**Model** (`Core/GameDataModels.cs`)
- `ServerPayload.ballPickemBonus` → `ServerBallPickemBonus` (`featureName`, `triggered`, `picks`,
  `totalWinMultiplier`, `winInCash`); each pick a `ServerBallPick` (`pickIndex`, `multiplier`,
  `isEnd`, `isCollect`). Unbound: `grid`, `gridDimensions`, `centerBox`, `triggerPositions`,
  `revealedPrize`, `revealed`.
- `SpinResult.ballGalePick` → `BallGalePickData` (`picks` in `pickIndex` order, each a `BallPick`
  `{value, isCollect, isEnd}`; `prize` = `winInCash`), from `ConvertBallGalePick` — null unless
  triggered with at least one pick.

**Respin** — `GameManager.GetPlayableRespin` accepts `"ballPickemBonus"` and the backend's
`"ballPickem"` (§9.4 item 4), so a Ball near miss gets MYSTERY RESPIN and reel 5's orange respin.
The Ball has no anticipation of its own (§3) — it is in neither `SlotView` rule.

**`FeaturePopupView`** — `congratulationsBallGalePick` and `ShowBallGalePickAwarded` (shows, holds,
fades on its own, like the Sandbell's). TOTAL WIN is the shared `ShowTotalWin` / `HideTotalWin`.

**`BallGalePickView`** (`Feature/BallGalePickView.cs`, on `BallGalePick`) — the panel's own
presentation. DOTween only, no Start / Awake work (the object is switched off between bonuses).
- `ResetToBaseGame` (init): the reels on and in place, the panel off, every gem whole, the logo on.
  The rest positions and each gem's rest sprite are read from the scene the first time.
- `EnterBonus`: gems whole and locked, total empty; `SlotObject` slides down by the canvas's height,
  the logo fades out, the panel switches on and slides up; then the glare starts.
- The glare: one shared timer — every 1.5 s a random 3–5 unpicked, idle gems play `glareFrames` once,
  started in the same frame; `StopAnimation` puts the rest frame back.
- `OpenPicking`: the unpicked gems' `GaleButton`s take one click; every gem locks on it. With nothing
  clickable it answers at once (gem -1), so the bonus never waits forever.
- `RevealGem`: first, on the gem's border behind it (`GemBorders`), the value shows ("X" + value in
  sprite digits) or, on COLLECT, the `CollectImage` (the word "COLLECT" in the value text if no image
  is wired) — before the break, so the breaking gem uncovers it (owner, 2026-11-09). Then
  `breakFrames` once at 24 fps; at its end the gem's **Image component** is disabled (not its object
  — the button is its child), and the box shows "X" + the running total (a
  COLLECT adds nothing); the callback follows 0.3 s later. Each bonus starts with every value and
  COLLECT image off.
- `EndPicking`: the glare stops; the glow loops if it has frames. `ExitBonus`: the panel slides down
  and goes off, the logo fades in, `SlotObject` slides back up.
- Editor only: "Fill gems from children" (`[ContextMenu]`) fills `gems` by structure — a gem is
  any object with an `ImageAnimation` and a Button inside it, in hierarchy order; a border is any
  object with a TMP text as a direct child and no Button (the box's total excluded). Each gem takes
  the border **in the same place** (within 5 units) — its TMP text as `valueText`, its Image child as
  `collectImage`. Logs a gem with no border, a border with no gem, and a border with no COLLECT image.
- Sounds: the swoosh at the start of the slide in and of the slide out; the glass break as a gem
  starts breaking (`AudioManager.PlayBonusSwoosh` / `PlayGemBreak`).
- Consts: `SlideDuration` 0.6, `LogoFadeDuration` 0.4, `GlareInterval` 1.5, `GlareGroupMin` /
  `Max` 3 / 5, `RevealSettle` 0.3 (placeholders).

**`GameManager.PlayBallGalePick`**, run by `PlayFeatureSequence` (2026-11-06) after the Sandbell
Pick and before Free Spins, on any spin carrying the bonus — the base game or a Free Spins round:
1. Already taken over: ShowingWin, autoplay parked, buttons locked; in the base game the balance
   held, GOOD LUCK in the win box (or the prizes so far, after a Sandbell Pick); the spin closed out.
2. Win boxes on the Balls (`ShowFeatureTriggerBoxes(ballBonusSymbolId)`), symbol taps blocked,
   `TriggerBoxesHold`; CONGRATULATIONS.
3. `EnterBonus(…, inFreeSpins)` — the logo fades, or in a round the FREE GAME counter; the reels'
   boxes cleared while they are off screen.
4. For each pick: `OpenPicking` → the click → `RevealGem(gem, pick, running total)`; stop after the
   pick marked `isEnd` (a warning if none is).
5. `EndPicking`; TOTAL WIN counts up to `prize`; TAKE (`FeatureTake`), waited for in the routine.
6. TOTAL WIN fades fully → `ExitBonus(…, inFreeSpins)` → in the base game the win box shows the
   prizes so far and the held balance steps up by the prize (`ShowPickPrize`) → taps unblocked, the
   button out of TAKE, greyed.
7. Then the next feature, the round going on, or the hand-back: the win box on the spin's total,
   the server's balance, buttons unlocked, Spin live, autoplay resumed.

The panel's total is for show; the money is always the server's `winInCash`.

**Wiring (owner, or on request):**

| Object | Field | Wire to |
|---|---|---|
| `GameManager` | `ballGalePickView` | `BallGalePick` |
| `BallGalePickView` | `slotObject` | `SlotBG/SlotObject` |
| `BallGalePickView` | `ballGalePick` | `SlotBG/BallGalePick` (or leave empty: itself) |
| `BallGalePickView` | `logoGroup` | `BG/Logo` |
| `BallGalePickView` | `freeGamesCounterGroup` | `SlotBG/FreeGames/FreeGamesRemainingText` (has a CanvasGroup) — faded instead of the logo inside Free Spins |
| `BallGalePickView` | `gems` | the "Fill gems from children" menu |
| `BallGalePickView` | `totalText` | `BallGaleBoard/TotalMultiplierValue` |
| `BallGalePickView` | `glareFrames` | `Graphics/Feature/BallGalePick/GemStone rays` (26) |
| `BallGalePickView` | `breakFrames` | `Graphics/Feature/BallGalePick/GemStone blast` (20) |
| `BallGalePickView` | `glow` | later: the glow object, its frames on its own `ImageAnimation` |
| `FeaturePopupView` | `congratulationsBallGalePick` | `Popup/CongratulationsBallGale` |

---

## 11. Open questions

**Settled — the rules page**
- Trigger: 3 or more Balls anywhere on the board. Base game only — overruled by the backend
  (2026-11-06): it can trigger in Free Spins too. (§1)
- Keep picking until COLLECT; prize = total multiplier × total bet. (§1, §6.4)

**Settled — the owner's description and answers**
- No anticipation before the respin; the respin has the orange layout on reel 5. (§3, §4)
- Orange is the respin colour for all three features. (§4)
- Win boxes on the Balls at the trigger; CONGRATULATIONS popup fades by itself; no button. (§5)
- The slide out and in (`SlotBG`), the 20-gem layout, the glare in synced groups, break → border +
  value, running total, glow, TOTAL WIN count-up, TAKE, the slide back. (§6, §7, §8)
- COLLECT is worth nothing. The server decides when picking ends. (§6.4, §9.2)
- Unpicked gems are not revealed. (§6.4)
- The balance updates only on TAKE. (§7)
- Until confirmed otherwise, the other gems are unclickable while a pick finishes. (§6.4)

**Owner — answered (2026-11-03, "use the defaults")**
1. The Ball never anticipates. (§3)
2. ~~The respin with 3–4 Balls already on reels 1–4~~ — the server decides; the client plays whatever
   respin it sends. (§4)
3. The trigger spin's line wins: paid, not shown. (§5)
4. The slide runs one step after another; durations are placeholders. (§6.1)
5. ~~Does the multiplier box span both middle rows?~~ Yes. (§6.2)
6. Glare: 3–5 gems a group, every 1.5 s. (§6.3)
7. Every gem is locked until a pick has finished. (§6.4)
8. The fall is part of the break frames. (§6.4)
9. No time limit, no automatic pick; autoplay paused, resumed after TAKE. (§6.4)
10. The glow loops until TAKE. (§6.5)
11. The reels return showing the trigger board, boxes cleared; GOOD LUCK in the win box during the
    bonus, the prize after TAKE; no big win. (§7)

**Owner — open**
- Art: COLLECT (written as text for now); the gems' sprite sheet with an "x" (sprite 11).
- Timings (the view's consts, `TriggerBoxesHold`).
- A spin that wins both the Sandbell Pick and the Ball Gale Pick plays the Sandbell's only — raise
  with the backend.

**Backend** (see §9.4)
1. ~~Deploy `symbolId: 8`.~~ Done (2026-11-02).
2. × 500 — the 20 × 25 maximum? What if all 20 gems are picked without a COLLECT?
3. Duplicate fields — which stay.
4. `featureName` `"ballPickem"` → `"ballPickemBonus"`.
5. A double near miss (Ball + Sandbell) names only one feature.
6. 6+ Balls trigger?
7. Respin with 3–4 Balls on reels 1–4 and none on reel 5?
8. Does the trigger spin's balance include the prize?
9. Never trigger during free spins.

---

## Appendix — the owner's original notes (2026-10-29, verbatim)

Rules given in the game rules:
3, 4 or 5 Ball bonus symbols anywhere in view during the basic game award the Ball Pickem Bonus.
Pick jewels to reveal prizes. keep picking until you reveal collect or reach the maximum win for the bonus.
2, 3 or 4 Ball symbils on 1-4 reels initiate the respin feature on the fifth reel. The prize is based on your total bet. Max win from the bonus is total bet x500.

Reference video explanation:
2 balls landed in the reels (1 and 2). There was no anticipation outline or any effect for anticipation. But when only 2 landed we had a popup saying “MYSTERY RESPIN” which pops up immediately and then fades away (Same as the other features).Then the 5th reel respins and it has the orange anticipation effect.When a third ball lands in the last reel now, the win boxes show up on the squares that the balls land on.A popup shows up again, similar to the other features, only the words are different, it says “CONGRATULATIONS! YOU WON BALL GALE PICK BONUS”. Same thing, pops up and then fades away.No take button or anything, the slot goes down below the screen and the logo fades away then the another frame comes back up from there and it has gems in it. The first and fourth(last) row have 6 gems, the second and third row have 4 gems.There are 2 gems and then a purple box that says “BALL GALE PICK/ TOTAL MULTIPLIER/ BONUS” and then 2 more gems after that.The gems have a glare animation that plays on some of them randomly. And it will play them in sync, not that one will start playing and another will start playing in between, a set of them start animating at once together.
The player has to click on the gems to pick them. So when they click on a gem there is another animation that plays on the gem which is an animation of the gem breaking and it falls down. An image of the gem border stays in place of the gem and there is a multiplier in there. (Like “X2”)
And the board in the middle now says “BALL GALE PICK/ TOTAL MULTIPLIER/ X2/ BONUS” (The number takes up the space in the middle and keeps adding up based on what the player got.Like in the video, the next gem said X1 so the number in the middle said X3, the next one said X2 so the middle said X5.And then the 4th gem said “COLLECT”The multiplier number in the box in the middle has a glow animation begin behind it. After the animation is over there is the same popup again which says “TOTAL WIN / X” The X is a number that counts up to the total win amount.Now it waits for the player to press the take button. After the player presses take, the popup fades away, and after it has faded away entirely, the ball gale pick panel/slot/whatever goes down below the screen, and the logo fades back in and then the main game’s slot comes back up in its place and we’re back to normal spins.
