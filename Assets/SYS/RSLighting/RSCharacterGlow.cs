// RE:AL STEEL - 캐릭터 밤 빛 (RSCharacterGlow)
//
// 옥토패스처럼 밤에 주인공 둘레가 은은하게 밝아지게 하는 포인트 라이트.
// 시간대(RS 시간대)를 보고 낮엔 꺼지고 밤엔 서서히 켜진다. 빛은 캐릭터 머리 위 · 카메라 쪽으로 살짝 띄워서
// 스프라이트 앞면과 발밑 바닥을 같이 밝힌다. 볼류메트릭 안개 속에서는 둘레가 뿌옇게 번진다.
//
// 이미 붙여 둔 Point Light 가 있으면 '빛' 칸에 넣으면 그걸 조절한다 (없으면 자식에 만든다).
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("캐릭터 밤 빛", "밤에 캐릭터 둘레를 은은하게 밝히는 포인트 라이트. 시간대를 보고 낮엔 꺼지고 밤엔 서서히 켜진다.\n" +
        "· 머리 위 · 카메라 쪽으로 띄워서 스프라이트 앞면과 발밑을 같이 밝힌다\n" +
        "· 그림자는 끈다 (포인트 라이트 그림자는 비싸고 스프라이트엔 어색하다)\n" +
        "· URP 에셋의 Additional Lights 가 Per Pixel 이어야 선명하다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/캐릭터 밤 빛")]
    public partial class RSCharacterGlow : MonoBehaviour
    {
        [Tooltip("조절할 Point Light. 비우면 자식에 만든다 (이미 붙여 둔 라이트가 있으면 여기에)")]
        public Light glowLight;
        [Tooltip("시간대. 비우면 씬에서 찾는다 (없으면 항상 밤 세기)")]
        public RSTimeOfDay timeOfDay;

        // ── 룩 (스테이지 룩 프로필로 옮길 수 있는 값) ──
        [System.Serializable]
        public class Look
        {
            [RSGroup("빛")]
            [Tooltip("빛 색. 따뜻한 등불 색이 옥토패스 느낌")]
            [RSKey]
            public Color color = new Color(1f, 0.8f, 0.55f);
            [Tooltip("밤 세기. 1 ~ 2 권장 — 너무 세면 캐릭터가 하얗게 뜬다")]
            [RSKey]
            public float nightIntensity = 1.5f;
            [Tooltip("낮 세기 (보통 0)")]
            public float dayIntensity = 0f;
            [Tooltip("빛이 닿는 반경 (m). 4 ~ 6")]
            [RSKey]
            public float range = 5f;
            [RSGroup("위치")]
            [Tooltip("발에서 빛까지 높이 (m)")]
            public float height = 1.8f;
            [Tooltip("카메라 쪽으로 당기는 거리 (m) — 스프라이트 앞면이 밝게")]
            public float towardCamera = 0.6f;
            [RSGroup("흔들림")]
            [Range(0f, 0.3f), Tooltip("등불처럼 살짝 일렁임 (플레이 중에만). 0 = 끔")]
            public float flicker = 0.04f;
            [Tooltip("일렁임 빠르기")]
            public float flickerSpeed = 3f;
        }

        [SerializeField, RSLook] Look look = new Look();

        /// <summary>지금 쓰는 룩 값 — 스테이지 룩 프로필이 있으면 그 값, 없으면 이 컴포넌트 값</summary>
        public Look L { get { var p = RSStageLook.Current; return p != null ? p.characterGlow : look; } }
        /// <summary>이 컴포넌트에 저장된 값 (프로필이 없을 때 쓰는 값)</summary>
        public Look LocalLook { get { return look; } }
        /// <summary>스테이지 룩 프로필 값을 쓰고 있는지</summary>
        public bool UsesStageLook { get { return RSStageLook.Current != null; } }




        const string ChildName = "__RS_Glow (자동 생성 · 저장 안 됨)";
        bool ownsLight;
        Camera viewCam;
        int nextCamFrame;

        void OnEnable()
        {

            MigrateLegacy();

            RSStageLook.Changed += OnStageLookChanged;
            if (timeOfDay == null) timeOfDay = FindAnyObjectByType<RSTimeOfDay>();
            EnsureLight();
            Apply(true);
        }

        /// <summary>스테이지 룩 프로필이 바뀌거나 값이 바뀌면 (인스펙터에서 프로필을 고친 경우 이 컴포넌트의 OnValidate 는 안 불린다)</summary>

        [System.NonSerialized] int lookSeen;
        [System.NonSerialized] RSStageLook lookFrom;
        void OnStageLookChanged()

        {

            if (this == null || !isActiveAndEnabled) return;
            // 다른 묶음(예: 젖은 바닥 슬라이더)만 바뀐 거면 건너뛴다 — 무거운 재계산을 피하려고
            int h = JsonUtility.ToJson(L).GetHashCode();
            if (h == lookSeen && RSStageLook.Current == lookFrom) return;
            lookSeen = h; lookFrom = RSStageLook.Current;

            Apply(true);

        }


        void OnDisable()
        {

            RSStageLook.Changed -= OnStageLookChanged;
            if (glowLight != null && ownsLight) glowLight.enabled = false;
        }

        void OnDestroy()
        {
            if (ownsLight && glowLight != null)
            {
                if (Application.isPlaying) Destroy(glowLight.gameObject); else DestroyImmediate(glowLight.gameObject);
            }
        }

        void EnsureLight()
        {
            if (glowLight != null) { ownsLight = glowLight.transform.name == ChildName; return; }
            var old = transform.Find(ChildName);
            GameObject go = old != null ? old.gameObject : null;
            if (go == null)
            {
                go = new GameObject(ChildName);
                go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                go.transform.SetParent(transform, false);
            }
            glowLight = go.GetComponent<Light>();
            if (glowLight == null) glowLight = go.AddComponent<Light>();
            ownsLight = true;
        }

        void LateUpdate() { Apply(false); }

        void Apply(bool force)
        {
            if (glowLight == null) return;
            float day = timeOfDay != null ? timeOfDay.DayFactor : 0f;
            float k = Mathf.Lerp(L.nightIntensity, L.dayIntensity, day);
            if (Application.isPlaying && L.flicker > 0f)
                k *= 1f + (Mathf.PerlinNoise(Time.time * L.flickerSpeed, 0.37f) - 0.5f) * 2f * L.flicker;

            // 바뀐 것만 쓴다 (편집 중 매 프레임 쓰면 인스펙터가 계속 다시 그려진다)
            Set(ref force, glowLight.type != LightType.Point, () => glowLight.type = LightType.Point);
            Set(ref force, glowLight.shadows != LightShadows.None, () => glowLight.shadows = LightShadows.None);
            Set(ref force, glowLight.renderMode != LightRenderMode.ForcePixel, () => glowLight.renderMode = LightRenderMode.ForcePixel);
            if (glowLight.color != L.color) glowLight.color = L.color;
            if (!Mathf.Approximately(glowLight.range, L.range)) glowLight.range = L.range;
            if (!Mathf.Approximately(glowLight.intensity, k)) glowLight.intensity = k;
            bool on = k > 0.01f;
            if (glowLight.enabled != on) glowLight.enabled = on;

            if (ownsLight)
            {
                Vector3 pos = transform.position + Vector3.up * L.height;
                // Camera.main 은 MainCamera 태그가 둘이면 안 보이는 쪽을 줄 수 있다 → 보이는 카메라를 가끔 다시 찾는다
                if (viewCam == null || !viewCam.isActiveAndEnabled || Time.frameCount >= nextCamFrame)
                { viewCam = RSAutoFocus.FindViewCamera(); nextCamFrame = Time.frameCount + 60; }
                var c = viewCam;
                if (c != null && L.towardCamera != 0f)
                {
                    Vector3 toCam = c.transform.position - transform.position; toCam.y = 0f;
                    if (toCam.sqrMagnitude > 1e-4f) pos += toCam.normalized * L.towardCamera;
                }
                if ((glowLight.transform.position - pos).sqrMagnitude > 1e-6f) glowLight.transform.position = pos;
            }
        }

        static void Set(ref bool force, bool differs, System.Action write) { if (differs || force) write(); }
    }
}
