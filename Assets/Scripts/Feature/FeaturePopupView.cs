using System;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// The feature popup shared by all three features (BonusFreeSpins.md §8): a backdrop that rotates
/// clockwise, a frame, and one child per message, of which exactly one is shown. A CanvasGroup on
/// this object fades it out.
///
/// Free Spins uses MYSTERY RESPIN, CONGRATULATIONS (with the awarded spin count) and TOTAL WIN (counted
/// up). The Sandbell's and the Ball's CONGRATULATIONS children are wired when those features are
/// built.
///
/// View layer only: GameManager calls in and is called back when a message has gone. Everything is
/// DOTween-driven rather than coroutines, because this object is switched off between uses. Every
/// reference is optional — a missing message still calls back, so a round never stalls on it.
/// </summary>
public class FeaturePopupView : MonoBehaviour
{
    [Header("Backdrop")]
    [Tooltip("PopupBackground — rotates clockwise while the popup is up.")]
    [SerializeField] private RectTransform popupBackground;

    [Header("Messages (children of Popup)")]
    [Tooltip("MysteryRespin — shown before a near-miss respin.")]
    [SerializeField] private GameObject mysteryRespin;
    [Tooltip("CongratulationsFreeSpins — shown when Free Spins are awarded, on a trigger or a retrigger.")]
    [SerializeField] private GameObject congratulationsFreeSpins;
    [Tooltip("TotalWin — the round's total, counted up, until TAKE.")]
    [SerializeField] private GameObject totalWin;

    [Header("Texts (sprite digits)")]
    [Tooltip("10Text — the number of spins awarded.")]
    [SerializeField] private TMPro.TMP_Text awardedSpinsText;
    [Tooltip("TotalWinAmount — the round's total win.")]
    [SerializeField] private TMPro.TMP_Text totalWinAmountText;

    // Tuning — placeholders until the owner signs off the timings (BonusFreeSpins.md §11).
    private const float MysteryRespinHold = 1.5f;        // seconds MYSTERY RESPIN stays before it fades
    private const float CongratulationsHold = 2.5f;      // seconds CONGRATULATIONS stays before it fades
    private const float FadeOutDuration = 0.4f;          // the CanvasGroup's fade to 0
    private const float RotationPeriod = 8f;             // seconds for one full clockwise turn of the backdrop
    private const float TotalWinCountUpDuration = 2f;    // TOTAL WIN's count from 0 to the total

    private CanvasGroup canvasGroup;
    private bool canvasGroupLooked;
    private Tween rotationTween;
    private Sequence activeSequence;
    private Tween countUpTween;

    // Found on first use rather than in Awake: this object starts switched off, and Awake would not
    // run until the first Open — after the alpha had to be set.
    private CanvasGroup Group
    {
        get
        {
            if (!canvasGroupLooked)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                canvasGroupLooked = true;
            }
            return canvasGroup;
        }
    }

    #region Called by GameManager

    /// <summary>Hidden, with nothing running. Called at init, so the scene's saved state never shows.</summary>
    internal void HideImmediate()
    {
        KillTweens();
        StopRotation();
        ShowOnly(null);
        gameObject.SetActive(false);
    }

    /// <summary>MYSTERY RESPIN: shows, holds, fades out. onClosed fires once it has gone.</summary>
    internal void ShowMysteryRespin(Action onClosed)
    {
        ShowThenFade(mysteryRespin, MysteryRespinHold, onClosed);
    }

    /// <summary>
    /// CONGRATULATIONS! YOU WON <spins> FREE SPINS: shows, holds, fades out. onClosed fires once it
    /// has gone. spins is the server's award, never a constant.
    /// </summary>
    internal void ShowFreeSpinsAwarded(int spins, Action onClosed)
    {
        if (awardedSpinsText != null) awardedSpinsText.text = SpriteTextFormatter.ToSpriteDigits(spins.ToString());

        AudioManager.Instance?.PlayCongratulations();
        ShowThenFade(congratulationsFreeSpins, CongratulationsHold, onClosed);
    }

    /// <summary>
    /// TOTAL WIN: shows and counts up from 0 to amount. It stays up — HideTotalWin takes it down.
    /// onCountedUp fires when the count has reached the total.
    /// </summary>
    internal void ShowTotalWin(double amount, Action onCountedUp)
    {
        if (totalWin == null)
        {
            onCountedUp?.Invoke();
            return;
        }

        Open(totalWin);

        double shown = 0;
        WriteTotal(0);
        AudioManager.Instance?.PlayWinCountUp();

        countUpTween = DOTween.To(() => shown, value => { shown = value; WriteTotal(value); }, amount, TotalWinCountUpDuration)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                countUpTween = null;
                WriteTotal(amount);   // land exactly on the server's figure
                onCountedUp?.Invoke();
            });
    }

    /// <summary>TOTAL WIN goes: fades out. onHidden fires once it has gone.</summary>
    internal void HideTotalWin(Action onHidden)
    {
        if (!gameObject.activeSelf)
        {
            onHidden?.Invoke();
            return;
        }

        KillTweens();
        FadeOut(onHidden);
    }

    #endregion

    #region Sequences

    // One message: open, hold, fade out, close, call back. A missing message calls straight back.
    private void ShowThenFade(GameObject message, float hold, Action onClosed)
    {
        if (message == null)
        {
            onClosed?.Invoke();
            return;
        }

        Open(message);

        activeSequence = DOTween.Sequence()
            .AppendInterval(hold)
            .OnComplete(() =>
            {
                activeSequence = null;
                FadeOut(onClosed);
            });
    }

    // Switches the popup on, fully visible, with only this message showing and the backdrop turning.
    private void Open(GameObject message)
    {
        KillTweens();

        ShowOnly(message);
        if (Group != null) Group.alpha = 1f;
        gameObject.SetActive(true);

        StartRotation();
    }

    private void FadeOut(Action onClosed)
    {
        void Close()
        {
            activeSequence = null;
            StopRotation();
            ShowOnly(null);
            gameObject.SetActive(false);
            if (Group != null) Group.alpha = 1f;
            onClosed?.Invoke();
        }

        if (Group == null)
        {
            Close();
            return;
        }

        activeSequence = DOTween.Sequence()
            .Append(Group.DOFade(0f, FadeOutDuration).SetEase(Ease.Linear))
            .OnComplete(Close);
    }

    #endregion

    #region Helpers

    // Clockwise is a negative z angle in Unity's UI.
    private void StartRotation()
    {
        StopRotation();
        if (popupBackground == null) return;

        rotationTween = popupBackground
            .DORotate(new Vector3(0f, 0f, -360f), RotationPeriod, RotateMode.FastBeyond360)
            .SetRelative(true)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart);
    }

    private void StopRotation()
    {
        rotationTween?.Kill();
        rotationTween = null;
        if (popupBackground != null) popupBackground.localRotation = Quaternion.identity;
    }

    // Exactly one message on (or none, for null).
    private void ShowOnly(GameObject message)
    {
        if (mysteryRespin != null) mysteryRespin.SetActive(mysteryRespin == message);
        if (congratulationsFreeSpins != null) congratulationsFreeSpins.SetActive(congratulationsFreeSpins == message);
        if (totalWin != null) totalWin.SetActive(totalWin == message);
    }

    private void WriteTotal(double amount)
    {
        if (totalWinAmountText != null) totalWinAmountText.text = SpriteTextFormatter.ToSpriteMoney(amount);
    }

    // Killed without completing, so no callback of an interrupted message ever fires.
    private void KillTweens()
    {
        activeSequence?.Kill();
        activeSequence = null;
        countUpTween?.Kill();
        countUpTween = null;
        if (Group != null) Group.DOKill();
    }

    private void OnDestroy()
    {
        KillTweens();
        rotationTween?.Kill();
    }

    #endregion
}
