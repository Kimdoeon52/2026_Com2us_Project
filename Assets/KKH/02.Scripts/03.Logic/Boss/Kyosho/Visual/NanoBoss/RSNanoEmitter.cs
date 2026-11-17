// RE:AL STEEL - 나노봇 리그 발사기
//
// 리그 아래 자식 오브젝트에 붙인다. 이 오브젝트 위치가 발사 원점 (z 를 주면 화면 안쪽 · 바깥쪽에서 쏜다).
// 애니메이션 창에서 '발사' 를 0 → 1 로 올리는 순간 쏜다 (나선 · 연사는 '지속' 동안 계속).
// 탄도 봇이다 — 리그가 쓰지 않는 봇(후광 → 덩어리 → 기사 순)을 꺼내 쓰고, 비행이 끝나면 돌아온다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public enum NanoEmitter
    {
        [InspectorName("고리")] Ring,
        [InspectorName("부채꼴")] Fan,
        [InspectorName("줄 (벽)")] Line,
        [InspectorName("격자 비")] Rain,
        [InspectorName("나선")] Spiral,
        [InspectorName("조준 연사")] Stream,
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/나노봇 리그 발사기")]
    public class RSNanoEmitter : MonoBehaviour
    {
        [Tooltip("0 → 1 로 올리는 순간 발사 (키를 잡는다)")]
        [Range(0f, 1f)] public float fire;

        public NanoEmitter type = NanoEmitter.Ring;
        [Tooltip("탄 수 (나선 · 연사는 초당 수)")] [Min(1)] public int count = 24;
        [Tooltip("탄 속도 (m/s)")] public float speed = 8f;
        [Tooltip("대열로 늘어서 기다리는 시간 = 예고 (초)")] public float telegraph = 0.5f;
        [Tooltip("비행 시간 (초)")] public float life = 2.5f;
        [Tooltip("고리 반경 · 줄 길이 · 나선 갈래 수")] public float size = 1.5f;
        [Tooltip("방향 (도, 0 = 보스가 보는 방향)")] public float angle;
        [Tooltip("부채꼴 각 범위 · 나선 초당 회전 · 연사 흩어짐 (도)")] public float spread = 60f;
        [Tooltip("플레이어를 향해 조준")] public bool aimAtPlayer = true;
        [Tooltip("고리 · 격자 비에 플레이어 쪽 틈")] public bool gap = true;
        [Tooltip("나선 · 연사 지속 (초)")] public float duration = 2f;
        [Tooltip("3페처럼 검을 때 흰 테두리")] public bool rim = true;
        [Tooltip("탄 발광")] public float glow;

        float lastFire;
        float activeUntil = -1f, carry, theta;
        readonly List<int> tmp = new List<int>();

        public void ResetState() { lastFire = fire; activeUntil = -1f; carry = 0f; }

        /// <summary>리그가 매 프레임 부른다</summary>
        public void Tick(RSNanoSwarm sw, Func<Vector3> player, float facing, float clock, float dt)
        {
            if (fire > 0.5f && lastFire <= 0.5f) Begin(sw, player, facing, clock);
            lastFire = fire;
            if (clock < activeUntil)
            {
                carry += dt * count;
                while (carry >= 1f) { carry -= 1f; EmitOne(sw, player, facing); }
            }
        }

        static Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f);

        float BaseAngle(Vector3 o, Func<Vector3> player, float facing)
        {
            if (aimAtPlayer)
            {
                Vector3 d = player() - o;
                return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
            return facing > 0f ? angle : 180f - angle;
        }

        void Shot(RSNanoSwarm sw, int i, Vector3 hold, float wait, Vector3 vel, bool snap) =>
            sw.Fire(i, hold, wait, vel, life, RSNanoSwarm.G_Halo, default, rim ? 1f : 0f, snap);

        void Begin(RSNanoSwarm sw, Func<Vector3> player, float facing, float clock)
        {
            Vector3 o = transform.position;
            if (glow > 0f) sw.freeGlow = glow;
            float baseA = BaseAngle(o, player, facing);
            int n = Mathf.Max(1, count);
            switch (type)
            {
                case NanoEmitter.Ring:
                {
                    var ids = sw.Ammo(n, o, tmp);
                    float step = 360f / n;
                    Vector3 toP = player() - o;
                    float gapA = Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg;
                    float startA = aimAtPlayer ? 0f : baseA;
                    int used = 0;
                    for (int j = 0; j < n && used < ids.Count; j++)
                    {
                        float a = startA + j * step;
                        if (gap && Mathf.Abs(Mathf.DeltaAngle(a, gapA)) < step * 2f) continue;
                        Vector3 d = Dir(a);
                        Shot(sw, ids[used++], o + d * size, telegraph, d * speed, false);
                    }
                    break;
                }
                case NanoEmitter.Fan:
                {
                    var ids = sw.Ammo(n, o, tmp);
                    for (int j = 0; j < ids.Count; j++)
                    {
                        float a = baseA + (n > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, j / (float)(n - 1)) : 0f);
                        Vector3 d = Dir(a);
                        Shot(sw, ids[j], o + d * Mathf.Max(0.3f, size * 0.4f), telegraph, d * speed, false);
                    }
                    break;
                }
                case NanoEmitter.Line:
                {
                    var ids = sw.Ammo(n, o, tmp);
                    Vector3 d = Dir(baseA), perp = new Vector3(-d.y, d.x, 0f);
                    for (int j = 0; j < ids.Count; j++)
                    {
                        float s = n > 1 ? Mathf.Lerp(-0.5f, 0.5f, j / (float)(n - 1)) : 0f;
                        Shot(sw, ids[j], o + perp * (s * size), telegraph + j * 0.004f, d * speed, false);
                    }
                    break;
                }
                case NanoEmitter.Rain:
                {
                    // 기둥 n 줄 × 3발. 원점 높이 · 깊이(z)에 걸렸다가 바닥(z = 0)으로 쏟아진다
                    float px = player().x;
                    float cx = aimAtPlayer ? px : o.x;
                    const float spacing = 1.3f;
                    var ids = sw.Ammo(n * 3, o, tmp);
                    int used = 0;
                    for (int c = 0; c < n; c++)
                    {
                        float x = cx + (c - (n - 1) * 0.5f) * spacing;
                        if (gap && Mathf.Abs(x - px) < spacing * 0.6f) continue;
                        Vector3 top = new Vector3(x, o.y, o.z);
                        Vector3 target = new Vector3(x + Mathf.Tan(angle * Mathf.Deg2Rad) * Mathf.Max(0f, o.y - sw.floorY), sw.floorY, 0f);
                        Vector3 vel = (target - top).normalized * speed;
                        for (int j = 0; j < 3 && used < ids.Count; j++)
                            Shot(sw, ids[used++], top + Vector3.up * (j * 0.27f), telegraph + c * 0.03f, vel, false);
                    }
                    break;
                }
                default:
                    activeUntil = clock + duration;
                    carry = 0f;
                    theta = baseA;
                    break;
            }
        }

        void EmitOne(RSNanoSwarm sw, Func<Vector3> player, float facing)
        {
            Vector3 o = transform.position;
            if (type == NanoEmitter.Spiral)
            {
                int arms = Mathf.Max(1, Mathf.RoundToInt(size));
                var ids = sw.Ammo(arms, o, tmp);
                for (int k = 0; k < ids.Count; k++)
                {
                    Vector3 d = Dir(theta + k * 360f / arms);
                    Shot(sw, ids[k], o + d * 0.5f, 0f, d * speed, true);
                }
                theta += spread / Mathf.Max(1f, count) * facing;
            }
            else
            {
                var ids = sw.Ammo(1, o, tmp);
                if (ids.Count == 0) return;
                float a = BaseAngle(o, player, facing) + ((float)sw.Rng.NextDouble() - 0.5f) * spread;   // 군집 난수 → 미리보기 되감기에도 같은 결과
                Vector3 d = Dir(a);
                Shot(sw, ids[0], o + d * 0.4f, 0f, d * speed, true);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.35f, 0.8f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
            if (type == NanoEmitter.Ring) { for (int i = 0; i < 24; i++) Gizmos.DrawLine(transform.position + Dir(i * 15f) * size, transform.position + Dir((i + 1) * 15f) * size); }
            if (Mathf.Abs(transform.position.z) > 0.05f)
                Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, transform.position.y, 0f));
        }
    }
}
