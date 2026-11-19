using System.Reflection;
using Michsky.UI.Heat;
using UnityEngine;

namespace RealSteel.MainMenu
{
    // 1920×1080 기준 UI가 작은 해상도에서 깨져서(BUG-050), Heat GraphicsManager가 만든 해상도 목록에서 최소값보다 작은 항목을 숨김
    // Heat 코드는 수정하지 않고 목록만 다시 그림. 모니터가 최소값보다 작으면 아무것도 숨기지 않음
    public static class ResolutionLimit
    {
        public const int MinWidth = 1600;
        public const int MinHeight = 900;
        private const string DropdownField = "resolutionDropdown";

        public static bool IsAllowed(int width, int height) => width >= MinWidth && height >= MinHeight;

        public static void Apply(Object context)
        {
            Resolution[] resolutions = Screen.resolutions;
            int best = LargestAllowed(resolutions);
            if (best < 0)
            {
                Debug.LogWarning($"[Resolution] 모니터가 {MinWidth}×{MinHeight}보다 작아 해상도 목록을 그대로 둡니다.", context);
                return;
            }

            Dropdown dropdown = FindDropdown();
            if (dropdown != null) HideSmall(dropdown, resolutions);
            else Debug.LogWarning("[Resolution] Heat GraphicsManager의 해상도 목록을 찾지 못해 목록은 거르지 않습니다.", context);

            if (!IsAllowed(Screen.width, Screen.height))
            {
                Resolution target = resolutions[best];
                Debug.Log($"[Resolution] 현재 {Screen.width}×{Screen.height}는 최소 {MinWidth}×{MinHeight}보다 작아 {target.width}×{target.height}로 바꿉니다.", context);
#if !UNITY_EDITOR
                Screen.SetResolution(target.width, target.height, Screen.fullScreenMode);
#endif
                if (dropdown != null) dropdown.selectedItemIndex = best;
            }

            if (dropdown != null) dropdown.Initialize();
        }

        private static int LargestAllowed(Resolution[] resolutions)
        {
            int best = -1;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (!IsAllowed(resolutions[i].width, resolutions[i].height)) continue;
                if (best < 0 || resolutions[i].width * resolutions[i].height >= resolutions[best].width * resolutions[best].height) best = i;
            }
            return best;
        }

        // GraphicsManager는 목록 번호 = Screen.resolutions 번호로 만듦
        private static void HideSmall(Dropdown dropdown, Resolution[] resolutions)
        {
            int count = Mathf.Min(dropdown.items.Count, resolutions.Length);
            int hidden = 0;
            for (int i = 0; i < count; i++)
            {
                if (IsAllowed(resolutions[i].width, resolutions[i].height) || dropdown.items[i].isInvisible) continue;
                dropdown.items[i].isInvisible = true;
                hidden++;
            }
            if (hidden > 0) Debug.Log($"[Resolution] {MinWidth}×{MinHeight}보다 작은 해상도 {hidden}개를 목록에서 숨김");
        }

        private static Dropdown FindDropdown()
        {
            GraphicsManager graphics = Object.FindAnyObjectByType<GraphicsManager>();
            if (graphics == null) return null;

            FieldInfo field = typeof(GraphicsManager).GetField(DropdownField, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(graphics) as Dropdown : null;
        }
    }
}
