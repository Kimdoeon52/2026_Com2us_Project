// RE:AL STEEL - RS Lighting 공용 시계
//
// 플레이 중에는 각 컴포넌트가 Update 에서 Time.deltaTime 으로 움직인다.
// 에디터(플레이 아님)에서는 Update 가 매 프레임 불리지 않으므로, 여기서 EditorApplication.update 로
// "에디터에서도 움직이기" 가 켜진 컴포넌트만 돌려주고 씬 뷰 · 게임 뷰를 다시 그린다.
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Lighting
{
    public interface IRSEditorAnimated
    {
        /// <summary>에디터(플레이 아님)에서 매 프레임 움직여야 하는지</summary>
        bool WantsEditorAnimation { get; }
        void EditorTick(float dt);
    }

    public static class RSLightingClock
    {
#if UNITY_EDITOR
        static readonly List<IRSEditorAnimated> items = new List<IRSEditorAnimated>();
        static bool hooked;
        static double last, lastRepaint;

        public static void Register(IRSEditorAnimated a)
        {
            if (!items.Contains(a)) items.Add(a);
            if (hooked) return;
            hooked = true;
            last = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update += Tick;
        }

        public static void Unregister(IRSEditorAnimated a) { items.Remove(a); }

        static void Tick()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = Mathf.Min(0.1f, (float)(now - last));
            last = now;
            if (Application.isPlaying) return;

            bool any = false;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var a = items[i];
                var o = a as Object;
                if (a == null || o == null) { items.RemoveAt(i); continue; }
                if (!a.WantsEditorAnimation) continue;
                a.EditorTick(dt);
                any = true;
            }

            // 30fps 정도로만 다시 그린다
            if (any && now - lastRepaint > 1.0 / 30.0)
            {
                lastRepaint = now;
                // 씬 뷰 · 게임 뷰만 다시 그린다 (플레이어 루프를 돌리면 모든 스크립트 Update 가 불려 무거워진다)
                UnityEditor.SceneView.RepaintAll();
                RepaintGameViews();
            }
        }
        static System.Type gameViewType;
        static void RepaintGameViews()
        {
            if (gameViewType == null) gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null) return;
            foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType))
                (w as UnityEditor.EditorWindow)?.Repaint();
        }
#else
        public static void Register(IRSEditorAnimated a) { }
        public static void Unregister(IRSEditorAnimated a) { }
#endif
    }
}
