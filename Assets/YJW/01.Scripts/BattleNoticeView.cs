using System.Collections.Generic;
using RealSteel.Boss.Core;
using RealSteel.Boss.JunkWheeler;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 전투 중 부위 파손·기믹 파훼를 '타이어 펑크 1/2', '왼팔 스킬 봉인'처럼 짧은 문구로 화면 위쪽에 알린다.
// 씬에 배치하지 않아도 게임 시작 때 스스로 만들어지고, 전투가 없는 씬에서는 아무것도 보이지 않는다
public class BattleNoticeView : MonoBehaviour
{
    private const float ShowTime = 2.5f;
    private const float FadeTime = 0.4f;
    private const int MaxLines = 3;
    private const float ScanInterval = 0.5f;
    private const string LossColor = "#FF6B5B";
    private const string GainColor = "#FFD34D";

    private static BattleNoticeView _instance;

    private TextMeshProUGUI _text;
    private CanvasGroup _group;
    private readonly List<string> _lines = new List<string>();
    private float _hideAt;

    private CombatDataHub _hub;
    private float _nextScan;

    // 기믹별 마지막으로 본 파괴 횟수, 보스별 봉인된 패턴 수. 늘어난 순간만 알린다
    private readonly Dictionary<BossPartGimmickBase, int> _breaks = new Dictionary<BossPartGimmickBase, int>();
    private readonly Dictionary<BossControllerBase, int> _sealed = new Dictionary<BossControllerBase, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (_instance != null) return;

        var go = new GameObject("[BattleNotice]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BattleNoticeView>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 125;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 클릭을 막지 않도록 GraphicRaycaster는 붙이지 않는다
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        var rect = (RectTransform)textGo.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -140f);
        rect.sizeDelta = new Vector2(1000f, 160f);

        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.Top;
        _text.fontSize = 34f;
        _text.raycastTarget = false;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(0, 0, 0, 200);
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + ScanInterval;
            WatchHub();
            ScanGimmicks();
        }

        if (_lines.Count > 0 && Time.unscaledTime >= _hideAt)
        {
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeTime);
            if (_group.alpha <= 0f) _lines.Clear();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // 전투 씬이 바뀌면 허브도 새로 생기므로 다시 구독한다
    private void WatchHub()
    {
        CombatDataHub hub = CombatDataHub.Instance;
        if (hub == _hub) return;

        Unsubscribe();
        _hub = hub;
        if (_hub == null) return;

        _hub.OnPlayerPartBroken += OnPlayerPartBroken;
        _hub.OnEnemyPartBroken += OnEnemyPartBroken;
    }

    private void Unsubscribe()
    {
        if (_hub == null) return;

        _hub.OnPlayerPartBroken -= OnPlayerPartBroken;
        _hub.OnEnemyPartBroken -= OnEnemyPartBroken;
        _hub = null;
    }

    private void OnPlayerPartBroken(BodyPart part)
    {
        Show(PartBrokenText(part), LossColor);
    }

    private void OnEnemyPartBroken(BodyPart part)
    {
        Show("상대 " + PartBrokenText(part), GainColor);
    }

    // 팔·다리는 그 부위 스킬(Q/W/E/R)을 못 쓰게 되므로 봉인까지 같이 알린다
    private static string PartBrokenText(BodyPart part)
    {
        string name = PartName(part);
        switch (part)
        {
            case BodyPart.LeftArm:
            case BodyPart.RightArm:
            case BodyPart.LeftLeg:
            case BodyPart.RightLeg:
                return $"{name} 파손 · {name} 스킬 봉인";
            default:
                return $"{name} 파손";
        }
    }

    private static string PartName(BodyPart part)
    {
        switch (part)
        {
            case BodyPart.Head: return "머리";
            case BodyPart.LeftArm: return "왼팔";
            case BodyPart.RightArm: return "오른팔";
            case BodyPart.LeftLeg: return "왼다리";
            case BodyPart.RightLeg: return "오른다리";
            default: return "코어";
        }
    }

    // 보스 부위 기믹은 파괴 이벤트가 따로 없어서 파괴 횟수가 늘어난 것을 보고 알린다
    private void ScanGimmicks()
    {
        var gimmicks = FindObjectsByType<BossPartGimmickBase>(FindObjectsSortMode.None);
        if (gimmicks.Length == 0)
        {
            _breaks.Clear();
            _sealed.Clear();
            return;
        }

        foreach (var gimmick in gimmicks)
        {
            int count = gimmick.BreakCount;
            if (_breaks.TryGetValue(gimmick, out int before) && count > before)
                Show(GimmickText(gimmick, count), GainColor);

            _breaks[gimmick] = count;
        }

        foreach (var gimmick in gimmicks)
        {
            BossControllerBase boss = gimmick.OwnerBoss;
            if (boss == null || boss.Snapshot == null) continue;

            int count = boss.Snapshot.sealedSkills.Count;
            if (_sealed.TryGetValue(boss, out int before) && count > before)
                Show("보스 패턴 봉인", GainColor);

            _sealed[boss] = count;
        }
    }

    private static string GimmickText(BossPartGimmickBase gimmick, int count)
    {
        if (gimmick is JunkWheelerTirePartGimmick tire)
            return tire.IsPermanentlyBroken ? "타이어 파괴 · 돌진 감속, 탄막 낮아짐" : $"타이어 펑크 {count}/2";

        return $"{gimmick.GimmickName} 파괴";
    }

    private void Show(string message, string color)
    {
        if (_text.font == null || !_text.font.HasCharacter('파', true, true))
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font != null) _text.font = font;
        }

        _lines.Add($"<color={color}>{message}</color>");
        while (_lines.Count > MaxLines) _lines.RemoveAt(0);

        _text.text = string.Join("\n", _lines);
        _group.alpha = 1f;
        _hideAt = Time.unscaledTime + ShowTime;
    }

    // 기본 TMP 폰트에는 한글이 없어서, 이미 불러온 폰트 중 한글이 있는 것을 쓴다
    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null && font.HasCharacter('파', true, true))
                return font;
        }
        return null;
    }
}
