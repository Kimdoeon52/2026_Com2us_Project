// 보스3 마에스트로(월광) — 나노봇 시험 배치 (에디터 전용)
//
// 메뉴 NYH/보스3/나노봇 시험 배치 (주먹돌진)
//   현재 씬에 [나노봇 시험] 묶음을 만든다 (있으면 지우고 다시 만든다).
//     나노봇 보스      RSNanoSwarm (군집 = 봇 수만 개)
//       리그_주먹돌진  RSNanoRig + Animator(SYS 컨트롤러 — 애니메이션 창 편집용)
//         팔/관절1/관절2/관절3/주먹/마디1~4   ← SYS 주먹돌진.anim 의 키 경로와 같은 구조
//         플레이어     리그의 플레이어 기준점
//     플레이어 더미    큐브 (← → 로 움직임)
//     바닥            큐브 (보기용)
//   부위 구조는 11-17 삭제된 SYS 템플릿(RSNanoRigTemplates.Fist)과 같은 값으로 만든다 — 클립 키가 그대로 맞도록.
//
// 엔진 코드(RSNano*)는 KKH 폴더 것을 참조만 한다. 수정하지 않는다.
using RealSteel.NanoBoss;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NanoBotTestBuilder
{
    const string RootName = "[나노봇 시험]";
    const string ClipPath = "Assets/SYS/Data/NanoBoss/리그/주먹돌진.anim";
    const string ControllerPath = "Assets/SYS/Data/NanoBoss/리그/리그_주먹돌진.controller";

    // 배치 위치 (씬 카메라 (0,1,-10) 기준으로 화면 안에 들어오게 잡은 시험 배치값)
    static readonly Vector3 BossFeet = new Vector3(-4f, -2f, 0f);
    static readonly Vector3 PlayerFeet = new Vector3(4f, -2f, 0f);

    [MenuItem("NYH/보스3/나노봇 시험 배치 (주먹돌진)", false, 300)]
    static void Build()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!clip) { Debug.LogError($"[나노봇 시험] 클립이 없습니다: {ClipPath}"); return; }

        var old = GameObject.Find(RootName);
        if (old) Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "나노봇 시험 배치");

        // 바닥 (보기용)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "바닥";
        ground.transform.SetParent(root.transform, false);
        ground.transform.localPosition = new Vector3(0f, BossFeet.y - 0.1f, 1f);
        ground.transform.localScale = new Vector3(30f, 0.2f, 4f);
        Object.DestroyImmediate(ground.GetComponent<Collider>());

        // 군집
        var bossGo = new GameObject("나노봇 보스");
        bossGo.transform.SetParent(root.transform, false);
        bossGo.transform.localPosition = BossFeet;
        var swarm = bossGo.AddComponent<RSNanoSwarm>();
        swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
        if (!swarm.shader) Debug.LogWarning($"[나노봇 시험] 셰이더를 못 찾았습니다: {RSNanoSwarm.ShaderName}");
        swarm.floorFromTransform = true;

        // 리그
        var rigGo = new GameObject("리그_주먹돌진");
        rigGo.transform.SetParent(bossGo.transform, false);
        var rig = rigGo.AddComponent<RSNanoRig>();
        rig.swarm = swarm;
        var anchor = new GameObject("플레이어").transform;
        anchor.SetParent(rigGo.transform, false);
        anchor.localPosition = rig.previewOffset;
        rig.playerAnchor = anchor;
        var animator = rigGo.AddComponent<Animator>();
        if (ctrl) animator.runtimeAnimatorController = ctrl;   // 편집용 (플레이 중엔 RSNanoRig.Awake가 비운다)

        BuildFist(rigGo.transform, swarm);

        // 플레이어 더미
        var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
        player.name = "플레이어 더미";
        player.transform.SetParent(root.transform, false);
        player.transform.localPosition = PlayerFeet;
        Object.DestroyImmediate(player.GetComponent<Collider>());
        // 큐브 원점이 가운데라 발밑 기준이 되도록 그림만 위로 올린다
        var mesh = new GameObject("그림").transform;
        mesh.SetParent(player.transform, false);
        var mf = player.GetComponent<MeshFilter>();
        var mr = player.GetComponent<MeshRenderer>();
        var cmf = mesh.gameObject.AddComponent<MeshFilter>(); cmf.sharedMesh = mf.sharedMesh;
        var cmr = mesh.gameObject.AddComponent<MeshRenderer>(); cmr.sharedMaterial = mr.sharedMaterial;
        mesh.localPosition = new Vector3(0f, 0.9f, 0f);
        mesh.localScale = new Vector3(0.8f, 1.8f, 0.8f);
        Object.DestroyImmediate(mr);
        Object.DestroyImmediate(mf);

        // 재생기
        var runner = root.AddComponent<NanoBotTestRunner>();
        runner.swarm = swarm;
        runner.rig = rig;
        runner.clip = clip;
        runner.player = player.transform;

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[나노봇 시험] 배치 완료 — 플레이 후 Space 또는 '재생' 버튼");
    }

    /// <summary>주먹 돌진 부위 — SYS 템플릿과 같은 이름 · 값 (클립 키 경로 일치)</summary>
    static void BuildFist(Transform rig, RSNanoSwarm sw)
    {
        var arm = AddChain(rig, "팔", 3, new Vector3(0.6f, 0f, 0f), sw, new[] { 1f, 0.95f, 0.9f, 0.85f });
        arm.radius = 0.6f;
        arm.ApplyPreset(RSNanoLimb.Presets[1]);
        arm.tipLag = 4f;
        arm.gatherOrder = GatherOrder.Root;
        AutoBots(arm, sw);

        var tip = arm.transform;
        while (tip.childCount > 0) tip = tip.GetChild(0);
        var fist = AddLimb(tip, "주먹", LimbType.Blob, new Vector3(0.6f, 0f, 0f), sw);
        fist.size = new Vector3(1.3f, 1.1f, 0.8f);
        fist.wobble = 0.04f;
        fist.ApplyPreset(RSNanoLimb.Presets[2]);
        fist.tipLag = 3f;
        fist.tint = new Color(1f, 0.6f, 0.35f);
        AutoBots(fist, sw);

        for (int k = 0; k < 4; k++)
        {
            var n = AddLimb(fist.transform, "마디" + (k + 1), LimbType.Blob, new Vector3(1.05f, 0.75f - k * 0.5f, -0.25f), sw);
            n.size = new Vector3(0.35f, 0.28f, 0.3f);
            n.wobble = 0.02f;
            n.ApplyPreset(RSNanoLimb.Presets[2]);
            n.tint = new Color(1f, 0.6f, 0.35f);
            AutoBots(n, sw);
        }
    }

    // ── 아래 셋은 삭제된 SYS RSNanoRigEditor 의 만들기 도우미와 같은 동작 ──

    static RSNanoLimb AddLimb(Transform parent, string name, LimbType type, Vector3 local, RSNanoSwarm sw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<RSNanoLimb>();
        l.type = type;
        if (type == LimbType.Blob) l.ApplyPreset(RSNanoLimb.Presets[1]);
        if (type == LimbType.Puddle) { l.ApplyPreset(RSNanoLimb.Presets[0]); l.bind = 0f; l.flow = 0.2f; }
        AutoBots(l, sw);
        return l;
    }

    static RSNanoLimb AddChain(Transform parent, string name, int joints, Vector3 step, RSNanoSwarm sw, float[] thickness)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var l = go.AddComponent<RSNanoLimb>();
        l.type = LimbType.Chain;
        var jc = go.AddComponent<RSNanoJoint>();
        jc.thickness = thickness[0];
        Transform t = go.transform;
        for (int i = 1; i <= joints; i++)
        {
            var j = new GameObject("관절" + i);
            j.transform.SetParent(t, false);
            j.transform.localPosition = step;
            j.AddComponent<RSNanoJoint>().thickness = i < thickness.Length ? thickness[i] : Mathf.Lerp(1f, 0.15f, i / (float)joints);
            t = j.transform;
        }
        l.RefreshJoints();
        AutoBots(l, sw);
        return l;
    }

    static void AutoBots(RSNanoLimb l, RSNanoSwarm sw)
    {
        float floor = sw.floorFromTransform ? sw.transform.position.y : sw.floorY;
        l.bots = l.EstimateBots(sw ? sw.spacing : 0.0275f, floor);
    }
}
