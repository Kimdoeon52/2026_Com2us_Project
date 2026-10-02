// RE:AL STEEL - 숨은 Volume 주인 표시
//
// 시간대 · 색감 · 자동 초점은 각자 '저장 안 되는' 전역 Volume 을 하나씩 만들어 화면 값을 덮는다.
// 씬의 Volume 만 뒤져서는 안 보이므로, 주인 컴포넌트가 이 인터페이스로 자기 Volume 을 알려 준다.
// (인스펙터의 '화면에 덮는 값' · Tools → RE_AL STEEL → Stage → 화면 효과 진단 창이 쓴다)
using UnityEngine.Rendering;

namespace RealSteel.Lighting
{
    public interface IRSVolumeOwner
    {
        /// <summary>이 컴포넌트가 만든 Volume (아직 없으면 null)</summary>
        Volume OwnedVolume { get; }
        /// <summary>무엇을 덮는지 한 줄 (예: "화면 색감 — 색 필터 · 노출 · 채도 · 비네트")</summary>
        string VolumeRole { get; }
    }
}
