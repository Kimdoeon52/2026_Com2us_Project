// RE:AL STEEL - 나노봇 리그 템플릿
//
// GameObject → RE_AL STEEL → 나노봇 리그 → 빈 리그 / 예제 3종
//   보스(나노봇 군집) 아래에 리그를 만들고, 예제는 부위 + 애니메이션 클립까지 만들어 두뇌의 리그 패턴 목록에 넣는다.
//   클립 · 컨트롤러는 SYS/Data/NanoBoss/리그/ 에 저장. 예제는 시작점일 뿐 — 애니메이션 창 · 나노봇 동작 창에서 키를 고쳐 쓴다.
//   봇 수는 군집의 격자 간격에 맞게 자동으로 정한다.
//
//   파도 → 가시     파도가 솟아 덮친다 → 부서지며 결속이 풀려 바닥에 물처럼 깔린다 → 가시 자리가 요동치다 →
//                   웅덩이 봇이 솟으며 '착' 잠겨 가시로 굳는다 (파악!)
//   주먹 돌진       덩어리가 거대한 주먹으로 모인다 → 뒤로 당기며 빛난다 → 팔이 늘어나며 앞으로 돌진
//   대검 잔상 (3페) 후광 봇이 거대한 칼로 모인다 → 머리 위로 들고 멈춘다 → 한 번에 휘두른다. 끝 늦음으로 궤적이 잔상이 된다
//   그림 따라가기   주먹 펀치 스프라이트 시트(8프레임, 예제로 그려 둠)의 검은 픽셀을 봇이 따라간다. 프레임은 계단 키
using UnityEditor;
using UnityEngine;

namespace RealSteel.NanoBoss.EditorTools
{
    static class RSNanoRigTemplates
    {
        const string Menu = "GameObject/RE_AL STEEL/나노봇 리그/";

        [MenuItem(Menu + "빈 리그", false, 16)]
        static void Empty(MenuCommand cmd)
        {
            if (Skip(cmd)) return;
            var rig = CreateRig("나노봇 리그", 2.1f, out _);
            if (!rig) return;
            RSNanoRigEditor.EnsurePlayerAnchor(rig);
            RSNanoRigEditor.EnsureAnimator(rig, null);
            Selection.activeGameObject = rig.gameObject;
            RSNanoActionWindow.Open(rig);
        }

        [MenuItem(Menu + "예제: 파도 → 가시", false, 17)]
        static void Wave(MenuCommand cmd)
        {
            if (Skip(cmd)) return;
            var rig = CreateRig("리그_파도가시", 2.1f, out var brain);
            if (!rig) return;
            var sw = rig.swarm;
            var player = RSNanoRigEditor.EnsurePlayerAnchor(rig);
            const float floor = -2.1f;

            // 파도 — 뒤(낮음)에서 앞(높고 말려 내려옴)으로 이어지는 물마루. 아래를 바닥까지 채운다
            var wave = RSNanoRigEditor.AddChain(rig.transform, "파도", 5, Vector3.zero, new Vector3(-2f, floor + 0.3f, 0f), sw, new[] { 1f, 1f, 0.9f, 0.8f, 0.6f, 0.5f });
            RSNanoLimbPresets.SetSteps(wave, new Vector3(1.4f, 1.2f, 0f), new Vector3(1.4f, 1.3f, 0f), new Vector3(1.2f, 0.8f, 0f), new Vector3(0.9f, -0.3f, 0f), new Vector3(0.6f, -0.9f, 0f));
            wave.fillToFloor = true; wave.radius = 0.5f;
            wave.ApplyPreset(RSNanoLimb.Presets[0]); wave.tipLag = 5f; wave.flow = 0.25f;
            RSNanoRigEditor.AutoBots(wave, sw);

            // 웅덩이 — 파도가 부서지면 물처럼 깔린다 (유체). 가시 뿌리 쪽으로 모여 요동친다
            var puddle = RSNanoRigEditor.AddLimb(rig.transform, "웅덩이", LimbType.Puddle, new Vector3(3f, floor, 0f), sw);
            puddle.extent = new Vector2(11f, 3f); puddle.layer = 0.12f; puddle.ripple = 0.1f;
            puddle.bind = 0f; puddle.flow = 0.22f;
            puddle.bumpRadius = 1.4f; puddle.bumpShake = 0.6f;
            RSNanoRigEditor.AutoBots(puddle, sw);

            // 가시 — 플레이어 기준점 아래. 웅덩이 봇으로 솟으며 '착' 잠겨 굳는다
            var spike = RSNanoRigEditor.AddChain(player, "가시", 3, new Vector3(0f, 1.25f, 0f), Vector3.zero, sw, new[] { 1f, 0.55f, 0.22f, 0.03f });
            spike.radius = 0.6f;
            spike.ApplyPreset(RSNanoLimb.Presets[3]); spike.gather = 6f;
            spike.gatherOrder = GatherOrder.Root;
            spike.bind = 0f;
            spike.tint = new Color(1f, 0.7f, 0.6f);
            RSNanoRigEditor.AutoBots(spike, sw);
            puddle.bumpAt = spike.transform;
            spike.feedFrom = puddle;

            var clip = RSNanoKeys.NewClip(rig, "파도가시", 3.6f);
            var R = rig.transform;
            Curve(clip, R, wave, "amount", (0f, 0f), (0.35f, 1f), (1.55f, 1f), (1.8f, 0f));
            Curve(clip, R, wave, "bind", (0f, 1f), (1.3f, 1f), (1.5f, 0f));                    // 덮치며 부서져 유체로
            Curve(clip, R, wave.transform, "m_LocalPosition.x", (0f, -2f), (1.0f, -1.5f), (1.55f, 5f));
            Curve(clip, R, wave.transform, "m_LocalScale.y", (0f, 0.35f), (0.9f, 1.25f), (1.3f, 1.4f), (1.55f, 0.5f));
            EulerZ(clip, R, wave.Joints[wave.Joints.Count - 3], (0f, 0f), (1.2f, -35f), (1.5f, -80f));   // 물마루 앞쪽 관절을 돌려 말아 내린다
            EulerZ(clip, R, wave.Joints[wave.Joints.Count - 2], (0f, 0f), (1.2f, -15f), (1.5f, -35f));
            Step(clip, R, wave, "danger", (0f, 0f), (1.1f, 1f), (1.65f, 0f));

            Curve(clip, R, puddle, "amount", (0f, 0f), (1.5f, 0f), (1.75f, 1f), (2.9f, 1f), (3.6f, 0f));
            Curve(clip, R, puddle, "ripple", (1.5f, 0.28f), (2.4f, 0.08f));
            Curve(clip, R, puddle.transform, "m_LocalPosition.x", (1.5f, 2.5f), (2.0f, 4f));
            Curve(clip, R, puddle, "bump", (0f, 0f), (1.9f, 0f), (2.45f, 1.4f), (2.52f, 0.15f));

            Curve(clip, R, spike, "amount", (0f, 0f), (2.45f, 0f), (2.5f, 1f), (2.95f, 1f), (3.3f, 0f));
            Curve(clip, R, spike, "grow", (0f, 0f), (2.45f, 0f), (2.56f, 1f), (2.95f, 1f), (3.25f, 0f));
            Curve(clip, R, spike, "bind", (0f, 0f), (2.48f, 0f), (2.54f, 1f), (2.95f, 1f), (3.15f, 0f));   // 파악! 솟는 순간 결정화
            Step(clip, R, spike, "danger", (0f, 0f), (2.5f, 1f), (2.85f, 0f));

            Step(clip, R, rig, "lockPlayer", (0f, 0f), (1.85f, 1f), (3.5f, 0f));
            Curve(clip, R, rig, "shake", (0f, 0f), (1.6f, 0f), (1.62f, 0.25f), (1.9f, 0f), (2.5f, 0f), (2.52f, 0.35f), (2.85f, 0f), (3.6f, 0f));
            Finish(rig, clip, brain, true, true, false);
        }

        [MenuItem(Menu + "예제: 주먹 돌진", false, 18)]
        static void Fist(MenuCommand cmd)
        {
            if (Skip(cmd)) return;
            var rig = CreateRig("리그_주먹돌진", 2.1f, out var brain);
            if (!rig) return;
            var sw = rig.swarm;
            RSNanoRigEditor.EnsurePlayerAnchor(rig);

            var arm = RSNanoRigEditor.AddChain(rig.transform, "팔", 3, new Vector3(0.6f, 0f, 0f), Vector3.zero, sw, new[] { 1f, 0.95f, 0.9f, 0.85f });
            arm.radius = 0.6f;
            arm.ApplyPreset(RSNanoLimb.Presets[1]); arm.tipLag = 4f;
            arm.gatherOrder = GatherOrder.Root;
            RSNanoRigEditor.AutoBots(arm, sw);

            var tip = arm.Joints[arm.Joints.Count - 1];
            var fist = RSNanoRigEditor.AddLimb(tip, "주먹", LimbType.Blob, new Vector3(0.6f, 0f, 0f), sw);
            fist.size = new Vector3(1.3f, 1.1f, 0.8f); fist.wobble = 0.04f;
            fist.ApplyPreset(RSNanoLimb.Presets[2]); fist.tipLag = 3f;
            fist.tint = new Color(1f, 0.6f, 0.35f);
            RSNanoRigEditor.AutoBots(fist, sw);
            var knuckles = new RSNanoLimb[4];
            for (int k = 0; k < 4; k++)
            {
                var n = RSNanoRigEditor.AddLimb(fist.transform, "마디" + (k + 1), LimbType.Blob, new Vector3(1.05f, 0.75f - k * 0.5f, -0.25f), sw);
                n.size = new Vector3(0.35f, 0.28f, 0.3f); n.wobble = 0.02f;
                n.ApplyPreset(RSNanoLimb.Presets[2]);
                n.tint = new Color(1f, 0.6f, 0.35f);
                RSNanoRigEditor.AutoBots(n, sw);
                knuckles[k] = n;
            }

            var clip = RSNanoKeys.NewClip(rig, "주먹돌진", 2.6f);
            var R = rig.transform;
            foreach (var l in new[] { arm, fist })
                Curve(clip, R, l, "amount", (0f, 0f), (0.7f, 1f), (2.3f, 1f), (2.6f, 0f));
            foreach (var n in knuckles)
            {
                Curve(clip, R, n, "amount", (0f, 0f), (0.7f, 1f), (2.3f, 1f), (2.6f, 0f));
                Step(clip, R, n, "danger", (0f, 0f), (1.3f, 1f), (1.85f, 0f));
                Curve(clip, R, n, "glow", (0f, 0f), (0.8f, 0f), (1.2f, 2.5f), (1.6f, 0f));
            }
            for (int j = 1; j < arm.Joints.Count; j++)
                Curve(clip, R, arm.Joints[j], "m_LocalPosition.x", (0f, 0.6f), (1.1f, 0.35f), (1.3f, 0.35f), (1.55f, 3.2f), (2.0f, 3.2f), (2.5f, 0.6f));
            foreach (var axis in new[] { "x", "y", "z" })
                Curve(clip, R, fist.transform, "m_LocalScale." + axis, (0f, 0.3f), (0.8f, 1f), (1.1f, 1.25f), (1.55f, 1f));
            Curve(clip, R, fist, "glow", (0f, 0f), (0.8f, 0f), (1.2f, 2.5f), (1.6f, 0f));
            Step(clip, R, fist, "danger", (0f, 0f), (1.3f, 1f), (1.85f, 0f));
            Curve(clip, R, rig, "shake", (0f, 0f), (1.55f, 0f), (1.57f, 0.3f), (1.85f, 0f), (2.6f, 0f));
            Finish(rig, clip, brain, true, true, false);
        }

        [MenuItem(Menu + "예제: 대검 잔상 (3페)", false, 19)]
        static void Sword(MenuCommand cmd)
        {
            if (Skip(cmd)) return;
            var rig = CreateRig("리그_대검잔상", 3.6f, out var brain);
            if (!rig) return;
            var sw = rig.swarm;
            rig.previewAsKnight = true;
            RSNanoRigEditor.EnsurePlayerAnchor(rig);

            var blade = RSNanoRigEditor.AddChain(rig.transform, "대검", 5, new Vector3(1.4f, 0f, 0f), new Vector3(1.4f, -1.0f, -0.4f), sw, new[] { 1f, 0.9f, 0.75f, 0.55f, 0.3f, 0.05f });
            blade.radius = 0.32f;
            blade.ApplyPreset(RSNanoLimb.Presets[3]); blade.tipLag = 13f;   // 끝이 13프레임 늦게 → 휘두른 궤적이 잔상
            RSNanoRigEditor.AutoBots(blade, sw);

            var wave = RSNanoRigEditor.AddEmitter(rig.transform, "검기", new Vector3(3f, -0.5f, 0f));
            wave.type = NanoEmitter.Fan; wave.count = 17; wave.spread = 110f; wave.speed = 16f; wave.telegraph = 0f; wave.life = 1.2f;
            wave.aimAtPlayer = false; wave.angle = -20f; wave.size = 1f;

            var clip = RSNanoKeys.NewClip(rig, "대검잔상", 2.4f);
            var R = rig.transform;
            Curve(clip, R, blade, "amount", (0f, 0f), (0.35f, 1f), (1.9f, 1f), (2.4f, 0f));
            EulerZ(clip, R, blade.transform, (0f, -70f), (0.6f, 150f), (0.95f, 150f), (1.08f, -60f), (1.6f, -60f), (2.1f, -80f));
            Curve(clip, R, blade, "glow", (0f, 0f), (0.6f, 0f), (0.95f, 1.6f), (1.08f, 0f));
            Step(clip, R, blade, "danger", (0f, 0f), (0.95f, 1f), (1.35f, 0f));
            Step(clip, R, wave, "fire", (0f, 0f), (1.08f, 1f), (1.5f, 0f));
            Curve(clip, R, rig, "shake", (0f, 0f), (1.05f, 0f), (1.07f, 0.4f), (1.35f, 0f), (2.4f, 0f));
            Finish(rig, clip, brain, false, false, true);
        }

        [MenuItem(Menu + "예제: 그림 따라가기 (주먹 펀치 시트)", false, 20)]
        static void Picture(MenuCommand cmd)
        {
            if (Skip(cmd)) return;
            var sheet = SamplePunchSheet();
            var rig = CreateRig("리그_그림주먹", 2.1f, out var brain);
            if (!rig) return;
            var sw = rig.swarm;
            RSNanoRigEditor.EnsurePlayerAnchor(rig);
            var shape = RSNanoSpriteBaker.CreateOrFind(sheet);
            if (shape.columns != 8 || shape.rows != 1) { shape.columns = 8; shape.rows = 1; RSNanoSpriteBaker.Bake(shape); AssetDatabase.SaveAssets(); }
            var pic = RSNanoRigEditor.AddSprite(rig.transform, "주먹 그림", new Vector3(1.2f, 0f, 0f), sw);
            pic.picture = shape;
            pic.bots = Mathf.Max(1, shape.botCount);
            pic.fps = 0f;                 // 프레임은 계단 키로 (픽셀 애니의 '노출' 처럼)
            pic.tint = new Color(1f, 0.75f, 0.6f);

            var clip = RSNanoKeys.NewClip(rig, "그림주먹", 2.0f);
            var R = rig.transform;
            Curve(clip, R, pic, "amount", (0f, 0f), (0.4f, 1f), (1.6f, 1f), (2.0f, 0f));
            //            프레임:  0 대기  1 당김  2 웅크림  3 뻗기  4 뻗기  5 다 뻗음  6 버팀  7 돌아옴
            Step(clip, R, pic, "frame", (0f, 0f), (0.5f, 1f), (0.62f, 2f), (0.8f, 3f), (0.87f, 4f), (0.94f, 5f), (1.2f, 6f), (1.4f, 7f), (1.6f, 0f));
            Curve(clip, R, pic, "glow", (0f, 0f), (0.5f, 0f), (0.78f, 2.2f), (0.82f, 0f));
            Step(clip, R, pic, "danger", (0f, 0f), (0.87f, 1f), (1.25f, 0f));
            Curve(clip, R, rig, "shake", (0f, 0f), (0.92f, 0f), (0.94f, 0.3f), (1.2f, 0f), (2.0f, 0f));
            Finish(rig, clip, brain, true, true, false);
        }

        /// <summary>예제 시트 — 48 × 28 칸 8개, 검은 픽셀 (몸통 원 + 팔 + 주먹). 없을 때만 그린다</summary>
        static Texture2D SamplePunchSheet()
        {
            string dir = RealSteel.Common.EditorTools.RSPaths.Ensure(RealSteel.Common.EditorTools.RSPaths.Data + "/NanoBoss/그림");
            string path = dir + "/예제_주먹펀치.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;
            const int CW = 48, CH = 28, N = 8;
            int[] armEnd = { 16, 14, 12, 22, 30, 36, 36, 24 };
            int[] bodyR = { 8, 8, 9, 8, 8, 8, 8, 8 };
            var px = new Color32[CW * N * CH];
            var ink = new Color32(0, 0, 0, 255);
            for (int f = 0; f < N; f++)
            {
                int ox = f * CW;
                void Put(int x, int y) { if (x >= 0 && x < CW && y >= 0 && y < CH) px[y * CW * N + ox + x] = ink; }
                // 몸통
                for (int y = 0; y < CH; y++)
                for (int x = 0; x < CW; x++)
                {
                    float dx = (x - 10) / (bodyR[f] * (f == 2 ? 1.15f : 1f)), dy = (y - 13) / (float)bodyR[f];
                    if (dx * dx + dy * dy <= 1f) Put(x, y);
                }
                // 팔
                for (int x = 14; x < armEnd[f]; x++) for (int y = 10; y <= 16; y++) Put(x, y);
                // 주먹 (마디 홈 두 줄)
                for (int x = armEnd[f]; x < armEnd[f] + 10; x++)
                for (int y = 8; y <= 18; y++)
                {
                    bool notch = x == armEnd[f] + 9 && (y == 11 || y == 15);
                    if (!notch) Put(x, y);
                }
            }
            var t = new Texture2D(CW * N, CH, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp)
            {
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ─────────────────────────────────────────────────────────────
        static bool Skip(MenuCommand cmd) =>
            cmd.context != null && Selection.objects.Length > 1 && cmd.context != Selection.objects[0];

        static RSNanoRig CreateRig(string name, float height, out RSNanoBossBrain brain)
        {
            brain = null;
            var sel = Selection.activeGameObject;
            RSNanoSwarm swarm = sel ? sel.GetComponentInParent<RSNanoSwarm>() : null;
            if (!swarm) swarm = Object.FindAnyObjectByType<RSNanoSwarm>();
            if (!swarm)
            {
                EditorUtility.DisplayDialog("나노봇 리그", "씬에 나노봇 군집(보스)이 없습니다. 보스를 먼저 놓아 주세요.", "확인");
                return null;
            }
            brain = swarm.GetComponent<RSNanoBossBrain>();
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "나노봇 리그");
            go.transform.SetParent(swarm.transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var rig = go.AddComponent<RSNanoRig>();
            rig.swarm = swarm;
            rig.previewOffset = new Vector3(7f, -height, 0f);
            return rig;
        }

        static void Curve(AnimationClip clip, Transform root, Object target, string prop, params (float t, float v)[] keys) =>
            RSNanoKeys.SetCurve(clip, root, target, prop, false, keys);

        static void Step(AnimationClip clip, Transform root, Object target, string prop, params (float t, float v)[] keys) =>
            RSNanoKeys.SetCurve(clip, root, target, prop, true, keys);

        /// <summary>z 회전 (x · y 는 0 고정)</summary>
        static void EulerZ(AnimationClip clip, Transform root, Transform t, params (float t, float v)[] keys)
        {
            RSNanoKeys.SetCurve(clip, root, t, "localEulerAnglesRaw.z", false, keys);
            RSNanoKeys.SetCurve(clip, root, t, "localEulerAnglesRaw.x", false, (keys[0].t, 0f));
            RSNanoKeys.SetCurve(clip, root, t, "localEulerAnglesRaw.y", false, (keys[0].t, 0f));
        }

        static void Finish(RSNanoRig rig, AnimationClip clip, RSNanoBossBrain brain, bool p1, bool p2, bool p3)
        {
            EditorUtility.SetDirty(clip);
            RSNanoRigEditor.EnsureAnimator(rig, clip);
            AssetDatabase.SaveAssets();
            if (brain)
            {
                Undo.RecordObject(brain, "리그 패턴 등록");
                brain.rigPatterns.Add(new RSNanoBossBrain.RigPattern { rig = rig, clip = clip, phase1 = p1, phase2 = p2, phase3 = p3, fromDifficulty = 1, weight = 1f });
                EditorUtility.SetDirty(brain);
            }
            Selection.activeGameObject = rig.gameObject;
            RSNanoActionWindow.Open(rig);
            Debug.Log("[나노봇 리그] '" + rig.name + "' 를 만들었습니다. 애니메이션 창에서 '" + clip.name + "' 미리보기를 켜면 어느 프레임이든 봇 모습이 고정되고, " +
                      "나노봇 동작 창에서 재생 · 키 넣기 · 점검을 할 수 있습니다." + (brain ? " 보스 두뇌의 리그 패턴 목록에도 넣었습니다." : ""), rig);
        }
    }
}
