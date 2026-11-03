using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] internal SocketIOManager socketManager;
    [SerializeField] internal UIManager uiManager;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private SlotView slotView;
    [SerializeField] private FreeGameView freeGameView;
    [Tooltip("The shared feature popup (FeaturePopupView) — MYSTERY RESPIN, CONGRATULATIONS, TOTAL WIN.")]
    [SerializeField] private FeaturePopupView featurePopupView;
    [Tooltip("The Sandbell Pick's pick cells (SandbellPickView, on PickCells) — PICK ME, the values, the explosion.")]
    [SerializeField] private SandbellPickView sandbellPickView;

    [Header("Spin Settings")]
    [SerializeField] private float normalSpinDuration = 3.5f;
    [SerializeField] private float turboSpinDuration = 2.0f;
    [SerializeField] private float quickSpinCycleDuration = 0.1f;

    // How long a Free Spins trigger's (or retrigger's) Drum boxes play before CONGRATULATIONS shows.
    // A placeholder until the owner signs off the timings.
    private const float TriggerBoxesHold = 1.5f;

    // Sandbell Pick timings — placeholders until the owner signs them off.
    private const float SandbellRevealHold = 2f;     // the revealed values animate this long before TOTAL WIN
    private const float TotalWinStayHold = 1.5f;     // TOTAL WIN stays this long after its count-up, then fades

    // The near-miss respins' featureNames the client plays — the init's keys. The Ball's respin isn't
    // built yet: its spins land straight on the final board.
    private const string FreeSpinsFeatureName = "freeSpins";
    private const string SandbellPickFeatureName = "sandbellPickBonus";

    // The big-win popup is switched off game-wide for now (owner, 2026-11-03). Set true to bring it
    // back; its threshold is bigWinMultiplierThreshold below.
    private const bool BigWinPopupEnabled = false;

    [Header("Win Settings")]
    // Win / total bet at or above which the big-win popup plays. Serialized, so the scene's value is
    // the one that runs — this default only matters for a fresh component. Kept equal to the scene.
    [SerializeField] private double bigWinMultiplierThreshold = 10.0;

    internal GameConfig gameConfig;
    internal PlayerData playerData;
    internal SpinResult lastResult;

    internal GameState currentState;
    internal SpinSpeed currentSpinSpeed;

    internal int currentBetIndex;
    internal double currentBetAmount;

    internal bool isAutoPlaying;
    internal int autoPlayTotalRounds;
    internal int autoPlayRemainingRounds;
    internal bool wasAutoPlayingBeforeFeature;
    internal int savedAutoPlayRemainingRounds;
    internal int savedAutoPlayTotalRounds;

    // Free Spins' round, all from the server's payload.freeSpins (BonusFreeSpins.md §9.4) — the client
    // counts nothing itself.
    internal bool isInFreeSpins;
    internal int freeSpinsRemaining;      // spins left after the last result; a retrigger folds in
    internal int freeSpinsPlayed;         // the counter's X: totalSpins - spinsRemaining
    internal int freeSpinsTotal;          // the counter's Y as shown: totalSpins, held back during a retrigger's presentation
    internal double freeSpinsRoundWin;    // the round's running total: totalWinCash

    // A retrigger landed and its Drums have not been shown yet — the counter's Y waits for them.
    private bool retriggerPending;
    private int retriggerTotal;

    // A feature holding the shown balance back while the server's already includes its pay: Free Spins
    // for the whole round, until TAKE (BonusFreeSpins.md §7); the Sandbell Pick until its TOTAL WIN has
    // counted up (SandbellPickBonus.md §6).
    private bool balanceHeld;
    private double heldBalance;

    // The balance shown while the reels spun — the pre-spin balance less the bet — recorded before the
    // result's balance replaces it. A feature that holds the balance from the start holds it here.
    private double balanceBeforeResult;

    // The balance the UI shows. playerData always holds the true, server balance; this is the one
    // place a feature holds the display back.
    internal double DisplayBalance => balanceHeld ? heldBalance : playerData.balance;

    // Whether the reels stopped quickly (Quick Spin, or STOP) — a respin that follows lands quickly too.
    private bool lastStopWasQuick;

    // What the shared button does in its FeatureStart / FeatureTake modes, set by the feature that
    // put it there and cleared on the press.
    private System.Action pendingFeatureStart;
    private System.Action pendingFeatureTake;

    internal bool isInitialized;
    internal bool initializationFailed;

    private Coroutine spinCoroutine;
    private bool stopRequested;
    private bool waitingForSpecialWin;

    #region Initialization

    private void Start()
    {
        currentState = GameState.Initializing;
        currentSpinSpeed = SpinSpeed.Normal;
        isInitialized = false;
        initializationFailed = false;
    }

    internal void OnInitDataReceived(GameConfig config, PlayerData player, List<List<int>> initialMatrix)
    {
        gameConfig = config;
        playerData = player;
        currentBetIndex = playerData.currentBetIndex;
        UpdateBetAmount();

        if (initialMatrix != null && slotView != null)
        {
            slotView.SetInitialMatrix(initialMatrix);
        }

        isInitialized = true;
        currentState = GameState.Idle;

        // The feature views start in the base game's look whatever the scene was saved with: logo on,
        // Free Game panel, black film and feature popup off.
        if (freeGameView != null) freeGameView.ResetToBaseGame();
        if (featurePopupView != null) featurePopupView.HideImmediate();
        if (sandbellPickView != null) sandbellPickView.HideImmediate();

        uiManager.OnGameInitialized();
    }

    #endregion

    #region Bet Management

    internal void IncreaseBet()
    {
        if (!CanChangeBet()) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex + 1;
        if (nextIndex > maxIndex)
        {
            nextIndex = 0;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    internal void DecreaseBet()
    {
        if (!CanChangeBet()) return;

        int maxIndex = gameConfig.availableBets.Count - 1;
        int nextIndex = currentBetIndex - 1;
        if (nextIndex < 0)
        {
            nextIndex = maxIndex;
        }

        if (nextIndex == maxIndex)
        {
            AudioManager.Instance?.PlayMaxBetReached();
        }
        else
        {
            AudioManager.Instance?.PlayBetPlusMinus();
        }

        SetBetIndex(nextIndex);
    }

    // The bet is fixed for a whole feature. Between free spins the state is briefly Idle, so Idle alone
    // doesn't keep it out — and a bet changed mid-round would shift the big-win threshold for spins
    // the player already started at the old bet. The locked buttons stop it in practice; this is the
    // guard behind them.
    private bool CanChangeBet()
    {
        if (currentState != GameState.Idle || isAutoPlaying) return false;
        if (isInFreeSpins) return false;
        return gameConfig != null && gameConfig.availableBets != null && gameConfig.availableBets.Count > 0;
    }

    internal void SetBetIndex(int index)
    {
        currentBetIndex = index;
        UpdateBetAmount();
        uiManager.UpdateBetDisplay();
        if (slotView != null) slotView.OnBetChanged();
    }

    private void UpdateBetAmount()
    {
        currentBetAmount = gameConfig.availableBets[currentBetIndex];
    }

    #endregion

    #region Spin Control
    
    internal void RequestSpin()
    {
        if (currentState != GameState.Idle) return;
        if (!socketManager.isConnected) return;

        double totalPay = GetTotalPay();
        if (!isInFreeSpins && playerData.balance < totalPay)
        {
            if (popupManager != null)
            {
                popupManager.ShowInsufficientFundsError();
            }
            return;
        }

        StartSpin();
    }

    internal void RequestStop()
    {
        if (currentState == GameState.Spinning)
        {
            if (isAutoPlaying)
            {
                StopAutoPlay();
            }
            // Free spins included: STOP quick-stops the current free spin's reels, exactly as in the
            // base game (owner). It does not stop the round — the next free spin still follows.
            else
            {
                stopRequested = true;
                uiManager.SetSpinStopButtonStates(isSpinningState: true, isInteractable: false);
            }
        }
    }

    private void StartSpin()
    {
        if (lastResult != null)
        {
            ProcessSpinResult();
        }

        lastResult = null;
        currentState = GameState.Spinning;
        stopRequested = false;

        // Deduct total pay from balance on spin start (except in free spins, which the server plays
        // for free — the triggering spin was the paid one).
        if (!isInFreeSpins)
        {
            playerData.balance -= GetTotalPay();
            if (playerData.balance < 0) playerData.balance = 0;
        }

        uiManager.OnSpinStarted();

        // A Sandbell Pick's values stay on screen until the next spin — this one.
        if (sandbellPickView != null) sandbellPickView.HideImmediate();

        if (slotView != null)
        {
            slotView.StartSpin();
        }

        socketManager.SendSpinRequest(currentBetIndex);

        if (spinCoroutine != null)
            StopCoroutine(spinCoroutine);
        spinCoroutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        float spinDuration = GetSpinDuration();
        float elapsed = 0f;

        while (elapsed < spinDuration && !stopRequested)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Player pressed Stop manually — hold for 0.5s so the reels keep
        // spinning briefly before snapping, giving clear visual feedback.
        if (stopRequested)
        {
            yield return new WaitForSeconds(0.5f);
        }

        while (lastResult == null)
        {
            yield return null;
        }

        currentState = GameState.Stopping;

        // A respin lands the board from before it first (the anticipation reads that board too); the
        // respin itself follows in OnReelsStoppedComplete.
        RespinData respin = GetPlayableRespin(lastResult);
        List<List<int>> landingMatrix = respin != null ? respin.originalMatrix : lastResult.resultMatrix;

        lastStopWasQuick = currentSpinSpeed == SpinSpeed.QuickSpin || stopRequested;

        if (slotView != null && landingMatrix != null)
        {
            if (lastStopWasQuick)
            {
                // The view reports when the snap has settled, as the normal stop does — never a fixed
                // wait, which would break as soon as the quick-stop timings are retuned.
                slotView.QuickStop(landingMatrix, OnReelsStoppedComplete);
            }
            else
            {
                slotView.StopSpin(landingMatrix, OnReelsStoppedComplete);
            }
        }
        else
        {
            OnReelsStoppedComplete();
        }
    }

    // The respin this spin carries for a built feature (Free Spins, Sandbell Pick), or null. A Ball
    // respin returns null too: that feature isn't built, so its spins land straight on the final board.
    private RespinData GetPlayableRespin(SpinResult result)
    {
        RespinData respin = result != null ? result.respin : null;
        if (respin == null || respin.originalMatrix == null) return null;
        return respin.featureName == FreeSpinsFeatureName || respin.featureName == SandbellPickFeatureName
            ? respin
            : null;
    }

    private void OnReelsStoppedComplete()
    {
        // Safety net. StopSpinSequence already cuts the loop at the exact landing moment, but it is
        // bypassed entirely when SlotView has no reels or no result matrix to stop onto. Without this
        // the loop would run until the next spin restarted it. No-ops when already stopped.
        AudioManager.Instance?.StopSpinLoop();

        // What the player saw during the spin, before the result's balance replaces it.
        balanceBeforeResult = playerData != null ? playerData.balance : 0;

        if (lastResult != null)
        {
            playerData = new PlayerData
            {
                balance = lastResult.playerData != null ? lastResult.playerData.balance : 0,
                currentBetIndex = lastResult.playerData != null ? lastResult.playerData.currentBetIndex : currentBetIndex
            };
        }

        // Anything that has to alter the board between the reels landing and the win being presented
        // belongs BEFORE PresentSpinOutcome, running it from its own completion callback rather than
        // alongside it — as the Mystery Respin does. The board has to be final before any win
        // animation starts.
        RespinData respin = GetPlayableRespin(lastResult);
        if (respin != null && slotView != null)
        {
            StartCoroutine(PlayMysteryRespin(respin));
            return;
        }

        PresentSpinOutcome();
    }

    // The near-miss respin (BonusFreeSpins.md §4): MYSTERY RESPIN, then the one reel spins again in
    // orange and lands on the final board, then the spin is presented as usual. One spin, one bet —
    // the server decided it in the same result.
    private IEnumerator PlayMysteryRespin(RespinData respin)
    {
        SpinResult result = lastResult;

        // Nothing to stop or start while the popup is up and the reel respins.
        uiManager.SetSpinStopButtonStates(isSpinningState: true, isInteractable: false);

        bool popupClosed = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.ShowMysteryRespin(() => popupClosed = true);
        yield return new WaitUntil(() => popupClosed);

        bool landed = false;
        slotView.RespinReel(respin.reelIndex, result.resultMatrix, lastStopWasQuick, () => landed = true);
        yield return new WaitUntil(() => landed);

        PresentSpinOutcome();
    }

    // Everything that happens once the board is final.
    private void PresentSpinOutcome()
    {
        // A base-game spin that awards Free Spins hands over to the feature, which presents it.
        if (IsFreeSpinsTrigger(lastResult))
        {
            StartCoroutine(PlayFreeSpinsTrigger());
            return;
        }

        // A base-game spin that wins the Sandbell Pick, likewise. Base game only (its rules).
        if (!isInFreeSpins && lastResult != null && lastResult.sandbellPick != null)
        {
            StartCoroutine(PlaySandbellPick());
            return;
        }

        if (lastResult != null && lastResult.winAmount > 0 && lastResult.winLines != null && lastResult.winLines.Count > 0)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;

            // The big-win popup is switched off for now (BigWinPopupEnabled), and never shows during
            // free spins (owner): they show their win boxes and move on.
            if (IsBigWin(multiplier))
            {
                uiManager.DisableControlsDuringWinAnimation();
                currentState = GameState.Idle;
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
                StartCoroutine(TriggerWinPopupWithDelay(1.5f, lastResult));
            }
            else
            {
                // For normal wins, trigger UI update immediately and enable controls
                uiManager.OnSpinStopping(lastResult);
                uiManager.EnableControlsAfterWinAnimation();
                uiManager.OnSpinCompleted(lastResult);
                currentState = GameState.Idle;
                slotView.ShowWinLineAnimation(lastResult.winLines, OnWinAnimationComplete);
            }
        }
        else
        {
            uiManager.OnSpinStopping(lastResult);
            currentState = GameState.Idle;

            // Still handed to the view, with nothing to present: SlotView owns the dim, and a losing
            // spin may still need it taken down.
            if (slotView != null)
            {
                slotView.ShowWinLineAnimation(null, OnWinAnimationComplete);
            }
            else
            {
                OnWinAnimationComplete();
            }
        }
    }

    // Whether a win of this many times the total bet gets the big-win popup: never while it is switched
    // off, and never during free spins.
    private bool IsBigWin(double multiplier)
    {
        return BigWinPopupEnabled && !isInFreeSpins && multiplier >= bigWinMultiplierThreshold;
    }

    private IEnumerator TriggerWinPopupWithDelay(float delay, SpinResult result)
    {
        double totalPay = GetTotalPay();
        double multiplier = totalPay > 0 ? (result.winAmount / totalPay) : 0;
        if (multiplier < bigWinMultiplierThreshold)
        {
            waitingForSpecialWin = false;
            yield break;
        }

        waitingForSpecialWin = true;

        yield return new WaitForSeconds(delay);

        if (lastResult == result && multiplier >= bigWinMultiplierThreshold)
        {
            uiManager.TriggerBigWinPopup(result, () =>
            {
                waitingForSpecialWin = false;
            });
        }
        else
        {
            waitingForSpecialWin = false;
        }
    }

    private void OnWinAnimationComplete()
    {
        if (lastResult != null)
        {
            double totalPay = GetTotalPay();
            double multiplier = totalPay > 0 ? (lastResult.winAmount / totalPay) : 0;

            // Only update UI here if it wasn't already updated in PresentSpinOutcome — a big win.
            if (IsBigWin(multiplier))
            {
                uiManager.OnSpinStopping(lastResult);
            }
        }

        StartCoroutine(ProcessSpecialFeaturesAfterWin());
    }

    private IEnumerator ProcessSpecialFeaturesAfterWin()
    {
        // Wait for special win popup to finish before starting special features
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        // A retrigger: the Drum boxes and CONGRATULATIONS, then the counter's Y climbs to its new
        // figure. No black fade and no START; the round simply carries on (owner).
        if (isInFreeSpins && retriggerPending)
        {
            yield return StartCoroutine(PlayRetriggerSequence());
        }

        ResumeAfterSpecialFeature();
    }

    private IEnumerator PlayRetriggerSequence()
    {
        retriggerPending = false;

        // One frame first: this runs from the win presentation's completion callback, and that
        // presentation tears its layer down right after the callback returns during free spins —
        // which would take the Drum boxes with it.
        yield return null;

        AudioManager.Instance?.PlayScatterTrigger();
        if (slotView != null) slotView.ShowFeatureTriggerBoxes(gameConfig != null ? gameConfig.freeSpinsSymbolId : -1);
        yield return new WaitForSeconds(TriggerBoxesHold);

        int added = retriggerTotal - freeSpinsTotal;
        if (added <= 0 && gameConfig != null) added = gameConfig.freeSpinsCount;
        yield return ShowFreeSpinsAwarded(added);

        freeSpinsTotal = retriggerTotal;
        if (freeGameView != null) freeGameView.UpdateCounter(freeSpinsPlayed, freeSpinsTotal);
    }

    // CONGRATULATIONS for an award of spins; returns once the popup has gone.
    private IEnumerator ShowFreeSpinsAwarded(int spins)
    {
        bool closed = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.ShowFreeSpinsAwarded(spins, () => closed = true);
        yield return new WaitUntil(() => closed);
    }

    private void ResumeAfterSpecialFeature()
    {
        if (isAutoPlaying || isInFreeSpins)
        {
            StartCoroutine(DelayBeforeNextRound());
        }
        else
        {
            ProcessSpinResult();
        }
    }

    private IEnumerator DelayBeforeNextRound()
    {
        float delayTime = currentSpinSpeed == SpinSpeed.QuickSpin ? 0.3f : 0.5f;
        yield return new WaitForSeconds(delayTime);

        // Wait for special win popup using the flag and active state
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        ProcessSpinResult();
    }

    // If a feature round ever wants a spin duration of its own, shorten it by the same proportion
    // Turbo shortens a base spin — the base game's Turbo duration can be longer than the feature's
    // own Normal one.
    private float GetSpinDuration()
    {
        return currentSpinSpeed switch
        {
            SpinSpeed.Normal => normalSpinDuration,
            SpinSpeed.Turbo => turboSpinDuration,
            SpinSpeed.QuickSpin => quickSpinCycleDuration,
            _ => normalSpinDuration
        };
    }

    internal void OnSpinResultReceived(SpinResult result)
    {
        lastResult = result;

        // The result is not handed to SlotView here. It writes the landed sprites itself as each reel
        // lands (StopSingleReel), in the same frame as the landing snap, so the swap is never seen.

        // Update the round's numbers as soon as the response lands, so the counter moves as the spin
        // starts. All of it is the server's: X = totalSpins - spinsRemaining, Y = totalSpins.
        if (isInFreeSpins)
        {
            FreeGameData round = result.freeGame;
            if (round == null || !round.present)
            {
                // Should never happen inside a round. Ending it is the one safe reading: the TOTAL WIN
                // shows the last total the server sent, and the balance its own figure at TAKE.
                Debug.LogError("[GameManager] A free spin's result carried no freeSpins block — ending the round.");
                freeSpinsRemaining = 0;
                return;
            }

            freeSpinsRemaining = round.spinsRemaining;
            freeSpinsPlayed = round.totalSpins - round.spinsRemaining;
            freeSpinsRoundWin = round.roundWin;

            // A retrigger's new Y waits until its Drums and popup have shown — this only records it.
            if (round.spinsAwarded)
            {
                retriggerPending = true;
                retriggerTotal = round.totalSpins;
            }
            else
            {
                freeSpinsTotal = round.totalSpins;
            }

            if (freeGameView != null) freeGameView.UpdateCounter(freeSpinsPlayed, freeSpinsTotal);
        }
    }

    private void ProcessSpinResult()
    {
        playerData = lastResult.playerData;

        uiManager.OnSpinCompleted(lastResult);

        // This is where a round is advanced. A feature added here should end its round on the server's own "still active" flag, never on
        // a spins-remaining counter reaching zero — a round can close on the very spin that reset its
        // counter, so the two do not agree.
        lastResult = null;

        if (isAutoPlaying && !isInFreeSpins)
        {
            if (autoPlayTotalRounds != -1)
            {
                autoPlayRemainingRounds--;
            }

            uiManager.UpdateAutoPlayCount();

            if (autoPlayTotalRounds != -1 && autoPlayRemainingRounds <= 0)
            {
                currentState = GameState.Idle;
                StopAutoPlay();
            }
            else
            {
                // Before requesting the next spin, verify the player can still afford it.
                // If not, stop autoplay (restores all UI) then show the popup.
                double totalPay = GetTotalPay();
                if (playerData.balance < totalPay)
                {
                    currentState = GameState.Idle;
                    StopAutoPlay();
                    if (popupManager != null) popupManager.ShowInsufficientFundsError();
                }
                else
                {
                    currentState = GameState.Idle;
                    RequestSpin();
                }
            }
        }
        else if (isInFreeSpins)
        {
            // Counters were updated in OnSpinResultReceived. spinsRemaining is the count *after*
            // this spin, so zero means the round is done; a retrigger on the last spin has already
            // put it back up. (The server's "active" flag on the last spin is not captured yet —
            // BonusFreeSpins.md §9.4.)
            if (freeSpinsRemaining <= 0)
            {
                EndFreeSpins();
            }
            else
            {
                currentState = GameState.Idle;
                StartCoroutine(DelayBeforeNextFreeSpin());
            }
        }
        else
        {
            currentState = GameState.Idle;
        }
    }

    #endregion

    #region Spin Speed Control

    internal void SetSpinSpeed(SpinSpeed speed)
    {
        currentSpinSpeed = speed;
    }

    #endregion



    #region Auto Play

    internal void StartAutoPlay(int rounds)
    {
        if (currentState != GameState.Idle) return;

        // Check balance BEFORE locking any UI — if insufficient, show popup and bail.
        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = rounds;
        autoPlayRemainingRounds = rounds;
        wasAutoPlayingBeforeFeature = false;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    internal void StopAutoPlay()
    {
        isAutoPlaying = false;
        autoPlayRemainingRounds = 0;
        wasAutoPlayingBeforeFeature = false;

        uiManager.OnAutoPlayStopped();

        // Autoplay skips the per-line cycle while it runs, so the round it just finished is parked
        // after Phase 1. Now that no further spin is coming, present it the way a manual spin would.
        // Covers both endings: the last scheduled round, and the player stopping part-way.
        //
        // Feature rounds are excluded. A round that parks autoplay before it begins may well have
        // paid a line on its triggering spin, and cycling those lines here would run them underneath
        // the feature intro for its whole duration. A feature trigger that parks autoplay in
        // ShowingWin is excluded for the same reason: the cycle would tear its trigger celebration
        // down.
        if (!isInFreeSpins && currentState != GameState.ShowingWin && slotView != null)
        {
            slotView.PlayWinLineCycle();
        }
    }

    internal bool ShouldResumeAutoPlay()
    {
        return wasAutoPlayingBeforeFeature && (savedAutoPlayTotalRounds == -1 || savedAutoPlayRemainingRounds > 0);
    }

    internal void ResumeAutoPlay()
    {
        if (!ShouldResumeAutoPlay()) return;

        int remaining = savedAutoPlayRemainingRounds;
        int total = savedAutoPlayTotalRounds;
        wasAutoPlayingBeforeFeature = false;

        if (currentState != GameState.Idle) return;

        double totalPay = GetTotalPay();
        if (playerData.balance < totalPay)
        {
            if (popupManager != null) popupManager.ShowInsufficientFundsError();
            return;
        }

        isAutoPlaying = true;
        autoPlayTotalRounds = total;
        autoPlayRemainingRounds = remaining;

        uiManager.OnAutoPlayStarted();
        RequestSpin();
    }

    #endregion

    #region Feature Buttons

    // Routed here by UIManager's FeatureStart mode. The feature that showed START set the callback.
    internal void OnFeatureStartPressed()
    {
        var callback = pendingFeatureStart;
        pendingFeatureStart = null;
        callback?.Invoke();
    }

    // Routed here by UIManager's FeatureTake mode. The feature that showed TAKE set the callback.
    internal void OnFeatureTakePressed()
    {
        var callback = pendingFeatureTake;
        pendingFeatureTake = null;
        callback?.Invoke();
    }

    // A feature parks autoplay for its whole length: Start and Take are the player's to press. The
    // trigger spin never reaches ProcessSpinResult's per-round decrement, so it is counted here.
    // StopAutoPlay clears the resume flag, so it is set after.
    private void SuspendAutoPlayForFeature()
    {
        if (!isAutoPlaying) return;

        int prevTotal = autoPlayTotalRounds;
        int prevRemaining = autoPlayRemainingRounds;

        StopAutoPlay();

        wasAutoPlayingBeforeFeature = true;
        savedAutoPlayTotalRounds = prevTotal;
        savedAutoPlayRemainingRounds = (prevTotal != -1) ? (prevRemaining - 1) : -1;
    }

    #endregion

    #region Free Spins

    // Free Spins (BonusFreeSpins.md): the trigger presentation, a round that plays itself, and TOTAL
    // WIN / TAKE. Every figure is the server's (payload.freeSpins); the views animate and call back.

    // A base-game spin that awards Free Spins. A retrigger (awarded inside the round) is not one —
    // it is presented by PlayRetriggerSequence.
    private bool IsFreeSpinsTrigger(SpinResult result)
    {
        if (isInFreeSpins || result == null || result.freeGame == null) return false;
        return result.freeGame.spinsAwarded && !result.freeGame.active;
    }

    // The trigger (BonusFreeSpins.md §5): win boxes on the Drums, CONGRATULATIONS, fade to black, the
    // Free Game panel in place of the logo, fade back in, START.
    private IEnumerator PlayFreeSpinsTrigger()
    {
        SpinResult result = lastResult;
        FreeGameData award = result.freeGame;

        // The feature owns the game from here until TAKE. ShowingWin before suspending autoplay, so
        // StopAutoPlay doesn't start a line walk under the trigger. Autoplay or not, the round waits
        // for START (owner); it resumes after TAKE.
        currentState = GameState.ShowingWin;
        SuspendAutoPlayForFeature();
        uiManager.SetFeatureButtonLock(true);

        // The trigger spin's own line win was paid on this spin: it shows in the win box, but its
        // lines are not animated — the Drums are the presentation (owner).
        uiManager.OnSpinStopping(result);
        uiManager.SetSpinStopButtonStates(isSpinningState: false, isInteractable: false);

        // The trigger spin is closed out here. Left in lastResult, START's StartSpin would hand it to
        // ProcessSpinResult — which, with the round already entered, would schedule a second spin.
        if (result.playerData != null) playerData = result.playerData;
        lastResult = null;

        AudioManager.Instance?.PlayScatterTrigger();
        if (slotView != null) slotView.ShowFeatureTriggerBoxes(gameConfig != null ? gameConfig.freeSpinsSymbolId : -1);
        yield return new WaitForSeconds(TriggerBoxesHold);

        yield return ShowFreeSpinsAwarded(award.totalSpins);

        StartFreeSpins(award);

        // The win box switches from the trigger spin's paid win to the round's running total while the
        // screen is fully black, so the change is never seen.
        bool entered = freeGameView == null;
        if (freeGameView != null)
        {
            freeGameView.EnterRound(freeSpinsPlayed, freeSpinsTotal, uiManager.RefreshFeatureDisplays, () => entered = true);
        }
        else
        {
            uiManager.RefreshFeatureDisplays();
        }
        yield return new WaitUntil(() => entered);

        pendingFeatureStart = BeginFreeSpins;
        uiManager.SetSpinButtonMode(UIManager.SpinButtonMode.FeatureStart);
    }

    // The round's state, from the trigger's figures. From here the shown balance is held at the
    // balance after the trigger spin — the same figure already on screen, so nothing visibly changes.
    // The win box is switched to the round's running total by the caller, under the black fade.
    private void StartFreeSpins(FreeGameData award)
    {
        isInFreeSpins = true;
        freeSpinsRemaining = award.spinsRemaining;
        freeSpinsTotal = award.totalSpins;
        freeSpinsPlayed = award.totalSpins - award.spinsRemaining;
        freeSpinsRoundWin = award.roundWin;
        retriggerPending = false;
        heldBalance = playerData != null ? playerData.balance : 0;
        balanceHeld = true;
    }

    // START: the first free spin, straight away.
    private void BeginFreeSpins()
    {
        currentState = GameState.Idle;

        // Out of the explicit START mode, so the spins can put the button into Stop.
        uiManager.ReleaseSpinButton(false);
        RequestSpin();
    }

    private IEnumerator DelayBeforeNextFreeSpin()
    {
        yield return new WaitForSeconds(0.3f);

        // Wait for special win popup if it's still active or pending
        while (waitingForSpecialWin || uiManager.IsSpecialWinActive)
        {
            yield return null;
        }

        RequestSpin();
    }

    // The round is over (BonusFreeSpins.md §7): TOTAL WIN counts up from 0 to the server's total, and
    // TAKE takes over the button once it has. The round is still "in free spins" until TAKE, so the
    // balance stays held.
    //
    // The last spin's win is not replayed here: free spins show no line walk, and the cycle would
    // carry on over the base game.
    private void EndFreeSpins()
    {
        currentState = GameState.ShowingWin;
        uiManager.SetSpinStopButtonStates(isSpinningState: false, isInteractable: false);

        if (featurePopupView != null) featurePopupView.ShowTotalWin(freeSpinsRoundWin, ShowFreeSpinsTake);
        else ShowFreeSpinsTake();
    }

    private void ShowFreeSpinsTake()
    {
        pendingFeatureTake = OnFreeSpinsTaken;
        uiManager.SetSpinButtonMode(UIManager.SpinButtonMode.FeatureTake);
    }

    private void OnFreeSpinsTaken()
    {
        StartCoroutine(FreeSpinsOutro());
    }

    // TAKE: the popup fades and the win boxes clear, the panel fades out while the logo fades in, and
    // the game is back in the base game with the server's balance. The round's total stays in the win
    // box until the next spin.
    private IEnumerator FreeSpinsOutro()
    {
        bool popupGone = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.HideTotalWin(() => popupGone = true);

        if (slotView != null) slotView.ClearWinPresentation();

        bool panelGone = freeGameView == null;
        if (freeGameView != null) freeGameView.ExitRound(() => panelGone = true);

        yield return new WaitUntil(() => popupGone && panelGone);

        isInFreeSpins = false;
        freeSpinsRemaining = 0;
        freeSpinsPlayed = 0;
        freeSpinsTotal = 0;
        retriggerPending = false;

        // The hold is off: the balance shown is the server's again.
        balanceHeld = false;
        uiManager.UpdateBalanceDisplay();

        // Spin again, but without clearing the win box, which still shows the round's total.
        uiManager.ReleaseSpinButton(true);
        uiManager.SetFeatureButtonLock(false);

        freeSpinsRoundWin = 0;
        currentState = GameState.Idle;
        AudioManager.Instance?.PlayBgMusic();

        if (ShouldResumeAutoPlay())
        {
            ResumeAutoPlay();
        }
    }

    #endregion

    #region Sandbell Pick

    // The Sandbell Pick (SandbellPickBonus.md §5, §6), all on the spin that won it — the pick is never
    // sent to the server:
    //  1. buttons locked, autoplay parked, the win box on GOOD LUCK, the balance held at its pre-result
    //     figure; the trigger spin's line wins are paid by the server but never shown (owner);
    //  2. CONGRATULATIONS, fading on its own;
    //  3. the pick — every Sandbell animating with its box and a pulsing PICK ME, waiting for the click
    //     with no time limit;
    //  4. the reveal — the winning value on the clicked Sandbell, the missed values on the rest;
    //  5. TOTAL WIN counts up to the prize; the win box and the server's balance then show; the popup
    //     fades on its own; Spin is live, autoplay resumes.
    // The Sandbells, boxes and values stay on screen until the next spin.
    private IEnumerator PlaySandbellPick()
    {
        SpinResult result = lastResult;
        SandbellPickData bonus = result.sandbellPick;

        // ShowingWin before suspending autoplay, so StopAutoPlay doesn't start a line walk.
        currentState = GameState.ShowingWin;
        SuspendAutoPlayForFeature();
        uiManager.SetFeatureButtonLock(true);

        heldBalance = balanceBeforeResult;
        balanceHeld = true;
        uiManager.RefreshFeatureDisplays();   // GOOD LUCK in the win box, the held balance
        uiManager.SetSpinStopButtonStates(isSpinningState: false, isInteractable: false);

        // The spin is closed out here, as the Free Spins trigger does: nothing must hand it to
        // ProcessSpinResult later.
        if (result.playerData != null) playerData = result.playerData;
        lastResult = null;

        AudioManager.Instance?.PlayScatterTrigger();

        bool congratulated = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.ShowSandbellPickAwarded(() => congratulated = true);
        yield return new WaitUntil(() => congratulated);

        // The Sandbells animate with their boxes (SlotView, the animation layer); their pick cells come
        // on over them (SandbellPickView). Only the Sandbells take clicks until the pick.
        if (slotView != null)
        {
            slotView.ShowFeatureTriggerBoxes(bonus.cells);
            slotView.SetSymbolTapsBlocked(true);
        }

        // Without the pick view nothing can be clicked: the first Sandbell stands as the pick.
        int pickedCell = bonus.cells != null && bonus.cells.Count > 0 ? bonus.cells[0] : -1;
        bool picked = sandbellPickView == null;
        if (sandbellPickView != null)
        {
            int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
            sandbellPickView.ShowPick(bonus.cells, reelCount, cell => { pickedCell = cell; picked = true; });
        }
        yield return new WaitUntil(() => picked);

        if (slotView != null) slotView.SetSymbolTapsBlocked(false);

        if (sandbellPickView != null) sandbellPickView.Reveal(pickedCell, bonus.winValue, bonus.missedValues);
        yield return new WaitForSeconds(SandbellRevealHold);

        bool countedUp = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.ShowTotalWin(bonus.prize, () => countedUp = true);
        yield return new WaitUntil(() => countedUp);

        // The count-up is done: the win box shows the prize, and the balance the server's own figure.
        balanceHeld = false;
        uiManager.ShowFeaturePayout(bonus.prize);

        yield return new WaitForSeconds(TotalWinStayHold);

        bool popupGone = featurePopupView == null;
        if (featurePopupView != null) featurePopupView.HideTotalWin(() => popupGone = true);
        yield return new WaitUntil(() => popupGone);

        // Spin is live again, keeping the prize in the win box until the next spin.
        uiManager.SetFeatureButtonLock(false);
        uiManager.ReleaseSpinButton(true);
        currentState = GameState.Idle;

        if (ShouldResumeAutoPlay())
        {
            ResumeAutoPlay();
        }
    }

    #endregion

    #region Connection Events

    internal void OnDisconnected()
    {
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }

        wasAutoPlayingBeforeFeature = false;
        if (isAutoPlaying)
        {
            StopAutoPlay();
        }

        currentState = GameState.Idle;
        // Note: The disconnection popup is shown by SocketIOManager.OnSocketDisconnected()
        // to avoid duplicates. GameManager only cleans up state here.
    }

    internal void ExitGame()
    {
        socketManager.CloseSocket();

    }

    #endregion

    #region Helper Methods

    internal double GetTotalPay()
    {
        double activeLine = (gameConfig != null && gameConfig.activeLine > 0) ? gameConfig.activeLine : 20;
        return currentBetAmount * activeLine;
    }

    internal bool IsSpinning()
    {
        return currentState == GameState.Spinning || currentState == GameState.Stopping;
    }

    #endregion
}