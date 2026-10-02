// RE:AL STEEL - SYS 폴더 경로 한곳 정리 (에디터)
//
// 도구가 새 에셋(머티리얼 · 프리셋 · LUT 등)을 만들 때 쓰는 폴더. 폴더 구조를 바꾸면 여기만 고치면 된다.
// 루트는 RSCommon.Runtime.asmdef 위치에서 찾으므로, SYS 폴더 이름이나 위치가 바뀌어도 따라간다.
//
//   SYS/Shaders           셰이더 (.shader)
//   SYS/Shaders/Include   공용 셰이더 코드 (.hlsl)
//   SYS/Materials         머티리얼 (도구가 만든 것도 여기)
//   SYS/Textures          텍스처
//   SYS/Presets           색감 프리셋 · LUT
//   SYS/Data              브러시 데이터 등 씬별 데이터
using System.IO;
using UnityEditor;

namespace RealSteel.Common.EditorTools
{
    public static class RSPaths
    {
        const string Fallback = "Assets/SYS";
        static string root;

        /// <summary>SYS 폴더 (예: Assets/SYS)</summary>
        public static string Root
        {
            get
            {
                if (root != null && AssetDatabase.IsValidFolder(root)) return root;
                root = Fallback;
                foreach (var g in AssetDatabase.FindAssets("RSCommon.Runtime t:AssemblyDefinitionAsset"))
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileName(p) != "RSCommon.Runtime.asmdef") continue;
                    root = Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/');   // SYS/RSCommon/x.asmdef → SYS
                    break;
                }
                return root;
            }
        }

        public static string Shaders => Root + "/Shaders";
        public static string Materials => Root + "/Materials";
        public static string Textures => Root + "/Textures";
        public static string Presets => Root + "/Presets";
        public static string ColorGradePresets => Presets + "/ColorGrade";
        public static string Luts => Presets + "/LUT";
        public static string Data => Root + "/Data";

        /// <summary>폴더가 없으면 만든다 (여러 단계 한 번에)</summary>
        public static string Ensure(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            return folder;
        }
    }
}
