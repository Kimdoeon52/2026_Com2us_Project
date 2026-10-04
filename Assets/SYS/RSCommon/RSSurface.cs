// RE:AL STEEL - 재질 (RSSurface)
//
// 텍스처 하나 + 그 텍스처를 3D 면에 붙이는 규칙. 지형 칠 · 조립 도구 · 나무 껍질 · 울타리가 모두 이걸 골라 쓴다.
//  · 픽셀 밀도는 1m = 32 픽셀 고정 (텍스처 기준). 텍스처 크기가 곧 반복 크기 (64 px = 2 m 마다 반복)
//  · 텍스처 가져오기 설정(Point · 무압축 · 밉맵 끔)은 에디터가 자동으로 맞춘다
//  · 머티리얼은 재질 에셋 안에 같이 저장된다 (셰이더: RE_AL STEEL/Build Pixel Lit). 없으면 실행 중에 만든다
//
// 재질을 고치면 그 재질을 쓰는 모든 조립품 · 지형이 같이 바뀐다.
using UnityEngine;

namespace RealSteel.Common
{
    [CreateAssetMenu(menuName = "RE_AL STEEL/재질 (텍스처 하나)", fileName = "재질", order = 20)]
    public class RSSurface : ScriptableObject
    {
        /// <summary>텍스처 기준 픽셀 밀도 (1m 에 몇 픽셀). 프로젝트 고정값</summary>
        public const int PixelsPerMeter = 32;
        public const string ShaderName = "RE_AL STEEL/Build Pixel Lit";

        [Tooltip("팔레트에 보이는 이름. 비우면 파일 이름")]
        public string displayName = "";
        [Tooltip("픽셀 텍스처. 1m = 32 픽셀로 붙는다 (64 × 64 텍스처 = 2m 마다 반복)")]
        public Texture2D texture;
        [Tooltip("색조 (흰색 = 그대로)")]
        public Color tint = Color.white;

        [Tooltip("투명한 부분을 오려낸다 (울타리 살 · 철망 · 잎). 양면이 필요하면 조립 도구의 '판' 부품 (앞 · 뒤 면이 따로 있음)")]
        public bool cutout = false;
        [Range(0.01f, 0.99f), Tooltip("오려낼 기준 (알파가 이보다 낮으면 구멍)")]
        public float cutoff = 0.5f;

        [Range(0f, 1f), Tooltip("비가 오면 얼마나 젖어 보이나 (아스팔트 · 돌 높음, 천 · 잔디 낮음) — 젖은 바닥 연출이 읽는다")]
        public float wetResponse = 0.5f;
        [Tooltip("표면 종류 (발소리 · 이펙트용, 예: 흙 · 쇠 · 나무 · 천 · 물)")]
        public string surfaceTag = "";

        [Tooltip("자동으로 만든 머티리얼 (재질 에셋 안에 저장). 직접 바꾸지 않아도 된다")]
        public Material material;

        /// <summary>팔레트에 보이는 이름</summary>
        public string Label { get { return string.IsNullOrEmpty(displayName) ? name : displayName; } }

        /// <summary>텍스처 크기 (픽셀). 텍스처가 없으면 32 × 32</summary>
        public Vector2Int TexturePixels
        {
            get { return texture != null ? new Vector2Int(Mathf.Max(1, texture.width), Mathf.Max(1, texture.height)) : new Vector2Int(PixelsPerMeter, PixelsPerMeter); }
        }

        [System.NonSerialized] Material runtimeMat;
        [System.NonSerialized] int runtimeStamp;

        /// <summary>그릴 때 쓸 머티리얼. 저장된 것이 있으면 그것, 없으면 실행 중에 만든 것 (저장 안 됨)</summary>
        public Material GetMaterial(Shader fallbackShader)
        {
            if (material != null) return material;
            int stamp = (texture != null ? texture.GetInstanceID() : 0) ^ tint.GetHashCode() ^ (cutout ? 7919 : 0) ^ cutoff.GetHashCode();
            if (runtimeMat != null && runtimeStamp == stamp) return runtimeMat;
            Shader s = fallbackShader != null ? fallbackShader : Shader.Find(ShaderName);
            if (s == null) return null;
            if (runtimeMat == null) runtimeMat = new Material(s) { name = Label + " (자동)", hideFlags = HideFlags.DontSave };
            ApplyTo(runtimeMat);
            runtimeStamp = stamp;
            return runtimeMat;
        }

        /// <summary>이 재질 값을 머티리얼에 넣는다 (텍스처 · 색 · 오려내기)</summary>
        public void ApplyTo(Material m)
        {
            if (m == null) return;
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            m.SetFloat("_Cull", 2f);
            if (cutout) m.EnableKeyword("_ALPHATEST_ON"); else m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = cutout ? 2450 : -1;
        }

        void OnValidate()
        {
            cutoff = Mathf.Clamp(cutoff, 0.01f, 0.99f);
            if (material != null) ApplyTo(material);
            runtimeStamp = 0;
        }
    }
}
