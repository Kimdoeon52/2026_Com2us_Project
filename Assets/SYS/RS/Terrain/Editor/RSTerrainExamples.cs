// RE:AL STEEL - RS Terrain 예제: 폐철장 40x40
//
// 예전 폐철장 생성기(StageJunkyardTerrainBuilder, 삭제됨)가 만들던 지형을 "요소 배치" 로 다시 짠 것.
// 만든 뒤에는 요소를 옮기고, 지우고, 복제해서 자유롭게 고치면 된다.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Terrain.EditorTools
{
    public static class RSTerrainExamples
    {
        const string GroupName = "예제_폐철장";

        public static void BuildJunkyard(RSTerrain t)
        {
            var old = t.transform.Find(GroupName);
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("예제 다시 만들기", "'" + GroupName + "' 그룹을 지우고 다시 만듭니다.", "다시 만들기", "취소")) return;
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            Undo.RecordObject(t, "예제 폐철장");
            t.size = new Vector2(40f, 40f);
            t.cellSize = 0.25f;

            var g = new GameObject(GroupName);
            Undo.RegisterCreatedObjectUndo(g, "예제 폐철장");
            g.transform.SetParent(t.transform, false);
            int salt = 100;

            // ── 뒤 성토 둔덕 2단 ──
            var b1 = Add<RSPlateau>(g, "둔덕_1단", 0f, 0f, 22f, ref salt);
            b1.size = new Vector2(122f, 26f); b1.cornerRound = 1f; b1.height = 2.4f; b1.cliffWidth = 0.6f;
            b1.terraces = 3; b1.terraceStrength = 0.35f; b1.rim = 0.14f; b1.edgeWobble = 0.8f; b1.wobbleFrequency = 0.3f; b1.topNoise = 0.22f;
            var b2 = Add<RSPlateau>(g, "둔덕_2단", 0f, 0f, 24f, ref salt);
            b2.size = new Vector2(122f, 18f); b2.cornerRound = 1f; b2.height = 1.8f; b2.cliffWidth = 0.6f;
            b2.terraces = 3; b2.terraceStrength = 0.35f; b2.rim = 0.14f; b2.edgeWobble = 0.8f; b2.wobbleFrequency = 0.39f; b2.topNoise = 0.22f;
            b2.order = 1;

            // ── 앞마당 낮은 2단 둔덕 ──
            Mesa(g, "낮은둔덕_오른쪽_위",  15.2f,  -7.6f, 8f, 6f, 1f, 0.9f, 0.35f, ref salt);
            Mesa(g, "낮은둔덕_오른쪽_아래", 11.4f,  -8.0f, 6.2f, 4.8f, 0.9f, 0.45f, 0.30f, ref salt);
            Mesa(g, "낮은둔덕_왼쪽_위",   -14.0f, -14.0f, 9.4f, 7.4f, 1.2f, 1.0f, 0.35f, ref salt);
            Mesa(g, "낮은둔덕_왼쪽_아래", -10.2f, -15.2f, 5.8f, 5.0f, 0.9f, 0.5f, 0.30f, ref salt);

            // ── 고물 더미 ──
            float[,] heaps =
            {
                {   6.0f, 18.2f, 3.0f, 2.0f }, { -16.0f, 18.5f, 2.0f, 1.2f },
                { -15.0f, 12.2f, 2.6f, 1.7f }, {  13.0f, 12.2f, 2.2f, 1.3f },
                {  13.0f,  5.6f, 2.6f, 2.1f }, {  -3.8f,  6.4f, 1.6f, 1.1f },
                { -15.0f, -7.6f, 2.4f, 1.5f }, {   3.0f, -17.5f, 2.2f, 1.4f },
                {  17.2f, -18.2f, 1.8f, 0.9f }, { -11.0f, -19.0f, 1.8f, 1.0f },
                {  -7.0f, -5.8f, 1.5f, 1.0f }, {   7.4f, -4.6f, 1.4f, 0.9f },
            };
            for (int i = 0; i < heaps.GetLength(0); i++)
            {
                var h = Add<RSHeap>(g, "더미_" + (i + 1), heaps[i, 0], 0f, heaps[i, 1], ref salt);
                h.radius = heaps[i, 2]; h.height = heaps[i, 3];
            }

            // ── 웅덩이 ──
            float[,] pits =
            {
                {  5.0f,  -8.0f, 1.4f, 0.45f, 0.12f }, { -6.5f, -11.0f, 1.0f, 0.32f, 0.08f },
                {  9.0f, -17.0f, 1.2f, 0.40f, 0.10f }, {  1.0f,   7.2f, 0.9f, 0.28f, 0.08f },
                {  2.0f,  14.0f, 0.9f, 0.30f, 0.08f }, { -1.0f,  18.0f, 1.2f, 0.38f, 0.10f },
            };
            for (int i = 0; i < pits.GetLength(0); i++)
            {
                var p = Add<RSPit>(g, "웅덩이_" + (i + 1), pits[i, 0], 0f, pits[i, 1], ref salt);
                p.radius = pits[i, 2]; p.depth = pits[i, 3]; p.rim = pits[i, 4];
            }

            // ── 깨진 콘크리트 작업장 ──
            var pad1 = Add<RSPad>(g, "작업장_뒤", -11f, 0.28f, 5.8f, ref salt);
            pad1.size = new Vector2(8.8f, 4.4f);
            var pad2 = Add<RSPad>(g, "작업장_앞", 11.5f, 0.24f, -13.8f, ref salt);
            pad2.size = new Vector2(7.2f, 4.8f);

            // ── 다리 받침 (반듯하게, 정점 고정) ──
            Abutment(g, "다리받침_앞", -3.0f, ref salt);
            Abutment(g, "다리받침_뒤",  1.0f, ref salt);

            // ── 길 (바퀴 자국) ──
            Path(g, "길_앞", ref salt, new[] { V(-5.5f, -21f), V(-4.5f, -16f), V(-2f, -11f), V(-0.4f, -7f), V(0f, -4.8f), V(0f, -3.2f) });
            Path(g, "길_곁", ref salt, new[] { V(-2f, -11f), V(3f, -13.2f), V(8.5f, -13.6f) });
            Path(g, "길_뒤", ref salt, new[] { V(0f, 0.6f), V(0.6f, 2.4f), V(2.8f, 3.8f), V(5f, 5f), V(5f, 10.4f), V(1f, 11.8f),
                                               V(-6f, 11.4f), V(-9f, 11.4f), V(-9f, 16.2f), V(-7f, 18.5f), V(-5f, 21f) });

            // ── 경사로 (점 높이로 평탄화) ──
            Ramp(g, "경사로_1단", ref salt, new Vector3(5f, 0f, 4.8f), new Vector3(5f, 2.4f, 10.2f), 1.15f);
            Ramp(g, "경사로_2단", ref salt, new Vector3(-9f, 2.4f, 11.2f), new Vector3(-9f, 4.2f, 16f), 1.1f);

            // ── 배수로: 양쪽 흙 배수로 + 다리 밑 콘크리트 수로 ──
            Trench(g, "배수로_왼쪽", ref salt, MeanderPoints(-21f, -4.3f));
            Trench(g, "배수로_오른쪽", ref salt, MeanderPoints(4.3f, 21f));
            var cul = Add<RSSpline>(g, "수로_다리밑", 0f, 0f, 0f, ref salt);
            cul.kind = RSSpline.Kind.Trench; cul.order = 51; cul.smooth = false;
            cul.points = new List<Vector3> { new Vector3(-3.4f, 0f, -1f), new Vector3(3.4f, 0f, -1f) };
            cul.bottomHalfWidth = 0.75f; cul.bankWidth = 0.5f; cul.depth = 1.7f; cul.wallSteepness = 3f;
            cul.bankErode = 0f; cul.floorNoise = 0.05f; cul.wallPaint = RSLayer.G; cul.floorPaint = RSLayer.None;
            cul.lockVertices = true; cul.water = true;

            // ── 폐자재 (지형 전체에 흩뿌림) ──
            var sc = Add<RSScatter>(g, "폐자재_전체", 0f, 0f, 0f, ref salt);
            sc.area = RSScatter.Area.Whole;
            sc.slabs = 16; sc.plates = 14; sc.pipes = 5; sc.tires = 12; sc.drums = 5; sc.platesOnScrap = 0.45f;
            RSTerrainMenu.FillScatterMaterials(sc);

            if (t.material == null) t.material = RSTerrainMenu.FindOrCreateSplatMaterial();
            if (t.waterMaterial == null) t.waterMaterial = RSTerrainMenu.FindMaterial("MAT_Stage_Water");

            t.MarkDirty();
            t.Rebuild();
            Selection.activeGameObject = t.gameObject;
            SceneView.RepaintAll();
            Debug.Log("[RS Terrain] 예제 폐철장 생성. 요소는 '" + GroupName + "' 아래 — 옮기고 지우고 복제해서 고치세요.\n" +
                      "다리 받침 윗면 y=0, 받침 사이 2.5 (다리 길이 4 이상, 폭 4 이하).");
        }

        static Vector3 V(float x, float z) { return new Vector3(x, 0f, z); }

        static List<Vector3> MeanderPoints(float x0, float x1)
        {
            var l = new List<Vector3>();
            for (float x = x0; x <= x1 + 0.01f; x += 1.5f)
            {
                float mm = Mathf.Clamp01((Mathf.Abs(x) - 2.5f) / 3.5f);
                mm = mm * mm * (3f - 2f * mm);
                float z = -1f + mm * 1.6f * (0.75f * Mathf.Sin(0.26f * x + 1.1f) + 0.25f * Mathf.Sin(0.702f * x + 4.2f));
                l.Add(new Vector3(x, 0f, z));
            }
            return l;
        }

        static T Add<T>(GameObject g, string name, float x, float y, float z, ref int salt) where T : RSTerrainFeature
        {
            var go = new GameObject(name);
            go.transform.SetParent(g.transform, false);
            go.transform.localPosition = new Vector3(x, y, z);
            var f = go.AddComponent<T>();
            f.salt = ++salt * 7919;
            f.order = f.DefaultOrder;
            return f;
        }

        static void Mesa(GameObject g, string name, float x, float z, float sx, float sz, float round, float h, float cliff, ref int salt)
        {
            var m = Add<RSPlateau>(g, name, x, 0f, z, ref salt);
            m.size = new Vector2(sx, sz); m.cornerRound = round; m.height = h; m.cliffWidth = cliff;
            m.terraces = 0; m.rim = 0.08f; m.edgeWobble = 0.56f; m.wobbleFrequency = 0.3f; m.topNoise = 0f;
            m.order = 2;
        }

        static void Abutment(GameObject g, string name, float z, ref int salt)
        {
            var p = Add<RSPad>(g, name, 0f, 0f, z, ref salt);
            p.size = new Vector2(4f, 1.5f); p.cornerRound = 0f; p.edgeFeather = 0.15f; p.raggedEdge = 0f;
            p.scatter = 0f; p.lockVertices = true; p.brokenSlabs = false; p.paint = RSLayer.G;
            p.order = 45;   // 길(40) 뒤, 배수로(50) 앞 — 길 칠을 덮고, 수로가 모서리를 깎는다
        }

        static void Path(GameObject g, string name, ref int salt, Vector3[] pts)
        {
            var s = Add<RSSpline>(g, name, 0f, 0f, 0f, ref salt);
            s.kind = RSSpline.Kind.Path; s.order = 40;
            s.points = new List<Vector3>(pts);
        }

        static void Ramp(GameObject g, string name, ref int salt, Vector3 a, Vector3 b, float halfW)
        {
            var s = Add<RSSpline>(g, name, 0f, 0f, 0f, ref salt);
            s.kind = RSSpline.Kind.Path; s.order = 39; s.smooth = false;
            s.heightMode = RSSpline.PathHeight.Points;
            s.halfWidth = halfW; s.feather = 0.3f; s.ruts = false; s.pathPaint = RSLayer.None;
            s.points = new List<Vector3> { a, b };
        }

        static void Trench(GameObject g, string name, ref int salt, List<Vector3> pts)
        {
            var s = Add<RSSpline>(g, name, 0f, 0f, 0f, ref salt);
            s.kind = RSSpline.Kind.Trench; s.order = 50;
            s.points = pts;
            s.bottomHalfWidth = 0.75f; s.bankWidth = 1.15f; s.depth = 1.7f; s.bankErode = 0.35f;
            s.floorPaint = RSLayer.A; s.water = true;
        }
    }
}
