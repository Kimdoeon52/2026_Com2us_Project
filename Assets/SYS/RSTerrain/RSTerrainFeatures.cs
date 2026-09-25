// RE:AL STEEL - RS Terrain 요소들
//
// RSTerrain 의 자식에 붙인다. 오브젝트를 옮기고 돌리면 지형이 따라 바뀐다 (Y 회전 지원).
// order 가 낮은 것부터 적용되고, 나중 것이 앞의 결과를 덮는다.
//   기본 순서: 둔덕 0 → 더미 10 → 웅덩이 20 → 작업장 30 → 길 40 → 배수로 50
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    // ═════════════════════════════════════════════════════════════════
    // 공통
    // ═════════════════════════════════════════════════════════════════

    [ExecuteAlways]
    public abstract class RSTerrainFeature : MonoBehaviour
    {
        [Tooltip("적용 순서. 낮은 것부터 쌓고 나중 것이 앞의 결과를 덮는다.\n기본: 둔덕 0 · 더미 10 · 웅덩이 20 · 작업장 30 · 길 40 · 배수로 50 · 폐자재 100")]
        public int order;
        [Range(0f, 1f), Tooltip("이 요소의 세기 (0 = 없는 것과 같음). 효과를 약하게 섞거나 잠깐 끌 때")]
        public float strength = 1f;
        [HideInInspector] public int salt = 1;

        // Prepare 에서 채우는 캐시 (지형 로컬 좌표)
        [System.NonSerialized] public Rect bounds;
        [System.NonSerialized] protected Vector3 c;
        [System.NonSerialized] protected float cs = 1f, sn = 0f;
        [System.NonSerialized] protected int seed;

        public virtual int DefaultOrder { get { return 0; } }
        public abstract Color GizmoColor { get; }

        protected virtual void Reset()
        {
            salt = Random.Range(1, 999999);
            order = DefaultOrder;
        }

        public RSTerrain Owner { get { return GetComponentInParent<RSTerrain>(); } }

        /// <summary>지형 기준 위치·회전 갱신</summary>
        public void UpdateFrame(RSTerrain t)
        {
            var m = t.transform.worldToLocalMatrix;
            c = m.MultiplyPoint3x4(transform.position);
            Vector3 f = m.MultiplyVector(transform.forward); f.y = 0f;
            float yaw = f.sqrMagnitude > 1e-8f ? Mathf.Atan2(f.x, f.z) : 0f;
            cs = Mathf.Cos(yaw); sn = Mathf.Sin(yaw);
            seed = t.seed * 7919 + salt;
        }

        public void Prepare(RSTerrain t)
        {
            UpdateFrame(t);
            OnPrepare(t);
            bounds = ComputeBounds();
        }

        protected virtual void OnPrepare(RSTerrain t) { }
        protected abstract Rect ComputeBounds();
        public abstract void Apply(ref RSSample s, float x, float z);
        public virtual void AddWater(RSTerrain t, List<Vector3> v, List<int> tri) { }

        /// <summary>지형 로컬 → 요소 로컬 (회전 반영)</summary>
        protected Vector2 Local(float x, float z)
        {
            float dx = x - c.x, dz = z - c.z;
            return new Vector2(dx * cs - dz * sn, dx * sn + dz * cs);
        }

        /// <summary>요소 로컬 → 지형 로컬 XZ</summary>
        protected Vector2 ToTerrain(Vector2 q)
        {
            return new Vector2(c.x + q.x * cs + q.y * sn, c.z - q.x * sn + q.y * cs);
        }

        protected static Rect RectAround(float x, float z, float r)
        {
            return new Rect(x - r, z - r, r * 2f, r * 2f);
        }

        protected float N(float x, float z, float f, int ch) { return RSMath.Noise(x, z, f, ch, seed); }
        protected float SN(float x, float z, float f, int ch) { return RSMath.SNoise(x, z, f, ch, seed); }
        protected static float SS(float t) { return RSMath.SmoothStep(t); }

        protected virtual void OnValidate() { Notify(); }
        protected virtual void OnEnable() { Notify(); }
        protected virtual void OnDisable() { Notify(); }

        public void Notify()
        {
            var t = Owner;
            if (t != null) t.MarkDirty();
        }

        // ── 기즈모: 지면 위에 외곽선 ──
        protected void DrawOutline(IList<Vector2> featureLocal, bool closed, bool selected)
        {
            var t = Owner;
            if (t == null) return;
            UpdateFrame(t);
            var col = GizmoColor; col.a = selected ? 0.95f : 0.45f;
            Gizmos.color = col;
            Vector3 first = Vector3.zero, prev = Vector3.zero;
            for (int i = 0; i < featureLocal.Count; i++)
            {
                Vector2 q = ToTerrain(featureLocal[i]);
                float y = (t.HasHeights ? t.SampleHeight(q.x, q.y) : c.y) + 0.06f;
                Vector3 w = t.transform.TransformPoint(new Vector3(q.x, y, q.y));
                if (i == 0) first = w; else Gizmos.DrawLine(prev, w);
                prev = w;
            }
            if (closed && featureLocal.Count > 2) Gizmos.DrawLine(prev, first);
        }

        protected static List<Vector2> CirclePts(float r, int seg = 40)
        {
            var l = new List<Vector2>(seg);
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                l.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return l;
        }

        protected static List<Vector2> RoundRectPts(Vector2 size, float round, int segPerCorner = 6)
        {
            float r = Mathf.Clamp(round, 0f, Mathf.Min(size.x, size.y) * 0.5f);
            float hx = size.x * 0.5f - r, hz = size.y * 0.5f - r;
            var l = new List<Vector2>();
            Vector2[] cen = { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            for (int k = 0; k < 4; k++)
                for (int s = 0; s <= segPerCorner; s++)
                {
                    float a = (k * 90f + s * 90f / segPerCorner) * Mathf.Deg2Rad;
                    l.Add(cen[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return l;
        }
    }
}
