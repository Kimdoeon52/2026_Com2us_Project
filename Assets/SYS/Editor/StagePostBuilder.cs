// RE:AL STEEL - HD-2D 룩 세팅 (카메라 / 조명 / 포스트 프로세싱)
//
// StageSampleBuilder 와 의존 관계가 없는 독립 클래스다.
// 지오메트리 생성과 룩 세팅을 따로 실행할 수 있게 일부러 분리해 두었다.
//
// 메뉴: Tools > RE_AL STEEL > Stage > 5. 카메라 + 조명 + 포스트
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealSteel.EditorTools
{
    public static class StagePostBuilder
    {
        /// <summary>
        /// 출력 폴더는 StageSampleBuilder 와 공유한다 —
        /// 메뉴 `0. 출력 폴더 지정` 에서 한 번 고르면 둘 다 그쪽에 쓴다.
        /// </summary>
        static string ArtDir { get { return StageSampleBuilder.ArtDir; } }
        static string ProfilePath { get { return ArtDir + "/VP_Stage_HD2D.asset"; } }

        const string RigName = "STAGE_Look";

        // 카메라 - 원근을 거의 없앤 망원 앵글이 "미니어처" 인상을 만든다
        const float Fov = 24f;      // 낮을수록 디오라마처럼 보임
        const float Pitch = 34f;    // 내려다보는 각도
        const float Distance = 30f; // FOV 를 낮춘 만큼 뒤로 뺀다

        // 깊이 버퍼 정밀도 = near/far 비율이 좌우한다. 좁을수록 좋다.
        const float NearClip = 2f;
        const float FarClip = 90f;

        static readonly Vector3 LookTarget = new Vector3(1.5f, 1.5f, 2f);

        [MenuItem("Tools/RE_AL STEEL/Stage/5. 카메라 + 조명 + 포스트 (HD-2D 룩)", false, 20)]
        public static void BuildLook()
        {
            var old = GameObject.Find(RigName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Build Stage Look");

            BuildCamera(rig.transform);
            BuildLights(rig.transform);
            BuildVolume(rig.transform);
            BuildAmbient();

            Selection.activeGameObject = rig;
            Debug.Log(
                "[Stage] HD-2D 룩 세팅 완료.\n" +
                "· Bloom 이 안 보이면 URP Asset 의 HDR 이 켜져 있는지 확인하세요.\n" +
                "· DOF 초점거리는 " + Distance + " 로 맞춰 두었습니다. 로봇이 흐려지면 focusDistance 를 조정하세요.\n" +
                "· 픽셀아트를 넣은 뒤에는 Anti-aliasing 을 끄는 편이 선명합니다.");
        }

        /// <summary>
        /// 씬의 모든 카메라에 near/far 를 다시 잡아준다.
        /// 면이 깜빡일 때(Z-fighting) 지오메트리를 건드리기 전에 먼저 눌러볼 레버.
        /// </summary>
        [MenuItem("Tools/RE_AL STEEL/Stage/Z-fighting 완화 - 카메라 클립 거리 최적화", false, 42)]
        public static void FixClipPlanes()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cams.Length == 0) { Debug.LogWarning("[Stage] 씬에 카메라가 없습니다."); return; }

            Undo.RecordObjects(cams, "Fix Clip Planes");
            foreach (var c in cams)
            {
                float before = c.farClipPlane / Mathf.Max(c.nearClipPlane, 0.0001f);
                c.nearClipPlane = NearClip;
                c.farClipPlane = FarClip;
                float after = FarClip / NearClip;
                Debug.Log("[Stage] " + c.name + " : near/far 비율 " +
                          Mathf.RoundToInt(before) + " → " + Mathf.RoundToInt(after) +
                          " (깊이 정밀도 약 " + (before / after).ToString("0.#") + "배 향상)");
            }
        }

        // ─────────────────────────────────────────────────────────────

        static void BuildCamera(Transform parent)
        {
            var go = new GameObject("CAM_Stage");
            go.transform.SetParent(parent, false);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;          // 직교는 납작해서 HD-2D 느낌이 안 난다
            cam.fieldOfView = Fov;

            // Z-fighting 은 near/far 비율에 가장 민감하다. 깊이 버퍼 정밀도는
            // near 평면 근처에 몰려 있어서, near 를 올리는 것만으로 정밀도가 배로 좋아진다.
            // 이 카메라는 30 유닛 밖에서 보므로 near 를 2 까지 올려도 잘리는 게 없다.
            cam.nearClipPlane = NearClip;      // 0.3 → 2.0  (정밀도 약 7배)
            cam.farClipPlane = FarClip;        // 200 → 90   (추가로 2배)
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);

            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            go.transform.rotation = rot;
            go.transform.position = LookTarget - rot * Vector3.forward * Distance;

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;   // 픽셀아트는 AA 를 끄는 게 선명하다
            }

            // 씬에 리스너가 없을 때만 추가 (중복 경고 방지)
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                go.AddComponent<AudioListener>();

            go.tag = "MainCamera";
        }

        static void BuildLights(Transform parent)
        {
            // 키 라이트 - 각도를 기울여 벽면끼리 밝기 차이가 나게 한다
            var key = NewLight(parent, "LIGHT_Key_Sun", LightType.Directional);
            key.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            key.color = new Color(1.00f, 0.95f, 0.84f);
            key.intensity = 1.15f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;

            // 필 라이트 - 그림자 쪽이 완전히 죽지 않게
            var fill = NewLight(parent, "LIGHT_Fill", LightType.Directional);
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            fill.color = new Color(0.55f, 0.66f, 0.85f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;

            // 아래 점광 둘은 생성기 샘플 씬(간판 · 수로) 위치에 맞춰 둔 것이다.
            // 다른 씬에 쓸 때는 위치만 옮기면 된다.

            // 간판 네온 - Emission + Bloom 과 함께 분위기를 만드는 국소 광원
            var neon = NewLight(parent, "LIGHT_Neon_Sign", LightType.Point);
            neon.transform.position = new Vector3(2.4f, 4.2f, -3.7f);   // 간판 바로 앞
            neon.color = new Color(1.00f, 0.45f, 0.28f);
            neon.intensity = 4.0f;
            neon.range = 9f;
            neon.shadows = LightShadows.None;

            // 수로 아래 - 차가운 반사광
            var canal = NewLight(parent, "LIGHT_Canal", LightType.Point);
            canal.transform.position = new Vector3(0f, -1.2f, 3.5f);
            canal.color = new Color(0.40f, 0.72f, 0.85f);
            canal.intensity = 3.0f;
            canal.range = 10f;
            canal.shadows = LightShadows.None;
        }

        static Light NewLight(Transform parent, string name, LightType type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            return l;
        }

        static void BuildVolume(Transform parent)
        {
            var profile = CreateProfile();

            var go = new GameObject("VOLUME_Stage");
            go.transform.SetParent(parent, false);

            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 0f;
            v.sharedProfile = profile;
        }

        static VolumeProfile CreateProfile()
        {
            EnsureFolders();

            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (existing != null) AssetDatabase.DeleteAsset(ProfilePath);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            // ── 틸트시프트 느낌의 피사계 심도 ──────────────────────
            // HD-2D 의 "미니어처" 인상은 사실상 이 항목이 절반을 담당한다.
            // 배경만 흐리고 주인공은 초점 안에 둔다.
            var dof = Add<DepthOfField>(profile);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(Distance);
            dof.focalLength.Override(75f);
            dof.aperture.Override(4.5f);
            dof.bladeCount.Override(6);

            // ── 블룸 ────────────────────────────────────────────────
            // Emission 을 넣은 간판/창문/로봇 아이라이트가 이걸로 살아난다.
            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);

            // ── 톤 / 색 ────────────────────────────────────────────
            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0.1f);
            color.contrast.Override(14f);
            color.saturation.Override(16f);

            var vig = Add<Vignette>(profile);
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static void BuildAmbient()
        {
            // 앰비언트가 지역 분위기색을 지배한다. 텍스처에 그려 넣지 말고 여기서 잡는다.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor    = new Color(0.32f, 0.38f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.25f, 0.29f);
            RenderSettings.ambientGroundColor  = new Color(0.12f, 0.11f, 0.12f);
            RenderSettings.fog = false;
        }

        static void EnsureFolders()
        {
            EnsureFolderPath(ArtDir);
        }

        /// <summary>"Assets/a/b/c" 처럼 몇 단계든 한 번에 만든다.</summary>
        static void EnsureFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
