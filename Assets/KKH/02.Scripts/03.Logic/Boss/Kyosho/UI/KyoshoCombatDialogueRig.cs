using System;
using System.IO;
using System.Reflection;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// 쿄쇼 전투 씬에서 DialogueManager / BattleBarkPlayer / CrowdBubbles를 보증하는 자립형 런타임 리그.
    /// 씬에 DialogueSystem이 없는 단독 테스트 씬(Battle_Boss_Test 3)에서도 크라우드 말풍선 및 보스 자막을 정상 구동한다.
    /// </summary>
    public class KyoshoCombatDialogueRig : MonoBehaviour
    {
        private const string DialogueDataDir = "Assets/LHM/03.Data/Dialogue/";
        private const string KoreanFontPath = "Assets/LJS/03. Asset/Fonts/NanumBarunGothicBold SDF.asset";

        public DialogueManager Manager { get; private set; }
        public BattleBarkPlayer BarkPlayer { get; private set; }
        public CrowdBubbles CrowdBubbles { get; private set; }

        public static KyoshoCombatDialogueRig EnsureInScene(KyoshoBossController boss, float arenaFloorY)
        {
            var existingRig = FindFirstObjectByType<KyoshoCombatDialogueRig>(FindObjectsInactive.Include);
            if (existingRig != null)
            {
                existingRig.RefreshVariables(boss);
                return existingRig;
            }

            var rootGo = new GameObject("[Kyosho] DialogueRig");
            var rig = rootGo.AddComponent<KyoshoCombatDialogueRig>();
            rig.InitializeRig(boss, arenaFloorY);
            return rig;
        }

        private void InitializeRig(KyoshoBossController boss, float arenaFloorY)
        {
            TMP_FontAsset fontAsset = ResolveFontAsset();
            Canvas hudCanvas = EnsureOverlayCanvas(transform);

            DialogueManager manager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                manager = CreateDialogueManager(transform, hudCanvas.transform, fontAsset);
            }
            Manager = manager;
            RefreshVariables(boss);

            BattleBarkPlayer barkPlayer = FindFirstObjectByType<BattleBarkPlayer>(FindObjectsInactive.Include);
            if (barkPlayer == null)
            {
                barkPlayer = CreateBattleBarkPlayer(transform, hudCanvas.transform, manager, fontAsset);
            }
            BarkPlayer = barkPlayer;

            CrowdBubbles crowd = FindFirstObjectByType<CrowdBubbles>(FindObjectsInactive.Include);
            if (crowd == null)
            {
                BubbleManager bubbleManager = FindFirstObjectByType<BubbleManager>(FindObjectsInactive.Include);
                if (bubbleManager == null)
                {
                    bubbleManager = CreateBubbleManager(transform, hudCanvas.transform, manager, boss, fontAsset);
                }
                else
                {
                    var worldCamField = bubbleManager.GetType().GetField("worldCamera", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (worldCamField != null && (Camera)worldCamField.GetValue(bubbleManager) == null)
                    {
                        worldCamField.SetValue(bubbleManager, boss != null && boss.TargetCamera != null ? boss.TargetCamera : Camera.main);
                    }
                }
                crowd = CreateCrowdBubbles(transform, manager, bubbleManager, arenaFloorY);
            }
            CrowdBubbles = crowd;
        }

        private void RefreshVariables(KyoshoBossController boss)
        {
            if (Manager == null || Manager.Variables == null) return;

            string currentBoss = Manager.Variables.Get("hunt.targetBoss");
            if (string.IsNullOrEmpty(currentBoss) || currentBoss != "boss.region3")
            {
                Manager.Variables.Set("hunt.targetBoss", "boss.region3");
            }

            if (boss != null)
            {
                string diffKey = boss.SelectedDifficulty switch
                {
                    BossDifficulty.VeryEasy => "veryEasy",
                    BossDifficulty.Easy => "easy",
                    BossDifficulty.Normal => "normal",
                    BossDifficulty.Hard => "hard",
                    BossDifficulty.Hell => "hell",
                    _ => "normal"
                };

                string curDiff = Manager.Variables.Get("hunt.targetDifficulty");
                if (string.IsNullOrEmpty(curDiff) || curDiff == "none")
                {
                    Manager.Variables.Set("hunt.targetDifficulty", diffKey);
                }
            }
        }

        private static Canvas EnsureOverlayCanvas(Transform parent)
        {
            var canvasGo = new GameObject("KyoshoDialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 58;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static DialogueManager CreateDialogueManager(Transform parent, Transform canvasTransform, TMP_FontAsset fontAsset)
        {
            // DialogueBox / DialogueManager가 Awake 시 null 검증 후 비활성화되지 않도록 비활성 상태에서 필드를 주입한다.
            var boxHostGo = new GameObject("HiddenDialogueBox", typeof(RectTransform));
            boxHostGo.transform.SetParent(canvasTransform, false);
            boxHostGo.SetActive(false);

            var boxPanelGo = new GameObject("BoxPanel", typeof(RectTransform), typeof(Image));
            boxPanelGo.transform.SetParent(boxHostGo.transform, false);
            var boxImg = boxPanelGo.GetComponent<Image>();
            boxImg.raycastTarget = false;
            boxImg.color = new Color(0.05f, 0.06f, 0.10f, 0.85f);
            boxPanelGo.SetActive(false);

            var speakerText = CreateTmpText(boxPanelGo.transform, "SpeakerName", Vector2.zero, new Vector2(300f, 36f), 20f, TextAlignmentOptions.Left, fontAsset);
            var bodyText = CreateTmpText(boxPanelGo.transform, "BodyText", Vector2.zero, new Vector2(680f, 96f), 22f, TextAlignmentOptions.Left, fontAsset);

            var dialogueBox = boxHostGo.AddComponent<DialogueBox>();
            SetPrivateField(dialogueBox, "box", boxPanelGo);
            SetPrivateField(dialogueBox, "speakerNameText", speakerText);
            SetPrivateField(dialogueBox, "bodyText", bodyText);
            boxHostGo.SetActive(true);

            var dmGo = new GameObject("DialogueManager");
            dmGo.transform.SetParent(parent, false);
            dmGo.SetActive(false);

            TextAsset barkPool = LoadDialogueTextAsset("battle_bark.json");
            TextAsset crowdPool = LoadDialogueTextAsset("crowd.json");
            TextAsset stringsKo = LoadDialogueTextAsset("strings_ko.json");
            TextAsset varsJson = LoadDialogueTextAsset("variables.json");

            var pools = new System.Collections.Generic.List<TextAsset>(2);
            if (barkPool != null) pools.Add(barkPool);
            if (crowdPool != null) pools.Add(crowdPool);

            var dm = dmGo.AddComponent<DialogueManager>();
            SetPrivateField(dm, "dialogueBox", dialogueBox);
            SetPrivateField(dm, "poolJsons", pools.ToArray());
            SetPrivateField(dm, "stringTableJson", stringsKo);
            SetPrivateField(dm, "variablesJson", varsJson);

            dmGo.SetActive(true);
            return dm;
        }

        private static BattleBarkPlayer CreateBattleBarkPlayer(Transform parent, Transform canvasTransform, DialogueManager manager, TMP_FontAsset fontAsset)
        {
            var subGo = new GameObject("BattleBarkSubtitle", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            subGo.transform.SetParent(canvasTransform, false);
            subGo.SetActive(false);

            var rt = subGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 118f);
            rt.sizeDelta = new Vector2(860f, 58f);

            var bg = subGo.GetComponent<Image>();
            bg.raycastTarget = false;
            bg.color = new Color(0.05f, 0.06f, 0.11f, 0.80f);

            var group = subGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var tmp = CreateTmpText(subGo.transform, "SubtitleText", Vector2.zero, new Vector2(820f, 50f), 24f, TextAlignmentOptions.Center, fontAsset);
            tmp.color = new Color(0.98f, 0.94f, 0.80f, 1f);
            tmp.fontStyle = FontStyles.Bold;

            var subtitle = subGo.AddComponent<BattleBarkSubtitle>();
            SetPrivateField(subtitle, "group", group);
            SetPrivateField(subtitle, "text", tmp);
            subGo.SetActive(true);

            var playerGo = new GameObject("BattleBarkPlayer");
            playerGo.transform.SetParent(parent, false);
            playerGo.SetActive(false);

            var player = playerGo.AddComponent<BattleBarkPlayer>();
            SetPrivateField(player, "manager", manager);
            SetPrivateField(player, "subtitle", subtitle);
            playerGo.SetActive(true);
            return player;
        }

        private static BubbleManager CreateBubbleManager(Transform parent, Transform canvasTransform, DialogueManager manager, KyoshoBossController boss, TMP_FontAsset fontAsset)
        {
            var templateGo = new GameObject("BubbleTemplate", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            templateGo.transform.SetParent(canvasTransform, false);
            templateGo.SetActive(false);

            var rt = templateGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 72f);

            var bg = templateGo.GetComponent<Image>();
            bg.raycastTarget = false;
            bg.color = new Color(0.06f, 0.08f, 0.15f, 0.88f);

            var group = templateGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var tmp = CreateTmpText(templateGo.transform, "BubbleText", Vector2.zero, new Vector2(248f, 56f), 18f, TextAlignmentOptions.Center, fontAsset);
            tmp.color = new Color(0.96f, 0.97f, 1f, 1f);
            tmp.fontStyle = FontStyles.Bold;

            var view = templateGo.AddComponent<BubbleView>();
            SetPrivateField(view, "group", group);
            SetPrivateField(view, "text", tmp);

            var bmGo = new GameObject("BubbleManager");
            bmGo.transform.SetParent(parent, false);
            bmGo.SetActive(false);

            var bm = bmGo.AddComponent<BubbleManager>();
            SetPrivateField(bm, "manager", manager);
            SetPrivateField(bm, "template", view);
            SetPrivateField(bm, "worldCamera", boss != null && boss.TargetCamera != null ? boss.TargetCamera : Camera.main);
            bmGo.SetActive(true);
            return bm;
        }

        private static CrowdBubbles CreateCrowdBubbles(Transform parent, DialogueManager manager, BubbleManager bubbleManager, float arenaFloorY)
        {
            var anchorsRoot = new GameObject("CrowdAnchors");
            anchorsRoot.transform.SetParent(parent, false);

            float[] xs = { -7.2f, -3.8f, 0f, 3.8f, 7.2f };
            var members = new Transform[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                var anchorGo = new GameObject($"CrowdAnchor_{i}");
                anchorGo.transform.SetParent(anchorsRoot.transform, false);
                anchorGo.transform.position = new Vector3(xs[i], arenaFloorY + 4.3f, 0f);
                members[i] = anchorGo.transform;
            }

            var crowdGo = new GameObject("CrowdBubbles");
            crowdGo.transform.SetParent(parent, false);
            crowdGo.SetActive(false);

            var crowd = crowdGo.AddComponent<CrowdBubbles>();
            SetPrivateField(crowd, "manager", manager);
            SetPrivateField(crowd, "bubbles", bubbleManager);
            SetPrivateField(crowd, "members", members);
            SetPrivateField(crowd, "minDelay", 3.2f);
            SetPrivateField(crowd, "maxDelay", 6.2f);
            SetPrivateField(crowd, "maxConcurrent", 2);
            crowdGo.SetActive(true);
            return crowd;
        }

        private static TextMeshProUGUI CreateTmpText(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;
            return tmp;
        }

        private static TMP_FontAsset ResolveFontAsset()
        {
#if UNITY_EDITOR
            var editorFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (editorFont != null) return editorFont;
#endif
            var loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (loaded != null)
            {
                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null && loaded[i].name.Contains("NanumBarunGothic", StringComparison.OrdinalIgnoreCase))
                    {
                        return loaded[i];
                    }
                }
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static TextAsset LoadDialogueTextAsset(string fileName)
        {
            string assetPath = DialogueDataDir + fileName;
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset != null) return asset;
#endif
            string stem = Path.GetFileNameWithoutExtension(fileName);
            var loaded = Resources.FindObjectsOfTypeAll<TextAsset>();
            if (loaded != null)
            {
                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null && string.Equals(loaded[i].name, stem, StringComparison.OrdinalIgnoreCase))
                    {
                        return loaded[i];
                    }
                }
            }

            string diskPath = Path.Combine(Application.dataPath, "LHM", "03.Data", "Dialogue", fileName);
            if (File.Exists(diskPath))
            {
                var runtimeAsset = new TextAsset(File.ReadAllText(diskPath));
                runtimeAsset.name = stem;
                return runtimeAsset;
            }
            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            FieldInfo fi = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null)
            {
                fi.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[KyoshoCombatDialogueRig] Field not found: {target.GetType().Name}.{fieldName}");
            }
        }
    }
}
