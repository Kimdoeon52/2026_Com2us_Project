#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using RealSteel.Boss.JunkWheeler;
using UnityEditor;
using UnityEngine;

namespace RealSteel.Boss.Editor
{
    /// <summary>
    /// [정크 휠러 사운드 에셋 자동 생성 및 바인딩 툴 (JunkWheelerSfxAutoSetup)]
    /// - Assets/KKH/04.Prefab/BOSS/First_Boss/Sound 하위 오디오 클립들을 파일명 규칙으로 자동 매칭하여
    ///   Assets/KKH/03.SOData/Boss/JunkWheelerSfxData.asset을 생성/갱신함.
    /// - Boss1_JunkWheeler.prefab 및 현재 씬의 JunkWheelerBossController에 자동 연결함.
    /// </summary>
    [InitializeOnLoad]
    public static class JunkWheelerSfxAutoSetup
    {
        public const string DefaultSfxDataAssetPath = "Assets/KKH/03.SOData/Boss/JunkWheelerSfxData.asset";
        private const string SoundRootFolder = "Assets/KKH/04.Prefab/BOSS/First_Boss/Sound";
        private const string Boss1PrefabPath = "Assets/KKH/04.Prefab/BOSS/First_Boss/Boss1_JunkWheeler.prefab";

        static JunkWheelerSfxAutoSetup()
        {
            EditorApplication.delayCall += EnsureInitializedOnce;
        }

        private static void EnsureInitializedOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<JunkWheelerSfxData>(DefaultSfxDataAssetPath) == null)
            {
                SetupAll();
            }
        }

        [MenuItem("Tools/JunkWheeler/사운드 데이터(SO) 자동 생성 및 1보스 연결")]
        public static void SetupAll()
        {
            JunkWheelerSfxData sfxData = LoadOrCreateSfxData();
            int matchedSfxCount = PopulateSfxClips(sfxData);
            int matchedBgmCount = PopulateBgmClips(sfxData);

            EditorUtility.SetDirty(sfxData);
            AssetDatabase.SaveAssets();

            int wiredCount = BindToPrefabAndScene(sfxData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"<color=green><b>[JunkWheelerSfxAutoSetup] 완료!</b></color> " +
                $"SFX 매칭: {matchedSfxCount}개 클립, BGM 매칭: {matchedBgmCount}개, 프리팹/씬 연결: {wiredCount}곳\n" +
                $"에셋 경로: {DefaultSfxDataAssetPath}"
            );
        }

        private static JunkWheelerSfxData LoadOrCreateSfxData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<JunkWheelerSfxData>(DefaultSfxDataAssetPath);
            if (existing != null) return existing;

            string dir = Path.GetDirectoryName(DefaultSfxDataAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var created = ScriptableObject.CreateInstance<JunkWheelerSfxData>();
            AssetDatabase.CreateAsset(created, DefaultSfxDataAssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        private static int PopulateSfxClips(JunkWheelerSfxData data)
        {
            string sfxFolder = $"{SoundRootFolder}/SFX";
            if (!AssetDatabase.IsValidFolder(sfxFolder)) return 0;

            var prefixToCue = new (string prefix, BossSfxCue cue)[]
            {
                ("SFX_JW_Idle_Loop", data.idleLoop),
                ("SFX_JW_V8_Idle", data.idleLoop),
                ("SFX_JW_Roll_Move", data.rollMove),
                ("SFX_JW_V8_Ignition", data.v8Ignition),
                ("SFX_JW_V8_Rev_Aggressive", data.v8RevAggressive),
                ("SFX_JW_V8_Backfire", data.v8Backfire),
                ("SFX_JW_Drift_Donut", data.driftDonut),
                ("SFX_JW_Drift_Screech", data.driftDonut),
                ("SFX_JW_Wheel_Spin_Burnout", data.driftDonut),
                ("SFX_JW_Tire_Squeal_Ripping", data.driftDonut),

                ("SFX_JW_Charge_Tell", data.chargeTell),
                ("SFX_JW_Charge_Rush", data.chargeRush),
                ("SFX_JW_Charge_HitPlayer", data.chargeHitPlayer),
                ("SFX_JW_Charge_WallSlam", data.chargeWallSlam),
                ("SFX_JW_Charge_Rebound", data.chargeRebound),
                ("SFX_JW_Charge_DecelRoll", data.chargeDecelRoll),
                ("SFX_JW_Charge_Interrupted", data.chargeInterrupted),
                ("SFX_JW_Charge_FireTrail", data.chargeFireTrail),

                ("SFX_JW_Slam_Launch", data.slamLaunch),
                ("SFX_JW_Slam_Hover", data.slamHover),
                ("SFX_JW_Slam_Drop", data.slamDrop),
                ("SFX_JW_Slam_Impact", data.slamImpact),
                ("SFX_JW_Nail_Puncture", data.nailPuncture),
                ("SFX_JW_Tire_Blowout", data.tireBlowout),

                ("SFX_JW_Barrage_Jump", data.barrageJump),
                ("SFX_JW_Barrage_Shot", data.barrageShot),
                ("SFX_JW_Barrage_Fly", data.barrageFly),
                ("SFX_JW_Barrage_Hit", data.barrageHit),
                ("SFX_JW_Barrage_Land", data.barrageLand),

                ("SFX_JW_Blade_Deploy", data.bladeDeploy),
                ("SFX_JW_Blade_Spin_Loop", data.bladeSpinLoop),
                ("SFX_JW_Blade_Bounce", data.bladeBounce),
                ("SFX_JW_Blade_SlashHit", data.bladeSlashHit),
                ("SFX_JW_Blade_Retract", data.bladeRetract),

                ("SFX_JW_Dribble_TellCrack", data.dribbleTellCrack),
                ("SFX_JW_Dribble_Bounce_1", data.dribbleBounce1),
                ("SFX_JW_Dribble_Bounce_2", data.dribbleBounce2),
                ("SFX_JW_Dribble_Bounce_3", data.dribbleBounce3),
                ("SFX_JW_Dribble_Bounce_4", data.dribbleBounce4),
                ("SFX_JW_Dribble_Shockwave", data.dribbleShockwave),
                ("SFX_JW_Dribble_SteamExhaust", data.dribbleSteamExhaust),

                ("SFX_JW_Hit_Armor", data.hitArmor),
                ("SFX_JW_Hit_Rubber", data.hitRubber),
                ("SFX_JW_Tire_Break_Final", data.tireBreakFinal),
                ("SFX_JW_State_Groggy", data.stateGroggy),
                ("SFX_JW_Phase2_Roar", data.phase2Roar),
                ("SFX_JW_Defeat_Explosion", data.defeatExplosion),

                ("SFX_JW_Mech_GearShift", data.mechGearShift),
                ("SFX_JW_Mech_TurboBlowOff", data.mechTurboBlowOff),
                ("SFX_JW_Mech_HydraulicServo", data.mechHydraulicServo),
                ("SFX_JW_Mech_ChainRatchet", data.mechChainRatchet),

                ("SFX_Field_NailBoard_Rise", data.fieldNailBoardRise),
                ("SFX_Field_ExplosiveBox_Tick", data.fieldExplosiveBoxTick),
                ("SFX_Field_ExplosiveBox_Blast", data.fieldExplosiveBoxBlast),
            };

            // 긴 prefix부터 먼저 비교해 정확한 매칭 보장
            Array.Sort(prefixToCue, (a, b) => b.prefix.Length.CompareTo(a.prefix.Length));

            var cueBuckets = new Dictionary<BossSfxCue, List<AudioClip>>();
            foreach (var entry in prefixToCue)
            {
                if (!cueBuckets.ContainsKey(entry.cue))
                {
                    cueBuckets[entry.cue] = new List<AudioClip>();
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { sfxFolder });
            int matchedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                foreach (var (prefix, cue) in prefixToCue)
                {
                    if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var list = cueBuckets[cue];
                        if (!list.Contains(clip))
                        {
                            list.Add(clip);
                            matchedCount++;
                        }
                        break;
                    }
                }
            }

            foreach (var kvp in cueBuckets)
            {
                if (kvp.Value.Count > 0)
                {
                    kvp.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                    kvp.Key.clips = kvp.Value.ToArray();
                }
            }

            return matchedCount;
        }

        private static int PopulateBgmClips(JunkWheelerSfxData data)
        {
            string bgmFolder = $"{SoundRootFolder}/Background";
            if (!AssetDatabase.IsValidFolder(bgmFolder)) return 0;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { bgmFolder });
            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                count++;
                if (clip.name.IndexOf("Phase2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    data.bgmPhase2 = clip;
                }
                else if (data.bgmMain == null)
                {
                    data.bgmMain = clip;
                }
            }
            return count;
        }

        private static int BindToPrefabAndScene(JunkWheelerSfxData sfxData)
        {
            int wiredCount = 0;

            // 1. 프리팹 에셋 바인딩
            if (File.Exists(Boss1PrefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Boss1PrefabPath);
                if (prefab != null)
                {
                    var ctrl = prefab.GetComponentInChildren<JunkWheelerBossController>(true);
                    if (ctrl != null)
                    {
                        SetSfxDataSerialized(ctrl, sfxData);
                        EditorUtility.SetDirty(prefab);
                        wiredCount++;
                    }
                }
            }

            // 2. 현재 열린 씬 내 정크 휠러 바인딩
            var sceneControllers = UnityEngine.Object.FindObjectsByType<JunkWheelerBossController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ctrl in sceneControllers)
            {
                if (ctrl == null) continue;
                SetSfxDataSerialized(ctrl, sfxData);
                EditorUtility.SetDirty(ctrl);
                wiredCount++;
            }

            return wiredCount;
        }

        private static void SetSfxDataSerialized(JunkWheelerBossController ctrl, JunkWheelerSfxData sfxData)
        {
            var so = new SerializedObject(ctrl);
            var prop = so.FindProperty("sfxData");
            if (prop != null)
            {
                prop.objectReferenceValue = sfxData;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
