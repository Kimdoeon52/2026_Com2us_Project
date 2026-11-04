using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RealSteel.Boss.Core;
using UnityEngine;

/// <summary>
/// 쿄다이 내려찍는 기물 (2026-11-04). 맵에 1개 — 전투 내내(1페이즈부터, 사용자 정정) 손 패턴과 **따로** 주기적으로 내려찍는다(동시 위협).
///
/// 한 번: 화면 위에서 플레이어를 따라 내려옴(바닥 그림자 예고) → 멈추고 떨림(피할 틈) → 찍기(판정) → 박힘 → 올라감 → 대기.
/// 로직은 전투 틱(KyodaiBossController가 손·레이저 다음에 부른다), 그림은 Update.
///
/// KKH 패턴 기믹을 상속하는 이유: 데미지를 KKH 방식(회피 무적 검사 + 패턴 데이터의 데미지·부위 피해 + 난이도 공격 배율)으로
/// 똑같이 내기 위해서다. 패턴 순서에는 안 들어간다 — 컨트롤러가 등록 직후 패턴 목록에서 뺀다.
/// 파괴 가능 여부는 미정이라 맞는 자리가 없다.
/// </summary>
public class KyodaiCrusher : BossPatternGimmickBase, IKyodaiHazard
{
    private enum Phase { Waiting, Descend, Lock, Slam, Stuck, Rise }

    [SerializeField] private KyodaiCrusherData data;

    [Tooltip("기물 그림 (이 오브젝트의 자식)")]
    [SerializeField] private SpriteRenderer pressRenderer;

    [Tooltip("기물을 매단 막대 그림 — 아래 끝 기준 스프라이트, 화면 위까지 늘린다")]
    [SerializeField] private SpriteRenderer rodRenderer;

    [Tooltip("이 기물 전용 바닥 예고 (보스 루트 밑)")]
    [SerializeField] private GroundTelegraph telegraph;

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private KyodaiBossController kyodai;
    private Phase phase = Phase.Waiting;
    private int frame, wait, hitStop;
    private bool running;
    private bool touched;
    private Vector2 pos, prevPos;
    private float velX;
    private float topY;
    private float riseFromY; // 올라가기 시작한 높이 (찍기 전에 끊겨도 그 자리에서 올라가게)

    /// <summary>찍는 순간 (바닥 위치) — 소리·이펙트용</summary>
    public event System.Action<Vector2> OnImpact;

    public bool IsRunning => running;
    public bool IsOutOfSight => phase == Phase.Waiting;

    private float S => kyodai != null ? kyodai.Size : 1f;

    protected override void OnAttached()
    {
        kyodai = OwnerBoss as KyodaiBossController;
        if (kyodai == null) Debug.LogError("[KyodaiCrusher] KyodaiBossController(2D) 밑에 있어야 함");
        if (data == null) Debug.LogWarning($"[KyodaiCrusher] {name}: 기물 수치가 비어 있음");
        HideNow();
    }

    /// <summary>전투 시작 때 켠다(첫 내려찍기까지 firstDelayFrames). 그로기·2페이즈 전환 연출 동안 끈다 — 끄면 찍던 중이면 올라가서 끝낸다</summary>
    public void SetRunning(bool value)
    {
        if (value == running) return;
        running = value;
        if (running && phase == Phase.Waiting) wait = data != null ? data.firstDelayFrames : 120;
        if (!running && phase != Phase.Waiting && phase != Phase.Rise) Begin(Phase.Rise);
    }

    // ───────────────────────── 전투 틱 ─────────────────────────

    /// <summary>호출: KyodaiBossController(CombatClock.OnCombatTick), 손·레이저 다음</summary>
    public void Tick()
    {
        prevPos = pos;
        if (kyodai == null || data == null) return;
        if (hitStop > 0) { hitStop--; return; }

        frame++;
        switch (phase)
        {
            case Phase.Waiting:
                if (!running) return;
                if (--wait <= 0) StartDrop();
                return;

            case Phase.Descend:
            {
                float goal = kyodai.ClampToArena(kyodai.PlayerX, data.hitSize.x * 0.5f * S);
                float w = 2f * Mathf.PI * data.trackFrequency;
                velX += (w * w * (goal - pos.x) - 2f * data.trackDamping * w * velX) * CombatClock.TICK;
                pos.x += velX * CombatClock.TICK;
                float k = frame / (float)data.descendFrames;
                pos.y = Mathf.Lerp(topY, HoverY, 1f - (1f - k) * (1f - k)); // 빨리 내려오다 천천히 멈춤
                ShowTelegraph();
                if (frame >= data.descendFrames) Begin(data.lockFrames > 0 ? Phase.Lock : Phase.Slam);
                break;
            }
            case Phase.Lock:
                ShowTelegraph();
                if (frame >= data.lockFrames) Begin(Phase.Slam);
                break;

            case Phase.Slam:
            {
                float k = frame / (float)data.slamFrames;
                pos.y = Mathf.Lerp(HoverY, ContactY, k * k); // 점점 빨라지며 떨어짐
                CheckTouch();
                if (frame >= data.slamFrames) Impact();
                break;
            }
            case Phase.Stuck:
                if (frame >= data.stuckFrames) Begin(Phase.Rise);
                break;

            case Phase.Rise:
            {
                float k = frame / (float)data.riseFrames;
                pos.y = Mathf.Lerp(riseFromY, topY, k * k); // 천천히 떼었다가 빨리 올라감
                if (frame >= data.riseFrames)
                {
                    HideNow();
                    wait = data.intervalFrames;
                }
                break;
            }
        }
        transform.position = new Vector3(pos.x, pos.y, transform.position.z);
    }

    private float HoverY => kyodai.GroundY + data.hoverHeight * S;
    private float ContactY => kyodai.GroundY + data.contactHeight * S;

    private void StartDrop()
    {
        topY = CameraTopY() + data.hitSize.y * S + 1f;
        pos = prevPos = new Vector2(kyodai.ClampToArena(kyodai.PlayerX, data.hitSize.x * 0.5f * S), topY);
        velX = 0f;
        touched = false;
        SetVisible(true);
        Begin(Phase.Descend);
    }

    private void Begin(Phase next)
    {
        phase = next;
        frame = 0;
        if (next == Phase.Rise) riseFromY = pos.y;
        if (next == Phase.Slam || next == Phase.Rise || next == Phase.Stuck) HideTelegraph();
    }

    private void Impact()
    {
        pos.y = ContactY;
        hitStop = data.impactFreezeFrames;
        Begin(Phase.Stuck);
        var ground = new Vector2(pos.x, kyodai.GroundY);
        if (Application.isPlaying) KyodaiCameraShake.Shake(data.impactShake, 0.3f);
        OnImpact?.Invoke(ground);
    }

    private void CheckTouch()
    {
        if (touched) return;
        int count = Physics2D.OverlapBox(pos, data.hitSize * S, 0f, overlapFilter, overlap);
        for (int i = 0; i < count; i++)
        {
            if (!overlap[i].CompareTag("Player")) continue;
            if (ApplyDamageToPlayer(overlap[i].gameObject)) touched = true; // 회피 무적이면 false — 찍는 동안 계속 본다
            return;
        }
    }

    private void ShowTelegraph()
    {
        if (telegraph == null) return;
        telegraph.Show(pos.x, kyodai.GroundY, data.telegraphWidth * S);
    }

    private void HideTelegraph()
    {
        if (telegraph != null) telegraph.Hide();
    }

    private void HideNow()
    {
        phase = Phase.Waiting;
        frame = 0;
        HideTelegraph();
        SetVisible(false);
    }

    // 화면 맨 위(경기장 평면 z=0 기준). 카메라가 없으면 바닥 위 넉넉히
    private float CameraTopY()
    {
        Camera cam = kyodai.TargetCamera;
        if (cam == null) return kyodai.GroundY + 12f;
        if (cam.orthographic) return cam.transform.position.y + cam.orthographicSize;
        float dist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.transform.position.y + Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist;
    }

    // ───────────────────────── 그림 ─────────────────────────

    private void Update()
    {
        if (pressRenderer == null || !pressRenderer.enabled || kyodai == null || data == null) return;
        Vector2 shown = Vector2.Lerp(prevPos, pos, CombatClock.InterpolationAlpha);
        if (phase == Phase.Lock) shown += Random.insideUnitCircle * data.lockShake * S;
        pressRenderer.transform.position = new Vector3(shown.x, shown.y, pressRenderer.transform.position.z);

        if (rodRenderer == null) return;
        // 막대: 기물 윗면에서 화면 위까지 — 길이는 월드, 기물이 줄어든 보스 루트 밑이라 로컬 크기는 나눠서
        float pressTop = shown.y + pressRenderer.bounds.extents.y;
        float length = Mathf.Max(0f, CameraTopY() + 2f - pressTop);
        float ly = Mathf.Max(0.0001f, Mathf.Abs(rodRenderer.transform.parent != null ? rodRenderer.transform.parent.lossyScale.y : 1f));
        float spriteH = rodRenderer.sprite != null ? rodRenderer.sprite.bounds.size.y : 1f;
        rodRenderer.transform.position = new Vector3(shown.x, pressTop, rodRenderer.transform.position.z);
        Vector3 s = rodRenderer.transform.localScale;
        rodRenderer.transform.localScale = new Vector3(s.x, length / ly / Mathf.Max(0.0001f, spriteH), s.z);
    }

    private void SetVisible(bool visible)
    {
        if (pressRenderer != null) pressRenderer.enabled = visible;
        if (rodRenderer != null) rodRenderer.enabled = visible;
    }

    // 패턴 순서에는 안 들어간다(컨트롤러가 뺀다) — 불려도 바로 끝
    protected override UniTask ExecuteActionAsync(CancellationToken token) => UniTask.CompletedTask;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (data == null || phase != Phase.Slam) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pos, data.hitSize * S);
    }
#endif
}
