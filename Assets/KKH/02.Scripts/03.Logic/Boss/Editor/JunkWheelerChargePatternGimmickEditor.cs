#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RealSteel.Boss.JunkWheeler;

namespace RealSteel.Boss.Editor
{
    /// <summary>
    /// [정크 휠러 돌진 기믹 전용 커스텀 인스펙터 에디터]
    /// 인스펙터 상에서 전조 점멸(OnTellStarted / OnTellEnded) 및 기믹 액션을 원클릭으로 테스트/제어할 수 있도록 GUI 버튼 제공.
    /// </summary>
    [CustomEditor(typeof(JunkWheelerChargePatternGimmick))]
    public class JunkWheelerChargePatternGimmickEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 필드 출력
            DrawDefaultInspector();

            var gimmick = (JunkWheelerChargePatternGimmick)target;

            GUILayout.Space(12);
            EditorGUILayout.LabelField("[인스펙터 실시간 전조 & 점멸 제어]", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("OnTellStarted() 실행 (점멸 ON)", GUILayout.Height(30)))
            {
                gimmick.OnTellStarted();
            }

            GUI.backgroundColor = new Color(0.6f, 0.9f, 1f, 1f);
            if (GUILayout.Button("OnTellEnded() 실행 (점멸 OFF)", GUILayout.Height(30)))
            {
                gimmick.OnTellEnded();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.HelpBox("플레이 모드에서 위의 버튼을 클릭하여 인스펙터에서 즉시 전조 점멸 및 진동을 테스트할 수 있습니다.\n컴포넌트 헤더 우클릭(Context Menu)으로도 실행 가능합니다.", MessageType.Info);

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
