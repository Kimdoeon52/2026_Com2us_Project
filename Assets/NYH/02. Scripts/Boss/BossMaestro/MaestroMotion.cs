using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마에스트로 모션 (2026-11-25 모션 편집기 1단계) — 퍼펫 관절 채널 + 몸 전체(기울기·이동·눌림)를 시간 키로 담는다.
/// 코드에 박혀 있던 몸짓 키(MaestroLook의 SwingKeys 등)를 데이터로 꺼낸 것. 모션 편집기(메뉴 NYH/보스3/모션 편집기)에서
/// 플레이 중에 손·팔꿈치·머리·몸을 끌어 자세를 잡고 키로 저장한다.
/// 그림 전용 — 판정·패턴 타이밍과 무관하다(시간은 초, MaestroLook이 Time.deltaTime으로 재생).
/// 관절 스프링·천 물리는 MaestroLook이 이 값 위에 그대로 얹는다(값 = 목표 자세).
/// </summary>
[CreateAssetMenu(fileName = "Maestro_Motion", menuName = "NYH/Boss/Maestro Motion")]
public class MaestroMotion : ScriptableObject
{
    /// <summary>채널 순서 = MaestroLook 몸짓 채널 순서 (바꾸지 말 것)</summary>
    public const int ChannelCount = 11;
    public const int ChBGrip = 6, ChOCurl = 10;

    public static readonly string[] ChannelNames =
    {
        "상체", "머리", "지휘 어깨", "지휘 팔꿈치", "지휘 손목", "지휘봉", "지휘손 쥠", "반대 어깨", "반대 팔꿈치", "반대 손목", "반대 손가락",
    };

    /// <summary>채널 기본값 (그림 그대로 서 있는 자세) — 쥠 0.5 = 그림의 반쯤 쥔 손, 손가락 0.3</summary>
    public static float ChannelDefault(int ch) => ch == ChBGrip ? 0.5f : ch == ChOCurl ? 0.3f : 0f;

    /// <summary>채널 값 범위 (편집기 슬라이더) — 각도는 도, 쥠·손가락은 0 = 폄, 1 = 꽉 쥠</summary>
    public static Vector2 ChannelRange(int ch) => ch == ChBGrip || ch == ChOCurl ? new Vector2(-1f, 1.5f) : new Vector2(-180f, 180f);

    public enum Ease
    {
        Smooth,   // 천천히 출발 → 천천히 도착
        Linear,   // 일정하게
        EaseIn,   // 천천히 출발 → 빠르게 도착 (내리치기)
        EaseOut,  // 빠르게 출발 → 천천히 도착 (튀어나갔다 버팀)
        Hold,     // 다음 키까지 그대로 있다가 딱 바뀜
    }

    [Serializable]
    public class Key
    {
        [Tooltip("시간 (초)")]
        [Min(0f)] public float time;
        [Tooltip("관절 채널 (각도는 도, + = 반시계 — 그림 기준). 순서는 ChannelNames")]
        public float[] ch = NewChannels();
        [Tooltip("몸 전체 기울기 (도, + = 앞 — 그림이 보는 쪽). 발밑을 축으로 위로 갈수록 더 기운다")]
        public float lean;
        [Tooltip("몸 전체 이동 (키 대비 비율) — x + = 앞, y + = 위. 0.1 = 키의 10%")]
        public Vector2 root;
        [Tooltip("눌림·늘어남 (1 = 그대로, 1 미만 = 눌림, 1 초과 = 늘어남 — 부피 유지)")]
        [Range(0.6f, 1.4f)] public float stretch = 1f;
        [Header("추가 조종점 (11-25 2차 — 키 대비 비율, x + = 앞, y + = 위)")]
        [Tooltip("지휘 어깨 들썩임 — 어깨 관절 자리를 옮김 (상체 기준이라 몸이 기울면 같이 기움)")]
        public Vector2 bShoulder;
        [Tooltip("반대 어깨 들썩임")]
        public Vector2 oShoulder;
        [Tooltip("허리 — 상체 전체를 하체 위에서 옮김 (하체 위쪽이 같이 따라와 끊기지 않음)")]
        public Vector2 hip;
        [Tooltip("하체 자락 — 아랫단을 옮김 (허리는 그대로, 아래로 갈수록 크게)")]
        public Vector2 hem;
        [Tooltip("머리 장식(달) 각도 (도) — 장식은 머리를 따라 돌고, 이 값만큼 더 돈다")]
        public float moon;

        [Tooltip("이 키에서 다음 키로 가는 곡선")]
        public Ease ease = Ease.Smooth;
        [Tooltip("메모 (예: 준비, 벰, 여운)")]
        public string note = "";

        public Key Clone()
        {
            var k = (Key)MemberwiseClone();
            k.ch = (float[])ch.Clone();
            return k;
        }

        public void EnsureSize()
        {
            if (ch == null) ch = NewChannels();
            else if (ch.Length != ChannelCount)
            {
                var n = NewChannels();
                Array.Copy(ch, n, Mathf.Min(ch.Length, ChannelCount));
                ch = n;
            }
        }
    }

    public static float[] NewChannels()
    {
        var c = new float[ChannelCount];
        for (int i = 0; i < c.Length; i++) c[i] = ChannelDefault(i);
        return c;
    }

    [Tooltip("모션 길이 (초) — 패턴이 몸짓을 다른 길이로 걸면 비율대로 늘이고 줄인다")]
    [Min(0.05f)] public float length = 1f;
    [Tooltip("반복 — 끝나면 처음 키로 이어서 돈다 (마지막 키 → 길이 끝 동안 첫 키로 넘어감)")]
    public bool loop;
    [Tooltip("절도 있게 — 관절 스프링을 단단하게(넘치지 않고 딱 멈춤). 끄면 출렁이며 무게감 있게")]
    public bool crisp = true;
    [Tooltip("메모")]
    [TextArea(1, 4)] public string memo = "";
    public List<Key> keys = new List<Key>();

    // ───────────────────────── 이벤트 (점 · 선, 11-25) ─────────────────────────
    // 패턴을 어디서·언제·어떻게 낼지 사용자가 모션 위에 표시한다 → 패턴 구현 때 이 값을 그대로 읽는다(시간 → 전투 틱으로 바꿔서).
    //  · 점: "이 시간, 이 자리(지휘봉 끝 등)에서 무엇이 생긴다/터진다" — 역할 + 등장 느낌
    //  · 선: "여기서 이 경로로 이렇게 나간다" — 역할 + 경로(직선/곡선) + 속도 느낌(가속·탄력·튕김…) + 물결
    // 좌표는 키 대비 비율, x + = 앞(보스가 보는 쪽), y + = 위. 기준(anchor)의 그 시간 자리에서 시작

    public enum EventType { Point, Line }

    /// <summary>위치 기준 — 이벤트 시간에 이 자리</summary>
    public enum Anchor
    {
        BatonTip,   // 지휘봉 끝
        BatonHand,  // 지휘 손
        OtherHand,  // 반대 손끝
        Head,       // 머리
        Body,       // 몸 가운데
        Feet,       // 발밑 (바닥)
    }

    /// <summary>점 역할</summary>
    public enum PointRole
    {
        Spawn,      // 생성 — 음표·탄이 여기서 생긴다
        Fire,       // 발사 — 이 순간 출발
        Hit,        // 판정 — 여기서 맞는다 (폭발·내려치기 지점)
        Telegraph,  // 예고 — 번쩍임·고리 등 미리 알림
        Effect,     // 이펙트만 (판정 없음)
        Sound,      // 소리
        Memo,       // 메모
    }

    /// <summary>점 등장 느낌</summary>
    public enum PointFeel
    {
        Appear,     // 그냥 생김
        Pop,        // 톡 튀어나옴 — 커졌다 살짝 줄며 자리 잡음(탄력)
        Gather,     // 주변에서 모여들며 생김
        Flash,      // 번쩍 — 크게 빛났다 사라짐
        Pulse,      // 두근두근 — 박자처럼 커졌다 작아짐
        Shake,      // 떨림 — 힘이 모이는 느낌
    }

    /// <summary>선 역할</summary>
    public enum LineRole
    {
        Projectile, // 탄 궤적 — 무언가 이 선을 따라 날아감
        Sweep,      // 휩쓸기 — 이 선이 지나간 자리가 판정 (베기·파도)
        Telegraph,  // 예고선 — 판정 없이 경로만 보여 줌
        BossMove,   // 보스 이동 경로 (돌진·후퇴)
        Memo,       // 메모
    }

    /// <summary>선을 따라 가는 속도 느낌</summary>
    public enum LineFeel
    {
        Constant,   // 일정하게
        Accelerate, // 점점 빠르게 (내리꽂기)
        Decelerate, // 확 나가서 점점 느리게 (던지기)
        Anticipate, // 뒤로 살짝 당겼다 튀어나감 (예비 동작)
        Elastic,    // 탄력 — 끝을 지나쳤다 출렁이며 돌아옴
        Bounce,     // 통통 — 끝에서 몇 번 튕기며 멈춤
        Smooth,     // 부드럽게 출발·도착
    }

    [Serializable]
    public class MotionEvent
    {
        public EventType type;
        [Tooltip("이름 (패턴 코드에서 찾는 이름 — 예: 음표생성, 음표발사)")]
        public string name = "";
        [Tooltip("시간 (초)")]
        [Min(0f)] public float time;
        [Tooltip("위치 기준")]
        public Anchor anchor = Anchor.BatonTip;
        [Tooltip("기준에서 떨어진 자리 (키 대비, x + = 앞, y + = 위)")]
        public Vector2 offset;
        [Tooltip("이 시간 동안 보임/이어짐 (초) — 점은 남아 있는 시간, 선은 끝까지 가는 시간")]
        [Min(0.01f)] public float duration = 0.5f;
        [Tooltip("개수 (음표 몇 개 등)")]
        [Min(1)] public int count = 1;
        [Tooltip("메모 — 의도를 자유롭게 (예: '플레이어 쪽으로 3개, 박자마다 하나씩')")]
        [TextArea(1, 4)] public string memo = "";

        [Header("점")]
        public PointRole pointRole = PointRole.Spawn;
        public PointFeel pointFeel = PointFeel.Pop;
        [Tooltip("개수가 여러 개일 때 퍼지는 반경 (키 대비)")]
        [Min(0f)] public float spread;
        [Tooltip("크기 (키 대비) — 판정이면 판정 반경")]
        [Min(0.005f)] public float size = 0.05f;

        [Header("선")]
        public LineRole lineRole = LineRole.Projectile;
        public LineFeel lineFeel = LineFeel.Constant;
        [Tooltip("느낌 세기 (0 = 약하게, 1 = 보통, 2 = 강하게) — 가속 정도·탄력 출렁임·튕김 높이")]
        [Range(0f, 2f)] public float feelStrength = 1f;
        [Tooltip("경로 점 (키 대비, 기준+offset에서). 첫 점은 보통 (0,0)")]
        public List<Vector2> path = new List<Vector2> { Vector2.zero, new Vector2(0.8f, 0f) };
        [Tooltip("켜면 경로 점들을 매끈한 곡선으로 잇는다 (끄면 꺾인 직선)")]
        public bool curved = true;
        [Tooltip("물결 크기 (키 대비) — 경로 옆으로 흔들리며 감")]
        [Min(0f)] public float waveAmp;
        [Tooltip("물결 횟수 (경로 전체에서 몇 번 흔들리나)")]
        [Min(0f)] public float waveCount = 2f;
        [Tooltip("켜면 경로 방향을 플레이어 쪽으로 돌린다 (모양은 그대로, 첫 점 → 끝 점 방향이 플레이어를 향함)")]
        public bool aimAtPlayer;
        [Tooltip("판정 두께 (키 대비) — 휩쓸기·탄의 굵기")]
        [Min(0f)] public float width = 0.04f;
        [Tooltip("연속 발사 간격 (초) — 개수가 여러 개면 이 간격으로 하나씩 이 선을 탄다 (0 = 한꺼번에)")]
        [Min(0f)] public float interval;
        [Tooltip("이 이름의 점 이벤트 자리에서 시작 (비우면 기준+offset) — '여기서 생긴 음표가 이 선으로 날아감'")]
        public string startAtPoint = "";

        public MotionEvent Clone()
        {
            var e = (MotionEvent)MemberwiseClone();
            e.path = new List<Vector2>(path ?? new List<Vector2>());
            return e;
        }

        public float End => time + duration + (type == EventType.Line ? interval * Mathf.Max(0, count - 1) : 0f);
    }

    public List<MotionEvent> events = new List<MotionEvent>();

    public MotionEvent FindEvent(string eventName)
    {
        if (events == null || string.IsNullOrEmpty(eventName)) return null;
        foreach (var e in events) if (e != null && e.name == eventName) return e;
        return null;
    }

    /// <summary>
    /// 선 진행률 u(0~1, 시간 비율) → 경로 위 위치 비율 s. 탄력·튕김·예비는 0~1을 넘거나 밑돌 수 있다(경로를 지나쳤다 돌아옴)
    /// </summary>
    public static float LineProgress(LineFeel feel, float u, float strength)
    {
        u = Mathf.Clamp01(u);
        float k = Mathf.Max(0f, strength);
        switch (feel)
        {
            case LineFeel.Accelerate: return Mathf.Pow(u, 1f + 1.5f * k);
            case LineFeel.Decelerate: return 1f - Mathf.Pow(1f - u, 1f + 1.5f * k);
            case LineFeel.Smooth: return u * u * (3f - 2f * u);
            case LineFeel.Anticipate:
            {
                float c = 1.7f * k; // 뒤로 당기는 정도
                return u * u * ((c + 1f) * u - c);
            }
            case LineFeel.Elastic:
            {
                // 지나쳤다 출렁이며 돌아옴 — 세기 0: 거의 안 넘침, 1: 약 10% 넘침, 2: 약 37% 넘침
                if (u <= 0f || u >= 1f) return u;
                float decay = Mathf.Lerp(10f, 4f, Mathf.Clamp01(k * 0.5f));
                float omega = Mathf.PI * (2f + k);
                return 1f - Mathf.Exp(-decay * u) * Mathf.Cos(omega * u);
            }
            case LineFeel.Bounce:
            {
                // 끝에 닿은 뒤 튕김 — 세기만큼 높게
                float h = 0.25f * k;
                if (u < 0.6f) { float a = u / 0.6f; return a * a; }
                if (u < 0.8f) { float a = (u - 0.7f) / 0.1f; return 1f - h * (1f - a * a); }
                if (u < 0.93f) { float a = (u - 0.865f) / 0.065f; return 1f - h * 0.35f * (1f - a * a); }
                return 1f;
            }
            default: return u;
        }
    }

    /// <summary>
    /// 점 등장 느낌 — u(0~1, 남아 있는 시간 비율) → 크기 배율 (0 = 안 보임). 미리보기·패턴 연출 공용
    /// </summary>
    public static float PointScale(PointFeel feel, float u, float strength)
    {
        u = Mathf.Clamp01(u);
        float k = Mathf.Max(0f, strength);
        switch (feel)
        {
            case PointFeel.Pop:
            {
                float a = Mathf.Clamp01(u / 0.35f);
                return 1f + Mathf.Sin(a * Mathf.PI) * 0.45f * k * (1f - a * 0.5f) - (1f - a) * (1f - a);
            }
            case PointFeel.Gather: return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.6f));
            case PointFeel.Flash: return u < 0.15f ? Mathf.Lerp(0.2f, 1.8f, u / 0.15f) : Mathf.Lerp(1.8f, 0f, (u - 0.15f) / 0.85f);
            case PointFeel.Pulse: return 0.75f + 0.35f * Mathf.Abs(Mathf.Sin(u * Mathf.PI * (2f + 2f * k)));
            case PointFeel.Shake: return 1f;
            default: return 1f;
        }
    }

    /// <summary>
    /// 경로 위 s(0~1, 길이 비율) 자리 (키 대비, 경로 첫 점 기준 아님 — path 좌표 그대로). 1을 넘으면 마지막 방향으로 이어 감, 0 밑이면 첫 방향 반대로.
    /// 물결은 경로에 수직으로 sin
    /// </summary>
    public static Vector2 PathPoint(MotionEvent e, float s)
    {
        var p = e.path;
        if (p == null || p.Count == 0) return Vector2.zero;
        if (p.Count == 1) return p[0];
        // 길이 비율 → 구간
        int segs = p.Count - 1;
        float total = 0f;
        var len = new float[segs];
        for (int i = 0; i < segs; i++) { len[i] = Vector2.Distance(p[i], p[i + 1]); total += len[i]; }
        if (total < 1e-6f) return p[0];

        Vector2 pos, tangent;
        if (s <= 0f)
        {
            tangent = (p[1] - p[0]).normalized;
            pos = p[0] + tangent * (s * total);
        }
        else if (s >= 1f)
        {
            tangent = (p[segs] - p[segs - 1]).normalized;
            pos = p[segs] + tangent * ((s - 1f) * total);
        }
        else
        {
            float d = s * total;
            int i = 0;
            while (i < segs - 1 && d > len[i]) { d -= len[i]; i++; }
            float t = len[i] > 1e-6f ? d / len[i] : 0f;
            if (e.curved && p.Count > 2)
            {
                Vector2 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(segs, i + 2)];
                pos = CatmullRom(p0, p1, p2, p3, t);
                tangent = (CatmullRom(p0, p1, p2, p3, Mathf.Min(1f, t + 0.01f)) - CatmullRom(p0, p1, p2, p3, Mathf.Max(0f, t - 0.01f))).normalized;
            }
            else
            {
                pos = Vector2.Lerp(p[i], p[i + 1], t);
                tangent = (p[i + 1] - p[i]).normalized;
            }
        }
        if (e.waveAmp > 0f && e.waveCount > 0f)
        {
            Vector2 n = new Vector2(-tangent.y, tangent.x);
            pos += n * (e.waveAmp * Mathf.Sin(Mathf.Clamp01(s) * e.waveCount * 2f * Mathf.PI));
        }
        return pos;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public void SortEvents() { if (events != null) events.Sort((a, b) => a.time.CompareTo(b.time)); }

    public void SortKeys() => keys.Sort((a, b) => a.time.CompareTo(b.time));

    /// <summary>추가 조종점 값 (어깨·허리·자락·머리 장식)</summary>
    public struct Extra
    {
        public Vector2 bShoulder, oShoulder, hip, hem;
        public float moon;
    }

    public void Evaluate(float time, float[] ch, out float lean, out Vector2 root, out float stretch) =>
        Evaluate(time, ch, out lean, out root, out stretch, out _);

    /// <summary>
    /// time(초) 자세 → ch(채널 11개), lean(도), root(키 대비), stretch, extra. 키가 없으면 기본 자세
    /// </summary>
    public void Evaluate(float time, float[] ch, out float lean, out Vector2 root, out float stretch, out Extra extra)
    {
        extra = default;
        int n = keys != null ? keys.Count : 0;
        if (n == 0)
        {
            for (int i = 0; i < ch.Length && i < ChannelCount; i++) ch[i] = ChannelDefault(i);
            lean = 0f; root = Vector2.zero; stretch = 1f;
            return;
        }

        Key a, b;
        float s;
        if (loop && length > 0f) time = Mathf.Repeat(time, length);
        else time = Mathf.Clamp(time, 0f, Mathf.Max(length, keys[n - 1].time));

        if (time <= keys[0].time && !(loop && n > 1))
        {
            a = b = keys[0]; s = 0f;
        }
        else if (time >= keys[n - 1].time || time < keys[0].time)
        {
            // 마지막 키 뒤 (반복이면 첫 키로 넘어감, 아니면 그대로)
            a = keys[n - 1];
            if (loop && n > 1)
            {
                b = keys[0];
                float span = length - a.time + b.time;
                float local = time >= a.time ? time - a.time : time + length - a.time;
                s = span > 1e-4f ? Mathf.Clamp01(local / span) : 1f;
            }
            else { b = a; s = 0f; }
        }
        else
        {
            int k = 0;
            while (k < n - 2 && time >= keys[k + 1].time) k++;
            a = keys[k]; b = keys[k + 1];
            float span = b.time - a.time;
            s = span > 1e-4f ? Mathf.Clamp01((time - a.time) / span) : 1f;
        }

        s = Shape(a.ease, s);
        a.EnsureSize(); b.EnsureSize();
        for (int i = 0; i < ch.Length && i < ChannelCount; i++) ch[i] = Mathf.Lerp(a.ch[i], b.ch[i], s);
        lean = Mathf.Lerp(a.lean, b.lean, s);
        root = Vector2.Lerp(a.root, b.root, s);
        stretch = Mathf.Lerp(a.stretch, b.stretch, s);
        extra.bShoulder = Vector2.Lerp(a.bShoulder, b.bShoulder, s);
        extra.oShoulder = Vector2.Lerp(a.oShoulder, b.oShoulder, s);
        extra.hip = Vector2.Lerp(a.hip, b.hip, s);
        extra.hem = Vector2.Lerp(a.hem, b.hem, s);
        extra.moon = Mathf.Lerp(a.moon, b.moon, s);
    }

    public static float Shape(Ease e, float s)
    {
        switch (e)
        {
            case Ease.Linear: return s;
            case Ease.EaseIn: return s * s * s;
            case Ease.EaseOut: { float r = 1f - s; return 1f - r * r * r; }
            case Ease.Hold: return s >= 1f ? 1f : 0f;
            default: return s * s * (3f - 2f * s);
        }
    }

    /// <summary>time에 딱 맞는 키 번호 (없으면 -1)</summary>
    public int KeyAt(float time, float tolerance = 0.0005f)
    {
        if (keys == null) return -1;
        for (int i = 0; i < keys.Count; i++) if (Mathf.Abs(keys[i].time - time) <= tolerance) return i;
        return -1;
    }

    /// <summary>time 자세를 그대로 담은 새 키</summary>
    public Key Sample(float time)
    {
        var k = new Key { time = time };
        Evaluate(time, k.ch, out k.lean, out k.root, out k.stretch, out var ex);
        k.bShoulder = ex.bShoulder; k.oShoulder = ex.oShoulder; k.hip = ex.hip; k.hem = ex.hem; k.moon = ex.moon;
        int prev = -1;
        if (keys != null) for (int i = 0; i < keys.Count; i++) if (keys[i].time <= time) prev = i;
        if (prev >= 0) k.ease = keys[prev].ease;
        return k;
    }
}
