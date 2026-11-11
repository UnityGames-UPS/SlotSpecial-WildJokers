# Wild Jokers — working list

The live list of what is open, grouped by area. If this and `GameExplanation.md` disagree, trust
this file. Finished work is summarised in **Done** at the bottom, one line per item.

Legend: `[ ]` open · `[x]` done · **(owner)** = the owner's to do or decide · **(backend)** = waiting
on the backend team.

---

## Next up (as of 2026-11-04)

- [ ] **(owner) Wire `BallGalePickView.freeGamesCounterGroup`** → `SlotBG/FreeGames/
  FreeGamesRemainingText` (it has a CanvasGroup). Empty: a Ball won inside Free Spins leaves the
  header as it is.
- [ ] **(owner) Test several features on one spin** (2026-11-06) — each feature on its own first
  (unchanged); then Sandbell + Ball, Sandbell + Free Spins, Ball + Free Spins, all three: the order,
  the 1 s gaps, the Sandbell's values hidden between, the win box (prizes so far, then the spin's
  total), the balance stepping up, autoplay resumed only at the end. Inside Free Spins: a pick
  bonus pauses the round and it carries on (with a retrigger, and on the last spin); the Ball fades
  the counter, not the logo; the Sandbell tease holds reels in the round.
- [ ] **(backend) Verify:** is a pick prize won in a free spin included in `freeSpins.totalWinCash`?
  Assumed yes. If not, the round's win box and TOTAL WIN miss it (the balance is still right).

**Phase: all three features are built and wired; testing and polish.** Free Spins (2026-11-02),
Sandbell Pick (2026-11-03) and Ball Gale Pick (2026-11-03) are built in code and wired — the
Sandbell Pick works in the editor; Free Spins and Ball Gale Pick are still to be tested. Wild Jokers'
own sounds are in (2026-11-04). The owner asks for a plan first and says when to make changes.

- [ ] **(owner) Test the Ball Gale Pick in the editor** — wired (2026-11-04). A Ball respin
  (MYSTERY RESPIN, Spin to start it, reel 5 in orange, no anticipation); the trigger (boxes on the
  Balls, GOOD LUCK, balance held, CONGRATULATIONS fades); the slide in (reels down, logo out, panel
  up, the swoosh); the glare in groups; a pick (glass break, value, total, the other gems locked
  meanwhile); COLLECT ends it; TOTAL WIN, TAKE; the slide back (trigger board, no boxes), the prize
  and the server's balance, Spin live; autoplay paused and resumed; a second bonus starts with
  every gem whole.
- [ ] **(owner) Retest a full Free Spins round** — Drum hold on reels 4 and 5 (not on 5 when 4
  lands the Drum); a Drum respin (press Spin; normal, Quick Spin and STOP); the trigger (Drum boxes,
  CONGRATULATIONS, fade, START, autoplay parked, the free-spins music); the round (STOP, counter,
  running total, balance held); a retrigger; the end (TOTAL WIN, TAKE, crossfade, balance, the main
  music, autoplay back).
- [ ] **(owner) Test the 2026-11-04 changes** — a respin waits for Spin (not on autoplay / in free
  spins) and STOP lands it; the new sounds; CONGRATULATIONS now holds 6.5 s (6.9 s with the fade);
  the popup backdrop turns anticlockwise; the Sandbell values green on the clicked one and orange on
  the rest — **wire `SandbellPickView.pickedValueSpriteAsset` (`1-0 GREEN`) and
  `missedValueSpriteAsset` (`0-1ORANGE`) and save the scene; they were still empty at 14:34.**
- [ ] **(owner) Remove the `WheelSpin` object** (a Transform and an `AudioSource`, beside `Ui1` /
  `SpinLoop` / `Anticipation`) — its `AudioManager.wheelSegmentSource` field is gone (it played the
  reel stop).
- [ ] **(owner) `TestText (TMP)`** in the scene uses `Artboard 1` — a test object? Delete if so.
- [ ] **Tune the timings** — placeholders: `BallGalePickView` (slide, logo fade, glare interval /
      group size, reveal settle), `GameManager.SandbellRevealHold` / `TotalWinStayHold` /
      `TriggerBoxesHold`, `FeaturePopupView` (popup holds, fade, rotation period, count-up),
      `FreeGameView` (black fade and hold, crossfade), `SlotView.RespinSpinTime` (2.4 s).
- **(owner) New scripts** — agents can't create files. The owner created `FeaturePopupView.cs`,
  `SandbellPickView.cs` and `BallGalePickView.cs` (2026-11-02 / 03).
- **(backend)** Capture the last spin of a Free Spins round; the Drum counting fix (Backend).

---

## Backend

- [ ] **(backend) The Drum checks count Drums instead of reels — trigger AND near miss.** Reported
      2026-11-02; **the backend is fixing it.** Near miss: two captures, both with both Drums on reel
      2 and none on reel 3, respun reel 4 — which can never complete Free Spins. Trigger: a trigger
      with no Drum on reel 3, and a retrigger with no Drum on reel 4 (a Wild there) — both 3+ Drums
      on reels 2–4 (`BonusFreeSpins.md` §9.4, §9.5). Expected: a Drum on each of reels 2, 3, 4 to
      trigger; on reels 2 and 3 to respin. Confirm both checks are fixed.
- [ ] **(backend) Free Spins — the last spin of a round.** Trigger, retrigger and free spins are
      captured (2026-11-02); what `active` reads on the last spin isn't. Until then the round ends on
      `spinsRemaining` 0.
- [ ] **(backend) `newMatrix` not always sent** on `nearMissRespin` — missing on the Ball respin that
      also triggered Free Spins. The client uses the top-level `matrix` as the final board and never
      reads `newMatrix`, so nothing waits on it.
- [ ] **(backend) Mystery Respin during free spins?** A Drum near miss on a free spin was not respun
      (`BonusFreeSpins.md` §9.4, free spin 3).
- [ ] **Resume after a reload mid-round** — parked (owner, 2026-11-02). A reload with 9 free spins
      left was followed by an ordinary paid spin; the init carries no round state.
- [ ] **(backend) Two near misses on one spin.** A capture had two Balls AND two Sandbells on reels
      1–4; only `ballPickem` was reported. Both bonuses respin reel 5, so one respin can serve both —
      `featureName` should name both (or become a list). (`SandbellPickBonus.md` §8.3)
- [x] **(backend) Sandbell prize — fixed and captured (2026-10-30).** The first trigger paid the
      **sum** of every Sandbell's value. Now: `pickedPosition` (the win) + `unpickedPositions` (one
      missed value per other Sandbell); prize = win × total bet. Left: a trigger capture after a
      respin; `totalWinMultiplier` vs `prizeMultiplier` (both sent, equal). **(owner)** which missed
      value goes on which unclicked Sandbell. (`SandbellPickBonus.md` §8.2)
- [ ] **(backend) `featureName` doesn't match the init's key** — the respin says `"ballPickem"`, the
      feature is `"ballPickemBonus"` (`"freeSpins"` and `"sandbellPickBonus"` do match). Seen again
      2026-11-02.
- [ ] **(backend) Sandbell respin only for exactly 2** (owner) — no respin once 3+ Sandbells are on
      the board. 2 on reels 1–4 + the third on reel 5 is **already right** (captured: no respin).
      Still wrong: **3–4 on reels 1–4 respins** (captured with 4) — `nearMissCountRange` (2–4) should
      be 2–2, and the rules page's "2, 3 or 4 Sandbell symbols on 1-4 reels" reworded. (This was once
      withdrawn on the rules page's wording; the owner's rule reopens it.) **(owner)** Ball Pick'em
      has the same 2–4 — same rule?
- [ ] **(backend) Stacked Sandbells** — captured (two on one reel), so 6+ Sandbells can land.
      Confirm the trigger counts cells and that 6+ triggers (owner's rule for now); the rules page
      says "3, 4 or 5".
- [ ] **(backend) Ball bonus result** (trigger captured 2026-10-30) — `picks` is the sequence
      (× 15, × 25, COLLECT; total × 40 = the cash paid), no pick requests; the client places each pick
      under the gem the player picks. **`grid`: agreed — ignore it, use only `picks` / `pickIndex`;
      the backend removes it later, nothing waits on that.** `isEnd` = `isCollect` (backend): `isEnd` ends
      the feature, `isCollect` shows the COLLECT gem. `picksCount` ignored (owner). Still open: the
      duplicate fields.
      (`BallGalePickBonus.md` §9.2, §9.4)
- [ ] **(backend) Confirm the low symbols' paytable** — Q, J, 10 pay 5 / 10 / 100. Captured as live
      (J ×5 paid 100); intent unconfirmed.
- [ ] **(backend) `triggerProbability`** — went 0.1 → 1 in the 2026-10-30 config. It looks like the
      chance a qualifying board actually awards the bonus: a 4-Sandbell board paid nothing while it
      was 0.1. The rules say 3+ **award** the bonus, so it must stay 1 or go. (Keep `nearMissReels`
      and `nearMissCountRange` — the near-miss rules need them.)
- [ ] **(backend) `maxWinMultiplier` 500 unreachable** — Sandbell: one pick, top prize × 50. Cap,
      rules text or prize list out of date? Ball: 20 gems × top × 25 = exactly 500, so it may be the
      most a round can show — confirm, and what's sent if all 20 are picked without a COLLECT.
      (Owner: ask after the feature captures.)

## Symbols

- [x] **(owner) Wire the symbol sprites** on `SlotView` — `spriteWild`, `spriteJackInTheBox`,
      `spriteA`, `spriteK`, `spriteQ`, `spriteJ`, `spriteTen`, `spriteDrum`, `spriteBall`,
      `spriteSandbell` (ids 0–9).
- [ ] **(owner) Sandbell and Wild trigger clips** — the only two symbols that animate, and only on a
      feature trigger. Every other `animSprites…` list stays empty by design (winning symbols are
      static in this game). Waiting on the art.
- [ ] **(owner) Which feature does the Wild trigger?** It animates "when it is triggering the
      feature", but the backend gives it no feature of its own — does it count toward a scatter
      trigger? Ties to the open backend question of Wild substituting for scatters.
- [ ] **(owner) Size `SymbolSizeOverrides`** against the new art — all at 200 × 200 for now. Over the
      245 pitch overlaps neighbours and steals their taps.
- [ ] **Tune `SymbolAnimationSpeeds`** for the Sandbell and Wild once their clips exist — all at 20.
      Length = frames² ÷ (24 × speed).
- [ ] `SymbolInfoCard` scatter text is Golden Dynasty's ("3 or more Bonus symbols trigger Free
      Games") and is shown for all three scatters. Needs per-feature text.

## Win presentation

- [x] **Win boxes — wired and working** (owner, 2026-10-30): 5 columns × 3 boxes. The clip is now
      `Graphics/Animations/WildJokers/SquareWin` (29 frames); the old `Square` (33) is unused.
      `GameExplanation.md` §5.
- [ ] **Win-box speed is fixed at 33** (`SlotView.winBoxAnimationSpeed`), set for the old 33-frame
      clip: with 29 frames it plays at ≈ 27 fps (1.06 s a loop instead of 1.21 s). Fix: speed =
      `winBoxFrames.Count`, as the other clips do.
- [x] **Board total with a burst behind it — built** (2026-11-04/05). `SpinWinText` is a child of
      `SpinWinAnimation` (Image + `ImageAnimation`, Raycast Target off, Start On Enable off), the
      field `SlotView.totalWinBurst`. The first show of a win (`ShowPhase1TotalWin`) plays the burst
      once and pops the text 1.7× → 1.3×. After the walk the total shows the same way again and goes
      when the burst ends (owner, 2026-11-10).
      `GameExplanation.md` §5.
  - [ ] **(owner)** Wire `SlotView.totalWinBurst` → `SpinWinAnimation`, and save it off.
  - [ ] **Show the server's total, not a sum.** `PlayTwoPhaseWinLines` and `SummariseWinLines` add up
        the lines' `winAmount`; use `lastResult.winAmount` (`grandTotalWin`) like the win box does.
- [x] **Line walk — built** (owner, 2026-11-05). Manual spins only: after the total, each winning
      line in turn shows its payline image, its amount on reel 3 in the row the line crosses there,
      and only its own cells and boxes (two rounds, ≈ 2.1 s). Two passes, then the total again with
      its burst and pop, hidden when the burst ends; the winning cells keep looping until the next
      spin (owner, 2026-11-10). Line amounts pop like the total. The total is hidden during the walk.
      `GameExplanation.md` §5.
  - [ ] **(owner)** One payline Image (Raycast Target off, saved off) over the reels — suggested in
        `AnimationLayer` after `WinBoxes`, before `WinLineAmounts` — wired as `SlotView.paylineImage`,
        and the 20 line sprites in `SlotView.paylineSprites`, in the server's line order (element 0 =
        line 0).

## Features

- [ ] **(owner) STOP during a Mystery Respin — what should it do?** For now the button reads STOP
      while the respun reel spins (2.4 s) and pressing it lands the reel quickly, like a normal spin's
      STOP. Check against the reference (owner, 2026-11-04). The respin itself now waits for the
      player's Spin press, except on autoplay and in free spins (all three features —
      `BonusFreeSpins.md` §10).
- [ ] **Free Spins** (Drum) — **built and wired; full retest pending** (see Next up); spec and
      what was built in `BonusFreeSpins.md` (§10). Waiting on: the editor test, the timings, the
      owner's remaining answers (its §11), and the backend (the Drum counting fix, the last spin of a
      round).
- [ ] **Sandbell Pick** — **built, wired and working in the editor (2026-11-03)**; what was built
      is `SandbellPickBonus.md` §9. Owner's answers in (its §10). The values are sprite digits
      (`SpriteTextFormatter.ToSpriteMultiplier`, "x" = sprite 11): `1-0 GREEN` on the clicked
      Sandbell, `0-1ORANGE` on the rest (2026-11-04) — the two fields still to wire (Next up).
  - [ ] **(owner) Still open:** a spin winning the Sandbell Pick together with another feature
        (thought impossible; Free Spins would win).
- [ ] **Ball Gale Pick** (Ball; the rules page's "Ball Pickem") — **built and wired; test pending**
      (see Next up); spec and what was built in `BallGalePickBonus.md` (§10). `SlotObject` slides
      out, not `SlotBG` (owner). The borders (`GemBorders`, behind the gems) and the glow are in;
      the values and the total are sprite digits.
      - [ ] **(owner) Art:** the COLLECT image (written as text for now). The gems' sheet
            `Artboard 1` has its "x" (sprite 11), but **glyph 11 is still 83 × 111** while the
            digits copied from `O-1GOLDEN` draw at 250 × 333 — the "x" shows at a third of their
            size. Set it to 250 × 333 (orange's x: bearing X −73, Y 307, advance 150, as a start).
      - [x] **Several features on one spin** (2026-11-06) — `PlayFeatureSequence`: Sandbell, Ball,
            then Free Spins, 1 s apart; the pick bonuses inside Free Spins too. Backend items in its
            §9.4 (× 500, duplicate fields, `featureName`).
- [ ] **(protected — ask first)** `SocketIOManager.GenerateRandomMatrix` keeps only the Wild and
      `scatterSymbolId` off the opening board, so a Drum or Ball can sit on it.

## Audio

Wild Jokers' own clips are wired (2026-11-04); the old games' clips, fields and calls are gone
(`GameExplanation.md` §8).
- [ ] **(owner) The anticipation sound** (`bonus scatter fail`, 3.6 s) plays once: a Sandbell hold
      over three reels (≈ 6 s) outlasts it — loop it? And should a respin, which starts on a press
      now, still play it?
- [ ] **(owner) Sounds still missing** — a feature triggering (the stinger before the boxes), each
      reel landing, a scatter landing, the Wild in a win, the Sandbell pick (click, explosion), the
      COLLECT gem, the big win. All silent until this game has clips for them.
- `bg music`, `box icon in reel` and `j icon` are not used (owner).

## UI — landscape only

- [x] **Rotate on a tall screen** (owner, 2026-11-10) — `OrientationChange` turns `UIWrapper` −90° and
      fits the swapped screen when it is not wider than tall (from `Helper/OrientationChangeRotate.md`),
      instantly at load, animated (0.2 s) on a resize; polls the screen every frame.
  - [ ] **(owner)** Wire `OrientationChange.UIWrapper` → `MainCanvas/BG` (on `OC`).
  - [ ] **Later:** a portrait/landscape switching mode, migrated from another project (owner).

- [x] **(owner) Landscape wiring on `UIManager`** finished.
- [ ] **Jackpots in landscape** — `jackpotButtons` still lists the portrait buttons; the landscape
      ones are `LandScapeObj/PotentialPayouts/Left|Right/…Button`. The four `…JackpotText` fields
      exist only as portrait, so landscape values need a small code change. **(owner) Does Wild
      Jokers have platform jackpots?**
- The portrait half (`PortraitObj`, portrait fields) stays as it is — owner's decision.

## Cleanup

- [ ] **(owner) Delete `Graphics/Animations/WildJokers/Square`** — unused (the win boxes use
      `SquareWin`). Unity's "Find References" on the folder lists the scene only because its path is
      a prefix of `SquareWin`'s.
- [ ] **Big-win popup: switched off game-wide for now (owner, 2026-11-03)** —
      `GameManager.BigWinPopupEnabled = false`; set it true to bring it back. Its threshold is still
      Gold of Luck's (10× total bet) — confirm when it returns.
- [x] **`GameExplanation.md` rewritten for Wild Jokers** (2026-11-01) — intro, file tree, model,
      lifecycle, features, UI, audio, oddities, known problems, docs table. Gold of Luck now appears
      only as history.

---

## Done

- [x] **2026-11-04** — The Mystery Respin waits for the player's Spin press (`UIManager`
      `RespinStart` mode; not on autoplay or in free spins), spins 2.4 s, follows the current Quick
      Spin setting, STOP lands it. Wild Jokers' sounds wired: main music (`wj`), free-spins music,
      reel spin, normal win, anticipation, CONGRATULATIONS (now held 6.5 s for its 6.9 s clip),
      MYSTERY RESPIN (cut as the popup closes), TOTAL WIN (cut when the popup goes), gem break, the
      Ball panel's swoosh. Gold of Luck's and Golden Dynasty's clip fields and calls removed, with
      `wheelSegmentSource`; their audio files deleted (owner). The popup backdrop turns
      anticlockwise. Value sprite fonts: `SpriteTextFormatter.ToSpriteMultiplier`; the Sandbell
      values swap `1-0 GREEN` / `0-1ORANGE`; the Ball's gems and total in sprite digits. The gem
      border moved out of the code (`GemBorders`, always on, behind the gems). `glowFrames` removed
      (the glow carries its own frames). `1-0 GREEN` and `Artboard 1` sprite assets given the
      orange / golden metrics. Builds verified.
- [x] **Cleanup (owner, 2026-11-04)** — `PanelClipPlayback.cs`, `FreeGamesOverlay`, the Genie
      Wheel's scene leftovers and the old games' audio deleted; `O-1GOLDEN` replaced by
      `Artboard 1`.
- [x] **Ball Gale Pick built in code (2026-11-03)** — model (`ballPickemBonus` picks), its respin
      (both `featureName`s), the Ball CONGRATULATIONS, `BallGalePickView` (slide, glare, break,
      values, total, glow, a fill menu for the gems), `PlayBallGalePick` / `BallGalePickOutro`.
      Build verified. `BallGalePickBonus.md` §10.

- [x] **Sandbell Pick built in code (2026-11-03)** — model (`sandbellPickBonus`), Sandbell
      anticipation and landing cue, its respin, the pick cells in their own view
      (`SandbellPickView` — `ShowPick` / `Reveal`), `PlaySandbellPick`, a general balance hold, the
      big-win popup switched off. Build verified. `SandbellPickBonus.md` §9.

- [x] **Free Spins built in code (2026-11-02)** — model (`payload.freeSpins`, `nearMissRespin`),
      Drum anticipation in green, the Mystery Respin in orange, `FeaturePopupView`, `FreeGameView`
      rewritten, trigger / round / retrigger / TOTAL WIN / TAKE in `GameManager`, Stop during free
      spins. Reels now report landing from their tweens. Gold of Luck's free-games model, scatter
      animation and congratulations panel removed. Build verified. `BonusFreeSpins.md` §10.

- [x] **(backend) Feature `symbolId`s and the `wild` block deployed** — in the captured init of
      2026-11-02: `wild` 0, `freeSpins` 7, `ballPickemBonus` 8, `sandbellPickBonus` 9. The converter
      already reads them; the "names no symbolId" errors stop.
- [x] **Free Spins captured (2026-11-02)** — two triggers, a retrigger, free spins, the init.
      Recorded in `BonusFreeSpins.md` §9.4.
- [x] **Win boxes coded** — `winBoxColumns` + `winBoxFrames` on `SlotView`; a box per winning cell,
      looping with the win, rounds paced on the box clip, stopped and hidden in `HideWinSlots`. The
      win guard now skips every scatter (Drum, Ball, Sandbell), not just `scatterSymbolId`.

- [x] **Genie Wheel removed from code** — wheel sequence and its state (`isInGenieWheel`, held-back
      prize) out of `GameManager`; wheel model, slices and conversion, plus the Genie wild's
      multipliers, out of `GameDataModels`; wheel sounds out of `AudioManager`; wheel-only
      `SlotView` methods gone. The startup "no wheel slices" error goes with it. Button modes renamed
      `FeatureStart` / `FeatureTake` (routed to `GameManager.OnFeatureStartPressed` /
      `OnFeatureTakePressed`) for Free Spins' START and TAKE; `DisplayBalance` kept as the hook for
      Free Spins' frozen balance. End of the free-games round no longer shows the Winner panel or
      fades — it ends straight away until Free Spins builds its own. Build verified without
      `GenieWheelView.cs`.
- [x] **Reels re-spaced for the bigger frame** — 245 pitch on all 25 icons of all five reels,
      `symbolHeight` 245, animation layer rows moved to match (reel rows − 123.383).
- [x] Big-win popup test trigger deleted from `UIManager`.
- [x] **Spins work in the editor** — the "dead buttons" were `UIManager`'s landscape fields being
      empty (Gold of Luck was portrait-only); spin, settings, autoplay, speed and win/balance
      displays now wired to `LandScapeObj` (owner).
- [x] `SlotView` moved to the Wild Jokers symbols (ids 0–9): sprite and animation fields, the sprite
      table, the size / speed tables. Genie multiplier code removed.
- [x] **Model bound to Wild Jokers** (`GameDataModels`): init (`multiplier` paytable, `totalLines` as
      the bet multiplier, paylines, feature models with `symbolId`) and result (`lineWins` → `WinLine`,
      paying symbol skips the Wild). Verified by running the converter on the captured init and four
      spins outside Unity: paytables, feature ids, every line win and every balance correct. Gold of
      Luck's blocks kept, arriving null.

---

## Later

- [ ] **Split `GameManager` by feature with partial-class files** (owner: not now, 2026-11-03).
      `GameManager.cs` is over 1000 lines. Each feature's controller code could move to its own file
      — `Managers/GameManager.FreeSpins.cs`, `Managers/GameManager.SandbellPick.cs`, later
      `GameManager.BallGalePick.cs` — each `public partial class GameManager { … }`, with
      `GameManager.cs` itself becoming `partial`. Still one component: no new wiring, and the
      controller rule holds. The owner creates the empty files; Unity's template must be emptied
      first.
