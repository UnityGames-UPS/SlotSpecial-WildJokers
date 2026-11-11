# Free Spins (Drum) — Wild Jokers

The full description of Wild Jokers' Free Spins feature: what the player sees, step by step, what
the backend sends for it, what is still unknown or wrong on the backend, and how it maps onto the
client. Written so a programmer or an agent can build it from this file alone.

**Status:** described by the owner (2026-10-28). **Built in code (2026-11-02) and wired by the
owner** — §10. The first editor test (2026-11-02) found the popups not showing, the anticipation not
animating and the respin still green; all three were wiring / scene state, since fixed (§8). A full
retest of the round is still to do. The backend side is captured
(2026-11-02): the init with `symbolId: 7`, two trigger spins, a retrigger and ordinary free spins
(§9.4) — only the **last spin of a round** is still missing. The backend's trigger and near-miss
checks both count Drums instead of reels — reported, the backend is fixing it (§9.5). Art and
animations are not in yet: the owner sets up the images and says which part to activate and
deactivate when; the flow is the one described below.

**Authority:** the owner's description is the spec. Where the backend disagrees with it, the
**backend is wrong** and gets changed — the client does not code around it. Anything marked
**(open)** is unconfirmed; the full list is in §11.

Related: `GameExplanation.md` (how the project works), `ToDo.md` (what is open).

---

## Terms used here

- **Reels are numbered 1–5, left to right**, in this document. The backend uses **0-based** reel
  indexes; where a backend value is quoted, the 0-based index is given too — e.g. "reel 4 (index 3)".
- **Rows** are 0 (top) to 2 (bottom). The board is 5 reels × 3 rows.
- **Drum** — symbol id **7**, group `scatter`. The Free Spins trigger symbol. Per the backend config
  it can only land on **reels 2, 3 and 4** (indexes 1, 2, 3); reels 1 and 5 never show one.
- **Win box** — the animated square overlay drawn over a winning symbol. The same box used for line
  wins (`GameExplanation.md` §5); here it is also drawn over the triggering Drums.
- **Feature popup** — one popup object (`FeaturePopupView`, §8) shared by all three features: a
  backdrop that **rotates anticlockwise**, a frame, and one child per message (MYSTERY RESPIN, a
  CONGRATULATIONS per feature, TOTAL WIN), of which exactly one is shown. A `CanvasGroup` on the root
  fades it. No `ImageAnimation` — the rotation is the only motion, done in code.
- **Anticipation layout** — an Image with an `ImageAnimation` around a reel, shown while that reel
  could still complete the trigger. Green for anticipation; **orange** (a different frame list on the
  same component) for the respin — the same orange for all three features (owner).

---

## 1. The flow at a glance

```
Base spin lands
 ├─ a Drum on reel 2 AND on reel 3, reels 4 and 5 still spinning
 │    └─ ANTICIPATION: green layout around reels 4 and 5, they spin longer (only reel 4 can land a Drum)
 │         ├─ a Drum lands  ─────────────────────────────────┐
 │         └─ no Drum       → MYSTERY RESPIN popup → fades   │
 │                            → reel 4 respins, orange      │
 │                               ├─ a Drum lands ────────────┤
 │                               └─ no Drum → spin ends normally (line wins, if any)
 └─ a Drum on each of reels 2, 3, 4 ─────────────────────────┤
                                                             ▼
TRIGGER: win boxes on the Drums → "CONGRATULATIONS! YOU WON 10 FREE SPINS" popup
 → fade to black → fade back in: Free Game panel replaces the logo, Drums still boxed
 → player presses START (in the spin button's place)
 → 10 free spins play themselves; START became STOP; counter "FREE GAME X OF Y" goes up
 → after the last spin: "TOTAL WIN X" popup counting up from 0
 → player presses TAKE (in the spin button's place)
 → Free Game panel fades out while the logo fades in; balance updated; back to the base game
```

---

## 2. Trigger

**The game's rules page (authoritative):**

> Drum bonus symbols anywhere on 2, 3 and 4 reels award 10 FREE SPINS.
> Free Games Feature can be triggered during free games.
> 2 Drum symbols on 2 and 3 reels initiate the respin feature on the fourth reel.

**A Drum on each of reels 2, 3 and 4 — in any row — awards 10 Free Spins.** It is per reel, not a
count: two Drums on reel 2 still cover only reel 2, so "two on reel 2 + one on reel 4" does **not**
trigger. This matches the backend config: `triggerReels: [1, 2, 3]`, `spinsCount: 10`.

The trigger is a server fact. The client never counts Drums to decide whether the feature
triggered — it presents what the result says (§8). The client does count landed Drums for
**anticipation**, which is presentation only.

---

## 3. Anticipation

**When reels 2 and 3 have each landed a Drum, reels 4 and 5 get the green anticipation layout and
spin longer.** Reel 4 is the only reel that can still complete the trigger; reel 5 is anticipated
**anyway**, even though a Drum can never land there (owner, matching the reference — to be raised
with the Unity lead).

- Two Drums on the **same** reel are not a near miss: they cover one reel, and the trigger needs
  reels 2 and 3 both.
- The layout is an Image with an `ImageAnimation` (green frames) around the reel.
- Each anticipated reel's spin is extended (the existing base-game timing is +2 s per held reel,
  +0.8 s on Turbo — **(open)** whether those values stay).
- When the reel lands, its layout goes away — unless it is about to respin (§4).

**Reel 5 is anticipated for the Drum (owner).** The rules cannot explain it — reel 5 never holds a
Drum and plays no part in the trigger — but the reference shows it, and the owner wants it kept for
now; it goes to the Unity lead at a later meeting. Not another feature's anticipation: Ball Pick'em
and Sandbell Pick both respin reel 5 (`respinReel: 4`), but the reference spin had no Balls or
Sandbells on the board. So the Drum's anticipated reels are **not** derivable from the backend's
`nearMissReels` / `respinReel` alone: for this feature the client anticipates every reel after reel
3 — reels 4 and 5. Keep that list per feature in the client so it is one place to change if the
Unity lead decides otherwise.

**Only reel 4 respins** (§4). Reel 5's anticipation ends when it lands; it is not part of the Mystery
Respin.

---

## 4. Mystery Respin (near miss)

**"2 Drum symbols on 2 and 3 reels initiate the respin feature on the fourth reel."** If reel 4
lands without a Drum after a Drum on each of reels 2 and 3, reel 4 gets one more chance.

1. Reel 4 lands with no Drum.
2. The **feature popup** appears with the text **"MYSTERY RESPIN"**. It shows up and starts
   animating; when its time is up it **fades out** (alpha of the parent `CanvasGroup` to 0).
3. After the fade, **reel 4 spins again** with the anticipation layout in **orange**, the respin
   colour shared by all three features (same component, different frame list). Only reel 4 respins.
   **The player then presses Spin to start it** (owner, 2026-11-04) — the button waits with its
   normal Spin face; on autoplay or in a free-spins round it starts by itself. The popup's sound
   plays with the popup only.
4. It lands:
   - **Drum lands** → the feature triggers (§5).
   - **No Drum** → the spin ends like any other: line wins, if there are any, then idle.

The backend agrees: `nearMissReels: [1, 2]` (reels 2, 3) and `respinReel: 3` (reel 4) in the config,
`reelIndex: 3` in the captured respin.

**It is one spin, not two.** The respin is decided by the server in the same `result` as the spin
that caused it — one request, one bet. The result carries the board before the respin and the board
after it (§9.3); the client lands the first, plays the popup, then respins only the affected reel(s)
onto the second.

**Is every near miss respun?** The backend sends `nearMissRespin` only when it decided to respin, so
the client needs no rule of its own: respin when the result says so, otherwise just land. A near
miss without a respin has been seen — during free spins (free spin 3 in §9.4) — so **(open)** whether
the owner wants every two-Drum near miss respun, and whether the respin exists during free spins.

---

## 5. Trigger presentation

On the spin that triggers (directly, or through the respin):

1. **Win boxes** appear over each triggering Drum and keep animating.
2. The **feature popup** appears with **"CONGRATULATIONS! YOU WON 10 FREE SPINS"**. The number is
   the server's award, not a constant.
3. The popup leaves — **(open)** presumably the same fade-out as the Mystery Respin popup.
4. The **whole screen fades to black**: a full-screen black overlay's alpha goes up.
5. While black: the **logo above the reels is switched off** and the **Free Game panel** takes its
   place — an Image (no animation) reading **"FREE GAME X OF Y"**, X = spins played, Y = spins
   awarded.
6. The **screen fades back in** (the black overlay's alpha goes back down). The Drums still have
   their win boxes animating.
7. The spin button is replaced by a **START** button. Nothing happens until the player presses it.

**The trigger spin's own line wins are paid at once** (captured, §9.4): it is an ordinary paid
spin — the bet is taken, its line win goes straight into the balance, and the round's total starts
from 0 without it. **(open)** How they are shown relative to the Drum boxes and the popup — the owner
says what to activate when, as the art is set up.

A trigger can follow a near-miss respin on the same spin (captured: a Ball respin of reel 5, then
Free Spins triggered) — the respin plays first, as part of landing, then the trigger.

---

## 6. Playing the free spins

1. The player presses **START**. The first free spin starts **immediately**.
2. START turns into a **STOP** button. It behaves exactly like the base game's STOP: it quick-stops
   the current spin's reels (owner). It does not stop the round — the next free spin still follows.
3. The counter goes up as each spin starts: "FREE GAME 1 OF 10", "2 OF 10", …
4. **The spins play themselves** — like autoplay; the player does not press anything between spins.
5. **A win** shows exactly as in the base game — the win boxes on the winning symbols and the spin's
   win amount in the centre of the board with the burst behind it — **but it moves on by itself**,
   where the base game holds the presentation until the next spin.
6. The **win box** (the UI's win amount) **keeps increasing** across the round: it shows the round's
   running total, up to the last spin.
7. The **balance does not change** during the round (§7).

**Autoplay:** if autoplay was running when the feature triggered, the round still **waits for
START** (owner).

**Retriggers — yes, +10 spins (captured).** "Free Games Feature can be triggered during free games":
a Drum on each of reels 2, 3 and 4 during free spins adds **10 spins to the round** — captured as 9
left → 18 left, 10 awarded → 20 (§9.4). The counter is built from the server's figures, so a
retrigger shows as Y going up: "FREE GAME 2 OF 20".

**Free spins play at the trigger spin's bet** (captured: same `betAmountPerLine`, `lineMultiplier` 1,
no multiplier) and cost nothing.

**(open)** — each needs an answer before this is built:
- **A retrigger's presentation:** the same win boxes on the Drums and the CONGRATULATIONS popup,
  without the fade to black and START? Does the round pause for it?
- Can the **Mystery Respin** happen during free spins? (The retrigger uses the same Drum rule, so
  presumably yes.)
- Can **Ball Pick'em / Sandbell Pick** trigger during free spins, and what happens if they do?
- **Different reels** during free spins? Nothing in the captures says so.
- ~~**Big-win popups**~~ — **answered:** none during free spins, and the big-win popup is switched
  off game-wide for now (owner, 2026-11-03).
- If autoplay was running at the trigger: does it **resume after TAKE**?

---

## 7. End of the round

1. After the last free spin's win (if any) has shown, the **feature popup** appears with
   **"TOTAL WIN X"**, where X **counts up from 0** to the round's total. **(open)** Count-up
   duration.
2. It **stays on screen** until the player presses **TAKE**, which is in the spin button's place.
3. On TAKE: the **Free Game panel fades out** (alpha to 0) while the **logo fades in** (alpha to 1),
   at the same time. **(open)** Does the popup fade out on TAKE too, and do the win boxes clear?
4. The **balance is updated** and the game returns to the base game.

**Money.**
- **Balance:** frozen at its pre-round value for the whole round, and updated only on TAKE. The
  figure shown after TAKE is the **server's** balance. The server itself pays each free spin's win
  into the balance **as it happens** (captured, §9.4), so the hold is purely a display matter: on
  TAKE the latest result's balance is shown, nothing is added.
- **Win box:** the round's running total during the round (`totalWinCash`); the final total at the
  end.
- **The total is the server's figure** (`totalWinCash`), never a client-side sum of the spins' wins.
- **The total does not include the trigger spin's own line win** (captured: `totalWinCash` 0 on a
  trigger that won 0.05) — that win was paid on the trigger spin.

---

## 8. What the client needs, by scene object

Created and placed in the editor by the owner; the code gets fields to wire. Every one is optional
in code — a missing reference skips that step, it never stalls the round.

**All set up by the owner (2026-11-02)** except where noted. Paths are under `MainCanvas/BG`.

| Object | In the scene |
|---|---|
| Anticipation layouts | `SlotBG/SlotObject/AnticipationAnimations/AnticipationEffect (1)`–`(4)`, over reels 2–5 (x −252.5 / 0 / 252.5 / 505), 350 × 872.5, Image + `ImageAnimation` (Start On Enable on, speed 31), inactive. Already `SlotView.anticipationEffects`. **Their frame lists are empty on purpose** — `SlotView` holds the green and orange lists (`Graphics/Animations/WildJokers/AnticipationEffects/Green` and `/Orange`, 40 frames each — **wired**) and hands one over at runtime. The Drum uses reels 4 and 5, orange on reel 4 only; shared with the other features |
| Feature popup | `SlotBG/FeaturePopupView` carries the **`FeaturePopupView` script** and stays on. Under it, **`PopupImage`** (2026-11-03) holds the **`CanvasGroup`** and is what the code switches on, fades and switches off (`popupGroup`). Under that: `PopupBackground` (1300 × 1300 backdrop — **rotates anticlockwise**) and `Popup` (the 1000 × 1000 frame), both switched on by the code whenever the popup opens, holding one child per message, all inactive: `MysteryRespin`; `CongratulationsFreeSpins` (with `10Text` — the awarded count in sprite digits); `CongratulationsSandbellPick`; `CongratulationsBallGale`; `TotalWin` (with `TotalWinAmount` — counted up in sprite digits). Exactly one child is shown at a time. No `ImageAnimation` |
| Win boxes | the 15 line-win boxes (`GameExplanation.md` §5), reused for the Drums |
| Black overlay | `BlackFlim` — full-screen (1920 × 1080) black Image + `CanvasGroup`, inactive, last in `BG` but for `RayCastBlocker`, so it covers everything |
| Free Game panel | `SlotBG/FreeGames/FreeGamesRemainingText` + `CanvasGroup`, at y 410 where the logo is: `purple panel 2` with `FreeGameText` ("FREE GAME"), `RemainingFreeSpins` (**X — spins played**, despite the name), `OfText` ("OF"), `TotalFreeSpins` (Y). X and Y are TMP sprite digits (`Artboard 1` (was `O-1GOLDEN`), the `SpriteTextFormatter` convention: 0–9 digits, 10 = point). Under `FreeGames`, which carries `FreeGameView` |
| Logo | `Logo` + **`CanvasGroup`**; switched off at the trigger, faded back in at TAKE |
| START / STOP / TAKE | in the spin button's place — the existing spin button already switches modes (`UIManager.SpinButtonMode`). The art (`startSprites`, `takeSprites`) is the shared one used across the studio's games — settled (owner) |
| Centre win amount + burst | the board total (`GameExplanation.md` §5), reused per free spin — the burst art is still to come |

**Starting state:** the views set it themselves at init (logo on; Free Game panel, popup and black
film off), so the base game starts right whatever the scene was saved with. **Wiring:** all of this
feature's fields are wired (owner, 2026-11-03).

---

## 9. Backend

### 9.1 Init — `features.freeSpins`

Captured (the 0-indexed init):

```json
"freeSpins": {
  "enabled": true,
  "respinReel": 3,
  "spinsCount": 10,
  "triggerReels": [1, 2, 3],
  "nearMissReels": [1, 2]
}
```

| Field | Meaning (as read) |
|---|---|
| `enabled` | the feature is on |
| `spinsCount` | spins awarded on a trigger — 10 |
| `triggerReels` | one Drum on each of these reels triggers — reels 2, 3, 4 |
| `nearMissReels` | Drums on these reels make a near miss — reels 2, 3 |
| `respinReel` | the reel respun on a near miss — reel 4 |
| `symbolId` | `7` — the Drum. **Deployed**: in the captured init of 2026-11-02 (with `wild.symbolId: 0`, Ball `8`, Sandbell `9`) |

Client binding today: `ServerFreeSpinsFeature` in `GameDataModels.cs` reads all of the above;
`GameConfig.freeSpinsSymbolId` gets `symbolId` (7 now that it is sent). The rest is not yet copied
into `GameConfig`. The init carries **no round state** — nothing to resume a round from after a
reload (§9.5).

The Drum's reel strips (`SL-WJ_config.json`, `reelsInstance`): 0 / 4 / 4 / 4 / 0 per reel — never on
reels 1 and 5.

### 9.2 The spin request

`{"type":"SPIN","payload":{"betIndex":N}}`. **A free spin is requested exactly the same way**
(captured): the server owns the round state and knows the spin is free. The client sends nothing
extra and never asks for the round to start — START only starts the client's presentation.

### 9.3 Result — `nearMissRespin`

Captured (the only Drum respin seen):

```json
"nearMissRespin": {
  "triggered": true,
  "reelIndex": 3,
  "originalMatrix": [["5","7","4","6","6"],["1","6","0","5","5"],["5","7","5","6","6"]],
  "newMatrix":      [["5","7","4","2","6"],["1","6","0","1","5"],["5","7","5","6","6"]],
  "featureName": "freeSpins"
},
"isRespin": true,
"respinCount": 1
```

- Matrices are **row-major** (one array per row, one string per reel), like `matrix`.
- `originalMatrix` is the board before the respin; `newMatrix` after it. Only reel `reelIndex`
  differs. The result's top-level `matrix` equals `newMatrix` — the final board.
- **`newMatrix` is not always sent.** The Ball respin that also triggered Free Spins (2026-11-02, §9.4)
  carried only `triggered`, `reelIndex`, `originalMatrix` and `featureName`. **The client takes the
  final board from the top-level `matrix` and never reads `newMatrix`.**
- `featureName` names the feature the near miss was for — `"freeSpins"` matches the init's key.
- `isRespin` / `respinCount`: true / 1 on a respin spin. **(open, backend)** Can `respinCount` ever
  exceed 1?
- Not bound in the client yet (`ServerPayload` notes it as unbound).

### 9.4 Result — the trigger and the round (captured 2026-11-02)

The spin that triggers, and every spin of the round, carries a **`freeSpins` block** in `payload`.
**An ordinary base spin has no block at all** (null → "not in a round").

```json
"freeSpins": {
  "triggered": true,
  "active": false,
  "spinsRemaining": 10,
  "totalSpins": 10,
  "totalWinCash": 0
}
```

| Field | Meaning (as read from the captures) |
|---|---|
| `triggered` | spins were awarded **on this spin** — true on the trigger and on a retrigger, false on every other spin. A true per-spin flag (Gold of Luck's stayed true all round) |
| `active` | this spin was **played inside the round** — false on the base-game trigger spin, true on every free spin, a retrigger included. **(open)** what the last spin sends |
| `spinsRemaining` | spins left **after** this one; a retrigger's spins are folded in, so it can go up |
| `totalSpins` | spins awarded in the round so far, retriggers included |
| `totalWinCash` | the round's running total in cash, **this spin included**; excludes the trigger spin's own win |

**Reading it:**
- **Trigger** = `triggered && !active`. **Retrigger** = `triggered && active`.
- **Counter:** X = `totalSpins − spinsRemaining`, Y = `totalSpins` — "1 OF 10" on the first free
  spin, "2 OF 20" on the retrigger below. Both from the server; the client counts nothing.
- **A retrigger adds `spinsCount` (10)** to both: 9 left → 18, 10 awarded → 20.
- **Money:** the trigger spin is an ordinary paid spin (bet taken, line win paid at once, not in
  `totalWinCash`). A free spin costs nothing and its win is in the result's balance at once.
- Free spins pay at the same per-line bet (`betAmountPerLine` 0.01, `lineMultiplier` 1).
- `totalBetAmount` / `betAmountPerLine` are sent on free spins too — echoes of the bet, not charges.

**The captures** (bet 0.01 per line, total 0.20; boards row-major as sent; ★ = Drum):

*Capture 1:*

| Spin | `matrix` | Line wins | `freeSpins` (trig / active / left / total / roundWin) | Balance |
|---|---|---|---|---|
| Trigger | `[2,7,7,6,3] [6,4,7,7,5] [3,6,7,6,8]` — ★ reel 2, reel 3 ×3, reel 4 | none | true / false / 10 / 10 / 0 | 6098.09 |
| Free spin 1 | `[6,0,5,7,5] [0,1,4,4,4] [6,2,1,7,6]` | lines 0, 8, 11, 12 = 0.59 | false / true / 9 / 10 / 0.59 | 6098.68 (+0.59) |

*The owner reloaded between the captures; the round's other 9 spins were never played (§9.5).*

*Capture 2:*

| Spin | `matrix` | Line wins | `freeSpins` | Balance |
|---|---|---|---|---|
| Base spin | `[2,3,6,6,5] [6,6,3,0,4] [2,4,4,6,2]` | line 11 (10s ×4 with a Wild) = 0.10 | — no block | 6098.58 (−0.20 +0.10) |
| Trigger, after a Ball respin | `[6,7,4,7,9] [8,1,2,7,4] [4,4,8,7,3]` — ★ reel 2, reel 4 ×3, **none on reel 3** | line 16 (Q ×3) = 0.05 | true / false / 10 / 10 / 0 | 6098.43 (−0.20 +0.05) |
| Free spin 1 | `[1,1,4,6,5] [3,6,7,3,5] [6,6,7,2,3]` | lines 1, 5, 15 = 0.06 | false / true / 9 / 10 / 0.06 | 6098.49 (+0.06) |
| Free spin 2 — retrigger | `[4,2,7,5,2] [5,7,6,2,4] [4,7,4,0,9]` — ★ reel 2 ×2, reel 3, **none on reel 4** (a Wild) | none | true / true / 18 / 20 / 0.06 | 6098.49 |
| Free spin 3 | `[4,6,7,6,9] [4,7,0,5,3] [6,5,6,4,5]` — ★ reels 2 and 3 | none | false / true / 17 / 20 / 0.06 | 6098.49 |

The trigger in capture 2 also carried a near-miss respin, **without `newMatrix`**:

```json
"nearMissRespin": {
  "triggered": true,
  "reelIndex": 4,
  "originalMatrix": [["6","7","4","7","6"],["8","1","2","7","5"],["4","4","8","7","6"]],
  "featureName": "ballPickem"
},
"isRespin": true,
"respinCount": 1
```

Two Balls (reels 1 and 3) → reel 5 (index 4) respun from 6 / 5 / 6 onto 9 / 4 / 3; then Free Spins
triggered on the same spin. `featureName` is the Ball's mismatched `"ballPickem"` again.

**Free spin 3 is a Drum near miss** (a Drum on reels 2 and 3) **with no respin.** Either the Mystery
Respin doesn't happen during free spins, or not every near miss is respun — see §4 and §11.

**Not captured yet:** the **last spin of a round** — `spinsRemaining` 0 and, above all, what `active`
reads there. Until it is, the round ends when `spinsRemaining` reaches 0, which a retrigger on the
last spin handles by itself (the count goes back up).

### 9.5 Known backend problems and questions

- **Bug — the server counts Drums instead of checking reels, in the near miss AND the trigger.**
  Reported 2026-11-02; **the backend will fix it.**
  - *Near miss:* the capture in §9.3 has both Drums on **reel 2** (rows 0 and 2) and **none on reel
    3**, yet reel 4 was respun. The respin needs a Drum on reels 2 **and** 3; that board can never
    complete, whatever reel 4 lands. Seen twice.
  - *Trigger:* two captured triggers break the rule (§9.4) — the trigger in capture 2 has **no Drum
    on reel 3** (★ on reels 2 and 4 only, four Drums), and the retrigger has **no Drum on reel 4**
    (★ on reels 2 and 3 only, three Drums).
  - Both boards have 3+ Drums on reels 2–4: the server counts symbols. A Wild standing in for a Drum
    could explain the retrigger (reel 4 has a Wild) but not the trigger (reel 3 has none), so it is
    one bug, not Wild substitution.
  - The config (`triggerReels: [1, 2, 3]`, `nearMissReels: [1, 2]`) is right — the checks are not.
    Until the fix lands, the client still presents whatever the server says triggered. (Also in
    `ToDo.md`, Backend.)
- **Ask after the fix:** is the near-miss part fixed too? The two are probably separate checks.
- **Are line wins evaluated on the final board** (`matrix`) only? Expected yes.
- **Wild substituting for the Drum** — can a Wild stand in for a Drum in the trigger? (The owner says
  the Wild animates "when it is triggering the feature", so this may be intended.)
- **Resume after a disconnect** mid-round — the init carries no round state, and a reload mid-round
  (capture 1, 9 spins left) was followed by an ordinary paid base spin: the round was not resumed.
  Whether the server dropped it or the client should have resumed it is unknown. **Parked (owner,
  2026-11-02).**
- **The last spin of a round** — capture it (§9.4).

---

## 10. Client implementation — built (2026-11-02)

Built in code and wired; a full editor retest is still to do. Each piece falls through when its
reference is missing, so nothing stalls a round (`GameExplanation.md` §2).

**Model — `GameDataModels.cs`.** `payload.freeSpins` → `ServerFreeSpinsResult` → `FreeGameData`
(`present`, `spinsAwarded` = `triggered`, `active`, `spinsRemaining`, `totalSpins`, `roundWin` =
`totalWinCash`). `payload.nearMissRespin` → `RespinData` on `SpinResult.respin` (`reelIndex`,
`originalMatrix` column-major, `featureName`); `newMatrix` is unbound — the final board is always
`resultMatrix`. `features.freeSpins` is copied into `GameConfig` (`freeSpinsCount`,
`freeSpinsTriggerReels`, `freeSpinsNearMissReels`, `freeSpinsRespinReel`). Gold of Luck's
`freeGames` model is gone.

**Reels — `SlotView`.**
- `ComputeAnticipatedReels` is the union of each feature's rule. The Drum's,
  `AddDrumAnticipation` (§3): a Drum on every `freeSpinsNearMissReels` reel holds each reel after
  them until one completes `freeSpinsTriggerReels` — so reel 4 landing the third Drum ends it and
  reel 5 is not held. (The Sandbell's rule and landing cue came back with Sandbell Pick,
  `SandbellPickBonus.md` §9.)
- The effects get `anticipationGreenFrames` / `anticipationOrangeFrames` handed to their
  `ImageAnimation` before they're switched on (`SetAnticipationEffect`).
- Reels report landing from their stop tweens (`StopSingleReel`'s `onLanded`); `StopSpinSequence`
  waits for all of them, with a timeout only as a safety net.
- `RespinReel` spins one reel in orange for the anticipation hold (briefly on Quick Spin / STOP)
  and lands it on the final board.
- `ShowFeatureTriggerBoxes(symbolId)` boxes every cell holding that symbol (the win boxes, scatters
  allowed), looping until the next spin; an overload takes explicit cells. `ClearWinPresentation`
  takes it down. Gold of Luck's `AnimateAllScatters` is gone.

**Popup — `FeaturePopupView`.** `ShowMysteryRespin`, `ShowFreeSpinsAwarded(spins)` (hold, fade
out), `ShowTotalWin(amount)` (count-up, stays) / `HideTotalWin`, `HideImmediate`. One child on at a
time; the backdrop rotates anticlockwise while it is up. The root (with the script) stays on;
`popupGroup` (`PopupImage`) is what is switched on, faded and switched off, and opening a message
also switches on the backdrop and the frame, so the scene can be saved with any of them off.
DOTween only. Timings are consts at the top (placeholders).

**Panel, logo, black film — `FreeGameView`** (Gold of Luck's congratulations panel removed).
`ResetToBaseGame`, `EnterRound(played, total, onBlack, onDone)` (to black → logo off, panel on, and `onBlack`, where the win box switches to the round total unseen → back), `UpdateCounter`,
`ExitRound` (panel out, logo in, together). (`PanelClipPlayback.cs`, which it no longer used, was deleted
2026-11-04.)

**Controller — `GameManager`.**
- `SpinRoutine` lands `respin.originalMatrix` for a respin `GetPlayableRespin` accepts —
  `"freeSpins"`, `"sandbellPickBonus"` and the Ball's `"ballPickemBonus"` / `"ballPickem"`;
  `OnReelsStoppedComplete` then runs `PlayMysteryRespin` before `PresentSpinOutcome`:
  - MYSTERY RESPIN shows and fades; its sound plays with the popup only and is cut as it closes.
  - **The player presses Spin to start the respin** (owner, 2026-11-04): the button waits in its
    `RespinStart` mode (the Spin face). On autoplay or in a free-spins round it starts by itself.
  - The reel respins in orange for 2.4 s (`SlotView.RespinSpinTime`) with the anticipation sound, or
    0.3 s when quick — a pressed respin follows the current Quick Spin setting, one that starts by
    itself follows how the spin stopped.
  - STOP is live during the respin and lands the reel quickly — for now: what it should do is open
    (`ToDo.md`).
- `PresentSpinOutcome` hands a base trigger (`spinsAwarded && !active`) to `PlayFreeSpinsTrigger`:
  autoplay parked, buttons locked, the paid win in the win box with **no line animation** (owner),
  the trigger spin closed out (`lastResult` nulled), Drum boxes, CONGRATULATIONS, `StartFreeSpins`,
  `EnterRound`, START.
- The round: STOP quick-stops (`RequestStop` no longer refuses), the counter is the server's
  (X = `totalSpins − spinsRemaining`), the win box shows `totalWinCash`, `DisplayBalance` returns the
  balance held at the round's start (`balanceHeld`, shared with Sandbell Pick), **no big-win popup**
  (owner — and it is off game-wide for now, `BigWinPopupEnabled`).
- A retrigger (`spinsAwarded` inside the round): Drum boxes, CONGRATULATIONS with the spins added,
  then Y goes up — no fade, no START (owner).
- **A pick bonus inside the round** (backend, 2026-11-06): the round pauses, the Sandbell Pick and/or
  the Ball Gale Pick play (the free spin's line wins are not shown), then the round carries on — the
  retrigger if any, then the next spin or TOTAL WIN. The balance stays held, the win box shows the
  round's running total, the counter and music stay; the Ball fades the FREE GAME counter instead of
  the logo. Assumed, to be checked: the prize is in that spin's `totalWinCash` as well as its
  balance — if not, only the round's TOTAL WIN misses it.
- **Won together with a pick bonus** on the trigger spin: the pick bonuses play first, then (1 s
  later) the trigger as above; the round holds the server's balance, prizes included
  (`GameExplanation.md`, "Several features on one spin").
- `EndFreeSpins` (on `spinsRemaining` 0): TOTAL WIN counts up, then TAKE → `FreeSpinsOutro`: popup
  and boxes go, panel / logo crossfade, the server balance, the total left in the win box, autoplay
  resumed (owner).

**UI — `UIManager`.** In free spins the button reads Stop — pressable while a spin runs, greyed
between spins. `RefreshFeatureDisplays` sets the win box and balance as the round starts.

**Wiring (owner)** — done (2026-11-03).

---

## 11. Open questions

**Settled — the rules page**
- Trigger: a Drum on each of reels 2, 3 and 4, any row → 10 Free Spins. Per reel, not a count. (§2)
- Respin: Drums on reels 2 and 3 → reel 4 respins, and only reel 4. (§4)
- Free Spins can retrigger during free spins. (§6)

**Settled — the captures (2026-11-02, §9.4)**
- A retrigger adds 10 spins to the round. (§6)
- The trigger spin's line win is paid at once and is not in the round's total. (§5, §7)
- Free spins play at the trigger's bet, no multiplier; each win is in the balance at once. (§6, §7)
- A free spin is requested like any spin. (§9.2)
- `symbolId: 7` is in the init. (§9.1)

**Owner — answered**
- **STOP** during free spins works like the base game's STOP — quick-stops the reels. (§6)
- **Autoplay** running at the trigger: the round still **waits for START**. (§6)
- **The presentation** follows the flow in §1–§7. The owner sets up the images and says which part
  to activate and deactivate when (2026-11-02).
- **The feature popup** gets its own shared view, `FeaturePopupView`; one child per message, no
  frame animation, the backdrop rotates anticlockwise. (§8)
- **Scene objects** — all set up (2026-11-02): popup, black film, logo `CanvasGroup`, Free Game
  panel, anticipation effects. START / TAKE use the studio's shared button art. (§8)
- **Resume after a reload mid-round** — parked. (§9.5)
- **Reel 5 is not held when reel 4 lands the third Drum**; held when reel 4 misses. (§3)
- **The respin** — the popup after all reels have landed; the effects are gone by then, and the
  respun reel spins in orange for the same hold. On Quick Spin / STOP: popup, then a brief respin.
  Only the server's respins are played. (§4)
- **The trigger spin's line wins are not shown on screen** — the paid amount shows in the win box,
  the Drums are the presentation. (§5)
- **CONGRATULATIONS fades out like MYSTERY RESPIN.** (§5)
- **A retrigger** — Drum boxes and CONGRATULATIONS, no fade and no START; the round carries on.
  (§6)
- **No big-win popup during free spins** — and the big-win popup is off game-wide for now
  (2026-11-03). (§6)
- **Autoplay resumes after TAKE.** (§6)
- **TAKE** — the TOTAL WIN popup fades and the win boxes clear. (§7)

**Owner — open**
1. Reel 5's anticipation for the Drum — kept for now (it can never land a Drum); *owner raising it
   with the Unity lead* (§3).
2. Should every two-Drum near miss be respun, or only when the server decides? Free spin 3 in §9.4 is
   a near miss with no respin. (§4)
3. Mystery Respin, Ball Pick'em and Sandbell Pick during free spins. (§6)
4. Timings: popup holds, fades, black fade, rotation speed, TOTAL WIN count-up — placeholders in
   `FeaturePopupView` / `FreeGameView` / `GameManager.TriggerBoxesHold`. (§4, §5, §7)

**Backend** (see §9.5)
1. **Being fixed** (reported 2026-11-02): the trigger and the near miss must check reels (a Drum on
   each of reels 2, 3, 4 / reels 2 and 3), not count Drums. Confirm both checks are fixed.
2. Capture the **last spin of a round** — what `active` reads there.
3. Line wins evaluated on the final board only?
4. Can a Wild substitute for a Drum?
5. Can `respinCount` exceed 1?
6. Is the Mystery Respin possible during free spins? (Free spin 3, §9.4: a near miss, no respin.)
7. Resume after a disconnect mid-round — parked.
