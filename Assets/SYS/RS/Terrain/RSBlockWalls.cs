// RE:AL STEEL - 이동 막기 (보이지 않는 벽)
//
// 상자 목록(boxes)으로 보이지 않는 BoxCollider 벽을 만든다. 맵 가장자리 · 빠지면 못 돌아오는 구덩이 앞을 막는 용도.
// 자동 벽(boxes)은 Tools → RE_AL STEEL → Stage → 이동 막기 (자동 경계) 창이 채운다.
// 손으로 고친 벽(manual)은 씬 뷰에서 옮기고 · 늘리고 · 돌린다 (RSBlockWallsEditor). 다시 분석해도 수동 벽은 그대로고,
// 손으로 옮기거나 지운 자동 벽 자리(suppressed)에는 자동 벽이 다시 생기지 않는다.
//
// 콜라이더는 다른 RS 도구처럼 저장하지 않는 숨은 자식(__RS_BlockWalls)에 켤 때마다 만든다
// → 프리팹 · 씬에는 상자 숫자만 저장된다. 씬 뷰에서는 이 오브젝트를 선택하면 빨간 상자로 보인다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Terrain
{
    [RSSummary("이동 막기 (보이지 않는 벽)", "맵 가장자리 · 빠지면 못 돌아오는 곳 앞에 보이지 않는 벽을 세운다.\n· 자동 벽(빨강)은 Tools → RE_AL STEEL → Stage → 이동 막기 (자동 경계) 창에서 분석해 채운다\n· 씬 뷰에서 벽 가운데 점을 눌러 옮기기 · 크기 · 방향 조정 → 수동 벽(하늘색)이 된다\n· 보이는 울타리가 필요한 곳은 벽 자리에 조립품 울타리를 놓으면 된다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/이동 막기 (보이지 않는 벽)")]
    public class RSBlockWalls : MonoBehaviour
    {
        [System.Serializable]
        public struct Box
        {
            public Vector3 center;
            public Vector3 size;
            [Tooltip("Y 회전 (도)")] public float yaw;
            public Box(Vector3 c, Vector3 s) { center = c; size = s; yaw = 0f; }
            public Box(Vector3 c, Vector3 s, float y) { center = c; size = s; yaw = y; }
            public Matrix4x4 Local { get { return Matrix4x4.TRS(center, Quaternion.Euler(0f, yaw, 0f), Vector3.one); } }
        }

        const string ChildName = "__RS_BlockWalls (자동 생성 · 저장 안 됨)";

        [Tooltip("자동 벽 (이 오브젝트 기준). 자동 경계 창이 채운다 — 다시 분석하면 바뀐다")]
        public List<Box> boxes = new List<Box>();
        [Tooltip("손으로 놓거나 고친 벽. 다시 분석해도 그대로")]
        public List<Box> manual = new List<Box>();
        [Tooltip("손으로 옮기거나 지운 자동 벽 자리. 다시 분석해도 여기엔 자동 벽을 안 세운다")]
        public List<Box> suppressed = new List<Box>();
        [Tooltip("선택하지 않아도 씬 뷰에 빨간 상자로 보여 준다")]
        public bool alwaysShow = false;

        [System.NonSerialized] GameObject holder;

        void OnEnable() { Build(); }

        void OnDisable() { if (holder != null) holder.SetActive(false); }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        void OnDestroy()
        {
            var t = transform != null ? transform.Find(ChildName) : null;
            if (t == null) return;
            var child = t.gameObject;
            if (Application.isPlaying) { Destroy(child); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (child != null && (child.transform.parent == null || child.transform.parent.GetComponent<RSBlockWalls>() == null)) DestroyImmediate(child);
            };
#endif
        }

        /// <summary>상자 목록대로 콜라이더를 다시 맞춘다</summary>
        public void Build()
        {
            if (!gameObject.scene.IsValid()) return;      // 프리팹 에셋에서는 만들지 않는다
            if (holder == null) { var t = transform.Find(ChildName); if (t != null) holder = t.gameObject; }
            if (holder == null)
            {
                holder = new GameObject(ChildName);
                holder.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                holder.transform.SetParent(transform, false);
            }
            holder.layer = gameObject.layer;
            if (!holder.activeSelf) holder.SetActive(true);

            // 돌리지 않은 벽은 holder 하나에 BoxCollider 여러 개, 돌린 벽은 자식 하나씩
            var flat = new List<Box>();
            var turned = new List<Box>();
            Collect(boxes, flat, turned);
            Collect(manual, flat, turned);

            var cols = holder.GetComponents<BoxCollider>();
            for (int i = 0; i < flat.Count; i++)
            {
                var c = i < cols.Length ? cols[i] : holder.AddComponent<BoxCollider>();
                c.center = flat[i].center; c.size = flat[i].size; c.isTrigger = false; c.enabled = true;
            }
            for (int i = flat.Count; i < cols.Length; i++) Kill(cols[i]);

            var ht = holder.transform;
            for (int i = 0; i < turned.Count; i++)
            {
                Transform k;
                if (i < ht.childCount) k = ht.GetChild(i);
                else
                {
                    var g = new GameObject("벽");
                    g.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                    g.AddComponent<BoxCollider>();
                    k = g.transform; k.SetParent(ht, false);
                }
                k.gameObject.layer = gameObject.layer;
                k.localPosition = turned[i].center;
                k.localRotation = Quaternion.Euler(0f, turned[i].yaw, 0f);
                k.localScale = Vector3.one;
                var bc = k.GetComponent<BoxCollider>();
                if (bc == null) bc = k.gameObject.AddComponent<BoxCollider>();
                bc.center = Vector3.zero; bc.size = turned[i].size; bc.isTrigger = false; bc.enabled = true;
            }
            for (int i = ht.childCount - 1; i >= turned.Count; i--) Kill(ht.GetChild(i).gameObject);
        }

        static void Collect(List<Box> src, List<Box> flat, List<Box> turned)
        {
            if (src == null) return;
            foreach (var b in src) (Mathf.Abs(Mathf.DeltaAngle(b.yaw, 0f)) < 0.01f ? flat : turned).Add(b);
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnDrawGizmos() { if (alwaysShow) Draw(0.55f); }
        void OnDrawGizmosSelected() { if (!alwaysShow) Draw(1f); }

        public static readonly Color AutoColor = new Color(1f, 0.25f, 0.2f);
        public static readonly Color ManualColor = new Color(0.25f, 0.85f, 1f);

        void Draw(float a)
        {
            DrawList(boxes, AutoColor, a);
            DrawList(manual, ManualColor, a);
            Gizmos.matrix = Matrix4x4.identity;
        }

        void DrawList(List<Box> list, Color col, float a)
        {
            if (list == null) return;
            foreach (var b in list)
            {
                Gizmos.matrix = transform.localToWorldMatrix * b.Local;
                Gizmos.color = new Color(col.r, col.g, col.b, 0.12f * a);
                Gizmos.DrawCube(Vector3.zero, b.size);
                Gizmos.color = new Color(col.r, col.g, col.b, a);
                Gizmos.DrawWireCube(Vector3.zero, b.size);
            }
        }
    }
}
