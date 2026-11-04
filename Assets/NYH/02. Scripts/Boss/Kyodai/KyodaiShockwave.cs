using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충격파 (2026-11-04, 원본 기획서 2페이즈 "충격파 내려치기"). 주먹이 바닥을 찍으면(키의 spawnShockwave) 그 자리에서
/// 좌우로 하나씩 바닥을 따라 퍼진다. 낮아서 **점프로 넘는다** — 지금 쿄다이 패턴 중 유일하게 점프로 피하는 공격.
///
/// 로직은 전투 틱(KyodaiBossController가 부른다), 그림은 Update. 데미지는 찍은 손 패턴의 판정 함수(KKH ApplyDamageToPlayer)를
/// 그대로 받아 쓴다 — 충격파마다 한 번씩만 맞는다. 크기·속도는 보스 단위(Size를 곱한다). 수치는 임시값.
/// </summary>
public class KyodaiShockwave : MonoBehaviour
{
    [Tooltip("충격파 그림 — 아래 끝 기준(바닥에 붙음), 오른쪽으로 가는 모양")]
    [SerializeField] private Sprite waveSprite;

    [Tooltip("TODO: 기획 수치 없음. 퍼지는 속도(보스 단위/초)")]
    [Min(0.1f)] [SerializeField] private float speed = 10f;

    [Tooltip("판정 크기(보스 단위) — 높이를 낮게 둬야 점프로 넘는다 (기본 점프 높이 약 0.9 월드)")]
    [SerializeField] private Vector2 size = new Vector2(1.2f, 0.9f);

    [SerializeField] private int sortingOrder = 4;
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.6f);

    private class Wave
    {
        public float x, prevX;
        public int dir, age;
        public bool hit;
        public Func<GameObject, bool> touch;
        public SpriteRenderer renderer;
    }

    private static readonly List<Collider2D> overlap = new List<Collider2D>(8);
    private static readonly ContactFilter2D overlapFilter = new ContactFilter2D { useTriggers = true };

    private KyodaiBossController owner;
    private readonly List<Wave> waves = new List<Wave>();
    private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

    private float S => owner != null ? owner.Size : 1f;

    public void Init(KyodaiBossController boss)
    {
        owner = boss;
        Clear();
    }

    /// <summary>x 자리에서 좌우로 하나씩. touch: 플레이어에 닿을 때(회피 무적이면 false → 계속 본다). null이면 데미지 없음</summary>
    public void Spawn(float x, Func<GameObject, bool> touch)
    {
        if (owner == null) return;
        for (int d = -1; d <= 1; d += 2)
            waves.Add(new Wave { x = x, prevX = x, dir = d, touch = touch, renderer = TakeRenderer() });
    }

    public void Clear()
    {
        foreach (var w in waves) Release(w.renderer);
        waves.Clear();
    }

    /// <summary>호출: KyodaiBossController(CombatClock.OnCombatTick)</summary>
    public void Tick()
    {
        if (owner == null) return;
        float s = S;
        Vector2 hitSize = size * s;
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            var w = waves[i];
            w.prevX = w.x;
            w.x += w.dir * speed * s * CombatClock.TICK;
            w.age++;
            // 경기장 끝을 넘으면 사라진다
            if (w.x < owner.ArenaMinX - hitSize.x || w.x > owner.ArenaMaxX + hitSize.x)
            {
                Release(w.renderer);
                waves.RemoveAt(i);
                continue;
            }
            if (w.hit || w.touch == null) continue;
            int count = Physics2D.OverlapBox(new Vector2(w.x, owner.GroundY + hitSize.y * 0.5f), hitSize, 0f, overlapFilter, overlap);
            for (int k = 0; k < count; k++)
            {
                if (!overlap[k].CompareTag("Player")) continue;
                if (w.touch(overlap[k].gameObject)) w.hit = true;
                break;
            }
        }
    }

    private void Update()
    {
        if (owner == null) return;
        float a = CombatClock.InterpolationAlpha;
        // 이 오브젝트는 보스 루트(크기 Size) 밑이라 로컬 크기 1 = 보스 단위 — 그림은 보스 단위로 두면 된다
        foreach (var w in waves)
        {
            if (w.renderer == null) continue;
            float x = Mathf.Lerp(w.prevX, w.x, a);
            w.renderer.transform.position = new Vector3(x, owner.GroundY, transform.position.z);
            float pulse = 1f + 0.18f * Mathf.Sin(w.age * 0.6f); // 출렁이며 굴러가는 느낌
            w.renderer.transform.localScale = new Vector3(1f / pulse, pulse, 1f);
            w.renderer.flipX = w.dir < 0;
            // 경기장 끝 근처에서 흐려진다
            float edge = Mathf.Min(x - owner.ArenaMinX, owner.ArenaMaxX - x);
            Color c = color;
            c.a = color.a * Mathf.Clamp01(edge / (size.x * S) + 0.3f);
            w.renderer.color = c;
        }
    }

    private SpriteRenderer TakeRenderer()
    {
        SpriteRenderer r = spare.Count > 0 ? spare.Pop() : null;
        if (r == null)
        {
            var go = new GameObject("충격파") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            r = go.AddComponent<SpriteRenderer>();
            r.sprite = waveSprite;
            r.sortingOrder = sortingOrder;
        }
        r.enabled = true;
        return r;
    }

    private void Release(SpriteRenderer r)
    {
        if (r == null) return;
        r.enabled = false;
        spare.Push(r);
    }
}
