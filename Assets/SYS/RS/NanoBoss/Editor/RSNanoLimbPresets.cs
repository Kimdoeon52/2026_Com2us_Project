// RE:AL STEEL - 나노봇 리그 부위 프리셋
//
// 관절 · 두께 · 느낌 · 결속이 미리 잡힌 부위 묶음. 리그 인스펙터 '+ 부위 프리셋' 에서 고른다.
// 리그 안의 부위 · 관절을 고른 채 만들면 그 아래에 붙는다 (손가락을 손바닥에, 칼을 팔 끝에).
// 봇 수는 군집의 격자 간격에 맞게 자동으로 정한다.
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    public static class RSNanoLimbPresets
    {
        public static void Menu(RSNanoRig rig, Transform selected)
        {
            Transform parent = selected && (selected == rig.transform || selected.IsChildOf(rig.transform)) ? selected : rig.transform;
            var m = new GenericMenu();
            m.AddItem(new GUIContent("가시 — 뾰족한 줄기, 칼같이"), false, () => Done(Spike(parent, rig)));
            m.AddItem(new GUIContent("주먹 — 팔 + 주먹 + 마디, 단단"), false, () => Done(Fist(parent, rig)));
            m.AddItem(new GUIContent("손 — 손바닥 + 손가락 5"), false, () => Done(Hand(parent, rig)));
            m.AddItem(new GUIContent("칼날 — 길고 얇은 줄기, 끝 늦음 (잔상)"), false, () => Done(Blade(parent, rig)));
            m.AddItem(new GUIContent("촉수 — 휘는 줄기, 물렁"), false, () => Done(Tentacle(parent, rig)));
            m.AddItem(new GUIContent("날개 — 뼈대 4개가 부채꼴"), false, () => Done(Wing(parent, rig)));
            m.AddSeparator("");
            m.AddItem(new GUIContent("파도 — 바닥까지 채운 물마루, 물렁"), false, () => Done(Wave(parent, rig)));
            m.AddItem(new GUIContent("웅덩이 — 바닥 층, 유체"), false, () => Done(Puddle(parent, rig)));
            m.ShowAsContext();
        }

        static void Done(Component c) { if (c) Selection.activeGameObject = c.gameObject; RSNanoRigPreview.MarkDirty(); }

        static RSNanoSwarm Sw(RSNanoRig rig) => rig.swarm ? rig.swarm : rig.GetComponentInParent<RSNanoSwarm>();

        static Vector3 Dir(float deg, float len) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad), 0f) * len;

        /// <summary>관절마다 앞 관절 기준 위치</summary>
        public static void SetSteps(RSNanoLimb chain, params Vector3[] steps)
        {
            chain.RefreshJoints();
            var J = chain.Joints;
            for (int i = 0; i < steps.Length && i + 1 < J.Count; i++)
            {
                Undo.RecordObject(J[i + 1], "관절");
                J[i + 1].localPosition = steps[i];
            }
            chain.RefreshJoints();
        }

        static void Feel(RSNanoLimb l, int preset, float tipLag = -1f)
        {
            l.ApplyPreset(RSNanoLimb.Presets[preset]);
            if (tipLag >= 0f) l.tipLag = tipLag;
        }

        public static RSNanoLimb Spike(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var l = RSNanoRigEditor.AddChain(parent, "가시", 3, new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 0f, 0f), sw, new[] { 1f, 0.55f, 0.22f, 0.03f });
            l.radius = 0.5f;
            Feel(l, 3);
            l.gatherOrder = GatherOrder.Root;
            l.tint = new Color(1f, 0.75f, 0.65f);
            RSNanoRigEditor.AutoBots(l, sw);
            return l;
        }

        public static RSNanoLimb Fist(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var arm = RSNanoRigEditor.AddChain(parent, "팔", 3, new Vector3(0.6f, 0f, 0f), Vector3.zero, sw, new[] { 1f, 0.95f, 0.9f, 0.85f });
            arm.radius = 0.55f;
            Feel(arm, 1, 4f);
            RSNanoRigEditor.AutoBots(arm, sw);
            var tip = arm.Joints[arm.Joints.Count - 1];
            var fist = RSNanoRigEditor.AddLimb(tip, "주먹", LimbType.Blob, new Vector3(0.6f, 0f, 0f), sw);
            fist.size = new Vector3(1.2f, 1.0f, 0.7f);
            fist.wobble = 0.04f;
            Feel(fist, 2, 3f);
            RSNanoRigEditor.AutoBots(fist, sw);
            for (int k = 0; k < 4; k++)
            {
                var n = RSNanoRigEditor.AddLimb(fist.transform, "마디" + (k + 1), LimbType.Blob, new Vector3(1.0f, 0.68f - k * 0.45f, -0.25f), sw);
                n.size = new Vector3(0.32f, 0.26f, 0.3f);
                n.wobble = 0.02f;
                Feel(n, 2);
                RSNanoRigEditor.AutoBots(n, sw);
            }
            return arm;
        }

        public static RSNanoLimb Hand(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var palm = RSNanoRigEditor.AddLimb(parent, "손바닥", LimbType.Blob, new Vector3(1.5f, 0f, 0f), sw);
            palm.size = new Vector3(0.85f, 0.75f, 0.5f);
            Feel(palm, 1);
            RSNanoRigEditor.AutoBots(palm, sw);
            float[] ang = { 62f, 28f, -5f, -38f };
            for (int f = 0; f < 4; f++)
            {
                var finger = RSNanoRigEditor.AddChain(palm.transform, "손가락" + (f + 1), 3, Dir(ang[f], 0.42f), Dir(ang[f], 0.75f), sw, new[] { 0.36f, 0.32f, 0.26f, 0.16f });
                finger.radius = 0.5f;
                Feel(finger, 1, 4f);
                RSNanoRigEditor.AutoBots(finger, sw);
            }
            var thumb = RSNanoRigEditor.AddChain(palm.transform, "엄지", 2, Dir(125f, 0.4f), Dir(125f, 0.7f), sw, new[] { 0.42f, 0.34f, 0.2f });
            thumb.radius = 0.5f;
            Feel(thumb, 1, 3f);
            RSNanoRigEditor.AutoBots(thumb, sw);
            return palm;
        }

        public static RSNanoLimb Blade(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var l = RSNanoRigEditor.AddChain(parent, "칼날", 5, new Vector3(1.4f, 0f, 0f), new Vector3(1f, 0f, -0.3f), sw, new[] { 1f, 0.9f, 0.75f, 0.55f, 0.3f, 0.05f });
            l.radius = 0.32f;
            Feel(l, 3, 12f);
            RSNanoRigEditor.AutoBots(l, sw);
            return l;
        }

        public static RSNanoLimb Tentacle(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var l = RSNanoRigEditor.AddChain(parent, "촉수", 6, new Vector3(0.7f, 0.2f, 0f), new Vector3(1f, 0f, 0f), sw, new[] { 1f, 0.85f, 0.7f, 0.55f, 0.4f, 0.25f, 0.1f });
            SetSteps(l, Dir(15f, 0.7f), Dir(30f, 0.7f), Dir(45f, 0.65f), Dir(60f, 0.6f), Dir(80f, 0.55f), Dir(100f, 0.5f));
            l.radius = 0.42f;
            Feel(l, 0, 10f);
            l.flow = 0.12f;
            RSNanoRigEditor.AutoBots(l, sw);
            return l;
        }

        public static RSNanoLimb Wing(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            var root = RSNanoRigEditor.AddLimb(parent, "날개 뿌리", LimbType.Blob, new Vector3(-0.8f, 1.2f, 0.3f), sw);
            root.size = new Vector3(0.5f, 0.4f, 0.4f);
            Feel(root, 1);
            RSNanoRigEditor.AutoBots(root, sw);
            float[] ang = { 100f, 125f, 150f, 172f };
            for (int k = 0; k < ang.Length; k++)
            {
                var rib = RSNanoRigEditor.AddChain(root.transform, "날개뼈" + (k + 1), 3, Dir(ang[k], 0.85f - k * 0.08f), Vector3.zero, sw, new[] { 0.6f, 0.5f, 0.32f, 0.08f });
                rib.radius = 0.45f;
                Feel(rib, 1, 6f);
                RSNanoRigEditor.AutoBots(rib, sw);
            }
            return root;
        }

        public static RSNanoLimb Wave(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            float floorLocal = RSNanoRigPreview.FloorY(rig) - parent.position.y;
            var l = RSNanoRigEditor.AddChain(parent, "파도", 5, Vector3.zero, new Vector3(-2f, floorLocal + 0.3f, 0f), sw, new[] { 1f, 1f, 0.9f, 0.8f, 0.6f, 0.5f });
            SetSteps(l, new Vector3(1.4f, 1.2f, 0f), new Vector3(1.4f, 1.3f, 0f), new Vector3(1.2f, 0.8f, 0f), new Vector3(0.9f, -0.3f, 0f), new Vector3(0.6f, -0.9f, 0f));
            l.fillToFloor = true;
            l.radius = 0.5f;
            Feel(l, 0, 5f);
            l.flow = 0.2f;
            RSNanoRigEditor.AutoBots(l, sw);
            return l;
        }

        public static RSNanoLimb Puddle(Transform parent, RSNanoRig rig)
        {
            var sw = Sw(rig);
            float floorLocal = RSNanoRigPreview.FloorY(rig) - parent.position.y;
            var l = RSNanoRigEditor.AddLimb(parent, "웅덩이", LimbType.Puddle, new Vector3(3f, floorLocal, 0f), sw);
            l.extent = new Vector2(8f, 2.5f);
            l.bind = 0f;
            l.flow = 0.2f;
            RSNanoRigEditor.AutoBots(l, sw);
            return l;
        }
    }
}
