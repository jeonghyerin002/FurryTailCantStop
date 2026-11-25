using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// 11강 더미 광고 SDK. 게임 씬의 빈 오브젝트에 붙이면 끝 (UI 를 따로 만들 필요 없음)
// 실제 광고 SDK 와 같은 순서로 동작: 미리 로드 → 준비 확인 → 표시 → 닫힘 (보상 여부) → 다시 로드
// 메디에이션도 흉내 냄: 가짜 네트워크 여러 개에 물어보고 하나를 고름 (워터폴 또는 비딩)
// 사용법
//   전면 광고:  DummyAds.Instance.ShowInterstitial(닫힌 뒤 부를 함수);
//   보상형 광고: DummyAds.Instance.ShowRewarded(닫힌 뒤 부를 함수);   ← 그 함수는 bool earned 를 받음
[Serializable]
public class DummyNetwork
{
    public string name;                            // 네트워크 이름
    public float eCPM;                             // 평균 단가 ($, 노출 1000 번당)
    [Range(0f, 1f)] public float fillRate;         // 광고를 채워 줄 확률
}

public class DummyAds : MonoBehaviour
{
    public static DummyAds Instance;
    public DummyNetwork[] networks = {             // 위에서부터 워터폴 순서
        new DummyNetwork { name = "Network A", eCPM = 10f, fillRate = 0.5f },
        new DummyNetwork { name = "Network B", eCPM = 9f, fillRate = 0.6f },
        new DummyNetwork { name = "Network C", eCPM = 7f, fillRate = 0.8f },
    };
    public bool useBidding;                        // 꺼짐: 워터폴 / 켜짐: 비딩
    public bool noFill;                            // 켜면 모든 네트워크가 광고 없음 (테스트용)
    public float askSeconds = 0.5f;                // 네트워크 한 곳에 물어보는 데 걸리는 시간
    public float interstitialSeconds = 3f;         // 전면 광고 길이
    public float rewardedSeconds = 5f;             // 보상형 광고 길이

    public static int Impressions;                 // 누적 노출 수
    public static float Revenue;                   // 누적 예상 수익 ($)

    DummyNetwork interstitialAd, rewardedAd;       // 받아 둔 광고. 없으면 null
    float interstitialPrice, rewardedPrice;
    int asked; float pickedPrice;                  // 마지막 Pick 의 결과
    bool showing, canClose, closed;
    string label = "";
    float prevScale = 1f;
    EventSystem eventSystem;

    void Awake() { Instance = this; }
    void Start() { LoadInterstitial(); LoadRewarded(); }      // 시작하자마자 미리 받아 둠

    // ---------- 로드
    public void LoadInterstitial() { StartCoroutine(Load(false)); }
    public void LoadRewarded() { StartCoroutine(Load(true)); }
    public bool IsInterstitialReady() { return interstitialAd != null; }
    public bool IsRewardedReady() { return rewardedAd != null; }

    IEnumerator Load(bool rewarded)
    {
        Debug.Log((rewarded ? "Rewarded" : "Interstitial") + " 로드 요청 (" + (useBidding ? "비딩" : "워터폴") + ")");
        DummyNetwork ad = Pick(true); float price = pickedPrice;   // 가격은 고른 직후에 따로 보관 (기다리는 동안 다른 로드가 덮어씀)
        yield return new WaitForSecondsRealtime(askSeconds * (useBidding ? 1 : asked));   // 워터폴은 물어본 만큼 오래 걸림
        if (rewarded) { rewardedAd = ad; rewardedPrice = price; } else { interstitialAd = ad; interstitialPrice = price; }
        Debug.Log((rewarded ? "Rewarded " : "Interstitial ") + (ad != null ? "loaded: " + ad.name : "load failed (no fill)"));
        if (ad != null) yield break;
        yield return new WaitForSecondsRealtime(2f);          // 실패하면 잠시 뒤 다시 시도
        StartCoroutine(Load(rewarded));
    }

    // 메디에이션: 네트워크들에 물어보고 하나를 고름. 고른 가격은 pickedPrice 에
    DummyNetwork Pick(bool log)
    {
        DummyNetwork winner = null; asked = 0; pickedPrice = 0f;
        foreach (DummyNetwork n in networks)
        {
            asked++;
            bool filled = !noFill && UnityEngine.Random.value < n.fillRate;
            float price = n.eCPM * UnityEngine.Random.Range(0.5f, 1.5f);    // 같은 네트워크도 요청마다 값이 다름
            if (log) Debug.Log("    " + n.name + (filled ? " : $" + price.ToString("F1") : " : 광고 없음"));
            if (!filled) continue;
            if (price > pickedPrice || !useBidding) { winner = n; pickedPrice = price; }
            if (!useBidding) break;                                         // 워터폴: 처음 채운 곳에서 멈춤
        }                                                                   // 비딩: 끝까지 묻고 가장 비싼 곳
        return winner;
    }

    // ---------- 표시
    public void ShowInterstitial(Action onClosed)
    {
        if (interstitialAd == null) { onClosed(); return; }   // 광고가 없으면 기다리지 않고 진행
        DummyNetwork ad = interstitialAd; interstitialAd = null;
        StartCoroutine(Play(ad, interstitialPrice, interstitialSeconds, false, earned => onClosed()));
    }

    public void ShowRewarded(Action<bool> onClosed)            // onClosed(true) = 끝까지 봄
    {
        if (rewardedAd == null) { onClosed(false); return; }
        DummyNetwork ad = rewardedAd; rewardedAd = null;
        StartCoroutine(Play(ad, rewardedPrice, rewardedSeconds, true, onClosed));
    }

    IEnumerator Play(DummyNetwork ad, float price, float seconds, bool rewarded, Action<bool> onClosed)
    {
        Impressions++; Revenue += price / 1000f; Debug.Log("노출 " + Impressions + "회, 누적 예상 수익 $" + Revenue.ToString("F4"));
        Pause(true);                                           // 광고 중: 게임 정지, 소리 끔
        showing = true; closed = false; canClose = rewarded;   // 보상형은 중간에 닫을 수 있음 (보상 없음)
        while (seconds > 0f && !closed)
        {
            label = (rewarded ? "보상형 광고 " : "전면 광고 ") + Mathf.CeilToInt(seconds) + "\n" + ad.name;
            seconds -= Time.unscaledDeltaTime; yield return null;
        }
        bool earned = !closed; label = "광고 끝"; canClose = true;   // 끝까지 봤는가
        while (!closed) yield return null;                     // 닫기 버튼을 기다림
        showing = false; Pause(false);
        if (rewarded) LoadRewarded(); else LoadInterstitial(); // 다음 광고를 미리 받음
        onClosed(earned);                                      // 게임 쪽 함수를 부름
    }


    void Pause(bool on)
    {
        if (on) { prevScale = Time.timeScale; Time.timeScale = 0f; eventSystem = EventSystem.current; }
        else Time.timeScale = prevScale;
        AudioListener.pause = on;
        if (eventSystem != null) eventSystem.enabled = !on;    // 광고 뒤의 게임 버튼이 눌리지 않게
    }

    // ---------- 설정 비교용. Inspector 에서 DummyAds 컴포넌트 이름을 우클릭 → 100회 시뮬레이션
    [ContextMenu("100회 시뮬레이션")]
    void Simulate()
    {
        int filled = 0, askedSum = 0; float sum = 0f;
        for (int i = 0; i < 100; i++)
        {
            DummyNetwork ad = Pick(false);
            askedSum += useBidding ? 1 : asked;
            if (ad != null) { filled++; sum += pickedPrice; }
        }
        Debug.Log((useBidding ? "비딩" : "워터폴") + " 100회: 채움 " + filled + "%, 평균 eCPM $" + (filled > 0 ? sum / filled : 0f).ToString("F1")
            + ", 예상 수익 $" + (sum / 1000f).ToString("F3") + ", 평균 대기 " + (askedSum / 100f * askSeconds).ToString("F2") + "초");
    }

    // ---------- 광고 화면 (코드로 직접 그림)
    void OnGUI()
    {
        if (!showing) return;
        GUI.depth = -1000;
        GUI.color = new Color(0.07f, 0.1f, 0.15f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        int unit = Mathf.Max(Screen.height / 18, 16);
        GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = unit, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), label, text);
        if (!canClose) return;
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = unit };
        if (GUI.Button(new Rect(Screen.width - unit * 2.4f, unit * 0.4f, unit * 2f, unit * 2f), "X", button)) closed = true;
    }
}