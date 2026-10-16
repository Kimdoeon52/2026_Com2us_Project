// RE:AL STEEL - 간접광 색 번짐 (요소)
//
// 이 오브젝트 둘레 바닥 · 벽 · 캐릭터를 은은하게 물들인다. 간접광(RS 간접광)이 받아서 섞는다.
// 예) 네온 간판 앞 바닥이 분홍으로, 모닥불 둘레가 주황으로, 빨간 컨테이너 옆이 붉게.
// 라이트를 하나 더 켜는 것보다 싸고(그림자 · 조명 계산 없음), 바닥에 부드럽게 번진다.
//
// 쓰는 법: 아무 오브젝트에 붙이고 색 · 반경만 고른다. 라이트에 붙이면 그 라이트 색을 따라간다.
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("간접광 색 번짐", "둘레 바닥 · 벽 · 캐릭터를 은은하게 물들인다 (라이트보다 싸고 부드럽다).\n" +
        "· 네온 간판 · 모닥불 · 빨간 컨테이너처럼 '주변에 색이 비치는 것' 에 붙인다\n" +
        "· 라이트에 붙이면 그 라이트 색 · 세기를 따른다 (밤에 켜지는 가로등 등)\n" +
        "· 씬에 '간접광' 이 있어야 보인다 (메뉴 '스테이지 연출 한 번에 설치')")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/간접광 색 번짐")]
    public class RSBounceSource : MonoBehaviour
    {
        public static readonly List<RSBounceSource> All = new List<RSBounceSource>();

        [Tooltip("번지는 색")]
        public Color color = new Color(1f, 0.55f, 0.25f);
        [Range(0f, 4f), Tooltip("세기")]
        public float intensity = 1f;
        [Range(0.5f, 20f), Tooltip("번지는 반경 (m)")]
        public float radius = 4f;

        [RSGroup("라이트 따라가기")]
        [Tooltip("이 오브젝트의 라이트 색 · 세기를 따른다 (라이트가 꺼지면 번짐도 꺼짐)")]
        public bool followLight = true;
        [RSGroup("시간")]
        [Tooltip("밤에만 (낮엔 서서히 꺼짐)")]
        public bool nightOnly = false;

        Light cachedLight;
        static RSTimeOfDay tod;

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            cachedLight = GetComponent<Light>();
        }

        void OnDisable() { All.Remove(this); }

        /// <summary>실제로 번지는 색 (선형 색공간 × 세기). 0 이면 안 번짐</summary>
        public Color EffectiveColor
        {
            get
            {
                Color c = color.linear * intensity;
                if (followLight && cachedLight != null)
                {
                    if (!cachedLight.isActiveAndEnabled) return Color.clear;
                    c = cachedLight.color.linear * cachedLight.intensity * intensity * 0.35f;
                }
                if (nightOnly)
                {
                    if (tod == null) tod = FindAnyObjectByType<RSTimeOfDay>();
                    if (tod != null) c *= 1f - tod.DayFactor;
                }
                c.a = 1f;
                return c;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.25f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
