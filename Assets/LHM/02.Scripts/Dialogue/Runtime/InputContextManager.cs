using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace RealSteel.Dialogue.Unity
{
    public enum InputContext { Gameplay, Dialogue, Menu, Cutscene }

    /// <summary>
    /// 입력 충돌 방지의 핵심. "지금 입력을 누가 가져가는가"를 스택 하나로 관리한다.
    ///
    /// 문제                                   | 해결
    /// ---------------------------------------|---------------------------------------------------
    /// 대화 확인(Space)이 회피(Space)로도 먹힘 | 컨텍스트별 Action Map을 배타적으로 활성화
    /// 마지막 대사를 넘긴 Space가 대화 종료    | Gameplay 복귀 시 "릴리즈 가드": 가드 대상 버튼이
    ///   직후 프레임에 회피로 이어짐           |   전부 떼어질 때까지 Gameplay 맵 활성화 보류
    /// 일시정지 메뉴 ↔ 대화가 겹칠 때 Pop 꼬임 | Push가 토큰을 반환, Pop은 토큰으로만 (순서 무관)
    /// 레거시 Input.GetKeyDown 직접 사용       | 금지. 불가피하면 GameplayAllowed로 게이트
    ///
    /// Action Map 구성(.inputactions):
    ///   Gameplay : Move(←/→), Jump(↑), Dodge(Space), Attack(D), Skill1~4(Q/W/E/R), Interact(↓), Pause(Esc)
    ///   Dialogue : Confirm(Space/Enter/D/좌클릭), FastForward(LeftCtrl 홀드), Auto(A), Skip(Esc), Backlog(L/휠업), Cancel(Backspace/우클릭)
    ///   Menu     : UI 내비게이션
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class InputContextManager : MonoBehaviour
    {
        public static InputContextManager Instance { get; private set; }

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string gameplayMap = "Gameplay";
        [SerializeField] private string dialogueMap = "Dialogue";
        [SerializeField] private string menuMap = "Menu";

        [Tooltip("Gameplay 복귀 시 떼어질 때까지 기다릴 액션 (이동처럼 '누르고 있는 게 자연스러운' 액션은 제외)")]
        [SerializeField] private string[] releaseGuardedActions = { "Jump", "Dodge", "Attack", "Skill1", "Skill2", "Skill3", "Skill4", "Interact" };

        [Tooltip("키가 끼어 눌린 상태로 남아도 소프트락되지 않도록 최대 대기 시간")]
        [SerializeField] private float releaseGuardMaxSec = 0.6f;

        private readonly List<(int token, InputContext ctx)> _stack = new List<(int, InputContext)>();
        private int _nextToken = 1;

        private InputActionMap _gameplay, _dialogue, _menu;
        private readonly List<InputAction> _guarded = new List<InputAction>();

        private bool _releaseGuardActive;
        private float _releaseGuardStart;
        private int _releaseGuardFrame;

        public InputContext Current => _stack.Count == 0 ? InputContext.Gameplay : _stack[_stack.Count - 1].ctx;

        /// <summary>플레이어 컨트롤러가 확인. 대화/메뉴 중이거나 릴리즈 가드 중이면 false.</summary>
        public bool GameplayAllowed => Current == InputContext.Gameplay && !_releaseGuardActive;

        public event Action<InputContext> ContextChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _gameplay = actions.FindActionMap(gameplayMap, throwIfNotFound: true);
            _dialogue = actions.FindActionMap(dialogueMap, throwIfNotFound: true);
            _menu = actions.FindActionMap(menuMap, throwIfNotFound: false);

            foreach (var name in releaseGuardedActions)
            {
                var a = _gameplay.FindAction(name, throwIfNotFound: false);
                if (a != null) _guarded.Add(a);
                else Debug.LogWarning($"[InputContext] 릴리즈 가드 대상 액션 '{name}'을 {gameplayMap} 맵에서 찾지 못했습니다.", this);
            }
            Apply(previous: InputContext.Gameplay);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public int Push(InputContext ctx)
        {
            var prev = Current;
            int token = _nextToken++;
            _stack.Add((token, ctx));
            Apply(prev);
            return token;
        }

        public void Pop(int token)
        {
            int idx = _stack.FindIndex(x => x.token == token);
            if (idx < 0) return; // 중복 Pop 무해
            var prev = Current;
            _stack.RemoveAt(idx);
            Apply(prev);
        }

        /// <summary>씬 전환 등 비상시. 모든 컨텍스트 제거 후 Gameplay로.</summary>
        public void ResetToGameplay()
        {
            var prev = Current;
            _stack.Clear();
            Apply(prev);
        }

        private void Apply(InputContext previous)
        {
            var cur = Current;
            _gameplay.Disable();
            _dialogue.Disable();
            _menu?.Disable();

            switch (cur)
            {
                case InputContext.Gameplay:
                    if (previous != InputContext.Gameplay) BeginReleaseGuard();
                    else _gameplay.Enable();
                    break;
                case InputContext.Dialogue:
                    _releaseGuardActive = false;
                    _dialogue.Enable();
                    break;
                case InputContext.Menu:
                    _releaseGuardActive = false;
                    _menu?.Enable();
                    break;
                case InputContext.Cutscene:
                    _releaseGuardActive = false;
                    break; // 아무 맵도 켜지 않음 (스킵 버튼은 Dialogue 맵을 쓰는 컷신이면 Dialogue로 Push)
            }

            if (cur != previous) ContextChanged?.Invoke(cur);
        }

        private void BeginReleaseGuard()
        {
            _releaseGuardActive = true;
            _releaseGuardStart = Time.unscaledTime;
            _releaseGuardFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!_releaseGuardActive) return;

            bool minFramePassed = Time.frameCount > _releaseGuardFrame;
            bool timedOut = Time.unscaledTime - _releaseGuardStart > releaseGuardMaxSec;
            if (minFramePassed && (!AnyGuardedHeld() || timedOut))
            {
                _releaseGuardActive = false;
                if (Current == InputContext.Gameplay) _gameplay.Enable();
            }
        }

        /// <summary>비활성 맵이라도 바인딩된 컨트롤의 물리 상태는 읽을 수 있다.</summary>
        private bool AnyGuardedHeld()
        {
            foreach (var a in _guarded)
                foreach (var c in a.controls)
                    if (c is ButtonControl b && b.isPressed) return true;
            return false;
        }
    }
}
