// RE:AL STEEL - 시간대 전환 키 (빌드 · 플레이 중)
//
// RS 시간대(RSTimeOfDay)를 게임 안에서 키로 바꾼다. 빌드한 실행 파일에서도 그대로 동작한다.
//   T        낮 ↔ 밤 (부드럽게)
//   1 ~ 6    새벽 · 아침 · 한낮 · 오후 · 노을 · 밤
//   [ / ]    한 시간 뒤로 / 앞으로 (누르고 있으면 계속)
//   P        시간 흐름 멈춤 / 재생
//   F1       화면 안내 보이기 / 숨기기
//   게임패드: Select(Back) = 낮 ↔ 밤 · 십자키 좌우 = 이전 / 다음 시간대
// 키는 인스펙터에서 바꿀 수 있다. Input System · 예전 Input Manager 둘 다 동작한다.
// (Input System 을 쓰려면 asmdef 밖이어야 해서 SYS/RSCharacter 에 둔다 — 간단 캐릭터와 같은 어셈블리)
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using RealSteel.Common;

namespace RealSteel.Lighting
{
    [RSSummary("시간대 전환 키", "플레이 · 빌드에서 키로 시간대를 바꾼다.\n" +
        "· T 낮↔밤 · 1~6 새벽/아침/한낮/오후/노을/밤 · [ ] 한 시간씩 · P 흐름 멈춤/재생 · F1 안내\n" +
        "· 게임패드: Select = 낮↔밤, 십자키 좌우 = 이전/다음 시간대\n" +
        "· 간단 캐릭터가 쓰는 키(WASD · Q/E · R/F · +/- · Home · Space · Shift)와 겹치지 않게 골랐다")]
    [DisallowMultipleComponent]
    [AddComponentMenu("RE_AL STEEL/Lighting/시간대 전환 키")]
    public class RSTimeHotkeys : MonoBehaviour
    {
        [Tooltip("바꿀 시간대. 비우면 씬에서 찾는다")]
        public RSTimeOfDay timeOfDay;

        [RSGroup("전환")]
        [RSHelp("키를 누르면 이 시간 동안 부드럽게 넘어간다. 0 = 바로 바뀜.")]
        [Tooltip("낮 ↔ 밤 · 시간대 키로 넘어가는 시간 (초)")]
        [RSKey]
        public float transitionSeconds = 2f;
        [Tooltip("[ ] 를 누르고 있을 때 초당 몇 시간씩 가는지")]
        public float scrubHoursPerSecond = 3f;
        [Tooltip("P 로 재생할 때 하루 길이 (분). 시간대 컴포넌트의 Day Length 가 0 일 때만 쓴다")]
        public float playDayLengthMinutes = 2f;

        [RSGroup("키")]
        [Tooltip("낮 ↔ 밤")]
        [RSKey]
        public KeyCode toggleDayNight = KeyCode.T;
        [Tooltip("새벽 · 아침 · 한낮 · 오후 · 노을 · 밤 (6개)")]
        public KeyCode[] presetKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };
        [Tooltip("한 시간 뒤로 (누르고 있으면 계속)")]
        public KeyCode hourBack = KeyCode.LeftBracket;
        [Tooltip("한 시간 앞으로 (누르고 있으면 계속)")]
        public KeyCode hourForward = KeyCode.RightBracket;
        [Tooltip("시간 흐름 멈춤 / 재생")]
        public KeyCode pauseFlow = KeyCode.P;
        [Tooltip("화면 안내 보이기 / 숨기기")]
        public KeyCode toggleHud = KeyCode.F1;
        [Tooltip("게임패드도 쓴다 (Select = 낮↔밤, 십자키 좌우 = 이전/다음 시간대)")]
        public bool gamepad = true;

        [RSGroup("화면 안내")]
        [Tooltip("시작할 때 안내를 보인다")]
        public bool showHud = true;
        [Tooltip("키를 안 누르면 몇 초 뒤 안내를 접는다 (0 = 계속 보임). 접혀도 시각은 작게 남는다")]
        public float hudAutoHide = 6f;

        static readonly string[] PresetNames = { "새벽", "아침", "한낮", "오후", "노을", "밤" };

        float savedDayLength;
        float lastInputTime;
        float holdTime;
        GUIStyle boxStyle, smallStyle;

        void Start()
        {
            if (timeOfDay == null) timeOfDay = FindAnyObjectByType<RSTimeOfDay>();
            if (timeOfDay == null) Debug.LogWarning("[시간대 전환 키] 씬에 RS 시간대(RSTimeOfDay)가 없습니다.", this);
            savedDayLength = playDayLengthMinutes;
            lastInputTime = Time.unscaledTime;
        }

        void Update()
        {
            var t = timeOfDay;
            if (t == null) return;

            if (Down(toggleHud)) { showHud = !showHud; Touch(); }

            if (Down(toggleDayNight)) { t.ToggleDayNight(transitionSeconds); Touch(); }

            for (int i = 0; i < presetKeys.Length && i < RSTimeOfDay.PresetHours.Length; i++)
                if (Down(presetKeys[i])) { t.TransitionTo(RSTimeOfDay.PresetHours[i], transitionSeconds); Touch(); }

            // 한 시간씩 — 누른 순간 한 칸, 계속 누르면 흘러간다
            int dir = (Held(hourForward) ? 1 : 0) - (Held(hourBack) ? 1 : 0);
            if (Down(hourForward)) { t.SetTime(t.Hour + 1f); holdTime = 0f; Touch(); }
            else if (Down(hourBack)) { t.SetTime(t.Hour - 1f); holdTime = 0f; Touch(); }
            else if (dir != 0)
            {
                holdTime += Time.unscaledDeltaTime;
                if (holdTime > 0.35f) { t.SetTime(t.Hour + dir * scrubHoursPerSecond * Time.unscaledDeltaTime); Touch(); }
            }

            if (Down(pauseFlow))
            {
                if (t.dayLengthMinutes > 0f) { savedDayLength = t.dayLengthMinutes; t.dayLengthMinutes = 0f; }
                else t.dayLengthMinutes = savedDayLength > 0f ? savedDayLength : Mathf.Max(0.1f, playDayLengthMinutes);
                Touch();
            }

            if (gamepad) GamepadInput(t);
        }

        void GamepadInput(RSTimeOfDay t)
        {
#if ENABLE_INPUT_SYSTEM
            var gp = Gamepad.current;
            if (gp == null) return;
            if (gp.selectButton.wasPressedThisFrame) { t.ToggleDayNight(transitionSeconds); Touch(); }
            if (gp.dpad.right.wasPressedThisFrame) { t.TransitionTo(NeighborPreset(t.Hour, +1), transitionSeconds); Touch(); }
            if (gp.dpad.left.wasPressedThisFrame) { t.TransitionTo(NeighborPreset(t.Hour, -1), transitionSeconds); Touch(); }
#endif
        }

        /// <summary>지금 시각 다음(또는 이전) 시간대 프리셋 시각</summary>
        static float NeighborPreset(float hour, int dir)
        {
            var hs = RSTimeOfDay.PresetHours;
            if (dir > 0)
            {
                foreach (var h in hs) if (h > hour + 0.05f) return h;
                return hs[0];
            }
            for (int i = hs.Length - 1; i >= 0; i--) if (hs[i] < hour - 0.05f) return hs[i];
            return hs[hs.Length - 1];
        }

        void Touch() { lastInputTime = Time.unscaledTime; }

        // ─────────────────────────────────────────────────────────────
        // 화면 안내 (빌드에서도 보인다)
        // ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (timeOfDay == null || !showHud) return;
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, richText = true, padding = new RectOffset(10, 10, 8, 8) };
                boxStyle.normal.textColor = Color.white;
                smallStyle = new GUIStyle(boxStyle) { fontSize = 13, padding = new RectOffset(8, 8, 4, 4) };
            }

            float h = timeOfDay.Hour;
            int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f);
            string clock = $"{hh:00}:{mm:00}  {NearestName(h)}{(timeOfDay.dayLengthMinutes > 0f ? "  ▶" : "  ❚❚")}";

            bool collapsed = hudAutoHide > 0f && Time.unscaledTime - lastInputTime > hudAutoHide;
            if (collapsed)
            {
                GUI.Box(new Rect(12, 12, 170, 28), clock, smallStyle);
                return;
            }

            string text = $"<b>{clock}</b>\n" +
                          $"{Name(toggleDayNight)}  낮 ↔ 밤\n" +
                          $"{Name(presetKeys, 0)}~{Name(presetKeys, 5)}  새벽 · 아침 · 한낮 · 오후 · 노을 · 밤\n" +
                          $"{Name(hourBack)} {Name(hourForward)}  한 시간씩 (누르고 있으면 계속)\n" +
                          $"{Name(pauseFlow)}  시간 흐름 멈춤 / 재생\n" +
                          $"{Name(toggleHud)}  이 안내 숨기기";
            GUI.Box(new Rect(12, 12, 330, 132), text, boxStyle);
        }

        static string NearestName(float hour)
        {
            var hs = RSTimeOfDay.PresetHours;
            int best = 0; float bd = 99f;
            for (int i = 0; i < hs.Length; i++)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(hour * 15f, hs[i] * 15f));
                if (d < bd) { bd = d; best = i; }
            }
            return PresetNames[best];
        }

        static string Name(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.None: return "-";
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            return s;
        }

        static string Name(KeyCode[] ks, int i) { return ks != null && i < ks.Length ? Name(ks[i]) : "-"; }

        // ─────────────────────────────────────────────────────────────
        // 입력 (Input System · 예전 Input Manager)
        // ─────────────────────────────────────────────────────────────

        static bool Down(KeyCode k)
        {
            if (k == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && TryKey(k, out Key key)) return kb[key].wasPressedThisFrame;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(k);
#else
            return false;
#endif
        }

        static bool Held(KeyCode k)
        {
            if (k == KeyCode.None) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && TryKey(k, out Key key)) return kb[key].isPressed;
            return false;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(k);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>KeyCode → Input System Key (자주 쓰는 키)</summary>
        static bool TryKey(KeyCode k, out Key key)
        {
            key = Key.None;
            // 숫자 키는 Input System 에서 순서가 1…9, 0 이라 이름으로 찾는다
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return Enum.TryParse("Digit" + (k - KeyCode.Alpha0), out key);
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return Enum.TryParse("Numpad" + (k - KeyCode.Keypad0), out key);
            if (k >= KeyCode.A && k <= KeyCode.Z) { key = Key.A + (k - KeyCode.A); return true; }
            if (k >= KeyCode.F1 && k <= KeyCode.F12) { key = Key.F1 + (k - KeyCode.F1); return true; }
            switch (k)
            {
                case KeyCode.LeftBracket:  key = Key.LeftBracket; return true;
                case KeyCode.RightBracket: key = Key.RightBracket; return true;
                case KeyCode.Comma:        key = Key.Comma; return true;
                case KeyCode.Period:       key = Key.Period; return true;
                case KeyCode.Slash:        key = Key.Slash; return true;
                case KeyCode.Semicolon:    key = Key.Semicolon; return true;
                case KeyCode.Quote:        key = Key.Quote; return true;
                case KeyCode.BackQuote:    key = Key.Backquote; return true;
                case KeyCode.Minus:        key = Key.Minus; return true;
                case KeyCode.Equals:       key = Key.Equals; return true;
                case KeyCode.Space:        key = Key.Space; return true;
                case KeyCode.Tab:          key = Key.Tab; return true;
                case KeyCode.Return:       key = Key.Enter; return true;
                case KeyCode.Backspace:    key = Key.Backspace; return true;
                case KeyCode.Insert:       key = Key.Insert; return true;
                case KeyCode.Delete:       key = Key.Delete; return true;
                case KeyCode.Home:         key = Key.Home; return true;
                case KeyCode.End:          key = Key.End; return true;
                case KeyCode.PageUp:       key = Key.PageUp; return true;
                case KeyCode.PageDown:     key = Key.PageDown; return true;
                case KeyCode.UpArrow:      key = Key.UpArrow; return true;
                case KeyCode.DownArrow:    key = Key.DownArrow; return true;
                case KeyCode.LeftArrow:    key = Key.LeftArrow; return true;
                case KeyCode.RightArrow:   key = Key.RightArrow; return true;
            }
            return Enum.TryParse(k.ToString(), out key);
        }
#endif
    }
}
