using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 쿄다이 동작 하나 (2026-10-29). "쿄다이 모션" 창(NYH/쿄다이/모션 창)에서 만들고, KyodaiMotionPattern이 재생한다.
///
/// 손 위치는 전부 "목표 기준"이다 — 목표(보통 플레이어 발밑, 경기장 안으로 자름)에서 얼마나 떨어졌나(월드).
/// 그래서 같은 동작이 플레이어가 어디 있든 그 자리에 떨어진다. 팔꿈치·어깨는 IK가 알아서 맞춘다.
///
/// 트랙 = 부위 하나(오른팔 등): 손 키 + 레이어 띠(배경·전이·공격·잔류·복귀) + 잔류 중 맞는 범위
/// 이벤트 = 그 시각에 한 번 일어나는 일: 판정(플레이어가 범위 안이면 맞음), 바닥 그림자 켜기/끄기
/// 기술이 늘어도 클래스는 안 늘어난다 — 새 패턴은 이 에셋을 하나 더 만든다(§1 원칙)
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Motion", fileName = "KyodaiMotion")]
public class KyodaiMotionData : ScriptableObject
{
    [Tooltip("동작 전체 길이(초)")]
    [Min(0.1f)] public float duration = 3f;

    [Tooltip("목표 x를 경기장 안으로 자를 때 벽에서 띄울 거리(월드). 넓은 공격이 벽 밖으로 안 나가게 — 보통 공격 너비의 절반")]
    [Min(0f)] public float targetMargin;

    // 2026-10-29: Unity 애니메이션 창에서 만든 클립. 모션 시간과 같은 시간으로 온몸(손가락·팔꿈치·몸통)을 움직이고,
    // 손 키의 IK는 그 위에서 키의 "IK 세기"만큼만 손 위치를 목표 쪽으로 보정한다. 비우면 예전처럼 IK만
    [Tooltip("온몸 애니메이션 클립 (리깅 모델 루트에서 녹화한 것). 모션 0초 = 클립 0초")]
    public AnimationClip clip;

    public List<KyodaiMotionTrack> tracks = new List<KyodaiMotionTrack>();
    public List<KyodaiMotionEvent> events = new List<KyodaiMotionEvent>();

    public void SortAll()
    {
        foreach (var t in tracks) t.Sort();
        events.Sort((a, b) => a.time.CompareTo(b.time));
    }
}

[Serializable]
public class KyodaiMotionTrack
{
    [Tooltip("움직일 부위 (KyodaiLimb의 부위 이름)")]
    public string partId = "오른팔";

    public List<KyodaiMotionKey> keys = new List<KyodaiMotionKey>();
    public List<KyodaiLayerMark> layers = new List<KyodaiLayerMark>();

    [Tooltip("잔류 중 플레이어가 때릴 수 있는 범위(월드, 손 위치 중심)")]
    public Vector2 hurtSize = new Vector2(4f, 1.5f);

    public void Sort()
    {
        keys.Sort((a, b) => a.time.CompareTo(b.time));
        layers.Sort((a, b) => a.time.CompareTo(b.time));
    }

    /// <summary>t 시각의 레이어 — 마지막으로 지나온 표시. 표시가 없으면 배경</summary>
    public LimbLayer LayerAt(float t)
    {
        LimbLayer layer = LimbLayer.Background;
        foreach (var mark in layers)
        {
            if (mark.time > t + 1e-4f) break;
            layer = mark.layer;
        }
        return layer;
    }
}

[Serializable]
public class KyodaiMotionKey
{
    [Min(0f)] public float time;

    [Tooltip("켜면 이 키는 대기 자세(배경에 있던 손 자리) — 처음 나오기 전·다 돌아간 뒤에 쓴다")]
    public bool rest;

    [Tooltip("목표에서 손까지(월드). (0, 0)이면 목표 바로 위")]
    public Vector2 offset = new Vector2(0f, 4f);

    [Tooltip("경기장 평면에서 카메라 쪽으로 나온 거리(월드). 0 = 플레이어와 같은 평면 — 판정 자리와 보이는 자리가 맞는다")]
    public float forward;

    [Tooltip("손목 회전(도). 애니메이션(없으면 대기 자세) 손목 기준")]
    public Vector3 handRotation;

    [Tooltip("IK 세기. 1 = 손이 이 키 자리에 정확히 / 0 = 애니메이션 그대로(위치 무시). 예비 동작은 0, 내려찍는 순간은 1")]
    [Range(0f, 1f)] public float ikWeight = 1f;

    [Tooltip("앞 키에서 이 키로 오는 방식. InQuad = 점점 빨라짐(내려찍기), OutCubic = 빨리 왔다가 멈춤")]
    public Ease ease = Ease.InOutSine;
}

[Serializable]
public class KyodaiLayerMark
{
    [Min(0f)] public float time;
    public LimbLayer layer;
}

public enum KyodaiMotionEventType
{
    Hit,           // 판정 — 그 순간 범위 안의 플레이어가 맞는다(KKH ApplyDamageToPlayer: 회피 무적 검사 포함)
    TelegraphShow, // 바닥 그림자 켜기 (중심 x = 목표 + center.x, 너비 = size.x)
    TelegraphHide, // 바닥 그림자 끄기
}

[Serializable]
public class KyodaiMotionEvent
{
    [Min(0f)] public float time;
    public KyodaiMotionEventType type;

    [Tooltip("범위 중심(목표 기준, 월드). 판정이면 박스 중심, 그림자면 x만 씀")]
    public Vector2 center = new Vector2(0f, 1.5f);

    [Tooltip("범위 크기(월드). 판정이면 박스, 그림자면 x = 너비")]
    public Vector2 size = new Vector2(6f, 3f);
}
