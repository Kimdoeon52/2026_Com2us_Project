using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RealSteel.Dialogue;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RealSteel.Tutorial
{
    // 튜토리얼 자동 열기 담당. 게임이 시작되면 Resources의 TutorialCatalog를 읽어 스스로 생기고 씬을 넘어가도 남음
    // 조건(씬 입장 · 컴포넌트 존재 · 창 열림 · 코드 신호)이 처음 맞는 순간:
    //   인물 안내 대사(대화창) → 끝나면 화면 튜토리얼 → 본 것으로 기록 (기획서 xxi: 처음 1회, 스킵해도 봤음)
    // 다른 팀 창은 코드를 고치지 않고 '컴포넌트 이름 · 칸 이름'으로 찾아 켜지는 순간만 감시함
    public class TutorialDirector : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Intro,
            Panel,
        }

        private sealed class Watch
        {
            public TutorialGuide Guide;
            public Component Target;
            public Func<bool> IsOpen;
            public bool WasOpen;
        }

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static TutorialDirector instance;

        public static TutorialDirector Instance => instance;

        private readonly List<TutorialGuide> queue = new List<TutorialGuide>();
        private readonly List<Watch> watches = new List<Watch>();
        private TutorialCatalog catalog;
        private TutorialPanel panel;
        private TutorialHelpList help;
        private DialogueManager manager;
        private DialogueBox introBox;
        private TutorialGuide current;
        private State state;
        private float queuedAt;

        public TutorialCatalog Catalog => catalog;
        public bool IsBusy => state != State.Idle || (panel != null && panel.IsOpen) || (help != null && help.IsOpen);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;

            var catalog = Resources.Load<TutorialCatalog>(TutorialCatalog.ResourcePath);
            if (catalog == null) return;

            var go = new GameObject("[Tutorial] Director");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TutorialDirector>();
            instance.Init(catalog);
        }

        // 코드에서 보내는 신호 (조건이 Event인 튜토리얼). 예: TutorialDirector.Notify("first_win")
        public static void Notify(string key)
        {
            if (instance != null) instance.OnEvent(key);
        }

        private void Init(TutorialCatalog source)
        {
            catalog = source;
            CreatePanel();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Scan(SceneManager.GetActiveScene());
            Debug.Log($"[Tutorial] 자동 튜토리얼 {catalog.guides.Count(g => g != null)}종 감시 시작 (본 것 {catalog.guides.Count(g => g != null && TutorialPanel.HasSeen(g.guideId))}개) · 도움말 {catalog.helpKey}");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (introBox != null) introBox.Closed -= OnIntroClosed;
            if (instance == this) instance = null;
        }

        private void CreatePanel()
        {
            if (catalog.panelPrefab == null)
            {
                Debug.LogWarning("[Tutorial] TutorialCatalog의 Panel Prefab이 비어 있어 튜토리얼 화면을 열 수 없습니다.", catalog);
                return;
            }

            GameObject canvas = Instantiate(catalog.panelPrefab, transform);
            canvas.name = "TutorialCanvas";
            panel = canvas.GetComponentInChildren<TutorialPanel>(true);
            if (panel == null) Debug.LogWarning("[Tutorial] Panel Prefab에 TutorialPanel이 없습니다.", catalog);
        }

        // ── 조건 확인 ─────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            manager = null;
            watches.RemoveAll(w => w.Target == null);
            Scan(scene);
        }

        private void Scan(Scene scene)
        {
            if (!catalog.autoOpen || !scene.IsValid()) return;

            foreach (TutorialGuide guide in catalog.guides)
            {
                if (guide == null || TutorialPanel.HasSeen(guide.guideId)) continue;

                switch (guide.trigger)
                {
                    case TutorialTrigger.SceneName:
                        if (NameMatches(scene.name, guide.triggerTarget)) Enqueue(guide, $"씬 입장 ({scene.name})");
                        break;

                    case TutorialTrigger.ComponentInScene:
                        if (FindInScene(scene, guide.triggerTarget).Any(IsLive)) Enqueue(guide, $"{guide.triggerTarget} 있는 씬 입장 ({scene.name})");
                        break;

                    case TutorialTrigger.WindowOpened:
                        AddWatches(scene, guide);
                        break;
                }
            }
        }

        private static bool NameMatches(string sceneName, string tokens)
        {
            if (string.IsNullOrEmpty(tokens)) return false;
            return tokens.Split(',').Select(t => t.Trim()).Any(t => t.Length > 0 && sceneName.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IEnumerable<MonoBehaviour> FindInScene(Scene scene, string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) yield break;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == typeName) yield return behaviour;
                }
            }
        }

        private static bool IsLive(MonoBehaviour behaviour)
        {
            return behaviour != null && behaviour.isActiveAndEnabled;
        }

        // '컴포넌트' 또는 '컴포넌트.칸' — 칸은 bool(참이면 열림) · GameObject(켜지면 열림) · 컴포넌트(그 오브젝트가 켜지면 열림)
        private void AddWatches(Scene scene, TutorialGuide guide)
        {
            string target = guide.triggerTarget ?? "";
            int dot = target.IndexOf('.');
            string typeName = dot < 0 ? target.Trim() : target.Substring(0, dot).Trim();
            string member = dot < 0 ? "" : target.Substring(dot + 1).Trim();

            foreach (MonoBehaviour behaviour in FindInScene(scene, typeName))
            {
                if (watches.Any(w => w.Target == behaviour && w.Guide == guide)) continue;

                Func<bool> isOpen = BuildGetter(behaviour, member);
                if (isOpen == null)
                {
                    Debug.LogWarning($"[Tutorial] '{guide.title}': {typeName}에서 '{member}' 칸을 찾지 못했거나 bool · GameObject가 아니라 감시하지 않습니다.", behaviour);
                    continue;
                }

                watches.Add(new Watch { Guide = guide, Target = behaviour, IsOpen = isOpen, WasOpen = SafeRead(isOpen) });
            }
        }

        private static Func<bool> BuildGetter(MonoBehaviour behaviour, string member)
        {
            if (string.IsNullOrEmpty(member)) return () => behaviour != null && behaviour.gameObject.activeInHierarchy;

            Type type = behaviour.GetType();
            PropertyInfo property = type.GetProperty(member, AnyInstance);
            FieldInfo field = property == null ? type.GetField(member, AnyInstance) : null;
            Type valueType = property != null ? property.PropertyType : field?.FieldType;
            if (valueType == null) return null;

            Func<object> read = property != null ? (Func<object>)(() => property.GetValue(behaviour)) : () => field.GetValue(behaviour);

            if (valueType == typeof(bool)) return () => behaviour != null && (bool)read();
            if (typeof(GameObject).IsAssignableFrom(valueType)) return () => behaviour != null && read() is GameObject go && go != null && go.activeInHierarchy;
            if (typeof(Component).IsAssignableFrom(valueType)) return () => behaviour != null && read() is Component c && c != null && c.gameObject.activeInHierarchy;
            return null;
        }

        private static bool SafeRead(Func<bool> isOpen)
        {
            try { return isOpen(); }
            catch (Exception) { return false; }
        }

        private void OnEvent(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            foreach (TutorialGuide guide in catalog.guides)
            {
                if (guide != null && guide.trigger == TutorialTrigger.Event && guide.triggerTarget == key) Enqueue(guide, $"신호 '{key}'");
            }
        }

        private void Enqueue(TutorialGuide guide, string reason)
        {
            if (!catalog.autoOpen || guide == null || TutorialPanel.HasSeen(guide.guideId) || guide == current || queue.Contains(guide)) return;

            if (queue.Count == 0) queuedAt = Time.unscaledTime;
            queue.Add(guide);
            Debug.Log($"[Tutorial] '{guide.title}' 대기 — {reason}");
        }

        // ── 진행 ─────────────────────────────

        private void Update()
        {
            HandleKeys();

            for (int i = watches.Count - 1; i >= 0; i--)
            {
                Watch watch = watches[i];
                if (watch.Target == null || TutorialPanel.HasSeen(watch.Guide.guideId))
                {
                    watches.RemoveAt(i);
                    continue;
                }

                bool open = SafeRead(watch.IsOpen);
                if (open && !watch.WasOpen) Enqueue(watch.Guide, $"{watch.Target.GetType().Name} 창 열림");
                watch.WasOpen = open;
            }

            Process();
        }

        private void Process()
        {
            if (state != State.Idle || queue.Count == 0 || panel == null) return;
            if (panel.IsOpen || (help != null && help.IsOpen)) return;

            DialogueManager dialogue = FindManager();
            if (dialogue != null)
            {
                // 씬 입장 직후엔 대화 시스템이 준비될 때까지 잠깐 기다림. 다른 대화 중이면 끝난 뒤에
                if (!dialogue.IsReady && Time.unscaledTime - queuedAt < catalog.waitForDialogueSeconds) return;
                if (dialogue.IsTalking) return;
            }

            current = queue[0];
            queue.RemoveAt(0);
            queuedAt = Time.unscaledTime;

            if (TutorialPanel.HasSeen(current.guideId))
            {
                current = null;
                return;
            }

            // 창이 열려서 걸린 튜토리얼은 안내 대사 없이 가이드 창을 바로 띄움 (대사를 기다리는 동안 늦게 뜨던 문제, BUG-046)
            if (current.trigger != TutorialTrigger.WindowOpened && TryIntro(dialogue, current)) return;
            OpenPanel(current);
        }

        private DialogueManager FindManager()
        {
            if (manager != null && manager.isActiveAndEnabled) return manager;
            manager = FindObjectsByType<DialogueManager>(FindObjectsSortMode.None).FirstOrDefault(m => m.isActiveAndEnabled);
            return manager;
        }

        private bool TryIntro(DialogueManager dialogue, TutorialGuide guide)
        {
            if (string.IsNullOrEmpty(guide.introDialogueId) || dialogue == null || !dialogue.IsReady || dialogue.IsFallback || dialogue.Box == null) return false;

            if (!dialogue.HasDialogue(guide.introDialogueId))
            {
                Debug.LogWarning($"[Tutorial] '{guide.title}': 안내 대사 '{guide.introDialogueId}'가 대사 풀에 없어 화면만 엽니다.", catalog);
                return false;
            }

            // 이미 본 대사(1회) 등으로 못 틀면 화면만
            if (!dialogue.PlayDialogue(guide.introDialogueId)) return false;

            state = State.Intro;
            introBox = dialogue.Box;
            introBox.Closed += OnIntroClosed;
            return true;
        }

        private void OnIntroClosed()
        {
            if (introBox != null) introBox.Closed -= OnIntroClosed;
            introBox = null;
            if (state == State.Intro && current != null) OpenPanel(current);
        }

        private void OpenPanel(TutorialGuide guide)
        {
            state = State.Panel;
            if (panel.Open(guide, OnPanelClosed)) return;

            // 단계가 비어 있는 등으로 못 열면 다시 걸리지 않게 본 것으로 기록
            TutorialPanel.MarkSeen(guide.guideId);
            OnPanelClosed();
        }

        private void OnPanelClosed()
        {
            state = State.Idle;
            current = null;
        }

        // ── 도움말 · 테스트 키 ─────────────────────────────

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || catalog.helpKey == Key.None || !keyboard[catalog.helpKey].wasPressedThisFrame) return;

            // Ctrl + 도움말 키 = 튜토리얼 초기화 (시연 빌드에서도 쓰도록 모든 빌드에서 동작)
            if (keyboard.ctrlKey.isPressed)
            {
                ResetAll();
                return;
            }
            ToggleHelp();
        }

        public void ToggleHelp()
        {
            if (help != null && help.IsOpen)
            {
                help.Close();
                return;
            }

            if (state != State.Idle || panel == null || panel.IsOpen) return;

            if (help == null)
            {
                TMP_Text sample = panel.GetComponentInChildren<TMP_Text>(true);
                help = TutorialHelpList.Create(transform, sample != null ? sample.font : null);
            }

            help.Open(catalog.guides.Where(g => g != null).ToList(), OpenFromHelp);
        }

        private void OpenFromHelp(TutorialGuide guide)
        {
            if (guide == null || panel == null || panel.IsOpen) return;
            state = State.Panel;
            current = guide;
            if (!panel.Open(guide, OnPanelClosed)) OnPanelClosed();
        }

        // 개발용: Ctrl + 도움말 키 = 모든 튜토리얼 본 기록 초기화 후 현재 씬 다시 확인
        public void ResetAll()
        {
            foreach (TutorialGuide guide in catalog.guides)
            {
                if (guide != null) TutorialPanel.ResetSeen(guide.guideId);
            }
            PlayerPrefs.Save();

            // 안내 대사(1회)도 대사 기록에서 지워야 다시 나옴
            DialogueManager dialogue = FindManager();
            string[] introIds = catalog.guides.Where(g => g != null && !string.IsNullOrEmpty(g.introDialogueId)).Select(g => g.introDialogueId).Distinct().ToArray();
            int forgotten = dialogue != null && dialogue.IsReady ? dialogue.ForgetDialogues(introIds) : 0;

            queue.Clear();
            watches.Clear();
            Scan(SceneManager.GetActiveScene());
            Debug.Log($"[Tutorial] 모든 튜토리얼 본 기록 초기화 ({catalog.guides.Count(g => g != null)}종) · 안내 대사 기록 {forgotten}개 지움" +
                      (dialogue == null ? " (이 씬엔 대화 시스템이 없어 안내 대사 기록은 그대로)" : ""));
        }
    }
}
