// RE:AL STEEL - 스테이지 연출 한 번에 설치 (에디터 메뉴)
//
// 예전 설치 메뉴 5개(카메라 리그 31 · 옥토패스 조명 33 · 분위기 35 · 색감 · 간접광 · 젖은 바닥 36 · 스테이지 룩 38)를 하나로 합쳤다.
// 이미 있는 것은 건너뛰고 빠진 것만 채우므로 여러 번 눌러도 된다 (카메라 리그도 있으면 다시 만들지 않는다).
// 물은 스테이지마다 다르므로 따로: Tools → RE_AL STEEL → Stage → 물 (반사 · 굴절 · 거품) 설치
using UnityEditor;
using UnityEngine;
using RealSteel.EditorTools;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSStageSetupMenu
    {
        [MenuItem("Tools/RE_AL STEEL/Stage/스테이지 연출 한 번에 설치 (카메라 · 조명 · 분위기 · 색감 · 젖은 바닥 · 스테이지 룩)", false, 1)]
        public static void InstallAll()
        {
            var log = new System.Text.StringBuilder("[스테이지 연출] ");
            int group = Undo.GetCurrentGroup();

            if (StagePostBuilder.EnsureLook()) log.Append("카메라 리그(STAGE_Look) 만듦 · ");
            else log.Append("카메라 리그 있음 · ");

            RSLightingMenu.Setup();       // 시간대 · 구름 그림자 · 햇살
            RSAtmosphereMenu.Setup();     // 볼류메트릭 안개 · 캐릭터 밤 빛 · 자동 초점
            RSSurfaceLookMenu.Setup();    // 색감 · 간접광 · 젖은 바닥 (+ URP Opaque · Depth 확인)
            RSStageLookMenu.Setup();      // 스테이지 룩 (프로필 없으면 지금 씬 값으로 만듦)

            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("스테이지 연출 한 번에 설치");

            var tod = Object.FindAnyObjectByType<RSTimeOfDay>();
            if (tod != null) Selection.activeGameObject = tod.gameObject;
            log.Append("시간대 · 구름 그림자 · 햇살 · 안개 · 캐릭터 밤 빛 · 자동 초점 · 색감 · 간접광 · 젖은 바닥 · 스테이지 룩 확인 완료.\n" +
                       "· 값은 LIGHTING_TimeOfDay 의 각 컴포넌트에서 고친다 (저장은 스테이지 룩 프로필 에셋)\n" +
                       "· 물이 있는 스테이지면: Tools → RE_AL STEEL → Stage → 물 (반사 · 굴절 · 거품) 설치\n" +
                       "· 씬을 저장하세요.");
            Debug.Log(log.ToString());
        }
    }
}
