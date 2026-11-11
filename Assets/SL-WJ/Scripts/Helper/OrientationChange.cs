using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class OrientationChange : MonoBehaviour
{
    // Wild Jokers is landscape-only (owner, 2026-11-10). On a wide screen the landscape game is fitted
    // as it is. On a screen taller than it is wide, the whole game (UIWrapper) turns -90° and is fitted
    // against the swapped screen, so it fills a phone held upright. The rotation is the one from
    // OrientationChangeRotate.md; a mode that switches between a portrait and a landscape layout,
    // for games that have both, is to be added from another project.
    [Header("References")]
    [Tooltip("What turns on a tall screen: MainCanvas/BG, the fixed 1920 × 1080 root everything visible sits under. Empty: the game is only fitted, never turned.")]
    [SerializeField] private RectTransform UIWrapper;
    [SerializeField] private CanvasScaler CanvasScaler;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 0.2f;
    [SerializeField] private float waitForRotation = 0.2f;

    // The turn on a tall screen: clockwise, as in the reference.
    private const float TallScreenAngle = -90f;

    private Vector2 referenceResolution;
    private Tween matchTween;
    private Tween rotationTween;
    private Coroutine rotationRoutine;

    // Last size the layout was applied for. Update polls against these — SwitchDisplay only fires
    // when the host page reports a resize (this game's own page never does), so without the poll
    // nothing would respond to a phone turning, or a window changing shape, after load.
    private int lastWidth = 0;
    private int lastHeight = 0;

    private void Awake()
    {
        if (CanvasScaler != null) referenceResolution = CanvasScaler.referenceResolution;

        // At once, not animated: a phone that starts upright must not be seen turning as the game loads.
        ApplyLayout(Screen.width, Screen.height, instant: true);
    }

    // The host page's resize report, "width,height" (sent by SendMessage).
    private void SwitchDisplay(string dimensions)
    {
        if (rotationRoutine != null) StopCoroutine(rotationRoutine);
        rotationRoutine = StartCoroutine(RotationCoroutine(dimensions));
    }

    private IEnumerator RotationCoroutine(string dimensions)
    {
        yield return new WaitForSecondsRealtime(waitForRotation);

        string[] parts = dimensions.Split(',');
        if (parts.Length == 2
            && int.TryParse(parts[0], out int w)
            && int.TryParse(parts[1], out int h)
            && w > 0 && h > 0)
        {
            ApplyLayout(w, h, instant: false);
        }
        else
        {
            Debug.LogWarning("[OrientationChange] Invalid dimensions: " + dimensions);
        }
    }

    // The one place the layout is decided. Today a tall screen always means "turn the landscape game";
    // the switching mode will choose between that and the portrait layout here.
    private void ApplyLayout(int screenW, int screenH, bool instant)
    {
        lastWidth = screenW;
        lastHeight = screenH;

        bool turned = IsTallScreen(screenW, screenH);
        ApplyRotation(turned, instant);
        ApplyMatch(screenW, screenH, turned, instant);
    }

    // As in the reference: anything not wider than it is tall counts as tall.
    private static bool IsTallScreen(int screenW, int screenH)
    {
        return screenW <= screenH;
    }

    private void ApplyRotation(bool turned, bool instant)
    {
        if (UIWrapper == null) return;

        Quaternion target = turned ? Quaternion.Euler(0f, 0f, TallScreenAngle) : Quaternion.identity;

        if (rotationTween != null && rotationTween.IsActive()) rotationTween.Kill();
        rotationTween = null;

        if (instant)
        {
            UIWrapper.localRotation = target;
            return;
        }

        rotationTween = UIWrapper.DOLocalRotateQuaternion(target, transitionDuration).SetEase(Ease.OutCubic);
    }

    // The CanvasScaler's match, chosen so the whole game fits the screen: the reference resolution
    // against the screen as it is, or — turned — against the screen with its sides swapped.
    private void ApplyMatch(int screenW, int screenH, bool turned, bool instant)
    {
        if (CanvasScaler == null) return;

        float refW = referenceResolution.x;
        float refH = referenceResolution.y;
        if (refW <= 0f || refH <= 0f) return;

        float widthScale = screenW / refW;
        float heightScale = screenH / refH;

        float targetScale = turned
            ? Mathf.Min(screenH / refW, screenW / refH)
            : Mathf.Min(widthScale, heightScale);

        // The scaler's scale is widthScale^(1 - match) × heightScale^match; solved for the target.
        float targetMatch;
        if (Mathf.Abs(heightScale - widthScale) < 0.0001f)
        {
            targetMatch = 0.5f;
        }
        else
        {
            float logRatio = Mathf.Log(heightScale / widthScale);
            targetMatch = Mathf.Clamp01(Mathf.Log(targetScale / widthScale) / logRatio);
        }

        if (matchTween != null && matchTween.IsActive()) matchTween.Kill();
        matchTween = null;

        if (instant)
        {
            CanvasScaler.matchWidthOrHeight = targetMatch;
            return;
        }

        matchTween = DOTween
          .To(
            () => CanvasScaler.matchWidthOrHeight,
            x => CanvasScaler.matchWidthOrHeight = x,
            targetMatch,
            transitionDuration)
          .SetEase(Ease.InOutQuad);
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            ApplyLayout(Screen.width, Screen.height, instant: false);
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
            SwitchDisplay($"{Screen.width},{Screen.height}");
#endif
    }
}
