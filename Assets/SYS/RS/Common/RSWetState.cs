// RE:AL STEEL - 젖음 공용 상태 (지형 · 조명 어셈블리가 서로 몰라도 '비 온 정도' 를 나눠 쓰게)
//
// 젖은 바닥 설정(RSWetness, 조명 쪽)이 비 온 정도와 반사 겹 머티리얼을 여기에 올리고,
// RS 지형(지형 쪽)은 여기를 보고 젖은 곳 위에 반사 겹을 씌울지 정한다.
using System;
using UnityEngine;

namespace RealSteel.Common
{
    public static class RSWetState
    {
        /// <summary>비 온 정도 0 ~ 1 (칠 안 한 바닥도 이만큼 젖음)</summary>
        public static float Rain { get; private set; }

        /// <summary>젖은 곳 위에 씌우는 반사 겹 머티리얼 (RE_AL STEEL/Wet Reflection)</summary>
        public static Material OverlayMaterial { get; private set; }

        /// <summary>비가 0 ↔ 0 초과로 바뀌거나 머티리얼이 바뀌면 불린다 (지형이 겹을 다시 씌움)</summary>
        public static event Action Changed;

        public static void Set(float rain, Material overlay)
        {
            bool wasRain = Rain > 0.001f, nowRain = rain > 0.001f;
            bool matChanged = OverlayMaterial != overlay;
            Rain = rain;
            OverlayMaterial = overlay;
            if (wasRain != nowRain || matChanged) Changed?.Invoke();
        }
    }
}
