// RE:AL STEEL - 자동 초점 (씬에 있는 Depth Of Field 의 '초점 거리'만 캐릭터에 맞춘다)
//
// DOF 효과 자체는 씬의 Volume(예: Depth → Volum Profile 1)에 있는 URP Depth Of Field 를 그대로 쓴다.
// URP DOF 는 초점 거리가 숫자 하나로 고정이라, 카메라와 캐릭터 사이 거리가 바뀌면(줌 · 점프 · 경사) 캐릭터가 흐려진다.
// 이 컴포넌트는 그 숫자만 매 프레임 캐릭터 거리로 바꾼다.
//
//  · 모드 · 조리개 · 초점 거리(mm) · 최대 반경 · 품질 = 전부 네 Volume 프로필 값 그대로 (거기서 조절)
//  · 바꾸는 것 = 보케의 Focus Distance, 가우시안의 Start/End (네 Start~End 폭은 유지)
//  · 프로필 에셋은 건드리지 않는다: 거리 칸 3개만 덮는 얇은 층을 네 Volume 바로 위 우선순위에 얹는다
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("자동 초점 (DOF 초점 거리)", "씬 Volume 의 Depth Of Field 는 그대로 두고, 초점 거리만 매 프레임 캐릭터에 맞춘다.\n" +
        "· 흐림 모양(모드 · 조리개 · 렌즈 mm · 반경)은 네 Volume 프로필에서 조절한다\n" +
        "· 보케: 초점 = 캐릭터 거리 / 가우시안: 캐릭터 뒤 '선명 여유' 부터 흐려짐 (네 Start~End 폭 유지)\n" +
        "· 프로필 에셋은 안 바뀐다. 이 컴포넌트를 끄면 원래 고정 거리로 돌아간다")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/자동 초점 (DOF)")]
    public class RSAutoFocus : MonoBehaviour, IRSVolumeOwner
    {
        [RSGroup("어느 DOF")]
        [Tooltip("초점을 옮길 Volume (Depth Of Field 가 들어 있는 것). 비우면 씬에서 DOF 가 켜진 Volume 을 찾는다")]
        public Volume source;

        [RSGroup("대상")]
        [Tooltip("초점을 맞출 대상. 비우면 Player 태그 → RS 캐릭터 → CharacterController 순으로 찾는다")]
        [RSKey]
        public Transform target;
        [Tooltip("대상 발에서 이만큼 위를 초점으로 (m). 몸 가운데쯤")]
        public float targetHeight = 1f;
        [Tooltip("카메라. 비우면 화면에 실제로 보이는 카메라(MainCamera 태그 중 Depth 가 가장 높은 것)")]
        public Camera cam;

        [RSGroup("가우시안 모드일 때")]
        [Tooltip("캐릭터 뒤로 이만큼까지는 선명 (m). 가우시안은 앞쪽은 원래 안 흐리다")]
        [RSKey]
        public float sharpBehind = 3f;

        [RSGroup("움직임")]
        [Tooltip("초점이 따라가는 시간 (초). 0 = 즉시. 점프할 때 초점이 출렁이지 않게 약간")]
        [RSKey]
        public float smoothTime = 0.15f;

        const string ChildName = "__RS_AutoFocus (자동 생성 · 저장 안 됨)";
        Volume vol;
        public Volume OwnedVolume { get { return vol; } }
        public string VolumeRole { get { return "자동 초점 — 씬 DOF Volume 위에 초점 거리 칸만 덮음 (원본 우선순위 + 1)"; } }
        VolumeProfile profile;
        DepthOfField dof;
        float focus = -1f, focusVel;
        Camera foundCam;
        Transform foundTarget;
        Volume foundSource;
        int nextSearchFrame;

        // ── 인스펙터 표시용 ──
        public Camera CurrentCamera => foundCam;
        public Transform CurrentTarget => foundTarget;
        public Volume CurrentSource => foundSource;
        /// <summary>지금 초점 거리 (m, 카메라 앞 방향 깊이). 못 재면 -1</summary>
        public float CurrentFocus => focus;
        /// <summary>대상의 실제 화면 깊이 (부드럽게 따라가기 전)</summary>
        public float TargetDepth { get; private set; } = -1f;

        void OnEnable()
        {
            focus = -1f;
            nextSearchFrame = 0;
        }

        // 끄면 얹은 층도 끈다 → 원래 Volume 의 고정 거리로 돌아감
        // (OnDisable 안에서 오브젝트를 지우면 '비활성화 중엔 삭제 불가' 에러가 날 수 있어 끄기만 한다)
        void OnDisable() { if (vol != null) vol.enabled = false; }
        void OnDestroy() { Cleanup(); }

        void Cleanup()
        {
            if (vol != null) Kill(vol.gameObject);
            else { var old = transform.Find(ChildName); if (old != null) Kill(old.gameObject); }
            if (profile != null) Kill(profile);
            vol = null; profile = null; dof = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void EnsureLayer()
        {
            if (vol == null)
            {
                var old = transform.Find(ChildName);
                if (old != null) vol = old.GetComponent<Volume>();
                if (vol == null)
                {
                    var go = new GameObject(ChildName);
                    go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                    go.transform.SetParent(transform, false);
                    vol = go.AddComponent<Volume>();
                }
            }
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "RS_AutoFocus (런타임 · 거리만)";
                profile.hideFlags = HideFlags.DontSave;
                // overrides = false → 모드 · 조리개 · 렌즈 · 반경은 덮지 않고 아래 Volume 값이 그대로 내려온다
                dof = profile.Add<DepthOfField>(false);
            }
            if (vol.sharedProfile != profile) vol.sharedProfile = profile;
            vol.isGlobal = true;
        }

        // ─────────────────────────────── 찾기 ───────────────────────────────

        /// <summary>
        /// 화면에 실제로 보이는 카메라. Camera.main 은 MainCamera 태그가 둘 이상이면 아무거나 돌려줘서
        /// 안 보이는 카메라에서 거리를 재는 일이 생긴다 → 태그 붙은 것 중 Depth 가 가장 높은(마지막에 그려져 위에 보이는) 것.
        /// </summary>
        public static Camera FindViewCamera()
        {
            Camera best = null; bool bestMain = false; float bestDepth = float.MinValue;
            foreach (var c in Camera.allCameras)   // 켜진 카메라만
            {
                if (c == null || c.targetTexture != null || c.cameraType != CameraType.Game) continue;
                if (c.name.StartsWith("__RS")) continue;   // 물 반사 같은 보조 카메라
                var ad = c.GetUniversalAdditionalCameraData();
                if (ad != null && ad.renderType == CameraRenderType.Overlay) continue;
                bool main = c.CompareTag("MainCamera");
                if (best == null || (main && !bestMain) || (main == bestMain && c.depth > bestDepth))
                { best = c; bestMain = main; bestDepth = c.depth; }
            }
            return best != null ? best : Camera.main;
        }

        /// <summary>초점 대상 찾기: Player 태그 → RS 캐릭터(RSSimpleCharacter) → CharacterController</summary>
        public static Transform FindCharacter()
        {
            GameObject p = null;
            try { p = GameObject.FindWithTag("Player"); } catch { }
            if (p != null) return p.transform;
            // RSSimpleCharacter 는 다른 어셈블리라 이름으로 찾는다
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb != null && mb.isActiveAndEnabled && mb.GetType().Name == "RSSimpleCharacter") return mb.transform;
            var cc = FindAnyObjectByType<CharacterController>();
            return cc != null ? cc.transform : null;
        }

        /// <summary>DOF 가 켜진 Volume 중 우선순위가 가장 높은 것 (이 컴포넌트가 얹은 층은 제외)</summary>
        public static Volume FindDofVolume()
        {
            Volume best = null;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v == null || !v.isActiveAndEnabled || v.name.StartsWith("__RS")) continue;
                if (!TryGetDof(v, out var d)) continue;
                // 우선순위가 같으면 세기(weight)가 큰 쪽 — 매번 같은 걸 고르게
                if (best == null || v.priority > best.priority || (v.priority == best.priority && v.weight > best.weight)) best = v;
            }
            return best;
        }

        public static bool TryGetDof(Volume v, out DepthOfField d)
        {
            d = null;
            var p = v != null ? v.sharedProfile : null;
            return p != null && p.TryGet(out d) && d.active && d.mode.overrideState && d.mode.value != DepthOfFieldMode.Off;
        }

        void Resolve()
        {
            // 찾기는 가끔만 (매 프레임 씬 전체를 뒤지지 않게)
            bool stale = Time.frameCount >= nextSearchFrame;
            if (cam != null) foundCam = cam;
            else if (foundCam == null || !foundCam.isActiveAndEnabled || stale) foundCam = FindViewCamera();
            if (target != null) foundTarget = target;
            else if (foundTarget == null || !foundTarget.gameObject.activeInHierarchy || stale) foundTarget = FindCharacter();
            if (source != null) foundSource = source;
            else if (foundSource == null || !foundSource.isActiveAndEnabled || stale) foundSource = FindDofVolume();
            if (stale) nextSearchFrame = Time.frameCount + 60;
        }

        // ─────────────────────────────── 매 프레임 ───────────────────────────────

        void LateUpdate()
        {
            Resolve();
            var c = foundCam;
            var t = foundTarget;
            if (c == null || t == null || foundSource == null || !TryGetDof(foundSource, out var src))
            {
                // 옮길 DOF 가 없으면 아무것도 얹지 않는다
                if (vol != null && vol.enabled) vol.enabled = false;
                focus = -1f; TargetDepth = -1f;
                return;
            }

            EnsureLayer();
            if (!vol.enabled) vol.enabled = true;
            // 네 Volume 바로 위에 얹는다. 거리 칸만 덮는 층이라 세기는 항상 1 —
            // 원본 세기(예: 0.775)를 따라가면 초점이 그만큼 원래 고정 거리 쪽으로 끌려가 캐릭터가 살짝 흐려진다
            float pr = foundSource.priority + 1f;
            if (vol.priority != pr) vol.priority = pr;
            if (vol.weight != 1f) vol.weight = 1f;

            // 카메라 앞 방향으로 잰 거리 (화면 깊이) — DOF 가 깊이 텍스처에서 읽는 거리와 같다
            Vector3 p = t.position + Vector3.up * targetHeight;
            float d = Mathf.Max(0.1f, Vector3.Dot(p - c.transform.position, c.transform.forward));
            TargetDepth = d;

            if (focus < 0f || !Application.isPlaying || smoothTime <= 0f) { focus = d; focusVel = 0f; }
            else focus = Mathf.SmoothDamp(focus, d, ref focusVel, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            // 거리 칸 3개만 덮는다 (모드와 상관없이 셋 다 — 모드는 네 Volume 이 정한다)
            dof.active = true;
            dof.focusDistance.Override(focus);
            float width = src.gaussianEnd.value - src.gaussianStart.value;
            float start = focus + Mathf.Max(0f, sharpBehind);
            dof.gaussianStart.Override(start);
            dof.gaussianEnd.Override(start + Mathf.Max(0.5f, width));
        }
    }
}
