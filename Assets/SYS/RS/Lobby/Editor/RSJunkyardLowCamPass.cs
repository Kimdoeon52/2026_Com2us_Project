// RE:AL STEEL - 고물상 정면 저각 배치 (한 번 실행용 · 기준_고물상_레벨디자인_톤앤매너 v0.3)
//
// 메뉴: Tools → RE_AL STEEL → Stage → 고물상 정면 저각 배치 (Map_Test, 한 번)
//  · 카메라를 정면(Yaw 0) · 가장 낮은 각도(Pitch 12)로 고정한다는 전제로 아트 배치만 바꾼다. 기능은 안 건드린다
//  · Ctrl+Z 로 한 번에 되돌린다. 다 쓰면 이 파일은 지워도 된다
//
// 이 각도에서 생기는 일 (FOV 24 · 거리 12 · 바라보는 높이 1.2 기준)
//  · 화면 위 가장자리가 정확히 수평이다 → 캐릭터 발 기준 3.7 m 보다 높은 것은 어디에 있든 화면에 안 나온다
//  · 그래서 윗단 2단(4.2 m) 위 · 2단 절벽 뒤에 있는 소품은 절대 안 보인다 → 숨김 묶음으로 옮겨 끈다 (지우지 않음)
//  · 화면 아래 가장자리는 캐릭터보다 3.5 m 앞 땅, 카메라는 캐릭터 뒤 11.7 m → 맵 남쪽 끝 밖이 보인다
//
// 하는 일
//  1. 씬 카메라(CAM_Stage)를 Pitch 12 · Yaw 0 · 거리 12 로 (플레이하면 이 각도로 시작. 고정 기능은 따로)
//  2. 마그넷 크레인: 2단 위(안 보임) → 동쪽 고철 더미 위로. 다리 사이로 고철 산이 보이고 들보는 화면 위로 잘린다
//  3. 리그 광고판: 2단 위(안 보임) → 다리 건너 정면, 1단 절벽 밑. 스폰 화면 가운데 뒤쪽에 걸린다
//  4. 서쪽 2단 컨테이너: 정면을 막던 벽 → 90° 돌려 서쪽 가장자리 '날개'로
//  5. 입구 가로등을 조금 동쪽으로 (스폰 화면 아래에 머리만 걸리던 것), 안내판은 입구 길가로
//  6. 안 보이는 것(2단 위 · 2단 절벽 뒤 · 전선 · 전봇대 · 북쪽 울타리)은 '안 보임' 묶음에 넣고 끈다
//  7. 지형 외곽을 켜고, 남쪽(카메라 쪽)은 평평하게 → 맵 밖 빈 공간이 안 보이게
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RealSteel.Terrain;

public static class RSJunkyardLowCamPass
{
    const string RootName = "JUNKYARD_꾸미기";
    const string HiddenName = "안 보임 (정면 저각 카메라)";
    const float Pitch = 12f;

    [MenuItem("Tools/RE_AL STEEL/Stage/고물상 정면 저각 배치 (Map_Test, 한 번)", false, 10)]
    static void Run()
    {
        var root = GameObject.Find(RootName);
        if (root == null) { EditorUtility.DisplayDialog("정면 저각 배치", "'" + RootName + "' 가 없습니다. Map_Test 를 열고 실행하세요.", "확인"); return; }
        if (root.transform.Find(HiddenName) != null &&
            !EditorUtility.DisplayDialog("정면 저각 배치", "이미 한 번 실행한 것 같습니다 ('" + HiddenName + "' 이 있음). 그래도 할까요?", "실행", "취소"))
            return;

        Undo.SetCurrentGroupName("고물상 정면 저각 배치");
        int group = Undo.GetCurrentGroup();
        var log = new List<string>();
        var terrain = Object.FindFirstObjectByType<RSTerrain>();

        // 숨길 것을 먼저 고른다 (옮길 크레인 · 광고판은 빼고)
        var crane = root.transform.Find("마그넷 크레인");
        var board = root.transform.Find("리그 광고판");
        var hideList = new List<Transform>();
        foreach (Transform t in root.transform)
        {
            if (t == crane || t == board) continue;
            if (ShouldHide(t)) hideList.Add(t);
        }

        // 1. 카메라
        SetupCamera(log);

        // 2. 크레인: 다리 둘 다 평지에 서고, 자석이 고철 더미 꼭대기 위에 뜨는 자리
        if (crane != null)
        {
            Undo.RecordObject(crane, "정면 저각 배치");
            float y = Mathf.Min(Ground(terrain, 10.9f, 7f), Ground(terrain, 15.1f, 7f));
            crane.SetPositionAndRotation(new Vector3(13f, y - 0.03f, 7f), Quaternion.identity);
            log.Add("마그넷 크레인 → 동쪽 고철 더미 (13, 7)");
        }

        // 3. 광고판: 1단 절벽 밑, 정면. 0.45 m 묻어서 그림 판 위쪽이 화면 위로 안 잘리게
        if (board != null)
        {
            Undo.RecordObject(board, "정면 저각 배치");
            float y = Mathf.Min(Ground(terrain, -1.7f, 7.9f), Ground(terrain, 0.5f, 7.9f));
            board.SetPositionAndRotation(new Vector3(-0.6f, y - 0.45f, 7.9f), Quaternion.identity);
            log.Add("리그 광고판 → 다리 건너 정면 (-0.6, 7.9)");
        }
        var sheet = Nearest(root, "기대 놓은 함석", new Vector3(-1.6f, 0f, 7.4f), 1.5f);
        if (sheet != null) MoveT(sheet, new Vector3(-3.3f, 0f, 7.6f), 150f, terrain, log);   // 광고판 다리와 겹쳐서

        // 4. 서쪽 컨테이너 → 날개 (긴 면이 마당 쪽을 보게)
        var cont = Nearest(root, "컨테이너", new Vector3(-15f, 0f, -13.6f), 2f);
        if (cont != null) MoveT(cont, new Vector3(-16.5f, 0f, -13.6f), 90f, terrain, log);

        // 5. 입구 가로등 · 안내판
        var lamp = Nearest(root, "가로등", new Vector3(1.35f, 0f, -14.8f), 1.5f);
        if (lamp != null) MoveT(lamp, new Vector3(2.2f, 0f, -14.8f), 180f, terrain, log);
        var sign = Nearest(root, "안내판", new Vector3(1.1f, 0f, -9.5f), 1.5f);
        if (sign != null) MoveT(sign, new Vector3(-4.4f, 0f, -12.4f), 8f, terrain, log);

        // 6. 안 보이는 것 끄기 (지우지 않는다. 묶음을 켜면 그대로 돌아온다)
        var hidden = new GameObject(HiddenName);
        Undo.RegisterCreatedObjectUndo(hidden, "정면 저각 배치");
        Undo.SetTransformParent(hidden.transform, root.transform, "정면 저각 배치");
        hidden.transform.localPosition = Vector3.zero;
        foreach (var t in hideList) Undo.SetTransformParent(t, hidden.transform, "정면 저각 배치");
        Undo.RecordObject(hidden, "정면 저각 배치");
        hidden.SetActive(false);
        log.Add("안 보이는 " + hideList.Count + "개 → '" + HiddenName + "' (꺼 둠)");

        // 7. 지형 외곽
        if (terrain != null)
        {
            Undo.RecordObject(terrain, "정면 저각 배치");
            terrain.outerEnabled = true;
            terrain.outerFrontFlat = 30f;
            terrain.MarkDirty();
            EditorUtility.SetDirty(terrain);
            log.Add("지형 외곽 켬 (남쪽 30 m 평평)");
        }

        Undo.CollapseUndoOperations(group);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[고물상 정면 저각 배치]\n · " + string.Join("\n · ", log));
        EditorUtility.DisplayDialog("정면 저각 배치", string.Join("\n", log) + "\n\nCtrl+Z 한 번으로 되돌립니다.", "확인");
    }

    /// <summary>캐릭터 발 높이(≈0) 에서 눈높이 3.7 m 위로만 있거나, 2단 절벽(z 15) 뒤에 있는 것</summary>
    static bool ShouldHide(Transform t)
    {
        string n = t.name;
        if (n == "전선" || n == "전봇대" || n.StartsWith("울타리_북")) return true;
        if (!TryBounds(t, out Bounds b)) return false;
        if (n.StartsWith("울타리_")) return false;
        return b.min.y > 3.55f || b.min.z > 15.3f;
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any;
    }

    static void SetupCamera(List<string> log)
    {
        var camGo = GameObject.Find("CAM_Stage");
        Camera cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
        if (cam == null) { log.Add("카메라 없음 (건너뜀)"); return; }
        var ch = Object.FindFirstObjectByType<RSSimpleCharacter>();
        float look = ch != null ? ch.lookHeight : 1.2f;
        float dist = ch != null ? ch.distance : 12f;
        Vector3 feet = ch != null ? ch.transform.position : Vector3.zero;
        var rot = Quaternion.Euler(Pitch, 0f, 0f);
        Undo.RecordObject(cam.transform, "정면 저각 배치");
        cam.transform.SetPositionAndRotation(feet + Vector3.up * look - rot * Vector3.forward * dist, rot);
        if (ch != null)
        {
            Undo.RecordObject(ch, "정면 저각 배치");
            ch.pitch = Pitch; ch.yaw = 0f;
            EditorUtility.SetDirty(ch);
        }
        log.Add(cam.name + " → Pitch 12 · Yaw 0 · 거리 " + dist.ToString("0.#"));
    }

    static float Ground(RSTerrain t, float x, float z)
    {
        return t != null ? t.SampleHeightWorld(new Vector3(x, 0f, z)) : 0f;
    }

    static void MoveT(Transform t, Vector3 xz, float yaw, RSTerrain terrain, List<string> log)
    {
        Undo.RecordObject(t, "정면 저각 배치");
        t.SetPositionAndRotation(new Vector3(xz.x, Ground(terrain, xz.x, xz.z) - 0.03f, xz.z), Quaternion.Euler(0f, yaw, 0f));
        log.Add(t.name + " → (" + xz.x.ToString("0.#") + ", " + xz.z.ToString("0.#") + ")");
    }

    /// <summary>이름이 같은 것 중 그 자리에서 가장 가까운 것 (maxDist 안)</summary>
    static Transform Nearest(GameObject root, string name, Vector3 near, float maxDist)
    {
        Transform best = null; float bd = maxDist * maxDist;
        foreach (Transform t in root.transform)
        {
            if (t.name != name) continue;
            float d = (new Vector2(t.position.x, t.position.z) - new Vector2(near.x, near.z)).sqrMagnitude;
            if (d <= bd) { bd = d; best = t; }
        }
        return best;
    }
}
