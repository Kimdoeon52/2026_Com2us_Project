// RE:AL STEEL - 시야 가림 투명 인스펙터
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using RealSteel.Common.EditorTools;

[CustomEditor(typeof(RSSeeThrough))]
public class RSSeeThroughEditor : Editor
{
    public override void OnInspectorGUI()
    {
        RSHelpGUI.DrawSummary(target);
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("이 기능을 못 받는 머티리얼 찾기 (콘솔에 목록)"))
            ReportUnsupported();

        if (Application.isPlaying)
        {
            var s = (RSSeeThrough)target;
            EditorGUILayout.LabelField("지금 세기", s.CurrentStrength.ToString("0.00"));
            Repaint();
        }
    }

    static void ReportUnsupported()
    {
        var bad = new Dictionary<Material, List<string>>();
        int off = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || m.shader == null) continue;
                if (m.shader.name.StartsWith("RE_AL STEEL/"))
                {
                    if (m.HasProperty("_SeeThrough") && m.GetFloat("_SeeThrough") < 0.5f) off++;
                    continue;
                }
                if (!bad.TryGetValue(m, out var list)) bad[m] = list = new List<string>();
                if (list.Count < 5) list.Add(r.name);
            }
        }

        if (bad.Count == 0)
        {
            Debug.Log("[시야 가림 투명] 씬의 메시는 전부 RS 셰이더를 씁니다." +
                      (off > 0 ? $" (그 중 '시야 가리면 뚫기'를 끈 머티리얼 사용처 {off}곳)" : ""));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[시야 가림 투명] RS 셰이더가 아니라서 안 뚫리는 머티리얼 {bad.Count}개 — 캐릭터를 가릴 만한 큰 물체면 RS 트라이플래너 등으로 바꾸세요.");
        foreach (var kv in bad)
            sb.AppendLine($"  · {kv.Key.name}  ({kv.Key.shader.name})  ← {string.Join(", ", kv.Value)}{(kv.Value.Count >= 5 ? " …" : "")}");
        Debug.Log(sb.ToString());
    }
}
