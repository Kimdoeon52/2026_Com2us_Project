using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// 대화 JSON 목록. TextAsset으로 참조하므로 모든 플랫폼에서 동일하게 동작하고 빌드에 포함된다.
    ///
    /// StreamingAssets를 쓰지 않는 이유:
    ///  - Android/WebGL에서 동기 File IO 불가
    ///  - 빌드 폴더에 평문 JSON이 노출되어 유저가 수정 → 진행 플래그 조작/크래시 유발 가능
    /// (모드 지원이 필요해지면 그때 외부 폴더 로더를 별도 추가하고, 동일한 Validator를 반드시 통과시킨다)
    /// </summary>
    [CreateAssetMenu(menuName = "RealSteel/Dialogue/Data Manifest", fileName = "DialogueDataManifest")]
    public sealed class DialogueDataManifest : ScriptableObject
    {
        public TextAsset variables;
        public TextAsset speakers;
        public List<TextAsset> dialogueFiles = new List<TextAsset>();
        public List<LanguageTable> stringTables = new List<LanguageTable>();

        [Serializable]
        public sealed class LanguageTable
        {
            public string lang = "ko";
            public TextAsset table;
        }

        public DialogueSourceSet ToSourceSet()
        {
            var set = new DialogueSourceSet
            {
                Variables = ToSource(variables),
                Speakers = ToSource(speakers),
            };
            foreach (var f in dialogueFiles)
                if (f != null) set.DialogueFiles.Add(ToSource(f));
            return set;
        }

        public TextAsset FindTable(string lang)
        {
            foreach (var t in stringTables) if (t.lang == lang) return t.table;
            return stringTables.Count > 0 ? stringTables[0].table : null;
        }

        private static DialogueSource ToSource(TextAsset a) =>
            a == null ? new DialogueSource("(missing)", null) : new DialogueSource(a.name + ".json", a.text);
    }
}
