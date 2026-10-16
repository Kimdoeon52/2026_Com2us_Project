// RE:AL STEEL - 구버전 씬 값 옮기기 도우미 (*.Legacy.cs 가 쓴다)
using UnityEngine;

namespace RealSteel.Lighting
{
    static class RSLegacyUtil
    {
        /// <summary>씬에 실제로 저장돼 있던 그라데이션인지 (값이 없으면 유니티가 흰색 → 흰색 기본 그라데이션을 채운다)</summary>
        public static bool IsSet(Gradient g)
        {
            if (g == null) return false;
            var c = g.colorKeys; var a = g.alphaKeys;
            bool plain = c.Length == 2 && c[0].color == Color.white && c[1].color == Color.white &&
                         a.Length == 2 && Mathf.Approximately(a[0].alpha, 1f) && Mathf.Approximately(a[1].alpha, 1f);
            return !plain;
        }
    }
}
