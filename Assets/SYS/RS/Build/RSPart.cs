// RE:AL STEEL - 조립 부품 (RSPart)
//
// 조립품(RSAssembly) 아래에 놓는 부품 하나. 모양(상자 · 경사 · 기둥 · 계단 · 지붕 · 판)과 크기, 면마다 칠을 가진다.
// 부품 자체는 그리지 않는다 — 조립품이 모든 부품을 모아 재질별로 합친 메시 하나로 그린다.
//  · 기준점 = 바닥 가운데 (x · z 가운데, y = 0 이 바닥)
//  · 크기는 m. 씬 도구에서 끌어 그리면 1 픽셀(1/32 m) 또는 격자에 맞는다
//  · 칠: 부품 기본 칠 + 면마다 따로 칠 (반복 · 도안 칸 · 늘리기)
using System;
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Build
{
    public enum RSPartShape { 상자, 경사, 기둥, 계단, 지붕, 판 }
    public enum RSRoofKind { 박공, 외쪽, 평지붕 }

    /// <summary>면에 텍스처를 붙이는 방식</summary>
    public enum RSPaintMode
    {
        [InspectorName("반복 (재질을 1m=32px 로 타일)")] 반복,
        [InspectorName("도안 칸 (이 조립품 전용 그림을 픽셀 1:1)")] 도안칸,
        [InspectorName("늘리기 (테두리 유지 · 가운데 반복)")] 늘리기,
    }

    [Serializable]
    public class RSFacePaint
    {
        [Tooltip("어느 면 (-1 = 부품 기본)")]
        public int face = -1;
        public RSPaintMode mode = RSPaintMode.반복;
        [Tooltip("재질. 비우면 조립품 기본 재질. 도안 칸은 그림이 아직 없는 곳을 이 재질로 채운다")]
        public RSSurface surface;
        [Tooltip("텍스처를 픽셀 단위로 밀기 (가로 · 세로)")]
        public Vector2Int offset;
        [Range(0, 3), Tooltip("90° 단위 돌리기")]
        public int rotate;
        [Tooltip("좌우 뒤집기")]
        public bool flip;
        [Tooltip("늘리기: 테두리 픽셀 (왼 · 오른 · 아래 · 위). 이 픽셀은 늘어나지 않는다")]
        public Vector4 border = new Vector4(4, 4, 4, 4);

        public RSFacePaint Clone()
        {
            return new RSFacePaint { face = face, mode = mode, surface = surface, offset = offset, rotate = rotate, flip = flip, border = border };
        }

        public bool SameLook(RSFacePaint o)
        {
            return o != null && o.mode == mode && o.surface == surface && o.offset == offset && o.rotate == rotate && o.flip == flip && o.border == border;
        }
    }

    [RSSummary("조립 부품", "조립품 아래에 놓는 부품 하나 (상자 · 경사 · 기둥 · 계단 · 지붕 · 판).\n" +
        "· 기준점은 바닥 가운데. 크기는 m (1 픽셀 = 1/32 m)\n" +
        "· 칠은 씬 뷰의 조립 도구 '칠하기' 로 면을 클릭하는 게 빠르다. 여기서는 숫자로 고친다\n" +
        "· 복제(Ctrl+D) · 이동 · 회전은 유니티 기본 도구 그대로")]
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Build/조립 부품")]
    public class RSPart : MonoBehaviour
    {
        [RSGroup("모양", true)]
        [RSKey, Tooltip("부품 모양")]
        public RSPartShape shape = RSPartShape.상자;
        [RSKey, Tooltip("크기 가로(X) · 높이(Y) · 깊이(Z) (m). 1 픽셀 = 0.03125 m")]
        public Vector3 size = Vector3.one;
        [Range(3, 32), Tooltip("기둥: 면 수 (4 = 사각 기둥, 6 = 육각, 12 이상 = 둥근 기둥)")]
        public int sides = 8;
        [Range(0f, 2f), Tooltip("기둥: 위쪽 굵기 배율 (0 = 원뿔, 1 = 같은 굵기)")]
        public float topScale = 1f;
        [Range(1, 64), Tooltip("계단: 단 수 (+Z 쪽으로 올라간다)")]
        public int steps = 4;
        [Tooltip("지붕: 박공(ㅅ자, 용마루가 X 방향) · 외쪽(한쪽 경사, 뒤가 높음) · 평지붕(+난간)")]
        public RSRoofKind roof = RSRoofKind.박공;
        [Min(0f), Tooltip("지붕: 처마 (벽 밖으로 나오는 길이, m)")]
        public float overhang = 0.25f;
        [Min(0.03125f), Tooltip("지붕: 두께 (m) · 평지붕 난간 두께")]
        public float thickness = 0.125f;

        [RSGroup("칠", true)]
        [Tooltip("부품 기본 칠 (따로 칠하지 않은 면)")]
        public RSFacePaint paint = new RSFacePaint();
        [Tooltip("면마다 따로 칠한 것")]
        public List<RSFacePaint> facePaints = new List<RSFacePaint>();
        [Range(0f, 0.3f), Tooltip("부품마다 밝기를 조금씩 다르게 (같은 판자를 여러 개 놓을 때 덜 반복돼 보임)")]
        public float brightJitter = 0f;

        [SerializeField, HideInInspector] string id;

        /// <summary>도안 칸을 찾는 고유 번호 (복제하면 조립품이 새로 매긴다)</summary>
        public string Id { get { if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N").Substring(0, 12); return id; } }
        internal void NewId() { id = Guid.NewGuid().ToString("N").Substring(0, 12); }

        /// <summary>이 부품이 속한 조립품</summary>
        public RSAssembly Owner { get { return transform.parent != null ? transform.parent.GetComponentInParent<RSAssembly>(true) : null; } }

        /// <summary>그 면에 실제로 쓰는 칠 (따로 칠한 게 없으면 기본 칠)</summary>
        public RSFacePaint PaintFor(int face)
        {
            if (facePaints != null)
                foreach (var p in facePaints) if (p != null && p.face == face) return p;
            return paint;
        }

        /// <summary>면 칠을 바꾼다. face = -1 이면 기본 칠 (따로 칠한 것은 지운다)</summary>
        public void SetPaint(int face, RSFacePaint p)
        {
            if (facePaints == null) facePaints = new List<RSFacePaint>();
            if (face < 0)
            {
                paint = p.Clone(); paint.face = -1;
                facePaints.Clear();
            }
            else
            {
                facePaints.RemoveAll(x => x == null || x.face == face);
                if (!p.SameLook(paint)) { var c = p.Clone(); c.face = face; facePaints.Add(c); }
            }
            Touch();
        }

        /// <summary>면 이름 (인스펙터 · 도안 칸 이름)</summary>
        public string FaceName(int face)
        {
            var n = RSShapes.FaceNames(this);
            return face >= 0 && face < n.Length && n[face] != null ? n[face] : ("면 " + face);
        }

        public void Touch()
        {
            var a = Owner;
            if (a != null) a.MarkDirty();
        }

        void OnValidate()
        {
            size = new Vector3(Mathf.Max(RSAssembly.Pixel, size.x), Mathf.Max(RSAssembly.Pixel, size.y), Mathf.Max(RSAssembly.Pixel, size.z));
            if (shape == RSPartShape.판) size.z = Mathf.Max(RSAssembly.Pixel, size.z);
            sides = Mathf.Clamp(sides, 3, 32);
            steps = Mathf.Clamp(steps, 1, 64);
            Touch();
        }

        void OnEnable() { Touch(); }
        void OnDisable() { Touch(); }
        void OnTransformParentChanged() { Touch(); }
    }
}
