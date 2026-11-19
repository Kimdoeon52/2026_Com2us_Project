using System.IO;
using RealSteel.NanoBoss;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 보스3 「마에스트로: 월광」 만들기·갱신 (2026-11-19, 1단계 뼈대).
/// 메뉴 NYH/보스3/마에스트로 만들기·갱신 (현재 씬)
///   · 마스터 데이터(04. SO/Boss/Maestro/Maestro_Master.asset) — 없을 때만 만든다
///   · 씬에 MaestroBossController가 없으면 루트(태그 Boss + 나노봇 군집 + 판정 콜라이더)를 세운다
///   · 이미 있으면 빠진 것(마스터 연결·셰이더)만 채운다. 손으로 고친 값은 건드리지 않는다
///   · 2단계(11-19): 음표 공격 — Maestro_Pattern_Notes(KKH 패턴 데이터)·Maestro_NoteData(음표 수치)와 자식 "패턴_음표 공격"
///   · 3단계(11-19): 달빛 커튼 — Maestro_Pattern_Curtain(머리)·Maestro_CurtainData와 자식 "패턴_달빛 커튼"
/// 전투에 필요한 씬 구성(Player·CombatDataHub·BattleManager·CombatClock·HP 캔버스)은 이 메뉴가 만들지 않는다 — Boss2 씬과 같은 구성을 써야 한다.
/// </summary>
public static class MaestroBuilder
{
    private const string DataFolder = "Assets/NYH/04. SO/Boss/Maestro";
    private const string MasterPath = DataFolder + "/Maestro_Master.asset";
    private const string NotePatternPath = DataFolder + "/Maestro_Pattern_Notes.asset";
    private const string NoteDataPath = DataFolder + "/Maestro_NoteData.asset";
    private const string CurtainPatternPath = DataFolder + "/Maestro_Pattern_Curtain.asset";
    private const string CurtainDataPath = DataFolder + "/Maestro_CurtainData.asset";
    private const float DefaultDistanceFromPlayer = 6f; // 새로 세울 때 플레이어 오른쪽 거리(월드) — 배치 편의값

    [MenuItem("NYH/보스3/마에스트로 만들기·갱신 (현재 씬)", false, 200)]
    private static void BuildOrUpdate()
    {
        var master = AssetDatabase.LoadAssetAtPath<BossMasterData>(MasterPath);
        if (master == null)
        {
            EnsureFolder(DataFolder);
            master = ScriptableObject.CreateInstance<BossMasterData>();
            master.bossID = "BOSS_REGION_03_MAESTRO";
            master.bossName = "마에스트로: 월광";
            master.regionIndex = 3;
            master.patterns.Clear();
            master.gimmickParts.Clear();
            master.phases.Clear();
            // 악장 = 페이즈. 넘어가는 건 HP 문턱이 아니라 HP 1 고정 전환이라 문턱 값은 안 쓴다. 패턴 목록 비움 = 전부
            for (int i = 1; i <= 3; i++) master.phases.Add(new BossPhaseData { phaseIndex = i, hpThresholdRatio = 1f });
            // maxHp·방어력: 기획서 0.6 산정식의 기준 DPS가 미결 — KKH 기본값 그대로(TODO)
            AssetDatabase.CreateAsset(master, MasterPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Maestro] 마스터 데이터 생성: {MasterPath}");
        }

        var boss = Object.FindAnyObjectByType<MaestroBossController>();
        if (boss == null)
        {
            float groundY = 0f;
            float x = 0f;
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                groundY = player.transform.position.y;
                x = player.transform.position.x + DefaultDistanceFromPlayer;
            }

            var root = new GameObject("마에스트로 (보스3)");
            Undo.RegisterCreatedObjectUndo(root, "마에스트로 만들기");
            root.tag = "Boss";
            root.transform.position = new Vector3(x, groundY, 0f);
            var swarm = root.AddComponent<RSNanoSwarm>();
            swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
            swarm.floorFromTransform = false;
            swarm.floorY = groundY;
            boss = root.AddComponent<MaestroBossController>();
            SetFields(boss, so =>
            {
                so.FindProperty("groundY").floatValue = groundY;
                so.FindProperty("masterData").objectReferenceValue = master;
            });
            Debug.Log($"[Maestro] 보스 생성 (바닥 y {groundY}, x {x}){(player == null ? " — 씬에 Player 태그가 없어 원점에 세움" : "")}");
        }
        else
        {
            var so = new SerializedObject(boss);
            if (so.FindProperty("masterData").objectReferenceValue == null)
            {
                SetFields(boss, s => s.FindProperty("masterData").objectReferenceValue = master);
                Debug.Log("[Maestro] 마스터 연결");
            }
            var swarm = boss.GetComponent<RSNanoSwarm>();
            if (swarm != null && swarm.shader == null)
            {
                Undo.RecordObject(swarm, "나노봇 셰이더");
                swarm.shader = Shader.Find(RSNanoSwarm.ShaderName);
            }
            Debug.Log("[Maestro] 이미 있음 — 빠진 것만 채움");
        }

        EnsureNotePattern(boss);
        EnsureCurtainPattern(boss);

        Selection.activeGameObject = boss.gameObject;
        EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
    }

    // 2단계 음표 공격 (기획서 1.4) — 데이터 두 개(KKH 패턴 데이터 + 음표 수치)와 보스 자식 오브젝트. 이미 있으면 빠진 연결만 채움
    private static void EnsureNotePattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(NotePatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_NOTES";
            p.patternName = "음표 공격";
            p.description = "보스3_월광_기획 1.4 — 휘두르기 1번에 음표 N개, 음이 울린 순서대로 플레이어를 향해 직선 비행";
            p.tellDuration = 0f;                 // KKH 전조 단계는 안 씀 — 예고는 패턴 안(③ 음 + 반짝임)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.LeftArm; // 기획서 0.3: 왼팔
            // baseDamage·partDurabilityDamage·postDelay: 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var noteData = LoadOrCreate<MaestroNoteData>(NoteDataPath, null);
        EnsurePatternChild<MaestroNotePattern>(boss, "패턴_음표 공격", pattern, "noteData", noteData);
    }

    // 3단계 달빛 커튼 (기획서 1.5)
    private static void EnsureCurtainPattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(CurtainPatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_CURTAIN";
            p.patternName = "달빛 커튼";
            p.description = "보스3_월광_기획 1.5 — 천장에서 커튼이 흘러내리고 움직이는 틈이 안전지대";
            p.tellDuration = 0f;              // 예고는 패턴 안(② 빛 기둥)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Head; // 기획서 0.3: 머리
            // baseDamage·partDurabilityDamage·postDelay: 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var curtainData = LoadOrCreate<MaestroCurtainData>(CurtainDataPath, null);
        EnsurePatternChild<MaestroCurtainPattern>(boss, "패턴_달빛 커튼", pattern, "curtainData", curtainData);
    }

    // 보스 자식에 패턴이 없으면 만들고, 비어 있는 연결(패턴 데이터·수치 에셋)만 채운다
    private static void EnsurePatternChild<T>(MaestroBossController boss, string name, BossPatternData pattern, string dataField, Object data)
        where T : Component
    {
        var existing = boss.GetComponentInChildren<T>(true);
        if (existing == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(boss.transform, false);
            existing = go.AddComponent<T>();
            Debug.Log($"[Maestro] {name} 추가");
        }
        var so = new SerializedObject(existing);
        var pd = so.FindProperty("patternData");
        var dd = so.FindProperty(dataField);
        if (pd.objectReferenceValue == null) pd.objectReferenceValue = pattern;
        if (dd.objectReferenceValue == null) dd.objectReferenceValue = data;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        asset = ScriptableObject.CreateInstance<T>();
        init?.Invoke(asset);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Maestro] 데이터 생성: {path}");
        return asset;
    }

    private static void SetFields(Object target, System.Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
