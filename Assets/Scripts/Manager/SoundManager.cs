using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 사운드 관리. GameManager에 붙이고 사운드 파일 4개만 넣으면 됨.
/// 1. 타이머 시작        → 브금(BGM) 반복 재생
/// 2. 플레이어 공격 시작  → 효과음 (눈빛을 쏘기 시작하는 순간)
/// 3. 경쟁자 공격 시작    → 효과음 (라이벌이 눈빛을 쏘기 시작하거나 대결에 끼어드는 순간)
/// 4. 타이머 종료        → 브금을 부드럽게 끄고 효과음
/// 다른 스크립트를 건드리지 않고, 상태가 바뀌는 순간을 지켜보다가 소리를 냄.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("사운드 파일")]
    [Tooltip("타이머가 시작되면 반복 재생")]
    [SerializeField] private AudioClip bgm;
    [Tooltip("플레이어가 눈빛 공격을 시작할 때")]
    [SerializeField] private AudioClip playerAttackStart;
    [Tooltip("경쟁자(라이벌)가 공격을 시작할 때")]
    [SerializeField] private AudioClip rivalAttackStart;
    [Tooltip("타이머가 끝났을 때")]
    [SerializeField] private AudioClip timeUp;

    [Header("볼륨")]
    [Range(0f, 1f)][SerializeField] private float bgmVolume = 0.5f;
    [Range(0f, 1f)][SerializeField] private float playerAttackVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float rivalAttackVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float timeUpVolume = 1f;

    [Header("세부 설정")]
    [Tooltip("타이머가 끝날 때 브금이 사라지는 시간(초)")]
    [SerializeField] private float bgmFadeOutTime = 0.6f;
    [Tooltip("같은 효과음이 너무 자주 겹쳐 울리지 않게 하는 최소 간격(초)")]
    [SerializeField] private float minSfxInterval = 0.25f;

    [Header("참조 (비워두면 자동으로 찾음)")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private PlayerAttack player;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private RivalAI[] rivals;
    private bool[] rivalWasAttacking;
    private bool playerWasCharming;
    private float lastPlayerSfx = -10f;
    private float lastRivalSfx = -10f;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    private void Start()
    {
        if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
        if (player == null) player = FindFirstObjectByType<PlayerAttack>();

        rivals = FindObjectsByType<RivalAI>(FindObjectsSortMode.None);
        rivalWasAttacking = new bool[rivals.Length];

        if (uiManager != null)
        {
            uiManager.OnGameStarted += HandleGameStarted;
            uiManager.OnGameEnded += HandleGameEnded;
        }
        else Debug.LogWarning("[SoundManager] 씬에 UIManager가 없어요. 브금/종료 사운드가 안 나와요.", this);
    }

    private void OnDestroy()
    {
        if (uiManager == null) return;
        uiManager.OnGameStarted -= HandleGameStarted;
        uiManager.OnGameEnded -= HandleGameEnded;
    }

    // 모든 캐릭터가 움직인 뒤에 상태 변화를 확인
    private void LateUpdate()
    {
        if (uiManager != null && !uiManager.IsPlaying) return;

        CheckPlayerAttack();
        CheckRivalAttack();
    }

    #region 1, 4. 타이머 시작 / 종료

    private void HandleGameStarted()
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = null;

        // 이전 판 상태 초기화
        playerWasCharming = false;
        for (int i = 0; i < rivalWasAttacking.Length; i++) rivalWasAttacking[i] = false;

        if (bgm == null) return;
        bgmSource.clip = bgm;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    private void HandleGameEnded()
    {
        if (bgmSource.isPlaying)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeOutBgm());
        }
        Play(timeUp, timeUpVolume);
    }

    // 게임이 끝나면 timeScale이 0이 되므로 실제 시간 기준으로 줄임
    private IEnumerator FadeOutBgm()
    {
        float start = bgmSource.volume;
        float t = 0f;
        while (t < bgmFadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(start, 0f, t / bgmFadeOutTime);
            yield return null;
        }
        bgmSource.Stop();
        bgmSource.volume = bgmVolume;
        fadeRoutine = null;
    }

    #endregion

    #region 2. 플레이어 공격 시작

    private void CheckPlayerAttack()
    {
        if (player == null) return;

        bool charming = player.IsCharming;
        if (charming && !playerWasCharming && Time.unscaledTime - lastPlayerSfx >= minSfxInterval)
        {
            Play(playerAttackStart, playerAttackVolume);
            lastPlayerSfx = Time.unscaledTime;
        }
        playerWasCharming = charming;
    }

    #endregion

    #region 3. 경쟁자 공격 시작

    private void CheckRivalAttack()
    {
        if (rivals == null) return;

        for (int i = 0; i < rivals.Length; i++)
        {
            RivalAI r = rivals[i];
            bool attacking = r != null && r.isActiveAndEnabled &&
                             (r.State == RivalState.Charming || r.State == RivalState.Dueling);

            // 공격하지 않다가 공격을 시작한 순간 (여러 마리가 동시에 시작해도 한 번만)
            if (attacking && !rivalWasAttacking[i] && Time.unscaledTime - lastRivalSfx >= minSfxInterval)
            {
                Play(rivalAttackStart, rivalAttackVolume);
                lastRivalSfx = Time.unscaledTime;
            }
            rivalWasAttacking[i] = attacking;
        }
    }

    #endregion

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }
}