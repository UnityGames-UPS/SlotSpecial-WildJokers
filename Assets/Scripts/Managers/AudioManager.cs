using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    internal static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicEnabled = PlayerPrefs.GetInt(PrefKeyMusic, 1) == 1;
        _sfxEnabled   = PlayerPrefs.GetInt(PrefKeysfx,   1) == 1;
        _musicVolume  = PlayerPrefs.GetFloat(PrefKeyMusicVol, 0.5f);
        _sfxVolume    = PlayerPrefs.GetFloat(PrefKeySfxVol,   1.0f);

        ApplyMusicVolume();
        ApplySfxVolume();
    }

    private const string PrefKeyMusic    = "audio_music_enabled";
    private const string PrefKeysfx      = "audio_sfx_enabled";
    private const string PrefKeyMusicVol = "audio_music_volume";
    private const string PrefKeySfxVol   = "audio_sfx_volume";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource wheelSegmentSource;
    [SerializeField] private AudioSource reserveSource;
    [Tooltip("Dedicated source for the spin loop. Needs its own source because the loop is stopped " +
             "at landing, and stopping a shared source would cut the reel-stop one-shots firing at " +
             "that same moment.")]
    [SerializeField] private AudioSource spinLoopSource;
    [Tooltip("Dedicated source for the scatter-anticipation tension. It is stopped the moment the last " +
             "held reel lands, which is exactly when the reel-stop one-shots fire — so it cannot share " +
             "their source, and the spin loop's source is still busy with the reels at that point.")]
    [SerializeField] private AudioSource anticipationSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip clipGameMainBg;
    [SerializeField] private AudioClip clipBetPlusMinus;
    [SerializeField] private AudioClip clipMaxBetReached;
    [SerializeField] private AudioClip clipScatterTrigger;
    [SerializeField] private AudioClip clipBigWin;
    [Tooltip("Spin button only. Stop / Take / AutoplayStop share clipPrimaryActionButton below.")]
    [SerializeField] private AudioClip clipSpinStart;
    [SerializeField] private AudioClip clipPrimaryActionButton;
    [SerializeField] private AudioClip clipGeneralButtonClick;
    [SerializeField] private AudioClip clipPopupOpenClose;
    [SerializeField] private AudioClip clipAutoplayPanelOpen;
    [SerializeField] private AudioClip clipWinPresentationStart;
    [SerializeField] private AudioClip clipReelStop;
    [Tooltip("One shot per reel that lands at least one Scatter (Lamp).")]
    [SerializeField] private AudioClip clipScatterLand;
    [SerializeField] private AudioClip clipTurboButton;

    [Header("Audio Clips - Golden Dynasty")]
    [Tooltip("Phase 2 of the win presentation moving to the next win line. Fires on every change, and Phase 2 cycles until the player spins.")]
    [SerializeField] private AudioClip clipWinLineChange;

    [Tooltip("A Wild ANIMATING as part of a win — once per spin, in Phase 1 only. Wild landings have no cue.")]
    [SerializeField] private AudioClip clipWildAnimate;

    [Tooltip("The win amount counting up in the universal win popup.")]
    [SerializeField] private AudioClip clipWinCountUp;

    [Header("Audio Clips - Gold of Luck")]
    [Tooltip("The free-games congratulations panel opening.")]
    [SerializeField] private AudioClip clipCongratulations;

    [Tooltip("The free-games congratulations panel closing.")]
    [SerializeField] private AudioClip clipCongratsClose;

    [Tooltip("Scatter anticipation: plays while reels are held after two Lamps have landed, and is cut when the last held reel lands. Needs anticipationSource.")]
    [SerializeField] private AudioClip clipAnticipation;

    [Tooltip("The big-win popup's sound (huge win.wav). Plays ONCE as the popup opens, and is cut when the popup " +
             "closes — on Take or at the auto-close. While unassigned, the old looping clipBigWin plays instead.")]
    [SerializeField] private AudioClip clipHugeWin;

    // A music switch dips rather than overlaps: there is one music source, so the old track fades out
    // over the first half and the new one starts and fades in over the second.
    private const float MusicFadeDuration = 0.5f;
    private Coroutine musicFadeRoutine;

    private bool _musicEnabled = true;
    private bool _sfxEnabled   = true;
    private float _musicVolume = 0.5f;
    private float _sfxVolume   = 1.0f;

    internal float MusicVolume => _musicVolume;
    internal float SfxVolume   => _sfxVolume;

    internal void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeyMusicVol, _musicVolume);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeySfxVol, _sfxVolume);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    private void ApplyMusicVolume()
    {
        if (bgMusicSource == null) return;
        bgMusicSource.volume = _musicEnabled ? _musicVolume : 0f;
    }

    private void ApplySfxVolume()
    {
        float v = _sfxEnabled ? _sfxVolume : 0f;
        if (uiSource           != null) uiSource.volume           = v;
        if (wheelSegmentSource != null) wheelSegmentSource.volume = v;
        if (reserveSource      != null) reserveSource.volume      = v;
        if (spinLoopSource     != null) spinLoopSource.volume     = v;
        if (anticipationSource != null) anticipationSource.volume = v;
    }

    /// <summary>
    /// Uses UI source (AudioSource 2). If busy/playing, falls back to reserve source (AudioSource 4).
    /// </summary>
    private void PlayUISound(AudioClip clip)
    {
        if (!_sfxEnabled || clip == null) return;

        if (uiSource != null && !uiSource.isPlaying)
        {
            uiSource.PlayOneShot(clip);
        }
        else if (reserveSource != null)
        {
            reserveSource.PlayOneShot(clip);
        }
        else if (uiSource != null)
        {
            uiSource.PlayOneShot(clip);
        }
    }

    // A looping effect, so it follows the sfx toggle and slider rather than the music volume.
    private void PlaySfxLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip   = clip;
        source.loop   = true;
        source.volume = _sfxEnabled ? _sfxVolume : 0f;
        source.Play();
    }

    // A single play of an EFFECT that may need cutting short — Play() rather than PlayOneShot, which
    // StopSource could not stop. Follows the sfx volume like PlaySfxLoop.
    private void PlaySfxOnce(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip   = clip;
        source.loop   = false;
        source.volume = _sfxEnabled ? _sfxVolume : 0f;
        source.Play();
    }

    private void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    // 1. Game Main BG. Starts instantly at load; coming back from a feature's music it fades across
    // and restarts the main track from the beginning (owner).
    internal void PlayBgMusic() => SwitchMusic(clipGameMainBg);

    // The one way the music track changes. Already playing that track: nothing happens, so it never
    // restarts mid-play. Nothing playing yet: starts straight away. Otherwise the 0.5 s dip. A missing
    // clip leaves whatever is playing alone.
    private void SwitchMusic(AudioClip clip)
    {
        if (bgMusicSource == null || clip == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clip) return;

        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (!bgMusicSource.isPlaying)
        {
            StartMusic(clip, _musicEnabled ? _musicVolume : 0f);
            return;
        }

        musicFadeRoutine = StartCoroutine(FadeToMusic(clip));
    }

    private IEnumerator FadeToMusic(AudioClip clip)
    {
        float half = MusicFadeDuration * 0.5f;
        float startVolume = bgMusicSource.volume;

        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            bgMusicSource.volume = Mathf.Lerp(startVolume, 0f, t / half);
            yield return null;
        }

        StartMusic(clip, 0f);

        // The target is re-read every frame, so a slider moved during the fade still lands right.
        for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
        {
            bgMusicSource.volume = Mathf.Lerp(0f, _musicEnabled ? _musicVolume : 0f, t / half);
            yield return null;
        }

        ApplyMusicVolume();
        musicFadeRoutine = null;
    }

    // From the beginning, looped.
    private void StartMusic(AudioClip clip, float volume)
    {
        bgMusicSource.clip   = clip;
        bgMusicSource.loop   = true;
        bgMusicSource.volume = volume;
        bgMusicSource.Play();
    }

    // 2. Bet Plus / Bet Minus (one for both)
    internal void PlayBetPlusMinus()
    {
        PlayUISound(clipBetPlusMinus);
    }

    // 3. Max Bet Reached
    internal void PlayMaxBetReached()
    {
        PlayUISound(clipMaxBetReached);
    }

    // 4. Bonus-trigger stinger (scatters triggering a feature) — a one-shot.
    internal void PlayScatterTrigger()
    {
        PlayUISound(clipScatterTrigger);
    }

    // 5. The big-win popup's sound. Gold of Luck's huge win plays once; Golden Dynasty's big win, the
    // fallback while it is unassigned, loops. Either way it is played with Play() on uiSource rather
    // than PlayOneShot, because a one-shot cannot be cut — and it has to stop with the popup. While it
    // plays, uiSource reads as busy, so other UI sounds go to reserveSource and are not cut with it.
    internal void PlayBigWin()
    {
        if (!_sfxEnabled) return;

        if (clipHugeWin != null)
        {
            PlaySfxOnce(uiSource, clipHugeWin);
            return;
        }

        if (clipBigWin == null) return;
        PlaySfxLoop(uiSource, clipBigWin);
    }

    internal void StopBigWin()
    {
        if (uiSource != null && IsBigWinClip(uiSource.clip))
        {
            StopSource(uiSource);
        }
        if (reserveSource != null && IsBigWinClip(reserveSource.clip))
        {
            StopSource(reserveSource);
        }
    }

    private bool IsBigWinClip(AudioClip clip)
    {
        return clip != null && (clip == clipHugeWin || clip == clipBigWin);
    }

    // 6. Stop / Take / AutoplayStop / FeatureStart Btn Sound
    internal void PlayPrimaryActionButton()
    {
        PlayUISound(clipPrimaryActionButton != null ? clipPrimaryActionButton : clipGeneralButtonClick);
    }

    // Spin is a duration sound, not a button click: it loops for as long as the reels turn and is cut
    // by StopSpinLoop at landing.
    internal void PlaySpinStart()
    {
        if (!_sfxEnabled) return;

        if (clipSpinStart != null)
        {
            PlaySfxLoop(spinLoopSource, clipSpinStart);
        }
        else
        {
            // No spin clip assigned: fall back to the shared primary-action click as a one-shot.
            // Looping a button click would be worse than the missing sound it stands in for.
            PlayUISound(clipPrimaryActionButton);
        }
    }

    // Safe to call when nothing is playing, which is what lets the two call sites in the reel-stop
    // path both fire without coordinating.
    internal void StopSpinLoop()
    {
        StopSource(spinLoopSource);
    }

    internal void PlaySpinStop()     => PlayPrimaryActionButton();
    internal void PlayTakeButton()   => PlayPrimaryActionButton();
    internal void PlayAutoplayStop() => PlayPrimaryActionButton();

    // 7. General Button Click
    internal void PlayButton()
    {
        PlayUISound(clipGeneralButtonClick);
    }

    // 8. Popup Open Close Sound
    internal void PlayPopupOpenClose()
    {
        PlayUISound(clipPopupOpenClose != null ? clipPopupOpenClose : clipGeneralButtonClick);
    }

    internal void PlayPopupClose() => PlayPopupOpenClose();

    // 9. Autoplay Panel Open Sound
    internal void PlayAutoplayPanelOpen()
    {
        PlayUISound(clipAutoplayPanelOpen != null ? clipAutoplayPanelOpen : clipPopupOpenClose);
    }

    // 13. Win Line Phase 1 Start
    internal void PlayWinPresentationStart()
    {
        PlayUISound(clipWinPresentationStart);
    }

    // 14. Slot Reel Column Stop Sound
    internal void PlayReelStop()
    {
        if (!_sfxEnabled || clipReelStop == null) return;

        if (wheelSegmentSource != null)
            wheelSegmentSource.PlayOneShot(clipReelStop);
        else
            PlayUISound(clipReelStop);
    }

    // 15. Scatter landing — one shot per reel that contains at least one, not per symbol. Wild
    // landings deliberately have NO cue in this game: the Wild is announced when it animates.
    internal void PlayScatterLand() => PlayUISound(clipScatterLand);

    // 16. Turbo / spin-speed toggle
    internal void PlayTurboButton() => PlayUISound(clipTurboButton);

    // 17. Golden Dynasty cues.
    internal void PlayWinLineChange()      => PlayUISound(clipWinLineChange);
    internal void PlayWildAnimate()        => PlayUISound(clipWildAnimate);
    internal void PlayWinCountUp()         => PlayUISound(clipWinCountUp);

    // 18. Gold of Luck cues.
    internal void PlayCongratulations() => PlayUISound(clipCongratulations);
    internal void PlayCongratsClose() => PlayUISound(clipCongratsClose);

    // Scatter anticipation is a DURATION sound: it runs while reels are held and stops when the last
    // held reel lands. Several reels can be held in one spin, so it is started once and left running
    // across them — a second call while it plays does nothing rather than restarting it. Not looped:
    // the clip outlasts any hold.
    private bool anticipationSourceWarned;
    internal void PlayAnticipation()
    {
        if (!_sfxEnabled || clipAnticipation == null) return;

        if (anticipationSource == null)
        {
            if (!anticipationSourceWarned)
            {
                anticipationSourceWarned = true;
                Debug.LogWarning("[AudioManager] anticipationSource is not wired — the scatter anticipation sound is silent.");
            }
            return;
        }

        if (anticipationSource.isPlaying) return;

        anticipationSource.clip   = clipAnticipation;
        anticipationSource.loop   = false;
        anticipationSource.volume = _sfxVolume;
        anticipationSource.Play();
    }

    // Safe to call when nothing is playing.
    internal void StopAnticipation()
    {
        StopSource(anticipationSource);
    }

    private bool isForceMuted = false;

    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        AudioListener.volume = forceMute ? 0f : 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetMuteAll(!hasFocus);
    }

    private void OnApplicationPause(bool isPaused)
    {
        SetMuteAll(isPaused);
    }
}
