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

/// <summary>
/// 쿄다이의 팔·다리 하나 (2026-10-28). 3D 모델 쪽(스튜디오)과 경기장 쪽(판정)을 잇는다.
///
///   · effector: 스튜디오 안에서 실제로 움직이는 끝점(손·발). 임시 모델은 손 오브젝트 자체, 리깅 모델이면 IK 타깃
///   · 레이어에 따라 부위 렌더러를 뒤 판/앞 판 렌더 레이어로 옮기고 색을 바꾼다
///   · 경기장 쪽 BossHurtZone을 매 프레임 effector가 보이는 자리로 옮기고, 잔류(Linger)일 때만 맞게 한다
///   · 패턴은 이 부위에 "경기장 이 자리로 와라"(MoveToWorld)만 시키면 된다 — 스튜디오 좌표는 몰라도 됨
/// </summary>
public class KyodaiLimb : MonoBehaviour
{
    [Tooltip("부위 이름 (왼팔/오른팔/왼다리/오른다리/메인렌즈)")]
    [SerializeField] private string partId = "오른팔";

    [SerializeField] private KyodaiBossController boss;

    [Tooltip("스튜디오 안에서 움직이는 끝점(손·발). 리깅 모델이면 IK 타깃")]
    [SerializeField] private Transform effector;

    [Tooltip("레이어를 바꿀 렌더러들의 뿌리(스튜디오). 비우면 effector")]
    [SerializeField] private Transform renderRoot;

    [Tooltip("임시 모델용: 어깨에서 손까지 늘려 잇는 팔 막대(선택). 리깅 모델이면 비움")]
    [SerializeField] private Transform segment;
    [SerializeField] private Transform shoulder;

    [Tooltip("경기장 쪽 피격 자리. 잔류(Linger)일 때만 켜진다")]
    [SerializeField] private BossHurtZone hurtZone;

    [Tooltip("전경으로 나왔을 때 스튜디오 카메라와의 거리. 가까울수록 크게 보인다")]
    [Min(0.1f)] [SerializeField] private float foregroundDepth = 20f;

    [Header("레이어별 색 (테두리 셰이더 전 임시 — 부위 전체에 곱함)")]
    [SerializeField] private Color attackColor = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color lingerColor = new Color(1.6f, 1.6f, 1.6f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private readonly List<Tween> tweens = new List<Tween>();

    public string PartId => partId;
    public LimbLayer Layer { get; private set; } = LimbLayer.Background;
    public BossHurtZone HurtZone => hurtZone;
    public float ForegroundDepth => foregroundDepth;
    public Transform Effector => effector;

    /// <summary>effector가 지금 경기장 어디에 보이는지</summary>
    public Vector2 WorldPosition => boss != null && boss.Studio != null ? boss.Studio.StudioToWorld(effector.position) : (Vector2)transform.position;

    private void Awake()
    {
        if (boss == null) boss = GetComponentInParent<KyodaiBossController>();
        if (renderRoot == null) renderRoot = effector;
        restPosition = effector.position;
        restRotation = effector.rotation;
        renderers = renderRoot.GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        SetLayer(LimbLayer.Background);
    }

    private void LateUpdate()
    {
        StretchSegment();
        if (hurtZone != null) hurtZone.transform.position = (Vector3)WorldPosition + Vector3.forward * hurtZone.transform.position.z;
    }

    public void SetLayer(LimbLayer layer)
    {
        Layer = layer;
        bool front = layer == LimbLayer.Transition || layer == LimbLayer.Attack || layer == LimbLayer.Linger;
        int renderLayer = boss != null && boss.Studio != null ? (front ? boss.Studio.FrontLayer : boss.Studio.BackLayer) : 0;
        SetRenderLayer(renderRoot, renderLayer);
        if (segment != null) SetRenderLayer(segment, renderLayer);

        Color tint = layer == LimbLayer.Attack ? attackColor : layer == LimbLayer.Linger ? lingerColor : Color.white;
        foreach (var r in renderers) ApplyTint(r, tint);
        if (segment != null) foreach (var r in segment.GetComponentsInChildren<Renderer>(true)) ApplyTint(r, tint);

        if (hurtZone != null) hurtZone.Vulnerable = layer == LimbLayer.Linger;
    }

    /// <summary>경기장 worldPoint 자리에 effector가 보이도록 duration 동안 옮긴다 (전경 거리에서)</summary>
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
        effector.SetPositionAndRotation(restPosition, restRotation);
        SetLayer(LimbLayer.Background);
    }

    /// <summary>경기장 기준 크기를 스튜디오 크기로 — 임시 모델의 손 크기를 정할 때</summary>
    public float StudioSizeFor(float worldSize) => worldSize / boss.Studio.WorldUnitsPerStudioUnit(foregroundDepth);

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

    private void StretchSegment()
    {
        if (segment == null || shoulder == null) return;
        Vector3 from = shoulder.position;
        Vector3 to = effector.position;
        Vector3 d = to - from;
        if (d.sqrMagnitude < 0.0001f) return;
        segment.position = (from + to) * 0.5f;
        segment.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        Vector3 s = segment.localScale;
        segment.localScale = new Vector3(s.x, d.magnitude, s.z);
    }

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
