using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 쿄다이 임시 체력 표시 (2026-11-02, 정식 UI 전까지 테스트용 — OnGUI로 그려서 캔버스 설정이 필요 없다).
///
///   · 화면 위: 보스 HP(공유) 막대·숫자, 그로기 문턱(KyodaiBossController.groggyHpRatios) 눈금
///   · 부위 막대: 부위(BossHurtZone)를 때리면 맞은 자리 위에 잠깐 뜬다
///
/// 부위 값은 기획서10_28 "부위 구성 HP 구조"의 무력화 게이지 자리 — 보스 HP는 공유이고 부위는 맞은 양만 따로 센다.
/// 게이지 최대치는 기획 수치가 없어 임시값(partGaugeMax)이고, 다 깎여도 봉인 같은 효과는 아직 없다.
/// KyodaiBossController가 시작할 때 붙인다(showDebugHp).
/// </summary>
public class KyodaiHpDebugView : MonoBehaviour
{
    [Tooltip("TODO: 기획 수치 없음. 부위 게이지 최대치(부위에 들어간 최종 데미지 합 기준)")]
    [Min(1)] [SerializeField] private int partGaugeMax = 1000;

    [Tooltip("부위 막대가 마지막으로 맞은 뒤 떠 있는 시간(초)")]
    [Min(0.1f)] [SerializeField] private float partShowSeconds = 1.5f;

    [Tooltip("맞은 자리에서 막대를 얼마나 위에 띄울지(월드)")]
    [SerializeField] private float partBarLift = 1.2f;

    private class PartGauge
    {
        public int damage;
        public float shownUntil;
        public Vector3 worldPoint;
    }

    private KyodaiBossController boss;
    private readonly Dictionary<string, PartGauge> parts = new Dictionary<string, PartGauge>();
    private readonly List<BossHurtZone> zones = new List<BossHurtZone>();
    private GUIStyle label;
    private float nextScan;

    private void Awake() => boss = GetComponent<KyodaiBossController>();

    private void OnDestroy()
    {
        foreach (var zone in zones) if (zone != null) zone.OnHit -= OnZoneHit;
    }

    // 위팔 판정은 KyodaiLimb.Start에서 복제되므로 1초마다 새로 생긴 자리를 찾아 구독한다
    private void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 1f;
        foreach (var zone in FindObjectsByType<BossHurtZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zones.Contains(zone)) continue;
            zones.Add(zone);
            zone.OnHit += OnZoneHit;
        }
    }

    private void OnZoneHit(BossHurtZone zone, int damage)
    {
        string id = string.IsNullOrEmpty(zone.PartId) ? zone.name : zone.PartId;
        if (!parts.TryGetValue(id, out var gauge)) parts[id] = gauge = new PartGauge();
        gauge.damage = Mathf.Min(partGaugeMax, gauge.damage + Mathf.Max(0, damage));
        gauge.shownUntil = Time.time + partShowSeconds;

        // 맞은 자리 = 판정 상자에서 플레이어와 가장 가까운 점 (때린 Hit박스 위치는 이벤트로 안 넘어온다)
        var player = GameObject.FindWithTag("Player");
        Vector2 from = player != null ? (Vector2)player.transform.position : (Vector2)zone.Box.bounds.center;
        Vector2 point = zone.Box.ClosestPoint(from);
        gauge.worldPoint = new Vector3(point.x, point.y + partBarLift, zone.transform.position.z);
    }

    private void OnGUI()
    {
        if (boss == null || boss.Snapshot == null) return;
        label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        DrawBossBar();
        DrawPartBars();
    }

    private void DrawBossBar()
    {
        var s = boss.Snapshot;
        float ratio = s.maxHp > 0 ? Mathf.Clamp01(s.currentHp / (float)s.maxHp) : 0f;
        var rect = new Rect(Screen.width * 0.25f, 12f, Screen.width * 0.5f, 18f);
        Fill(rect, new Color(0f, 0f, 0f, 0.7f));
        Fill(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), s.isGroggy ? new Color(1f, 0.8f, 0.2f) : new Color(0.85f, 0.15f, 0.15f));
        foreach (float mark in boss.GroggyHpRatios)
            Fill(new Rect(rect.x + rect.width * mark - 1f, rect.y, 2f, rect.height), Color.white);

        string state = s.isGroggy ? " 그로기" : "";
        GUI.Label(new Rect(rect.x, rect.yMax, rect.width, 20f), $"{s.bossName} HP {s.currentHp} / {s.maxHp} ({ratio:P0}){state}", label);
    }

    private void DrawPartBars()
    {
        Camera cam = boss.TargetCamera;
        if (cam == null) return;
        foreach (var pair in parts)
        {
            PartGauge gauge = pair.Value;
            float left = gauge.shownUntil - Time.time;
            if (left <= 0f) continue;

            Vector3 screen = cam.WorldToScreenPoint(gauge.worldPoint);
            if (screen.z <= 0f) continue;
            float alpha = Mathf.Clamp01(left / 0.3f); // 끝날 때 0.3초 동안 흐려진다
            var rect = new Rect(screen.x - 50f, Screen.height - screen.y - 6f, 100f, 10f);
            float ratio = 1f - gauge.damage / (float)partGaugeMax;

            Fill(rect, new Color(0f, 0f, 0f, 0.7f * alpha));
            Fill(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), new Color(0.3f, 0.9f, 1f, alpha));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x - 20f, rect.y - 20f, rect.width + 40f, 18f), $"{pair.Key} {partGaugeMax - gauge.damage}/{partGaugeMax}", label);
            GUI.color = Color.white;
        }
    }

    private static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
