// RE:AL STEEL - 먼 배경 카드 (고정 평면 그림)
//
// 3D 맵에서 멀리 있는 곳을 처리하듯, 건물군 · 스카이라인 · 랜드마크 그림을 평면 한 장씩 맵 뒤 정해진 자리에 세워 둔다.
//  · 그 자리에 고정이다 (기본). 여러 장을 서로 다른 거리에 세우면 카메라가 움직일 때 실제 거리 차이로 겹쳐 흐른다
//  · 조명을 받지 않는다 (Unlit). 대신 시간대(RS 시간대)에 따라 색조가 바뀌고, 밤에는 '불빛' 그림(창 · 가로등)이 켜진다
//  · 그림의 투명한 부분은 오려낸다 (하늘이 뚫린 건물 실루엣 그림을 그대로 쓴다)
//  · 기준점은 아래 가운데 (땅에 세운다). 위치 · 회전 · 크기는 보통 오브젝트처럼 직접 옮겨 맞춘다
//  · 메시 · 머티리얼은 자동으로 만들고 저장하지 않는다. 카메라의 Far 보다 가까이 둬야 보인다
// 만들기: GameObject → RE_AL STEEL → 먼 배경 카드
using UnityEngine;
using UnityEngine.Rendering;
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("먼 배경 카드 (고정 평면 그림)", "맵 뒤 정해진 자리에 세워 두는 평면 그림 한 장 (건물군 · 스카이라인 · 랜드마크).\n" +
        "· 그 자리에 고정. 위치 · 회전은 이 오브젝트를 직접 옮겨 맞춘다 (기준점 = 아래 가운데)\n" +
        "· 조명을 받지 않고, 시간대에 따라 색조가 바뀐다. 밤에는 불빛 그림이 켜진다\n" +
        "· 여러 장을 서로 다른 거리에 세우면 카메라가 움직일 때 자연스럽게 겹쳐 흐른다\n" +
        "· 안 보이면: 카메라 Far 보다 멀리 있거나, 지형 · 건물에 가려진 것")]
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [AddComponentMenu("RE_AL STEEL/Lighting/먼 배경 카드 (고정 평면 그림)")]
    public class RSBackdrop : MonoBehaviour
    {
        [RSGroup("그림", true)]
        [RSKey, Tooltip("배경 그림")]
        public Texture2D image;
        [Tooltip("밤에 켜지는 불빛 그림 (같은 크기, 불빛만 있고 나머지는 투명). 비워도 된다")]
        public Texture2D lights;
        [RSKey, Min(1f), Tooltip("가로 크기 (m). 세로는 그림 비율대로")]
        public float width = 60f;
        [Tooltip("기준점을 그림 아래 가운데로 (땅에 세울 때). 끄면 그림 한가운데")]
        public bool pivotBottom = true;

        [RSGroup("카메라 따라가기 (보통은 끔 — 고정 배치)")]
        [Tooltip("기준 카메라. 비우면 Main Camera")]
        public Camera cam;
        [Tooltip("카메라가 보는 방향에 수직으로 눕힌다. 끄면 놓은 그대로 서 있다 (기본)")]
        public bool faceCamera = false;
        [Range(0f, 1f), Tooltip("0 = 제자리에 고정 (기본). 올리면 카메라가 움직일 때 그만큼 따라와 더 멀리 있는 것처럼 보인다")]
        public float parallax = 0f;

        [RSGroup("시간대")]
        [RSKey, Tooltip("RS 시간대의 시각을 따른다. 끄거나 씬에 시간대가 없으면 아래 고정 시각")]
        public bool followTimeOfDay = true;
        [Range(0f, 24f), Tooltip("고정 시각 (시)")]
        public float fixedHour = 12f;
        [Tooltip("시각별 색조 (왼쪽 끝 = 0시, 오른쪽 끝 = 24시)")]
        public Gradient tintByHour = DefaultTint();
        [Tooltip("추가로 곱하는 색 (전체를 어둡게 · 물들이기)")]
        public Color tint = Color.white;
        [Tooltip("시각별 불빛 세기 (가로 = 시, 세로 0 ~ 1)")]
        public AnimationCurve lightsByHour = DefaultLights();
        [Range(0f, 6f), Tooltip("불빛 밝기 (1 보다 크면 블룸에 번진다)")]
        public float lightsBrightness = 2.2f;

        [SerializeField, HideInInspector] Shader shader;   // 빌드에 셰이더가 빠지지 않게

        const string ShaderName = "Universal Render Pipeline/Unlit";
        const string LightsChild = "__RS_Backdrop 불빛 (자동 · 저장 안 됨)";
        static readonly int IdBaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

        Mesh mesh;
        Material mat, lightsMat;
        MeshRenderer mr, lightsMr;
        GameObject lightsGo;
        Vector2 builtSize;
        bool builtPivot;
        RSTimeOfDay tod;
        int todSearchFrame = -1;
        bool anchored;
        Vector3 anchorPos, anchorCam;

        static Gradient DefaultTint()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(0.16f, 0.2f, 0.36f), 0f),
                new GradientColorKey(new Color(0.2f, 0.24f, 0.42f), 4.5f / 24f),
                new GradientColorKey(new Color(1f, 0.74f, 0.62f), 6.2f / 24f),
                new GradientColorKey(Color.white, 8.5f / 24f),
                new GradientColorKey(Color.white, 16f / 24f),
                new GradientColorKey(new Color(1f, 0.68f, 0.5f), 18.6f / 24f),
                new GradientColorKey(new Color(0.3f, 0.3f, 0.5f), 20f / 24f),
                new GradientColorKey(new Color(0.16f, 0.2f, 0.36f), 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static AnimationCurve DefaultLights()
        {
            return new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(5.2f, 1f), new Keyframe(6.6f, 0f),
                                      new Keyframe(17.6f, 0f), new Keyframe(19.4f, 1f), new Keyframe(24f, 1f));
        }

        void Reset() { shader = Shader.Find(ShaderName); }

        void OnEnable()
        {
            if (shader == null) shader = Shader.Find(ShaderName);
            mr = GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            builtSize = Vector2.zero;
            anchored = false;
            Apply();
        }

        void OnDisable()
        {
            if (mr != null) mr.sharedMaterial = null;
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh == mesh) mf.sharedMesh = null;
            Kill(mesh); Kill(mat); Kill(lightsMat);
            mesh = null; mat = null; lightsMat = null;
            if (lightsGo != null) lightsGo.SetActive(false);
            lightsGo = null; lightsMr = null;
        }

        static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        void OnValidate() { width = Mathf.Max(1f, width); }

        void LateUpdate()
        {
            Apply();
            var c = cam != null ? cam : Camera.main;
            if (c == null) return;
            if (faceCamera && Quaternion.Angle(transform.rotation, c.transform.rotation) > 0.01f) transform.rotation = c.transform.rotation;
            if (!Application.isPlaying || parallax <= 0.001f) return;   // 기본은 고정
            // 시차: 플레이를 시작한 순간의 자리를 기준으로, 카메라가 움직인 만큼의 일부만 따라간다
            if (!anchored) { anchored = true; anchorPos = transform.position; anchorCam = c.transform.position; }
            transform.position = anchorPos + (c.transform.position - anchorCam) * parallax;
        }

        float Hour()
        {
            if (!followTimeOfDay) return fixedHour;
            if (tod == null && (Time.frameCount >= todSearchFrame || Time.frameCount < todSearchFrame - 200))
            {
                tod = FindAnyObjectByType<RSTimeOfDay>();
                todSearchFrame = Time.frameCount + 120;
            }
            return tod != null ? tod.Hour : fixedHour;
        }

        void Apply()
        {
            if (shader == null || mr == null) return;
            if (mat == null)
            {
                mat = new Material(shader) { name = "RS_Backdrop (자동)", hideFlags = HideFlags.DontSave };
                mat.SetFloat("_Cull", 0f);
                mat.SetFloat("_AlphaClip", 1f);              // 투명한 부분(하늘)은 오려낸다
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450;
                mr.sharedMaterial = mat;
            }
            float aspect = image != null && image.width > 0 ? (float)image.height / image.width : 0.5625f;
            var size = new Vector2(width, width * aspect);
            if (mesh == null || builtSize != size || builtPivot != pivotBottom) BuildMesh(size);

            float h = Mathf.Repeat(Hour(), 24f);
            Color c = (tintByHour != null ? tintByHour.Evaluate(h / 24f) : Color.white) * tint;
            c.a = 1f;
            mat.SetTexture(IdBaseMap, image);
            mat.SetColor(IdBaseColor, c);

            float k = lights != null && lightsByHour != null ? Mathf.Clamp01(lightsByHour.Evaluate(h)) : 0f;
            if (k > 0.01f)
            {
                EnsureLights();
                if (!lightsGo.activeSelf) lightsGo.SetActive(true);
                lightsMat.SetTexture(IdBaseMap, lights);
                Color lc = Color.white * (lightsBrightness * k);
                lc.a = 1f;
                lightsMat.SetColor(IdBaseColor, lc);
            }
            else if (lightsGo != null && lightsGo.activeSelf) lightsGo.SetActive(false);
        }

        void EnsureLights()
        {
            if (lightsGo == null)
            {
                var t = transform.Find(LightsChild);
                lightsGo = t != null ? t.gameObject : new GameObject(LightsChild);
                lightsGo.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                lightsGo.transform.SetParent(transform, false);
                lightsGo.transform.localPosition = new Vector3(0f, 0f, -0.2f);   // 그림보다 살짝 카메라 쪽
                lightsGo.transform.localRotation = Quaternion.identity;
                lightsGo.transform.localScale = Vector3.one;
                if (lightsGo.GetComponent<MeshFilter>() == null) lightsGo.AddComponent<MeshFilter>();
                lightsMr = lightsGo.GetComponent<MeshRenderer>();
                if (lightsMr == null) lightsMr = lightsGo.AddComponent<MeshRenderer>();
                lightsMr.shadowCastingMode = ShadowCastingMode.Off;
                lightsMr.receiveShadows = false;
            }
            if (lightsMat == null)
            {
                lightsMat = new Material(shader) { name = "RS_Backdrop 불빛 (자동)", hideFlags = HideFlags.DontSave };
                lightsMat.SetFloat("_Cull", 0f);
                lightsMat.SetFloat("_AlphaClip", 1f);        // 불빛 픽셀만 남기고 오려낸다
                lightsMat.SetFloat("_Cutoff", 0.5f);
                lightsMat.EnableKeyword("_ALPHATEST_ON");
                lightsMat.renderQueue = 2450;
            }
            if (lightsMr.sharedMaterial != lightsMat) lightsMr.sharedMaterial = lightsMat;
            var lmf = lightsGo.GetComponent<MeshFilter>();
            if (lmf.sharedMesh != mesh) lmf.sharedMesh = mesh;
        }

        void BuildMesh(Vector2 size)
        {
            if (mesh == null) mesh = new Mesh { name = "RS_Backdrop", hideFlags = HideFlags.DontSave };
            float hx = size.x * 0.5f;
            float y0 = pivotBottom ? 0f : -size.y * 0.5f, y1 = y0 + size.y;
            mesh.Clear();
            mesh.vertices = new[] { new Vector3(-hx, y0, 0f), new Vector3(hx, y0, 0f), new Vector3(hx, y1, 0f), new Vector3(-hx, y1, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            builtSize = size;
            builtPivot = pivotBottom;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, builtPivot ? builtSize.y * 0.5f : 0f, 0f), new Vector3(builtSize.x, builtSize.y, 0.01f));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
