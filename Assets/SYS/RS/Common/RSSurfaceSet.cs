// RE:AL STEEL - 재질 세트 (RSSurfaceSet)
//
// 재질 여러 개를 한 묶음으로 (예: '결투장', '공용'). 조립 도구 팔레트 · 지형 칠이 이 목록을 보여 준다.
// 인스펙터에 텍스처를 끌어다 놓으면 재질이 만들어져 이 세트에 들어간다.
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Common
{
    [CreateAssetMenu(menuName = "RE_AL STEEL/재질 세트", fileName = "재질세트", order = 19)]
    public class RSSurfaceSet : ScriptableObject
    {
        [TextArea(1, 3), Tooltip("이 세트 설명 (어느 스테이지 · 테마용인지)")]
        public string note = "";
        [Tooltip("재질 목록 (팔레트 순서)")]
        public List<RSSurface> surfaces = new List<RSSurface>();

        /// <summary>목록에서 이름으로 찾기 (없으면 null)</summary>
        public RSSurface Find(string label)
        {
            foreach (var s in surfaces) if (s != null && s.Label == label) return s;
            return null;
        }
    }
}
