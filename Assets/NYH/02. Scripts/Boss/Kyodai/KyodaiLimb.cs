using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>쿄다이 부위가 지금 어느 레이어에 있는지 (기획서10_28 "레이어 판정 규칙")</summary>
public enum LimbLayer
{
    Background, // 본체·대기 중인 팔다리 — 맞지도 때리지도 않음, 어둡게
    Transition, // 배경에서 전경으로 들어오는 중 — 크기 확대 + 그림자(예고)
    Attack,     // 휘두르기·찍기 진행 중 — 플레이어를 때림, 붉은 테두리
    Linger,     // 공격 후 박힘·정지 — 플레이어가 때릴 수 있음, 흰 테두리
    Return,     // 전경에서 배경으로 빠지는 중 — 다시 어두워짐
}

/// <summary>리깅 모델의 손이 목표까지 안 닿을 때 (2026-10-29)</summary>
public enum LimbReach
{
    Grow, // 팔만 커지며 뻗음 — 몸은 뒤에 그대로
    Body, // 몸을 기울여 보냄
    None, // 닿는 데까지만
}

/// <summary>
/// 쿄다이의 팔·다리 하나 (2026-10-28). 3D 모델 쪽(스튜디오)과 경기장 쪽(판정)을 잇는다.
///
///   · effector: 스튜디오 안의 목표점. 패턴이 이걸 움직인다
///       - 임시 상자 모델: effector = 손 상자 자체(손이 날아감)
///       - 리깅 모델(rig 지정, 2026-10-29): effector = 보이지 않는 IK 타깃. 위팔·아래팔을 꺾어 손이 거기 닿게 한다(2본 IK).
///         안 닿으면 reachMode대로 — 기본은 팔만 커지며 뻗음(몸은 뒤에), Body면 KyodaiRig가 몸을 기울인다
///   · 레이어에 따라 부위 렌더러를 뒤 판/앞 판 렌더 레이어로 옮기고 색을 바꾼다
///   · 경기장 쪽 BossHurtZone을 매 프레임 손이 보이는 자리로 옮기고, 잔류(Linger)일 때만 맞게 한다
///   · 패턴은 이 부위에 "경기장 이 자리로 와라"(MoveToWorld)만 시키면 된다 — 스튜디오 좌표·모델 종류는 몰라도 됨
/// </summary>
public class KyodaiLimb : MonoBehaviour
{
    [Tooltip("부위 이름 (왼팔/오른팔/왼다리/오른다리/메인렌즈). 리깅 모델이면 KyodaiRig의 Parts와 같은 이름")]
    [SerializeField] private string partId = "오른팔";

    [SerializeField] private KyodaiBossController boss;

    [Tooltip("스튜디오 안의 목표점. 임시 상자 모델은 손 상자, 리깅 모델이면 비워두면 IK 타깃을 손 위치에 자동으로 만든다")]
    [SerializeField] private Transform effector;

    [Tooltip("레이어를 바꿀 렌더러들의 뿌리(스튜디오). 비우면 effector (리깅 모델은 쪼갠 부위 렌더러를 자동으로 씀)")]
    [SerializeField] private Transform renderRoot;

    [Tooltip("임시 모델용: 어깨에서 손까지 늘려 잇는 팔 막대(선택). 리깅 모델이면 비움")]
    [SerializeField] private Transform segment;
    [SerializeField] private Transform shoulder;

    [Tooltip("경기장 쪽 피격 자리. 잔류(Linger)일 때만 켜진다")]
    [SerializeField] private BossHurtZone hurtZone;

    [Tooltip("전경으로 나왔을 때 스튜디오 카메라와의 거리. 가까울수록 크게 보인다")]
    [Min(0.1f)] [SerializeField] private float foregroundDepth = 20f;

    [Header("리깅 모델 (비우면 임시 상자 방식)")]
    [SerializeField] private KyodaiRig rig;
    [SerializeField] private string upperBone = "RightUpperArm";
    [SerializeField] private string lowerBone = "RightLowerArm";
    [SerializeField] private string endBone = "RightHand";

    [Tooltip("팔꿈치가 꺾이는 쪽(월드 방향). 팔이 일자로 펴져 있을 때 어느 쪽으로 굽힐지 정한다")]
    [SerializeField] private Vector3 elbowHint = Vector3.up;

    [Tooltip("손이 안 닿을 때 어떻게 할지.\n" +
             "Grow: 몸은 뒤에 두고 팔만 커지며 뻗어 나온다(원근이라 앞으로 튀어나온 것처럼 보임)\n" +
             "Body: 몸을 그쪽으로 기울여 보낸다\nNone: 닿는 데까지만 뻗는다")]
    [SerializeField] private LimbReach reachMode = LimbReach.Grow;

    [Tooltip("Grow: 팔이 최대 몇 배까지 커질 수 있나")]
    [Min(1f)] [SerializeField] private float maxGrow = 4f;

    [Tooltip("Body: 손이 안 닿을 때 몸을 얼마나 따라 보낼지 (0 = 안 따라감, 1 = 모자란 만큼 전부)")]
    [Range(0f, 1f)] [SerializeField] private float bodyAssist = 1f;

    [Header("레이어별 색 (테두리 셰이더 전 임시 — 부위 전체에 곱함)")]
    [SerializeField] private Color attackColor = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color lingerColor = new Color(1.6f, 1.6f, 1.6f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Renderer[] renderers = new Renderer[0];
    private MaterialPropertyBlock block;
    private readonly List<Tween> tweens = new List<Tween>();
    private bool ready;

    // 리깅 모델
    private Transform upper, lower, end;
    private Quaternion upperRest, lowerRest, endRest;
    private Quaternion handOffset = Quaternion.identity; // 모션 키의 손목 회전 (애니메이션 손목 기준)
    private float ikWeight = 1f;                         // 0 = 애니메이션 그대로, 1 = 손이 effector에 정확히
    private Vector3 upperRestScale;
    private float baseArmLength; // 커지기 전 위팔 + 아래팔 길이(월드)
    private bool RigMode => rig != null && upper != null && lower != null && end != null;

    public string PartId => partId;
    public LimbLayer Layer { get; private set; } = LimbLayer.Background;
    public BossHurtZone HurtZone => hurtZone;
    public float ForegroundDepth => foregroundDepth;
    public Transform Effector => effector;
    /// <summary>준비 끝(Start 이후) — 모션 재생·미리보기는 이걸 보고 적용한다</summary>
    public bool Ready => ready;
    /// <summary>대기 자세의 손 자리(스튜디오 좌표)</summary>
    public Vector3 RestPosition => restPosition;

    /// <summary>손(끝점)이 지금 경기장 어디에 보이는지</summary>
    public Vector2 WorldPosition
    {
        get
        {
            if (boss == null || boss.Studio == null || effector == null) return transform.position;
            return boss.Studio.StudioToWorld(RigMode ? end.position : effector.position);
        }
    }

    private void Awake()
    {
        if (boss == null) boss = GetComponentInParent<KyodaiBossController>();
        block = new MaterialPropertyBlock();
    }

    // Start에서 준비하는 이유: KyodaiRig가 Awake에서 메쉬를 쪼갠 뒤라야 부위 렌더러를 받을 수 있다
    private void Start()
    {
        if (rig != null) SetupRig();
        else SetupBox();

        if (effector == null) { Debug.LogError($"[KyodaiLimb] {partId}: effector가 없음"); enabled = false; return; }
        restPosition = effector.position;
        restRotation = effector.rotation;
        ready = true;
        SetLayer(LimbLayer.Background);
    }

    private void SetupBox()
    {
        if (renderRoot == null) renderRoot = effector;
        if (renderRoot != null) renderers = renderRoot.GetComponentsInChildren<Renderer>(true);
    }

    private void SetupRig()
    {
        upper = rig.FindBone(upperBone);
        lower = rig.FindBone(lowerBone);
        end = rig.FindBone(endBone);
        if (!RigMode)
        {
            Debug.LogError($"[KyodaiLimb] {partId}: 뼈를 못 찾음 ({upperBone}/{lowerBone}/{endBone}) — 뼈 이름 확인");
            return;
        }
        upperRest = upper.localRotation;
        lowerRest = lower.localRotation;
        endRest = end.localRotation;
        upperRestScale = upper.localScale;
        baseArmLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, end.position);

        if (effector == null)
        {
            effector = new GameObject($"IK 타깃 ({partId})").transform;
            effector.SetParent(rig.transform, true);
            effector.SetPositionAndRotation(end.position, end.rotation);
        }

        var partRenderer = rig.GetPartRenderer(partId);
        if (partRenderer != null)
        {
            renderRoot = partRenderer.transform;
            renderers = new Renderer[] { partRenderer };
        }
        else Debug.LogWarning($"[KyodaiLimb] {partId}: 쪼갠 부위 메쉬가 없음 — 앞/뒤 판 전환 없이 몸통과 같이 그려진다");

        rig.Register(this);
    }

    private void LateUpdate()
    {
        if (!ready) return;
        if (!RigMode) StretchSegment();
        if (hurtZone != null) hurtZone.transform.position = (Vector3)WorldPosition + Vector3.forward * hurtZone.transform.position.z;
    }

    public void SetLayer(LimbLayer layer)
    {
        Layer = layer;
        bool front = layer == LimbLayer.Transition || layer == LimbLayer.Attack || layer == LimbLayer.Linger;
        int renderLayer = boss != null && boss.Studio != null ? (front ? boss.Studio.FrontLayer : boss.Studio.BackLayer) : 0;
        if (renderRoot != null) SetRenderLayer(renderRoot, renderLayer);
        if (segment != null) SetRenderLayer(segment, renderLayer);

        Color tint = layer == LimbLayer.Attack ? attackColor : layer == LimbLayer.Linger ? lingerColor : Color.white;
        foreach (var r in renderers) ApplyTint(r, tint);
        if (segment != null) foreach (var r in segment.GetComponentsInChildren<Renderer>(true)) ApplyTint(r, tint);

        if (hurtZone != null) hurtZone.Vulnerable = layer == LimbLayer.Linger;
    }

    /// <summary>경기장 worldPoint 자리에 손이 보이도록 duration 동안 옮긴다 (전경 거리에서)</summary>
    public UniTask MoveToWorld(Vector2 worldPoint, float duration, Ease ease, CancellationToken token)
    {
        Vector3 target = boss.Studio.WorldToStudio(worldPoint, foregroundDepth);
        return Play(effector.DOMove(target, duration).SetEase(ease), token);
    }

    /// <summary>대기 자세(배경)로 돌아간다</summary>
    public UniTask ReturnToRest(float duration, CancellationToken token)
    {
        var seq = DOTween.Sequence()
            .Join(effector.DOMove(restPosition, duration).SetEase(Ease.InOutSine))
            .Join(effector.DORotateQuaternion(restRotation, duration));
        return Play(seq, token);
    }

    /// <summary>트윈 없이 즉시 대기 자세 + 배경으로 (패턴 중단·정리용)</summary>
    public void SnapToRest()
    {
        KillTweens();
        handOffset = Quaternion.identity;
        ikWeight = 1f;
        if (effector != null) effector.SetPositionAndRotation(restPosition, restRotation);
        SetLayer(LimbLayer.Background);
    }

    /// <summary>모션 재생: 손을 스튜디오 좌표 studioPos에 두고 손목을 hand만큼 돌린다. weight = IK 세기 (KyodaiMotionPlayer)</summary>
    public void SetPose(Vector3 studioPos, Quaternion hand, float weight = 1f)
    {
        if (!ready) return;
        if (tweens.Count > 0) KillTweens();
        effector.position = studioPos;
        handOffset = hand;
        ikWeight = Mathf.Clamp01(weight);
        if (!RigMode) effector.rotation = restRotation * hand;
    }

    /// <summary>IK 세기만 바꾼다 (0 = 애니메이션 그대로)</summary>
    public void SetIKWeight(float weight) => ikWeight = Mathf.Clamp01(weight);

    private async UniTask Play(Tween tween, CancellationToken token)
    {
        tweens.Add(tween);
        try { await tween.ToUniTask(cancellationToken: token); }
        finally { tweens.Remove(tween); }
    }

    public void KillTweens()
    {
        // 복사해서 돈다 — Kill이 기다리던 쪽(Play의 finally)을 바로 깨워 목록에서 빼기 때문
        var running = tweens.ToArray();
        tweens.Clear();
        foreach (var t in running) if (t != null && t.IsActive()) t.Kill();
    }

    // ── 리깅 모델: KyodaiRig.LateUpdate가 순서대로 부른다 ──

    // 호출: KyodaiRig. 손이 목표까지 모자란 거리(월드 벡터). 몸이 이만큼 따라간다. 배경에 있을 땐 0
    public Vector3 ReachShortfall()
    {
        if (!RigMode || !ready || reachMode != LimbReach.Body || Layer == LimbLayer.Background || bodyAssist <= 0f) return Vector3.zero;
        Vector3 a = upper.position;
        Vector3 toTarget = effector.position - a;
        float length = Vector3.Distance(a, lower.position) + Vector3.Distance(lower.position, end.position);
        float excess = toTarget.magnitude - length * 0.98f;
        return excess > 0f ? toTarget.normalized * (excess * bodyAssist) : Vector3.zero;
    }

    // 호출: KyodaiRig(기본 자세 복원 → 애니메이션 → 몸 위치 뒤). 2본 IK — 위팔·아래팔 회전만 바꿔 손목이 effector에 닿게 한다(KyodaiIK).
    // 출발점은 지금 자세(애니메이션이 있으면 그 자세). IK 세기만큼만 섞는다
    public void SolveRig()
    {
        if (!RigMode || !ready) return;
        Quaternion endBase = end.localRotation;
        if (ikWeight > 0f)
        {
            Quaternion upperBase = upper.localRotation, lowerBase = lower.localRotation;
            Vector3 scaleBase = upper.localScale;
            // Grow: 모자란 만큼 팔만 커진다 — 목표가 대기 자리로 돌아오면 비율도 1로 돌아온다
            if (reachMode == LimbReach.Grow) KyodaiIK.Grow(upper, upperRestScale, baseArmLength, effector.position, maxGrow);
            KyodaiIK.SolveTwoBone(upper, lower, end, effector.position, elbowHint);
            if (ikWeight < 1f)
            {
                upper.localRotation = Quaternion.Slerp(upperBase, upper.localRotation, ikWeight);
                lower.localRotation = Quaternion.Slerp(lowerBase, lower.localRotation, ikWeight);
                upper.localScale = Vector3.Lerp(scaleBase, upper.localScale, ikWeight);
            }
        }
        end.localRotation = endBase * handOffset; // 손목: 애니메이션 손목 위에 모션 키 회전
    }

    // ── 임시 상자 모델 ──

    private void StretchSegment()
    {
        if (segment == null || shoulder == null) return;
        KyodaiIK.Stretch(segment, shoulder.position, effector.position);
    }

    // ── 모션 창 편집 미리보기가 읽는 설정 (2026-10-29) ──
    public KyodaiRig Rig => rig;
    public string UpperBone => upperBone;
    public string LowerBone => lowerBone;
    public string EndBone => endBone;
    public Vector3 ElbowHint => elbowHint;
    public LimbReach ReachMode => reachMode;
    public float MaxGrow => maxGrow;
    public Transform EffectorSetting => effector;
    public Transform Segment => segment;
    public Transform Shoulder => shoulder;

    private static void SetRenderLayer(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetRenderLayer(child, layer);
    }

    private void ApplyTint(Renderer r, Color tint)
    {
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, tint);
        block.SetColor(ColorId, tint);
        r.SetPropertyBlock(block);
    }
}
