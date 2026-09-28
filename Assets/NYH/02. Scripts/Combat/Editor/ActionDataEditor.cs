using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ActionData의 FrameBox를 Scene 뷰에서 마우스로 드래그해 맞추는 에디터 도구 (§4, §9).
/// 숫자를 인스펙터에 직접 타이핑하는 대신, 스프라이트를 보면서 박스 중심/모서리를 끌어 맞춘다.
/// "Editor" 폴더 안에 있어 빌드에는 포함되지 않는다 — Unity는 폴더 이름이 정확히 "Editor"면
/// 그 안의 스크립트를 자동으로 에디터 전용으로 취급해서 실제 게임 빌드에서 제외해준다.
///
/// 좌표는 항상 "오른쪽을 본다(facingRight=true)"고 가정한 로컬 좌표로 편집한다.
/// 좌우 반전은 런타임에 BoxResolver 한 곳에서만 하므로, 여기서 왼쪽 기준으로 맞추면 좌표가 꼬인다 (§4).
/// </summary>
// [CustomEditor(typeof(ActionData))]를 붙이면, 프로젝트 창에서 ActionData 에셋을 선택했을 때
// 유니티 기본 인스펙터 대신 이 클래스의 OnInspectorGUI/OnSceneGUI가 대신 호출된다
[CustomEditor(typeof(ActionData))]
public class ActionDataEditor : Editor
{
    // 박스 종류별 색 — BoxDrawer(런타임용)와 같은 색 규칙을 따른다. 알파값(4번째 숫자)을 준 이유는
    // 이 에디터에서는 채우기(반투명 사각형)까지 그리기 때문에, 뒤에 있는 스프라이트가 비쳐 보이게 하려는 것
    private static readonly Color HitColor = new Color(1f, 0f, 0f, 0.9f);
    private static readonly Color HurtColor = new Color(0.4f, 0.8f, 1f, 0.9f);
    private static readonly Color PushColor = new Color(0f, 1f, 0f, 0.9f);

    // 아래 세 필드는 SerializeField가 아니라 그냥 private 필드다 — 즉 이 값들은 ActionData 에셋에
    // 저장되지 않고, 에디터 창을 닫거나 다른 에셋을 선택하면 초기화된다. "박스가 실제로 어디 있는지"가
    // 아니라 "지금 이 에디터를 켜놓고 뭘 보고 있는지"에 대한 값이라 굳이 영구 저장할 필요가 없기 때문
    private Transform previewTarget; // Scene 뷰에서 박스를 그릴 때 기준으로 삼을 로봇의 위치(피벗)
    private int previewFrame = 1;    // "모든 프레임 표시"를 끄면, 이 프레임에 활성화된 박스만 걸러서 보여줌
    private bool showAllFrames = true; // 켜져 있으면 프레임 상관없이 등록된 박스를 전부 보여줌(작업 초반엔 이게 편함)

    // 유니티가 인스펙터 창을 그릴 때마다 호출하는 함수. 여기서 그린 UI가 곧 인스펙터에 보이는 내용이다
    public override void OnInspectorGUI()
    {
        // ActionData 원래 필드들(StartupFrames, Damage 등)은 그대로 기본 방식으로 그려준다 —
        // 이 커스텀 에디터는 "추가"로 박스 편집 UI를 덧붙이는 것뿐이지, 기존 필드 편집 기능을 대체하는 게 아니다
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("박스 미리보기 / 편집 (Scene 뷰)", EditorStyles.boldLabel);

        // BeginChangeCheck~EndChangeCheck 사이에서 그린 컨트롤(위 세 줄) 중 하나라도 값이 바뀌면
        // EndChangeCheck가 true를 반환한다 — "이 중 뭐라도 바뀌었으면 Scene 뷰를 다시 그려라"는 신호로 쓰기 위함.
        // 안 그러면 예를 들어 미리보기 프레임 슬라이더를 옮겨도 Scene 뷰가 바로 안 따라와서 한 박자 늦게 보인다
        EditorGUI.BeginChangeCheck();

        // 씬에 있는 로봇의 Transform을 여기 드래그해서 넣으면, 그 위치를 기준(피벗)으로 박스를 그린다.
        // ActionData는 씬과 무관한 에셋이라 "지금 어디에 그려야 하는지" 자체적으로는 알 방법이 없어서,
        // 사용자가 직접 기준점을 지정해줘야 한다 (안 넣으면 그냥 월드 원점 0,0,0 기준으로 그림)
        previewTarget = (Transform)EditorGUILayout.ObjectField(
            new GUIContent("기준 위치(선택)", "씬의 로봇 Transform을 넣으면 그 현재 위치를 피벗으로 박스를 그린다. 비우면 월드 원점(0,0,0) 기준"),
            previewTarget, typeof(Transform), true);

        // target은 "지금 이 에디터가 보여주고 있는 대상 오브젝트" — 여기선 항상 ActionData 에셋 하나이므로 그대로 캐스팅
        var action = (ActionData)target;
        // 슬라이더 최대값으로 쓸 값. TotalFrames가 0(프레임을 아직 하나도 안 채운 상태)이면 슬라이더 범위가
        // 0~0이 되어 조작이 안 되므로, 최소 1은 되도록 보정한다
        int totalFrames = Mathf.Max(1, action.TotalFrames);

        showAllFrames = EditorGUILayout.Toggle("모든 프레임 표시", showAllFrames);
        // DisabledScope 안에 들어간 컨트롤은 회색으로 비활성화되어 조작이 안 된다.
        // "모든 프레임 표시"가 켜져 있으면 어차피 프레임 슬라이더 값을 안 쓰니, 헷갈리지 않게 아예 잠가둔다
        using (new EditorGUI.DisabledScope(showAllFrames))
        {
            previewFrame = EditorGUILayout.IntSlider("미리보기 프레임", previewFrame, 1, totalFrames);
        }

        if (EditorGUI.EndChangeCheck())
        {
            // 인스펙터 값이 바뀐 걸 Scene 뷰에도 즉시 반영시키기 위해 강제로 다시 그리게 요청한다
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("빠른 추가", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        // 버튼 세 개로 각 타입의 박스를 기본값으로 하나씩 추가한다 — 매번 배열 크기를 수동으로 늘리고
        // 필드를 하나하나 채우는 것보다 훨씬 빠르게 시작할 수 있게 하려는 용도
        if (GUILayout.Button("Hit 추가")) AddBox(BoxType.Hit, action);
        if (GUILayout.Button("Hurt 추가")) AddBox(BoxType.Hurt, action);
        if (GUILayout.Button("Push 추가")) AddBox(BoxType.Push, action);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Scene 뷰에서 네모 핸들(중심)을 끌면 이동, 점 핸들(모서리)을 끌면 크기 조절됩니다.\n" +
            "좌표는 항상 오른쪽을 본다고 가정하고 편집합니다 — 좌우 반전은 런타임에 자동 처리됩니다.",
            MessageType.Info);

        DrawAnchorSection(action);
    }

    // 절대 프레임(startFrame/endFrame)으로 찍힌 박스를 구간 앵커로 바꾸는 변환 UI + 검수 경고.
    // 변환은 startFrame/endFrame을 지우지 않는다 — 앵커만 채운다. 그래서 되돌리려면 anchor를 Legacy로
    // 돌려놓기만 하면 되고, 값이 날아갈 일이 없다
    private void DrawAnchorSection(ActionData action)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("구간 앵커", EditorStyles.boldLabel);

        if (action.TotalFrames <= 0)
        {
            EditorGUILayout.HelpBox("프레임이 0이라 앵커를 계산할 수 없습니다. 이 에셋은 Legacy(절대 프레임) 그대로 두세요.", MessageType.None);
            return;
        }

        if (GUILayout.Button("절대 프레임 → 앵커 변환 (창은 그대로 유지)"))
            ConvertToAnchors(action);

        // 변환과 무관하게 항상 확인할 수 있게, 지금 데이터의 이상한 점을 모아서 보여준다.
        // 이런 건 에러가 안 나고 조용히 틀어지기만 해서, 눈에 띄는 자리에 띄워두지 않으면 영영 안 보인다
        string report = BuildInspectionReport(action);
        if (!string.IsNullOrEmpty(report))
            EditorGUILayout.HelpBox(report, MessageType.Warning);
    }

    // 박스마다 "지금 창이 어느 구간에 가장 많이 걸치는지"를 보고 앵커를 정한다.
    // 창 자체는 바꾸지 않는다 — 변환 전후로 판정이 달라지면 변환이 원인인지 원래 틀렸는지 구분할 수 없게 된다
    private void ConvertToAnchors(ActionData action)
    {
        serializedObject.Update();
        var boxesProp = serializedObject.FindProperty("frameBoxes");

        int startup = action.StartupFrames;
        int active = action.ActiveFrames;
        int recovery = action.RecoveryFrames;
        int total = action.TotalFrames;
        int converted = 0;

        for (int i = 0; i < boxesProp.arraySize; i++)
        {
            var boxProp = boxesProp.GetArrayElementAtIndex(i);
            var anchorProp = boxProp.FindPropertyRelative("anchor");
            if (anchorProp.enumValueIndex != (int)FrameAnchor.Legacy)
                continue; // 이미 변환된 박스는 건드리지 않는다 — 두 번 눌러도 안전하게

            int s = Mathf.Max(1, boxProp.FindPropertyRelative("startFrame").intValue);
            int e = Mathf.Min(total, boxProp.FindPropertyRelative("endFrame").intValue);
            if (e < s)
                continue; // 창이 성립 안 하는 박스는 사람이 보고 고쳐야 한다

            // 앵커는 "창이 시작하는 프레임이 들어있는 구간"으로 정한다.
            // 겹침이 가장 큰 구간을 고르면, 창이 그 구간보다 앞에서 시작할 때 오프셋이 음수가 되고
            // 그걸 0으로 자르면서 창이 통째로 뒤로 밀려버린다 (예: 스트레이트 푸시박스 1~33 → 15~34).
            // 시작 프레임이 속한 구간을 고르면 오프셋이 항상 0 이상이라 원래 창이 그대로 재현된다
            FrameAnchor anchor;
            int phaseStart;
            int phaseLength;

            if (s <= startup)
            {
                anchor = FrameAnchor.Startup;
                phaseStart = 1;
                phaseLength = startup;
            }
            else if (s <= startup + active)
            {
                anchor = FrameAnchor.Active;
                phaseStart = startup + 1;
                phaseLength = active;
            }
            else
            {
                anchor = FrameAnchor.Recovery;
                phaseStart = startup + active + 1;
                phaseLength = recovery;
            }

            int offset = s - phaseStart;
            int length = e - s + 1;

            // 창이 한 구간과 정확히 일치하면 길이를 0(= 구간 끝까지)으로 둔다.
            // 이게 앵커를 쓰는 진짜 이유다 — 그 구간 프레임이 늘거나 줄면 박스도 같이 따라간다
            if (offset == 0 && length == phaseLength)
            {
                length = 0;
            }
            else if (s == 1 && e == total)
            {
                // 행동 전체를 덮는 박스(몸통 허트·푸시)는 구간이 아니라 행동 전체를 따라가야 한다
                anchor = FrameAnchor.WholeAction;
                offset = 0;
                length = 0;
            }

            anchorProp.enumValueIndex = (int)anchor;
            boxProp.FindPropertyRelative("startOffset").intValue = offset;
            boxProp.FindPropertyRelative("length").intValue = length;
            converted++;
        }

        serializedObject.ApplyModifiedProperties();
        Debug.Log($"[ActionDataEditor] {action.ActionName} — 박스 {converted}개를 앵커로 변환함 (startFrame/endFrame은 그대로 남겨둠)");
    }

    // 조용히 틀어지기 쉬운 것들만 모은다 — 컴파일도 되고 게임도 돌아가서 눈으로는 안 보이는 것들
    private static string BuildInspectionReport(ActionData action)
    {
        var lines = new System.Text.StringBuilder();
        int total = action.TotalFrames;
        int activeStart = action.StartupFrames + 1;
        int activeEnd = action.StartupFrames + action.ActiveFrames;

        for (int i = 0; i < action.FrameBoxes.Length; i++)
        {
            FrameBox box = action.FrameBoxes[i];

            if (box.rect.width <= 0f || box.rect.height <= 0f)
                lines.AppendLine($"#{i} {box.type} — 폭/높이가 0 이하라 판정에 절대 안 걸립니다 ({box.rect.width:F2} x {box.rect.height:F2})");

            if (box.anchor != FrameAnchor.Legacy)
                continue; // 아래 둘은 절대 프레임을 쓰는 박스에만 의미가 있다

            if (box.endFrame > total)
                lines.AppendLine($"#{i} {box.type} — endFrame({box.endFrame})이 전체 프레임({total})을 넘습니다");

            if (box.type == BoxType.Hit && (box.startFrame != activeStart || box.endFrame != activeEnd))
                lines.AppendLine($"#{i} Hit — 창({box.startFrame}~{box.endFrame})이 활성 구간({activeStart}~{activeEnd})과 다릅니다");
        }

        return lines.ToString();
    }

    // "Hit/Hurt/Push 추가" 버튼을 눌렀을 때, frameBoxes 배열 끝에 기본값짜리 박스 하나를 더 만들어 넣는다
    private void AddBox(BoxType type, ActionData action)
    {
        // SerializedObject/SerializedProperty를 쓰는 이유: ActionData의 frameBoxes 필드는 private라
        // 코드에서 직접 action.frameBoxes = ... 처럼 못 건드린다. 대신 유니티의 직렬화 시스템을 통해
        // "이 필드를 이렇게 바꿔라"라고 요청하면, Undo(Ctrl+Z) 기록과 에셋 파일 저장까지 자동으로 처리해준다
        serializedObject.Update(); // 에셋의 실제 값을 최신으로 동기화(다른 경로로 값이 바뀌었을 수 있으니 먼저 최신화)
        var boxesProp = serializedObject.FindProperty("frameBoxes");

        // 배열 크기를 하나 늘리면, 새로 생긴 마지막 칸은 일단 모든 필드가 기본값(0, false 등)으로 채워진다.
        // 그래서 아래에서 쓸만한 기본값으로 하나하나 다시 채워준다
        int newIndex = boxesProp.arraySize;
        boxesProp.arraySize++;
        var newBox = boxesProp.GetArrayElementAtIndex(newIndex);
        newBox.FindPropertyRelative("type").enumValueIndex = (int)type;
        newBox.FindPropertyRelative("bodyPart").enumValueIndex = (int)BodyPart.Core; // 일단 코어로 기본값. 필요하면 인스펙터에서 바꾸면 됨
        newBox.FindPropertyRelative("rect").rectValue = new Rect(0f, 0f, 1f, 1f); // 피벗 위치에서 오른쪽·위로 1x1 크기 — Scene 뷰에서 바로 보일 정도의 크기
        // 새 박스는 처음부터 앵커로 만든다 — Hit은 활성 구간 전체, Hurt/Push는 행동 전체가 기본값이다 (§4).
        // Length 0이라 나중에 프레임이 바뀌어도 알아서 따라간다
        newBox.FindPropertyRelative("anchor").enumValueIndex =
            (int)(type == BoxType.Hit ? FrameAnchor.Active : FrameAnchor.WholeAction);
        newBox.FindPropertyRelative("startOffset").intValue = 0;
        newBox.FindPropertyRelative("length").intValue = 0;
        // 아래 둘은 Legacy 박스에서만 읽히는 값이라 지금은 안 쓰이지만, 값이 비어 보이지 않게 채워둔다
        newBox.FindPropertyRelative("startFrame").intValue = 1;
        newBox.FindPropertyRelative("endFrame").intValue = Mathf.Max(1, action.TotalFrames);

        // 지금까지 SerializedProperty로 바꾼 값들을 실제 에셋에 반영(저장)한다 — 이 호출 전까지는 메모리상 값만 바뀐 상태
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll(); // 새로 추가된 박스가 바로 보이도록 Scene 뷰 갱신 요청
    }

    // 유니티가 "지금 이 오브젝트(ActionData 에셋)가 선택된 상태에서 Scene 뷰가 그려질 때"마다 자동으로 호출하는 함수.
    // OnInspectorGUI가 "인스펙터 창"을 그린다면, 이건 "Scene 뷰" 위에 뭔가를 겹쳐 그리는 용도다
    private void OnSceneGUI()
    {
        var boxesProp = serializedObject.FindProperty("frameBoxes");
        if (boxesProp == null) return; // 이론상 항상 있어야 하지만, 혹시 필드명이 바뀌는 등의 상황에 대비한 방어 코드

        // 기준 위치가 없으면(아직 로봇 Transform을 안 넣었으면) 월드 원점을 기준으로 그린다
        Vector3 pivot = previewTarget != null ? previewTarget.position : Vector3.zero;

        // 앵커 박스의 실제 창을 계산하려면 이 에셋의 프레임 값이 필요하다
        var action = (ActionData)target;

        serializedObject.Update();

        // 유니티 Handles는 기본적으로 zTest(깊이 비교)가 걸려있어서, 바닥이나 캐릭터 스프라이트 같은
        // 3D 오브젝트 뒤에 있으면 그려놓고도 화면에 안 나타난다(가려짐). 판정 박스는 캐릭터 몸 바로
        // 위·근처에 그려지기 때문에 이 문제에 자주 걸린다 — 그래서 항상 맨 위에 그려지도록 강제로 풀어준다.
        // 다른 도구(이동/회전 기즈모 등)에 영향 안 주려고, 끝나면 반드시 원래 값으로 되돌린다 (prevZTest에 잠깐 보관)
        CompareFunction prevZTest = Handles.zTest;
        Handles.zTest = CompareFunction.Always;

        // 기준 위치(피벗)를 눈으로 바로 확인할 수 있게 노란 십자가를 그려둔다.
        // 박스가 안 보일 때 "애초에 기준점을 어디로 잡고 있는지" 자체를 확인하는 용도의 디버그 마커
        Handles.color = Color.yellow;
        float crossSize = HandleUtility.GetHandleSize(pivot) * 0.15f;
        Handles.DrawLine(pivot + Vector3.left * crossSize, pivot + Vector3.right * crossSize);
        Handles.DrawLine(pivot + Vector3.up * crossSize, pivot + Vector3.down * crossSize);

        for (int i = 0; i < boxesProp.arraySize; i++)
        {
            SerializedProperty boxProp = boxesProp.GetArrayElementAtIndex(i);
            SerializedProperty rectProp = boxProp.FindPropertyRelative("rect");
            SerializedProperty typeProp = boxProp.FindPropertyRelative("type");
            // "모든 프레임 표시"가 꺼져 있으면, 지금 슬라이더로 고른 프레임 구간 밖의 박스는 건너뛰어서
            // 화면이 복잡해지지 않게 한다 (특정 프레임 하나만 정밀하게 맞추고 싶을 때 유용).
            // 창은 startFrame/endFrame을 직접 읽지 않고 런타임과 같은 ResolvedAction.GetWindow로 구한다 —
            // 앵커를 쓰는 박스는 그 두 칸이 안 읽히므로, 직접 읽으면 Scene 뷰가 실제 판정과 다른 걸 보여준다 (§4)
            if (!showAllFrames)
            {
                ResolvedAction.GetWindow(action.FrameBoxes[i], action.StartupFrames, action.ActiveFrames, action.RecoveryFrames,
                    out int boxStart, out int boxEnd);
                if (previewFrame < boxStart || previewFrame > boxEnd)
                    continue;
            }

            DrawBoxHandle(i, pivot, rectProp, (BoxType)typeProp.enumValueIndex);
        }

        // 이 함수 시작할 때 걸어둔 zTest 강제 설정을 원래대로 복구 — 다른 에디터 도구(이동 기즈모 등)가 이후에도 정상 동작하도록
        Handles.zTest = prevZTest;

        // 핸들을 드래그해서 rectProp.rectValue를 바꾼 게 있다면, 여기서 실제 에셋 파일에 반영(저장)된다
        serializedObject.ApplyModifiedProperties();
    }

    // 박스 하나를 그리고, 중심/모서리 핸들 드래그를 rect 값으로 되돌려 쓴다.
    // 항상 facingRight=true 기준 로컬 좌표로 편집 — BoxResolver의 변환 공식과 반드시 짝을 맞춰야 한다 (§4).
    private void DrawBoxHandle(int index, Vector3 pivot, SerializedProperty rectProp, BoxType type)
    {
        // 지금 저장된 로컬 rect 값을 읽어온다 — 이후 계산은 전부 이 r을 기준으로 하다가, 바뀌면 마지막에 다시 rectProp에 씀
        Rect r = rectProp.rectValue;

        // 로컬 rect(x, y, width, height)를 오른쪽을 본다고 가정한 월드 좌표로 변환한다.
        // BoxResolver.ToWorldRect의 facingRight=true 분기와 정확히 같은 계산식이다 — 여기서 다른 공식을 쓰면
        // "에디터에서 맞춘 위치"와 "실제 게임에서 판정되는 위치"가 어긋나버리므로 반드시 같은 식을 써야 한다
        Vector3 worldMin = new Vector3(pivot.x + r.x, pivot.y + r.y, pivot.z);                     // 좌하단 모서리
        Vector3 worldMax = new Vector3(pivot.x + r.x + r.width, pivot.y + r.y + r.height, pivot.z); // 우상단 모서리
        Vector3 worldCenter = (worldMin + worldMax) * 0.5f;                                         // 중심점(핸들 드래그로 이동시킬 때 씀)

        Color c = ColorFor(type);
        // 사각형 테두리 + 반투명 채우기를 그려서 박스의 실제 범위가 한눈에 보이게 한다.
        // 네 꼭짓점을 시계 방향(또는 반시계 방향) 순서로 넘겨야 사각형이 꼬이지 않고 제대로 채워진다
        Handles.DrawSolidRectangleWithOutline(
            new[]
            {
                new Vector3(worldMin.x, worldMin.y, pivot.z),
                new Vector3(worldMax.x, worldMin.y, pivot.z),
                new Vector3(worldMax.x, worldMax.y, pivot.z),
                new Vector3(worldMin.x, worldMax.y, pivot.z)
            },
            new Color(c.r, c.g, c.b, 0.15f), c); // 채우기는 아주 옅게(0.15), 테두리는 원래 색 그대로

        // 박스 여러 개가 겹쳐 있을 때 "이게 몇 번째 박스이고 무슨 타입인지" 바로 구분할 수 있게 라벨을 띄운다
        Handles.Label(worldMax, $"[{index}] {type}");

        Handles.color = c;
        // 핸들(드래그 가능한 점/네모)의 화면상 크기를 카메라와의 거리에 비례해서 정한다.
        // 고정 크기로 두면 카메라를 멀리서 볼 때는 너무 작아서 클릭하기 힘들고, 가까이서 볼 땐 너무 커진다
        float handleSize = HandleUtility.GetHandleSize(worldCenter) * 0.06f;

        // --- 여기서부터 핸들 3개(중심 1개 + 모서리 2개)를 각각 그린다 ---
        // 패턴은 동일: BeginChangeCheck로 "드래그 전" 상태를 기록 → FreeMoveHandle이 마우스 드래그를 처리하고
        // 새 위치를 돌려줌 → EndChangeCheck로 "정말 움직였는지" 확인 → 움직였으면 그 결과로 r(로컬 rect)을 다시 계산

        // 중심 핸들 — 드래그하면 크기(width/height)는 그대로 두고 위치(x, y)만 옮긴다.
        // "전체를 통째로 이동"시키고 싶을 때 크기까지 같이 흔들리면 불편하므로 위치만 바꾸게 만든 것
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.FreeMoveHandle(worldCenter, handleSize, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 delta = newCenter - worldCenter; // 마우스로 옮긴 만큼의 월드 이동량
            r.x += delta.x;
            r.y += delta.y;
            rectProp.rectValue = r;
            // 한 틱(한 번의 OnSceneGUI 호출)에 핸들 하나만 반응하게 하려고 여기서 바로 리턴한다.
            // 안 그러면 이론상 같은 프레임에 다른 핸들도 동시에 처리하려다 값이 꼬일 수 있음
            return;
        }

        // 최소 모서리(좌하단) 핸들 — 이 점을 옮기면 반대쪽(최대 모서리)은 제자리에 고정한 채 크기만 바뀐다.
        // 즉 "왼쪽 벽을 밀어서 박스를 넓히거나 좁히는" 느낌의 조작
        EditorGUI.BeginChangeCheck();
        Vector3 newMin = Handles.FreeMoveHandle(worldMin, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            // 최대 모서리는 고정시켜야 하므로, 계산 전에 그 절대 위치를 먼저 구해서 기억해둔다
            float fixedMaxX = r.x + r.width;
            float fixedMaxY = r.y + r.height;
            r.x = newMin.x - pivot.x; // 새 최소 모서리의 로컬 좌표
            r.y = newMin.y - pivot.y;
            r.width = fixedMaxX - r.x;  // 고정해둔 최대 모서리 기준으로 너비를 역산
            r.height = fixedMaxY - r.y;
            rectProp.rectValue = r;
            return;
        }

        // 최대 모서리(우상단) 핸들 — 반대로 최소 모서리(r.x, r.y)는 그대로 두고 width/height만 새로 계산한다.
        // 최소 모서리를 안 건드리니 fixedMin을 따로 구할 필요 없이 바로 계산 가능
        EditorGUI.BeginChangeCheck();
        Vector3 newMax = Handles.FreeMoveHandle(worldMax, handleSize, Vector3.zero, Handles.DotHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            r.width = newMax.x - pivot.x - r.x;
            r.height = newMax.y - pivot.y - r.y;
            rectProp.rectValue = r;
        }
    }

    // 박스 타입 → 색 매핑. BoxDrawer(런타임용)에도 같은 규칙이 따로 있는데, 두 곳이 서로 다른 값을 쓰면
    // "Scene 뷰에서 편집할 때 본 색"과 "플레이 중 본 색"이 달라져서 헷갈리니 값을 바꿀 땐 두 파일 다 같이 맞춰야 한다
    private static Color ColorFor(BoxType type)
    {
        switch (type)
        {
            case BoxType.Hit: return HitColor;
            case BoxType.Hurt: return HurtColor;
            case BoxType.Push: return PushColor;
            default: return Color.white;
        }
    }
}
