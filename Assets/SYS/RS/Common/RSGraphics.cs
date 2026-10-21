// RE:AL STEEL - 그래픽 설정 (플레이어 옵션)
//
// ESC 설정창이 바꾸는 '품질' 값을 한곳에 모은다. 룩(색 · 밀도 · 세기)은 스테이지 룩 프로필이 정하고,
// 여기 값은 그 위에 곱해지거나(배율 · 상한) 켜고 끄기만 한다:   최종값 = 룩 값 × 그래픽 설정
//
//  · 각 조명 컴포넌트(안개 · 갓레이 · 구름 · 햇살 · 밤 빛 · 젖은 바닥 · 물 · 간접광)가 RSGraphics.Effective 를 매 프레임 읽는다
//  · 후처리(DOF · 블룸 · 밝기) · URP 그림자 거리 · 해상도는 RSGraphicsManager(RSLighting)가 처리한다 — 씬에 놓을 것 없음, 자동 시작
//  · 플레이어가 고른 값은 PlayerPrefs 에 저장된다
//  · 낮음 · 중간 · 높음 단계 값과 품질 숫자는 프리셋 에셋(SYS/Presets/Graphics/Resources/RS_GraphicsPresets.asset)에 있다
//    → 인스펙터 표에서 고친다 (Tools → RE_AL STEEL → 그래픽 프리셋). 에셋이 없으면 코드 기본값
//  · 에디터에서 플레이 중이 아닐 때는 항상 '높음'으로 보인다 (아트 작업 화면이 플레이어 설정에 흔들리지 않게)
//
// ESC 설정창에서 쓰는 법
//   var o = RSGraphics.Options.Clone();      // 창을 열 때 사본
//   o.bloom = toggle.isOn; ...               // 사용자가 바꾸는 동안 사본만 고친다
//   RSGraphics.Set(o);                        // '적용' — 화면 반영 + 저장
//   RSGraphics.SetLevel(RSGraphicsLevel.중간); // 품질 단계 버튼 — 품질 항목만 바뀌고 화면 · 밝기는 그대로
//   o.level  → 지금 값이 어느 단계와 같은지 (손대면 '사용자')
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Common
{
    public enum RSGraphicsLevel { 낮음 = 0, 중간 = 1, 높음 = 2, 사용자 = 3 }

    [Serializable]
    public class RSGraphicsOptions
    {
        [Tooltip("지금 값이 어느 품질 단계와 같은지 (자동으로 정해진다)")]
        public RSGraphicsLevel level = RSGraphicsLevel.높음;

        // ── 대기 ─────────────────────────────────────────────
        [Tooltip("볼류메트릭 안개. 끄면 안개 속 빛줄기(갓레이 주력)도 같이 사라진다")]
        public bool fog = true;
        [Range(0, 2), Tooltip("안개 계산 품질 (0 거칠게 · 2 원래대로) — 걸음 간격 · 걸음 수 · 라이트 수")]
        public int fogQuality = 2;
        [Tooltip("화면 갓레이 (화면 위 빛살)")]
        public bool screenGodRays = true;
        [Range(0, 2), Tooltip("화면 갓레이 샘플 (0 = 8 · 1 = 12 · 2 = 원래대로)")]
        public int godRayQuality = 2;
        [Tooltip("구름 · 나뭇잎 그림자. 끄면 안개 빛줄기의 얼룩 무늬도 빠진다")]
        public bool cloudShadows = true;
        [Range(0f, 1f), Tooltip("햇살 기둥 개수 배율 (0 = 끔 · 1 = 원래대로)")]
        public float lightShafts = 1f;
        [Tooltip("캐릭터 밤 빛 (밤에 캐릭터가 읽히게 하는 등불)")]
        public bool characterGlow = true;

        // ── 반사 · 간접광 ───────────────────────────────────
        [Range(0, 2), Tooltip("젖은 바닥 (0 = 어둡게만, 반사 없음 · 1 = 화면 반사 · 2 = 화면 반사 + 라이트 반사)")]
        public int wetFloor = 2;
        [Range(0, 2), Tooltip("물 반사 (0 = 하늘색만 · 1 = 화면 반사 · 2 = 물 반사 컴포넌트 설정대로)")]
        public int water = 2;
        [Range(0, 2), Tooltip("간접광 (0 = 끔 · 1 = 칸 2배(거칠게) · 2 = 원래대로). 바꾸면 다시 계산한다")]
        public int indirect = 2;

        // ── 후처리 ───────────────────────────────────────────
        [Tooltip("피사계 심도 (배경 흐림). 후처리 중 가장 무겁다")]
        public bool depthOfField = true;
        [Tooltip("블룸 (네온 · 발광이 번져 빛남)")]
        public bool bloom = true;
        [Tooltip("블룸 고품질 필터")]
        public bool bloomHighQuality = true;

        // ── 그림자 (URP 에셋) ───────────────────────────────
        [Range(0, 2), Tooltip("그림자 거리 (0 = 절반 · 1 = 3/4 · 2 = 원래대로). 안개 빛줄기도 이 그림자를 쓴다")]
        public int shadows = 2;

        // ── 품질 단계와 상관없는 값 ─────────────────────────
        [Range(-1f, 1f), Tooltip("밝기 (노출 EV 더하기). 0 = 그대로")]
        public float brightness = 0f;
        [Tooltip("해상도 가로 · 세로. 0 = 바꾸지 않음 (빌드에서만 적용)")]
        public int width = 0, height = 0;
        [Tooltip("전체 화면 방식 (빌드에서만 적용)")]
        public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
        [Tooltip("수직 동기화")]
        public bool vSync = true;
        [Tooltip("최대 프레임 (수직 동기화를 끈 때만). -1 = 제한 없음")]
        public int targetFps = -1;

        public RSGraphicsOptions Clone() { return (RSGraphicsOptions)MemberwiseClone(); }

        /// <summary>품질 항목만 그 단계 값으로 (밝기 · 해상도 · 수직 동기화는 그대로)</summary>
        public void ApplyLevel(RSGraphicsLevel l)
        {
            if (l == RSGraphicsLevel.사용자) return;
            var p = RSGraphics.Preset(l);
            fog = p.fog; fogQuality = p.fogQuality;
            screenGodRays = p.screenGodRays; godRayQuality = p.godRayQuality;
            cloudShadows = p.cloudShadows; lightShafts = p.lightShafts; characterGlow = p.characterGlow;
            wetFloor = p.wetFloor; water = p.water; indirect = p.indirect;
            depthOfField = p.depthOfField; bloom = p.bloom; bloomHighQuality = p.bloomHighQuality;
            shadows = p.shadows;
            level = l;
        }

        /// <summary>품질 항목이 다른 값과 모두 같은지</summary>
        public bool SameQuality(RSGraphicsOptions p)
        {
            return fog == p.fog && fogQuality == p.fogQuality
                && screenGodRays == p.screenGodRays && godRayQuality == p.godRayQuality
                && cloudShadows == p.cloudShadows && Mathf.Approximately(lightShafts, p.lightShafts) && characterGlow == p.characterGlow
                && wetFloor == p.wetFloor && water == p.water && indirect == p.indirect
                && depthOfField == p.depthOfField && bloom == p.bloom && bloomHighQuality == p.bloomHighQuality
                && shadows == p.shadows;
        }

        internal void Clamp()
        {
            fogQuality = Mathf.Clamp(fogQuality, 0, 2);
            godRayQuality = Mathf.Clamp(godRayQuality, 0, 2);
            lightShafts = Mathf.Clamp01(lightShafts);
            wetFloor = Mathf.Clamp(wetFloor, 0, 2);
            water = Mathf.Clamp(water, 0, 2);
            indirect = Mathf.Clamp(indirect, 0, 2);
            shadows = Mathf.Clamp(shadows, 0, 2);
            brightness = Mathf.Clamp(brightness, -1f, 1f);
            if (width < 0) width = 0;
            if (height < 0) height = 0;
            if (targetFps == 0 || targetFps < -1) targetFps = -1;
        }
    }

    public static class RSGraphics
    {
        const string PrefsKey = "RS.GraphicsOptions.v1";

        static RSGraphicsOptions options;
        static RSGraphicsOptions best;

        /// <summary>바뀌면 (적용 · 단계 변경 · 불러오기)</summary>
        public static event Action Changed;

        static int revision;
        /// <summary>바뀔 때마다 1 씩 오르는 번호 (매 프레임 비교용)</summary>
        public static int Revision { get { return revision; } }

        /// <summary>플레이어가 고른 값 (저장되는 값). 직접 고쳤으면 NotifyChanged() 를 부른다</summary>
        public static RSGraphicsOptions Options
        {
            get { if (options == null) Load(); return options; }
        }

        /// <summary>
        /// 컴포넌트가 읽는 값. 플레이 중 = Options, 에디터 편집 중 = 항상 '높음'
        /// (아트 작업 화면이 저장된 플레이어 설정에 흔들리지 않게)
        /// </summary>
        public static RSGraphicsOptions Effective
        {
            get
            {
                if (Application.isPlaying) return Options;
                if (best == null) best = Preset(RSGraphicsLevel.높음);
                return best;
            }
        }

        /// <summary>새 값으로 바꾸고 화면에 반영 (+ 저장)</summary>
        public static void Set(RSGraphicsOptions o, bool save = true)
        {
            if (o == null) return;
            options = o.Clone();
            options.Clamp();
            options.level = Detect(options);
            Raise();
            if (save) Save();
        }

        /// <summary>품질 단계 버튼. 품질 항목만 바뀐다 (밝기 · 해상도는 그대로)</summary>
        public static void SetLevel(RSGraphicsLevel l, bool save = true)
        {
            var o = Options.Clone();
            o.ApplyLevel(l);
            Set(o, save);
        }

        /// <summary>Options 를 직접 고친 뒤 부른다</summary>
        public static void NotifyChanged(bool save = true)
        {
            Set(Options, save);
        }

        /// <summary>처음 실행 상태로 (권장 단계 + 화면 기본값)</summary>
        public static void ResetToDefault(bool save = true)
        {
            var o = new RSGraphicsOptions();
            o.ApplyLevel(Recommended());
            Set(o, save);
        }

        public static void Save()
        {
            if (options == null) return;
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(options));
            PlayerPrefs.Save();
        }

        /// <summary>저장된 값을 다시 읽는다 (없으면 권장 단계)</summary>
        public static void Load()
        {
            RSGraphicsOptions o = null;
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { o = JsonUtility.FromJson<RSGraphicsOptions>(json); }
                catch (Exception e) { Debug.LogWarning("[그래픽 설정] 저장된 값을 읽지 못해 기본값을 씁니다: " + e.Message); }
            }
            if (o == null)
            {
                o = new RSGraphicsOptions();
                o.ApplyLevel(Recommended());
            }
            o.Clamp();
            o.level = Detect(o);
            options = o;
            Raise();
        }

        static void Raise()
        {
            revision++;
            var h = Changed;
            if (h != null) h();
        }

        // ─────────────────────────────────────────────────────────────
        // 프리셋 에셋 (SYS/Presets/Graphics/Resources/RS_GraphicsPresets.asset)
        // ─────────────────────────────────────────────────────────────

        static RSGraphicsPresets presets;
        static bool presetsFallback;

        /// <summary>단계 값 · 품질 숫자 에셋. 없으면 코드 기본값으로 만든 임시 것 (저장 안 됨)</summary>
        public static RSGraphicsPresets Presets
        {
            get
            {
                if (presets == null)
                {
                    presets = Resources.Load<RSGraphicsPresets>(RSGraphicsPresets.ResourceName);
                    presetsFallback = presets == null;
                    if (presetsFallback)
                    {
                        presets = ScriptableObject.CreateInstance<RSGraphicsPresets>();
                        presets.name = "RS_GraphicsPresets (기본값 · 에셋 없음)";
                        presets.hideFlags = HideFlags.DontSave;
                    }
                }
                return presets;
            }
        }

        /// <summary>프리셋 에셋 없이 코드 기본값으로 동작 중인지</summary>
        public static bool UsingFallbackPresets { get { var p = Presets; return presetsFallback; } }

        /// <summary>프리셋 에셋을 다시 찾는다 (에디터가 에셋을 새로 만든 뒤)</summary>
        public static void ReloadPresets()
        {
            if (presetsFallback && presets != null) UnityEngine.Object.DestroyImmediate(presets);
            presets = null;
            PresetsEdited(null);
        }

        /// <summary>프리셋 에셋 값이 바뀌었을 때 (에셋의 OnValidate 가 부른다)</summary>
        internal static void PresetsEdited(RSGraphicsPresets p)
        {
            if (p != null && (p.hideFlags & HideFlags.DontSave) == 0)
            {
                if (presetsFallback && presets != null && presets != p) UnityEngine.Object.DestroyImmediate(presets);
                presets = p;
                presetsFallback = false;
            }
            best = null;
            if (options == null) { Raise(); return; }
            // 지금 낮음 · 중간 · 높음 중 하나면 그 단계의 새 값으로 다시 맞춘다 ('사용자' 는 그대로)
            var lv = options.level;
            if (lv != RSGraphicsLevel.사용자)
            {
                var o = options.Clone();
                o.ApplyLevel(lv);
                o.Clamp();
                o.level = lv;
                options = o;
                Save();
            }
            Raise();
        }

        // ─────────────────────────────────────────────────────────────
        // 품질 단계
        // ─────────────────────────────────────────────────────────────

        /// <summary>그 단계의 품질 값 (프리셋 에셋에서). 밝기 · 화면 값은 기본값</summary>
        public static RSGraphicsOptions Preset(RSGraphicsLevel l)
        {
            if (l == RSGraphicsLevel.사용자) l = RSGraphicsLevel.높음;
            var o = new RSGraphicsOptions();
            var v = Presets.Get(l);
            if (v != null) v.CopyTo(o);
            o.level = l;
            return o;
        }

        /// <summary>지금 값이 어느 단계와 같은지 (아무 것과도 다르면 '사용자')</summary>
        public static RSGraphicsLevel Detect(RSGraphicsOptions o)
        {
            for (int i = 0; i <= 2; i++)
                if (o.SameQuality(Preset((RSGraphicsLevel)i))) return (RSGraphicsLevel)i;
            return RSGraphicsLevel.사용자;
        }

        /// <summary>처음 실행할 때 고를 단계 (그래픽 메모리로 대충 가늠, 기준은 프리셋 에셋)</summary>
        public static RSGraphicsLevel Recommended()
        {
            int mb = SystemInfo.graphicsMemorySize;
            var p = Presets;
            if (mb > 0 && mb < p.recommendLowBelowMB) return RSGraphicsLevel.낮음;
            if (mb > 0 && mb < p.recommendMidBelowMB) return RSGraphicsLevel.중간;
            return RSGraphicsLevel.높음;
        }

        // ─────────────────────────────────────────────────────────────
        // 품질 단계 → 실제 숫자 (프리셋 에셋 아래쪽 '품질 숫자')
        // ─────────────────────────────────────────────────────────────

        /// <summary>안개 걸음 간격 배율 (클수록 가볍다)</summary>
        public static float FogStepLengthScale(int q) { return Mathf.Max(0.1f, Presets.fogStepLengthScale.Get(q)); }
        /// <summary>안개 걸음 수 상한 (컴포넌트 값과 작은 쪽). 0 = 제한 없음</summary>
        public static int FogMaxSteps(int q) { int v = Presets.fogMaxSteps.Get(q); return v <= 0 ? int.MaxValue : v; }
        /// <summary>안개를 밝히는 포인트 · 스폿 라이트 최대 수</summary>
        public static int FogMaxLights(int q) { return Mathf.Clamp(Presets.fogMaxLights.Get(q), 0, 8); }
        /// <summary>화면 갓레이 샘플 상한. 0 = 제한 없음</summary>
        public static int GodRaySamples(int q) { int v = Presets.godRaySamples.Get(q); return v <= 0 ? int.MaxValue : v; }
        /// <summary>젖은 바닥 '화면 반사'(1) 일 때 반사 걸음 · 거리 상한</summary>
        public static int WetMidSteps { get { return Mathf.Max(4, Presets.wetMidSteps); } }
        public static float WetMidDistance { get { return Mathf.Max(1f, Presets.wetMidDistance); } }
        /// <summary>간접광 칸 크기 배율 (1 = 거칠게)</summary>
        public static float IndirectCellScale(int q) { return q == 1 ? Mathf.Max(1f, Presets.indirectCoarseScale) : 1f; }
        /// <summary>그림자 거리 배율</summary>
        public static float ShadowDistanceScale(int q) { return Mathf.Max(0f, Presets.shadowDistanceScale.Get(q)); }

        // ─────────────────────────────────────────────────────────────
        // 설정창용 도우미
        // ─────────────────────────────────────────────────────────────

        /// <summary>고를 수 있는 해상도 (가로 × 세로, 주사율 무시, 중복 없음, 작은 것부터)</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (!list.Contains(v)) list.Add(v);
            }
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }
    }
}
