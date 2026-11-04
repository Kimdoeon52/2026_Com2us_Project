using System.Collections.Generic;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 쿄다이 임시 체력 표시 (2026-11-02, 정식 UI 전까지 테스트용 — OnGUI로 그려서 캔버스 설정이 필요 없다).
///
///   · 화면 위: 보스 HP(공유) 막대·숫자, 그로기 문턱(SetGroggyMarks로 받은 비율) 눈금
///   · 부위 막대: 부위(BossHurtZone)를 때리면 맞은 자리 위에 잠깐 뜬다
///
/// 부위 값은 기획서10_28 "부위 구성 HP 구조"의 무력화 게이지 자리 — 보스 HP는 공유이고 부위는 맞은 양만 따로 센다.
/// 게이지 최대치는 기획 수치가 없어 임시값(partGaugeMax)이고, 다 깎여도 봉인 같은 효과는 아직 없다.
/// 쿄다이 컨트롤러(2D·3D 보관본 공용)가 시작할 때 붙이고 SetGroggyMarks를 부른다(showDebugHp).
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
        public KyodaiHand hand; // 쿄다이 손이면 진짜 무력화 게이지(손이 들고 있음)를 그린다
    }

    private BossControllerBase boss;
    private IReadOnlyList<float> groggyMarks;
    private KyodaiTentacleSpawner tentacles; // 있으면 촉수 수·이동속도 배율도 표시 (2026-11-04)
    private readonly Dictionary<string, PartGauge> parts = new Dictionary<string, PartGauge>();
    private readonly List<BossHurtZone> zones = new List<BossHurtZone>();
    private GUIStyle label;
    private float nextScan;

    private void Awake()
    {
        boss = GetComponent<BossControllerBase>();
        tentacles = GetComponentInChildren<KyodaiTentacleSpawner>(true);
    }

    /// <summary>HP 막대에 그릴 그로기 문턱(남은 HP 비율)</summary>
    public void SetGroggyMarks(IReadOnlyList<float> marks) => groggyMarks = marks;

    private void OnDestroy()
    {
        foreach (var zone in zones) if (zone != null) zone.OnHit -= OnZoneHit;
    }

    // 판정 자리가 시작 뒤에 생길 수 있어서(3D 위팔 복제·촉수 생성) 1초마다 새로 생긴 자리를 찾아 구독한다
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
        if (gauge.hand == null) gauge.hand = zone.GetComponentInParent<KyodaiHand>();

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
        if (groggyMarks != null)
            foreach (float mark in groggyMarks)
                Fill(new Rect(rect.x + rect.width * mark - 1f, rect.y, 2f, rect.height), Color.white);

        string state = s.isGroggy ? " 그로기" : "";
        string phase = s.currentPhase >= 2 ? " · 2페이즈" : "";
        GUI.Label(new Rect(rect.x, rect.yMax, rect.width, 20f), $"{s.bossName} HP {s.currentHp} / {s.maxHp} ({ratio:P0}){state}{phase}", label);
        if (tentacles != null)
            GUI.Label(new Rect(rect.x, rect.yMax + 18f, rect.width, 20f),
                      $"촉수 {tentacles.Count}/{tentacles.MaxCount} · 이동속도 ×{tentacles.SpeedMultiplier:0.00}", label);
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
            // 쿄다이 손: 무력화 게이지(2026-11-04) — 남은 양 = 기준 − 누적, 누워 있는 동안은 0
            int max = partGaugeMax, remain = partGaugeMax - gauge.damage;
            string title = pair.Key;
            if (gauge.hand != null && boss is KyodaiBossController kyodai && kyodai.ArmDisableDamage > 0)
            {
                max = kyodai.ArmDisableDamage;
                remain = gauge.hand.IsFrozen ? 0 : Mathf.Max(0, max - gauge.hand.DisableDamage);
                title = gauge.hand.IsFrozen ? $"{pair.Key} 무력화" : pair.Key;
            }
            float ratio = remain / (float)Mathf.Max(1, max);

            Fill(rect, new Color(0f, 0f, 0f, 0.7f * alpha));
            Fill(new Rect(rect.x, rect.y, rect.width * ratio, rect.height), new Color(0.3f, 0.9f, 1f, alpha));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(rect.x - 20f, rect.y - 20f, rect.width + 40f, 18f), $"{title} {remain}/{max}", label);
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
