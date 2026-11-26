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
///   · 4단계(11-24): 거대 지휘봉 — Maestro_Pattern_Baton(오른팔)·Maestro_BatonData와 자식 "패턴_거대 지휘봉"
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
    private const string BatonPatternPath = DataFolder + "/Maestro_Pattern_Baton.asset";
    private const string BatonDataPath = DataFolder + "/Maestro_BatonData.asset";
    private const string SlashPatternPath = DataFolder + "/Maestro_Pattern_Slash.asset";
    private const string SlashDataPath = DataFolder + "/Maestro_SlashData.asset";
    private const string OrbitPatternPath = DataFolder + "/Maestro_Pattern_Orbit.asset";
    private const string OrbitDataPath = DataFolder + "/Maestro_OrbitData.asset";
    private const string WavePatternPath = DataFolder + "/Maestro_Pattern_Wave.asset";
    private const string WaveDataPath = DataFolder + "/Maestro_WaveData.asset";
    private const string OpenPatternPath = DataFolder + "/Maestro_Pattern_Open.asset";
    private const string OpenDataPath = DataFolder + "/Maestro_OpenData.asset";
    private const string OpenMotionPath = DataFolder + "/Motions/Maestro_Motion_OpenMotion.asset";
    private const string CirclePatternPath = DataFolder + "/Maestro_Pattern_Circle.asset";
    private const string CircleDataPath = DataFolder + "/Maestro_CircleData.asset";
    private const string CircleMotionPath = DataFolder + "/Motions/Maestro_Motion_DrawCircle.asset";
    // 남는 봇 (몸 그림에 겹쳐 숨김) — 패턴이 빌려 쓴다 (커튼 3000 · 지휘봉 2400 · 음표 ~1500, 동시엔 하나). TODO 임시값
    private const int LookPoolBots = 4000;
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

        // 외형 (2026-11-24): 사용자 참고 그림 → 부위 그림 + 몸짓. 누를 때마다 다시 굽는다(그림 수정 반영). MaestroLook 값은 유지
        var look = MaestroLookBuilder.BuildLook(boss.GetComponent<RSNanoSwarm>(), boss.transform, LookPoolBots);
        if (look != null) SetFields(boss, s => s.FindProperty("look").objectReferenceValue = look);
        // 디버그 패널 (플레이 중 F8) — 패턴 골라 쓰기·상태 보기·실시간 조절 (11-25)
        if (boss.GetComponent<MaestroDebugPanel>() == null) Undo.AddComponent<MaestroDebugPanel>(boss.gameObject);

        EnsureNotePattern(boss);
        EnsureCurtainPattern(boss);
        EnsureBatonPattern(boss);
        EnsureSlashPattern(boss);
        EnsureOrbitPattern(boss);
        EnsureWavePattern(boss);
        EnsureOpenPattern(boss);
        EnsureCirclePattern(boss);

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

    // 4단계 거대 지휘봉 (기획서 1.3)
    private static void EnsureBatonPattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(BatonPatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_BATON";
            p.patternName = "거대 지휘봉";
            p.description = "보스3_월광_기획 1.3 — 보스는 배경으로, 지휘봉 끝이 8자·무한대 궤적. 지휘봉 파괴 시 그로기";
            p.tellDuration = 0f;                  // 예고는 패턴 안(③ 궤적 예고선)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.RightArm; // 기획서 0.3: 오른팔
            // baseDamage·partDurabilityDamage·postDelay: 기획 수치 없음 — KKH 기본값 그대로(TODO)
        });
        var batonData = LoadOrCreate<MaestroBatonData>(BatonDataPath, null);
        EnsurePatternChild<MaestroBatonPattern>(boss, "패턴_거대 지휘봉", pattern, "batonData", batonData);
    }

    // 11-25 사용자: 거대 지휘봉(8자·무한대)은 잠시 꺼 두고, 지휘봉 베기(검처럼 오른쪽→왼쪽·왼쪽→오른쪽·오른쪽→왼쪽, 회피로 피함)를 대신 쓴다.
    // 예전 패턴은 지우지 않는다 — 오브젝트만 꺼 둠(다시 켜면 그대로 돌아온다). 꺼 둔 패턴은 컨트롤러가 등록하지 않는다
    private static void EnsureSlashPattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(SlashPatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_SLASH";
            p.patternName = "지휘봉 베기";
            p.description = "지휘봉을 검처럼 길게 — 오른쪽→왼쪽, 왼쪽→오른쪽, 오른쪽→왼쪽 수평 베기 3번. 가슴 높이 화면 전체, 회피로 피함";
            p.tellDuration = 0f;                  // 예고는 패턴 안(감아올림 · 빛줄기)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.RightArm; // 기획서 0.3: 거대 지휘봉 = 오른팔 자리를 이어받음
        });
        var data = LoadOrCreate<MaestroSlashData>(SlashDataPath, null);
        bool firstTime = boss.GetComponentInChildren<MaestroSlashPattern>(true) == null;
        EnsurePatternChild<MaestroSlashPattern>(boss, "패턴_지휘봉 베기", pattern, "slashData", data);

        // 베기를 처음 만들 때만 예전 패턴을 꺼 둔다 — 사용자가 다시 켜 두면 그대로 둔다
        var old = boss.GetComponentInChildren<MaestroBatonPattern>(true);
        if (firstTime && old != null && old.gameObject.activeSelf)
        {
            Undo.RecordObject(old.gameObject, "거대 지휘봉 꺼 두기");
            old.gameObject.SetActive(false);
            Debug.Log("[Maestro] 예전 거대 지휘봉 패턴을 꺼 둠 (오브젝트 '패턴_거대 지휘봉' — 다시 켜면 다시 나온다)");
        }
    }

    // 2악장 후보 (11-24 사용자 결정 '1, 2 만들어봐') — 원형 음탄 (시트 '음표 폭풍'). 기획서에 아직 없음. 부위 임시 왼팔
    private static void EnsureOrbitPattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(OrbitPatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_ORBIT";
            p.patternName = "원형 음탄";
            p.description = "2악장 후보 — 음표가 보스 둘레 고리로 돌다가 박자마다 바깥으로 발사(나선), 마지막 박에 한꺼번에";
            p.tellDuration = 0f;                  // 예고는 패턴 안(② 고리 회전)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.LeftArm; // TODO: 기획서에 없음 — 음표 탄 계열이라 임시로 왼팔
        });
        var data = LoadOrCreate<MaestroOrbitData>(OrbitDataPath, null);
        EnsurePatternChild<MaestroOrbitPattern>(boss, "패턴_원형 음탄", pattern, "orbitData", data);
        // 선율·음원은 음표 공격 수치를 같이 쓴다
        var orbit = boss.GetComponentInChildren<MaestroOrbitPattern>(true);
        var so = new SerializedObject(orbit);
        var nd = so.FindProperty("noteData");
        if (nd.objectReferenceValue == null) nd.objectReferenceValue = AssetDatabase.LoadAssetAtPath<MaestroNoteData>(NoteDataPath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 2악장 후보 — 크레셴도 (바닥 모래 파도). 부위 오른다리 = 기획서 0.3 '2악장에서 배정' 자리
    private static void EnsureWavePattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(WavePatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_WAVE";
            p.patternName = "크레셴도";
            p.description = "2악장 후보 — 내리칠 때마다 모래 파도가 좌우로 밀려가며 점점 커짐. 점프로 넘고 마지막은 회피";
            p.tellDuration = 0f;                  // 예고는 패턴 안(치켜듦 · 바닥 고리)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.RightLeg;
        });
        var data = LoadOrCreate<MaestroWaveData>(WaveDataPath, null);
        EnsurePatternChild<MaestroWavePattern>(boss, "패턴_크레셴도", pattern, "waveData", data);
    }

    // 빛기둥 (11-26 사용자 — 패턴 제조기 모션 '오픈모션' 기반, 처음 '빛선 연타'에서 사용자 그림대로 바꿈). 기획서에 아직 없음. 부위 임시 머리
    private const string OpenDescription = "순간이동으로 뒤판에 들어가 두 팔을 활짝 든 동안, 화면을 세로 띠로 나눠 빛기둥 칸 예고 → 낙하를 1~4번(매번 자리 뒤바꿈). 끝나면 순간이동으로 돌아옴";

    private static void EnsureOpenPattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(OpenPatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_OPEN";
            p.patternName = "빛기둥";
            p.description = OpenDescription;
            p.tellDuration = 0f;                // 예고는 패턴 안(예고선)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.Head; // TODO: 기획서에 없음 — 위에서 꽂히는 빛이라 임시로 머리
        });
        if (pattern.patternName == "빛선 연타")
        {
            pattern.patternName = "빛기둥";
            pattern.description = OpenDescription;
            EditorUtility.SetDirty(pattern);
        }
        var data = LoadOrCreate<MaestroOpenData>(OpenDataPath, null);
        var oldChild = boss.transform.Find("패턴_빛선 연타");
        if (oldChild != null) { Undo.RecordObject(oldChild.gameObject, "이름"); oldChild.name = "패턴_빛기둥"; }
        if (data.motion == null)
        {
            data.motion = AssetDatabase.LoadAssetAtPath<MaestroMotion>(OpenMotionPath);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            if (data.motion == null) Debug.LogWarning($"[Maestro] 빛선 연타 모션이 없음: {OpenMotionPath} — Maestro_OpenData에 직접 넣을 것");
        }
        EnsurePatternChild<MaestroOpenPattern>(boss, "패턴_빛기둥", pattern, "openData", data);
    }

    // 원 음표탄 (11-26 사용자 — 패턴 제조기 모션 '원그리기' + 메모). 기획서에 아직 없음. 부위 임시 왼팔(음표 탄 계열)
    private static void EnsureCirclePattern(MaestroBossController boss)
    {
        var pattern = LoadOrCreate<BossPatternData>(CirclePatternPath, p =>
        {
            p.patternID = "PATTERN_MAESTRO_CIRCLE";
            p.patternName = "원 음표탄";
            p.description = "지휘봉으로 원을 그리며 음표가 차례로 맺힘 → 하나씩 빠르게(다다다닥) 플레이어 쪽으로, 약한 유도 뒤 직선";
            p.tellDuration = 0f;                    // 예고는 패턴 안(맺히는 음표)
            p.hasDpsCheck = false;
            p.targetPlayerPart = BodyPart.LeftArm;  // TODO: 기획서에 없음 — 음표 탄 계열이라 임시로 왼팔
        });
        var data = LoadOrCreate<MaestroCircleData>(CircleDataPath, null);
        bool dirty = false;
        if (data.motion == null) { data.motion = AssetDatabase.LoadAssetAtPath<MaestroMotion>(CircleMotionPath); dirty = true; }
        if (data.noteData == null) { data.noteData = AssetDatabase.LoadAssetAtPath<MaestroNoteData>(NoteDataPath); dirty = true; }
        if (dirty) { EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); }
        if (data.motion == null) Debug.LogWarning($"[Maestro] 원 음표탄 모션이 없음: {CircleMotionPath} — Maestro_CircleData에 직접 넣을 것");
        EnsurePatternChild<MaestroCirclePattern>(boss, "패턴_원 음표탄", pattern, "circleData", data);
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
