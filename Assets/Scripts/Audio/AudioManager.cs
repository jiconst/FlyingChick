using UnityEngine;

namespace FlyingChick
{
    // Plays the procedurally-synthesized clips from ProceduralAudio.cs for
    // gameplay events and UI button clicks, plus a simple looping ambient
    // pad. Not a singleton (only GameManager/ScoreManager/SaveSystem are,
    // per project convention) -- wired in via Configure like everything
    // else. For gameplay sounds it just subscribes to events the relevant
    // systems already expose (same pattern as HUD/DailyMissions); for UI
    // clicks, StartScreen/DayOverScreen call PlayClick() directly since
    // button presses aren't already events.
    public class AudioManager : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.16f;
        [SerializeField] private int sfxSourceCount = 6;

        private AudioSource[] sfxSources;
        private int nextSfxSource;
        private AudioSource bgmSource;

        private AudioClip coinClip, speedClip, greatSlideClip, feverClip, cloudClip, islandClip, launchClip, clickClip, dayOverClip;

        private BirdController bird;
        private SlideJudge slideJudge;
        private FeverSystem fever;
        private CoinSpawner coinSpawner;
        private CloudSpawner cloudSpawner;
        private GameManager gameManager;
        private DayCycle dayCycle;

        // 설정 화면에서 슬라이더로 조절 가능한 볼륨. sfxVolume/bgmVolume은 아직
        // 직렬화 필드로 남겨서 인스펙터 초기값 용도로만 쓰고, 실제 재생 볼륨은
        // 저장된 값(LoadSavedVolumes)이 로드된 뒤 값을 그대로 씀.
        public float MusicVolume => bgmVolume;
        public float SfxVolume => sfxVolume;

        public void SetMusicVolume(float value)
        {
            bgmVolume = Mathf.Clamp01(value);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
            PersistVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            PersistVolumes();
        }

        private void LoadSavedVolumes()
        {
            if (SaveSystem.Instance == null) return;
            var data = SaveSystem.Instance.Data;
            bgmVolume = data.musicVolume;
            sfxVolume = data.sfxVolume;
        }

        private void PersistVolumes()
        {
            if (SaveSystem.Instance == null) return;
            SaveSystem.Instance.Data.musicVolume = bgmVolume;
            SaveSystem.Instance.Data.sfxVolume = sfxVolume;
            SaveSystem.Instance.Save();
        }

        private void Awake()
        {
            // 오디오 소스를 만들기 전에 저장된 볼륨부터 불러와야 bgmSource.volume에
            // 인스펙터 기본값이 아니라 저장된 값이 반영됨.
            LoadSavedVolumes();

            sfxSources = new AudioSource[sfxSourceCount];
            for (int i = 0; i < sfxSourceCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxSources[i] = src;
            }

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.volume = bgmVolume;

            BuildClips();
        }

        private void BuildClips()
        {
            coinClip = ProceduralAudio.Chime("SFX_Coin", new[] { 1046.5f, 1568f }, 0.06f, 0.35f);
            speedClip = ProceduralAudio.Sweep("SFX_Speed", 500f, 1400f, 0.25f, 0.4f);
            greatSlideClip = ProceduralAudio.Chime("SFX_GreatSlide", new[] { 784f, 987.8f, 1174.7f }, 0.08f, 0.35f);
            feverClip = ProceduralAudio.Chime("SFX_Fever", new[] { 523.3f, 659.3f, 784f, 1046.5f }, 0.09f, 0.4f);
            cloudClip = ProceduralAudio.Tone("SFX_Cloud", 1318.5f, 0.3f, 0.25f, 0.02f, 0.6f);
            islandClip = ProceduralAudio.Chime("SFX_Island", new[] { 659.3f, 830.6f, 1046.5f }, 0.1f, 0.4f);
            launchClip = ProceduralAudio.Sweep("SFX_Launch", 300f, 900f, 0.18f, 0.3f);
            clickClip = ProceduralAudio.NoiseBurst("SFX_Click", 0.05f, 0.25f);
            dayOverClip = ProceduralAudio.Chime("SFX_DayOver", new[] { 587.3f, 493.9f, 392f }, 0.35f, 0.3f);

            // Placeholder ambient bed, not real composed music. The loop
            // seam itself is fixed (see ProceduralAudio.Pad), but two
            // sustained low sine tones (220/330Hz, near the register of an
            // actual appliance/AC hum) still read as a "웅-" drone by their
            // very nature -- feedback that it's "still humming" after the
            // seam fix was really about this, not the seam. Moved up an
            // octave (440/659.3Hz) and turned down (0.5 -> 0.35) so it sits
            // further from that hum register instead of on top of it.
            bgmSource.clip = ProceduralAudio.Pad("BGM_Ambient", 440f, 659.3f, 6f, 0.35f);
        }

        public void Configure(BirdController birdRef, SlideJudge slideJudgeRef, FeverSystem feverRef, CoinSpawner coinSpawnerRef, CloudSpawner cloudSpawnerRef, GameManager gameManagerRef, DayCycle dayCycleRef)
        {
            bird = birdRef;
            slideJudge = slideJudgeRef;
            fever = feverRef;
            coinSpawner = coinSpawnerRef;
            cloudSpawner = cloudSpawnerRef;
            gameManager = gameManagerRef;
            dayCycle = dayCycleRef;

            bird.OnLaunch += HandleLaunch;
            slideJudge.OnGreatSlide += HandleGreatSlide;
            fever.OnFeverStart += HandleFeverStart;
            coinSpawner.OnCoinCollected += HandleCoin;
            coinSpawner.OnSpeedCoinCollected += HandleSpeedCoin;
            cloudSpawner.OnCloudTouched += HandleCloudTouch;
            gameManager.OnIslandAdvanced += HandleIslandAdvanced;
            gameManager.OnRunStart += HandleRunStart;
            dayCycle.OnDayOver += HandleDayOver;
        }

        private void OnDestroy()
        {
            if (bird != null) bird.OnLaunch -= HandleLaunch;
            if (slideJudge != null) slideJudge.OnGreatSlide -= HandleGreatSlide;
            if (fever != null) fever.OnFeverStart -= HandleFeverStart;
            if (coinSpawner != null)
            {
                coinSpawner.OnCoinCollected -= HandleCoin;
                coinSpawner.OnSpeedCoinCollected -= HandleSpeedCoin;
            }
            if (cloudSpawner != null) cloudSpawner.OnCloudTouched -= HandleCloudTouch;
            if (gameManager != null)
            {
                gameManager.OnIslandAdvanced -= HandleIslandAdvanced;
                gameManager.OnRunStart -= HandleRunStart;
            }
            if (dayCycle != null) dayCycle.OnDayOver -= HandleDayOver;
        }

        // Called directly by StartScreen/DayOverScreen button clicks -- those
        // aren't backed by an event to subscribe to like the gameplay ones.
        public void PlayClick() => Play(clickClip);

        private void HandleLaunch() => Play(launchClip);
        private void HandleGreatSlide(int streak, int gained) => Play(greatSlideClip);
        private void HandleFeverStart() => Play(feverClip);
        private void HandleCoin() => Play(coinClip);
        private void HandleSpeedCoin() => Play(speedClip);
        private void HandleCloudTouch() => Play(cloudClip);
        private void HandleIslandAdvanced(int island) => Play(islandClip);
        private void HandleDayOver() => Play(dayOverClip);

        private void HandleRunStart()
        {
            if (!bgmSource.isPlaying) bgmSource.Play();
        }

        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            var src = sfxSources[nextSfxSource];
            nextSfxSource = (nextSfxSource + 1) % sfxSources.Length;
            src.PlayOneShot(clip, sfxVolume);
        }
    }
}
