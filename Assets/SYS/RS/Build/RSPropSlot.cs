// RE:AL STEEL - 소품 자리 (RSPropSlot)
//
// "여기에 이런 소품이 온다" 를 표시하는 빈 자리. 임시 모형(이름이 '임시_' 로 시작하는 자식)을 품고 있다가,
// 진짜 모델을 이 오브젝트의 자식으로 넣으면 임시 모형이 자동으로 꺼진다 (진짜 모델을 빼면 다시 켜진다).
//  · 기준점 = 바닥 가운데, 앞 = -Z (카메라 쪽)
//  · size 는 이 자리에 맞는 소품 크기 (m) — 씬 뷰에 선 상자로 보인다. Meshy 등에서 뽑을 때 이 크기를 목표로
using UnityEngine;

namespace RealSteel.Build
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Build/소품 자리 (슬롯)")]
    public class RSPropSlot : MonoBehaviour
    {
        public const string TempPrefix = "임시_";

        [TextArea(1, 4), Tooltip("이 자리에 무엇이 오는지 · 어떤 역할인지")]
        public string note = "";
        [Tooltip("이 자리에 맞는 소품 크기 가로(X) · 높이(Y) · 깊이(Z) (m)")]
        public Vector3 size = Vector3.one;
        [Tooltip("진짜 모델을 자식으로 넣으면 임시 모형('임시_' 로 시작하는 자식)을 자동으로 끈다")]
        public bool autoHideTemp = true;
        [Tooltip("씬 뷰 선 상자 색")]
        public Color gizmoColor = new Color(1f, 0.62f, 0.2f, 0.9f);

        void OnEnable() { Refresh(); }
        void OnTransformChildrenChanged() { Refresh(); }

        /// <summary>진짜 모델이 들어와 있으면 임시 모형을 끄고, 없으면 켠다</summary>
        public void Refresh()
        {
            if (!autoHideTemp) return;
            bool real = false;
            foreach (Transform c in transform)
                if (!c.name.StartsWith(TempPrefix)) { real = true; break; }
            foreach (Transform c in transform)
                if (c.name.StartsWith(TempPrefix) && c.gameObject.activeSelf == real) c.gameObject.SetActive(!real);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(new Vector3(0f, size.y * 0.5f, 0f), size);
            Gizmos.DrawLine(Vector3.zero, new Vector3(0f, 0f, -size.z * 0.5f - 0.3f));   // 앞 방향
            Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * (size.y * Mathf.Abs(transform.lossyScale.y) + 0.25f), name);
#endif
        }
    }
}
