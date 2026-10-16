// RE:AL STEEL - SYS 폴더 경로 한곳 정리 (에디터)
//
// 도구가 새 에셋(머티리얼 · 프리셋 · LUT 등)을 만들 때 쓰는 폴더. 폴더 구조를 바꾸면 여기만 고치면 된다.
// 루트는 RSCommon.Runtime.asmdef 위치에서 찾으므로, SYS 폴더 이름이나 위치가 바뀌어도 따라간다.
//
// 10-16 정리: 도구(코드 · 셰이더)는 SYS/RS 아래, 아트 에셋은 SYS 바로 아래
//   SYS/RS                도구 루트 (ToolsRoot) — Common · Terrain · Lighting · Water · Build · Character · StageSetup
//   SYS/RS/Shaders        셰이더 (.shader)
//   SYS/RS/Shaders/Include 공용 셰이더 코드 (.hlsl)
//   SYS/Materials         머티리얼 (도구가 만든 것도 여기)
//   SYS/Textures          텍스처
//   SYS/Prefabs           프리팹
//   SYS/Presets           색감 프리셋 · LUT · 스테이지 룩
//   SYS/Data              브러시 데이터 · 표면 세트 등
using System.IO;
using UnityEditor;

namespace RealSteel.Common.EditorTools
{
    public static class RSPaths
    {
        const string Fallback = "Assets/SYS";
        static string root, toolsRoot;

        /// <summary>SYS 폴더 (예: Assets/SYS) — 아트 에셋 · 데이터 · 프리셋이 있는 곳</summary>
        public static string Root { get { Resolve(); return root; } }

        /// <summary>도구 폴더 (예: Assets/SYS/RS) — 코드 · 셰이더가 있는 곳</summary>
        public static string ToolsRoot { get { Resolve(); return toolsRoot; } }

        static void Resolve()
        {
            if (root != null && AssetDatabase.IsValidFolder(root) && AssetDatabase.IsValidFolder(toolsRoot)) return;
            root = Fallback;
            toolsRoot = Fallback + "/RS";
            foreach (var g in AssetDatabase.FindAssets("RSCommon.Runtime t:AssemblyDefinitionAsset"))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileName(p) != "RSCommon.Runtime.asmdef") continue;
                // SYS/RS/Common/RSCommon.Runtime.asmdef → 도구 루트 = SYS/RS, SYS 루트 = 그 위
                // (RS 폴더 이름을 바꿔도 따라감. 예전 구조 SYS/RSCommon 이면 둘 다 SYS)
                string common = Path.GetDirectoryName(p).Replace('\\', '/');
                string tools = Path.GetDirectoryName(common).Replace('\\', '/');
                toolsRoot = tools;
                root = Path.GetFileName(common) == "RSCommon"
                    ? tools
                    : Path.GetDirectoryName(tools).Replace('\\', '/');
                break;
            }
        }

        public static string Shaders => ToolsRoot + "/Shaders";
        public static string Materials => Root + "/Materials";
        public static string Textures => Root + "/Textures";
        public static string Presets => Root + "/Presets";
        public static string ColorGradePresets => Presets + "/ColorGrade";
        public static string Luts => Presets + "/LUT";
        public static string Prefabs => Root + "/Prefabs";
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
