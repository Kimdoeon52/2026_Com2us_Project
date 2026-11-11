// RE:AL STEEL - 실내 갓레이 넣기 (볼류메트릭 안개 + 고정 빛 + 창 빛줄기 구역)
//
// 메뉴: Tools → RE_AL STEEL → Stage → 실내 갓레이 넣기 (열린 씬에)
//  · 'GODRAYS_실내 갓레이' 를 만든다 (다시 누르면 지우고 새로). 방 · 소품 · 조명은 건드리지 않는다
//  · 구성: 빛 방향 오브젝트 + 볼류메트릭 안개 상자(고정 빛 켬) + 빛줄기마다 빈 오브젝트(BEAM_) 와 안개 구역
//  · 고정 빛이라 씬의 해 · 시간대와 상관없이 빛줄기가 늘 같다 (밤이어도, 해가 없는 실내 씬이어도)
//  · 옥토패스식: 방 전체는 아주 옅은 먼지, 창에서 바닥으로 비스듬히 떨어지는 넓고 가장자리 없는 빛기둥만 진하게
//
// 고치는 법
//  · 빛 방향: '빛 방향' 오브젝트를 돌린다 (BEAM_ 들이 자식이라 같이 돈다)
//  · 빛줄기 위치: BEAM_ 오브젝트를 옮긴다 (시작점 = 창). 폭 · 두께 · 길이 · 진하기는 안개 상자 인스펙터의 '안개 구역'
//  · 전체 세기 · 색: 안개 상자의 '고정 빛' 묶음 / 먼지 양: '밀도'
//  · 새 씬: 아는 씬(Home_Workshop · League_Lobby)이 아니면 씬 크기에 맞춘 상자 + 빛줄기 1개를 놓는다 → BEAM_ 을 복제해도
//    구역은 안 늘어나니, 안개 상자 인스펙터에서 '+ 선택한 오브젝트 크기로' / '+ 빈 구역' 으로 추가 (최대 8개)
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RealSteel.Lighting;

public static class RSIndoorGodRays
{
    public const string RootName = "GODRAYS_실내 갓레이";

    struct Beam
    {
        public string name; public Vector3 start; public float width, thick, length, density;
        public Beam(string n, Vector3 s, float w, float t, float l, float d = 0.15f) { name = n; start = s; width = w; thick = t; length = l; density = d; }
    }

    // 빛이 나아가는 방향: 뒤 · 오른쪽 · 위에서 앞 · 왼쪽 · 아래로 (카메라 쪽으로 떨어지는 역광 — 빛기둥이 가장 잘 보인다)
    // 높이 4 m 에서 출발해 방 깊이 5 m 안에 바닥에 닿는 기울기 (더 눕히면 빛기둥이 방 앞 단면 밖으로 삐져나간다)
    static readonly Vector3 LightDir = new Vector3(-0.38f, -0.62f, -0.69f).normalized;

    [MenuItem("Tools/RE_AL STEEL/Stage/실내 갓레이 넣기 (열린 씬에)", false, 7)]
    static void Menu()
    {
        var scene = SceneManager.GetActiveScene();
        var go = Build(scene.name);
        Undo.RegisterCreatedObjectUndo(go, "실내 갓레이");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = go;
    }

    /// <summary>지금 열린(활성) 씬에 실내 갓레이 세트를 만든다. 이미 있으면 지우고 새로</summary>
    public static GameObject Build(string sceneName)
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        Vector3 center, size;
        var beams = new List<Beam>();
        Vector3 lightDir = LightDir;
        if (sceneName == "Home_Workshop")
        {
            center = new Vector3(0f, 3.4f, 6.75f); size = new Vector3(19f, 8.2f, 17f);
            // 컨테이너 집: 뒷벽 창 3개
            beams.Add(new Beam("집_창_왼", new Vector3(-2.5f, 5.5f, 13.9f), 1.1f, 0.9f, 2.5f, 0.17f));
            beams.Add(new Beam("집_창_가운데", new Vector3(1.5f, 5.5f, 13.9f), 1.1f, 0.9f, 2.5f, 0.17f));
            beams.Add(new Beam("집_창_오른", new Vector3(5.5f, 5.5f, 13.9f), 1.1f, 0.9f, 2.5f, 0.17f));
            // 지하 정비소: 지상에서 떨어지는 긴 빛기둥 (정비대 · 작업대 · 계단 통)
            beams.Add(new Beam("정비소_정비대", new Vector3(0.8f, 4.4f, 6.3f), 2.4f, 1.3f, 7.2f, 0.13f));
            beams.Add(new Beam("정비소_작업대", new Vector3(-4.2f, 4.4f, 6.3f), 2f, 1.1f, 7.2f, 0.11f));
            beams.Add(new Beam("정비소_계단", new Vector3(7.2f, 4.6f, 6.2f), 1.4f, 1.2f, 7.5f, 0.11f));
        }
        else if (sceneName == "Auction_Hall")
        {
            // 경매장: 창이 아니라 무대 위 조명에서 떨어지는 스포트라이트 기둥 → 거의 수직
            center = new Vector3(0f, 3.3f, 3.5f); size = new Vector3(18f, 7f, 13f);
            lightDir = new Vector3(-0.12f, -0.96f, -0.25f).normalized;
            beams.Add(new Beam("무대_전시대", new Vector3(0.7f, 6.4f, 8.35f), 2.4f, 2.2f, 5.9f, 0.17f));
            beams.Add(new Beam("무대_경매사", new Vector3(-2.6f, 6.4f, 8.2f), 1.5f, 1.5f, 5.9f, 0.14f));
            beams.Add(new Beam("무대_입찰석", new Vector3(4.2f, 6.4f, 8.1f), 1.8f, 1.5f, 5.9f, 0.1f));
            beams.Add(new Beam("객석_은은하게", new Vector3(0.7f, 6.4f, 4.6f), 9.5f, 3.5f, 6.6f, 0.035f));
        }
        else if (sceneName == "League_Lobby")
        {
            center = new Vector3(0f, 2.8f, 4f); size = new Vector3(18f, 6.4f, 13f);
            beams.Add(new Beam("높은창_왼", new Vector3(-4.5f, 4.6f, 9.8f), 3.2f, 1.1f, 7.5f, 0.13f));
            beams.Add(new Beam("높은창_오른", new Vector3(4.5f, 4.6f, 9.8f), 3.2f, 1.1f, 7.5f, 0.13f));
        }
        else
        {
            // 모르는 씬: 보이는 것 전체를 덮는 상자 (너무 크면 60 m 로 자름) + 가운데 뒤쪽 위에서 떨어지는 빛줄기 하나
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r.GetComponent<RSFogVolume>() != null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) b = new Bounds(new Vector3(0, 3, 0), new Vector3(20, 6, 20));
            center = b.center;
            size = new Vector3(Mathf.Clamp(b.size.x + 2, 6, 60), Mathf.Clamp(b.size.y + 1, 4, 20), Mathf.Clamp(b.size.z + 2, 6, 60));
            float top = center.y + size.y * 0.5f - 0.5f;
            beams.Add(new Beam("빛줄기_1", new Vector3(center.x, top, center.z + Mathf.Min(size.z * 0.25f, 5f)), 3f, 1.2f, Mathf.Min((size.y - 1f) / 0.62f, 14f)));
        }

        var root = new GameObject(RootName);

        var dir = new GameObject("빛 방향 (파란 화살표 = 빛이 나아가는 쪽 · 돌리면 빛줄기가 같이 돈다)").transform;
        dir.SetParent(root.transform, false);
        dir.position = center + new Vector3(0, size.y * 0.5f + 1f, 0);
        dir.rotation = Quaternion.LookRotation(lightDir, Vector3.up);

        var fogGo = new GameObject("FOG_실내 (볼류메트릭 안개 · 고정 빛)");
        fogGo.transform.SetParent(root.transform, false);
        fogGo.transform.position = center;
        var fog = fogGo.AddComponent<RSFogVolume>();
        fog.size = size;
        fog.edgeFade = 1f;
        fog.steps = 28;
        fog.stepLength = 0.6f;       // 빛기둥 두께(약 1 m)보다 촘촘하게
        fog.maxDistance = 60f;
        fog.ownLook = true;          // 스테이지 룩 프로필이 있어도 이 상자는 실내 값 그대로

        fog.fixedLight = true;
        fog.fixedLightFrom = dir;
        fog.fixedLightColor = new Color(1f, 0.9f, 0.72f);
        fog.fixedLightIntensity = 1.1f;

        var l = fog.LocalLook;
        l.color = new Color(1f, 0.97f, 0.9f);
        l.followTimeOfDay = false;
        l.density = 0.012f;          // 방 전체는 아주 옅은 먼지 — 빛기둥(구역)만 진하다
        l.heightFalloff = 0f;
        l.noiseScale = 0.25f;
        l.noiseStrength = 0.45f;
        l.occlusion = 0.2f;          // 실내 먼지: 빛기둥은 밝고 방 안은 또렷하게
        l.wind = new Vector3(0.15f, 0.02f, 0.05f);
        l.sunStrength = 1f;
        l.shadowStrength = 1f;
        l.anisotropy = 0.3f;
        l.ambientStrength = 0.15f;
        l.pointLights = true;
        l.pointStrength = 0.6f;
        l.pointCore = 0.5f;
        l.shaftBoost = 2.2f;
        l.shaftContrast = 0.6f;
        l.shaftPattern = 0.55f;      // 빛기둥 안에 빛 방향으로 늘어진 결
        l.shaftPatternScale = 0.9f;
        l.shaftEven = 0.75f;         // 옥토패스 카메라는 빛기둥을 옆에서 본다 → 옆에서도 보이게
        l.shaftSharpness = 1.2f;
        l.cookieShafts = 0f;
        l.shaftsFollowTime = false;
        l.moonShafts = 0f;

        fog.zones.Clear();
        foreach (var bm in beams)
        {
            var t = new GameObject("BEAM_" + bm.name + " (시작점 = 창 · 옮기면 빛줄기가 따라온다)").transform;
            t.SetParent(dir, false);
            t.position = bm.start;
            t.rotation = dir.rotation;
            fog.zones.Add(new RSFogVolume.Zone
            {
                name = bm.name,
                follow = t,
                center = new Vector3(0f, 0f, bm.length * 0.5f),
                size = new Vector3(bm.width, bm.thick, bm.length),
                edgeSoftness = Mathf.Min(bm.width, bm.thick) * 0.48f,   // 가장자리 없이 연무처럼
                mode = RSFogVolume.ZoneMode.덮어쓰기,
                density = bm.density,
                shaftMultiplier = 2f,
                tint = Color.white,
            });
        }
        EditorUtility.SetDirty(fog);
        RSFogVolume.RescanLights();

        int others = Object.FindObjectsByType<RSFogVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length - 1;
        Debug.Log("[실내 갓레이] 안개 상자 + 빛줄기 " + beams.Count + "개를 넣었습니다 (" + RootName + ")." +
                  (others > 0 ? " 이 씬에 다른 안개 상자가 " + others + "개 더 있습니다 — 겹치는 곳은 안개가 두 번 쌓이니 한쪽을 줄이세요." : ""));
        return root;
    }
}
