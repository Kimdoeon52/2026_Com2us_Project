// RE:AL STEEL - 조립품 도안 (RSSheet)
//
// 조립품 전용 그림 한 장과 '어느 면이 어느 칸인지' 표. 조립품 · 그 프리팹 복사본들이 같이 쓴다.
//  · source  = 그리는 원본 (.aseprite — 가이드 참조 레이어 + 그림 레이어, 또는 .png)
//  · texture = 원본을 처리한 결과 PNG (빈 칸 채움 · 칸 테두리 번짐 막기). 게임은 이걸 쓴다
// 원본을 저장하면 에디터가 결과를 다시 만든다 (UV 는 그대로라 조립품을 다시 만들 필요 없음).
using System;
using System.Collections.Generic;
using UnityEngine;
using RealSteel.Common;

namespace RealSteel.Build
{
    [Serializable]
    public class RSSheetCell
    {
        public string key;          // 부품 id : 면 번호
        public RectInt rect;        // 텍스처 안 위치 (픽셀, 아래 왼쪽 기준)
        public string label;        // 부품 이름 · 면 이름
        public RSSurface fallback;  // 아직 안 그린 칸을 채울 재질
    }

    [RSSummary("조립품 도안", "조립품 전용 그림 한 장 + 면 ↔ 칸 표. 조립품 인스펙터의 '도안' 에서 만들고 고친다.\n" +
        "· 원본(.aseprite)을 Aseprite 로 열어 '그림' 레이어에 그리고 저장하면 결과가 자동으로 바뀐다\n" +
        "· '가이드' 레이어는 참조 레이어라 그림에 섞이지 않는다")]
    public class RSSheet : ScriptableObject
    {
        [Tooltip("그리는 원본 파일 (프로젝트 경로, .aseprite 또는 .png)")]
        public string source = "";
        [Tooltip("처리된 결과 (게임이 쓰는 텍스처)")]
        public Texture2D texture;
        [Tooltip("그림이 없는 칸 · 빈 픽셀을 채운다 (빈 칸 = 그 면 재질로, 일부만 그린 칸 = 가장 가까운 그린 픽셀로)")]
        public bool fillEmpty = true;
        public int width = 64, height = 64;
        public List<RSSheetCell> cells = new List<RSSheetCell>();

        public RSSheetCell Find(string key)
        {
            foreach (var c in cells) if (c != null && c.key == key) return c;
            return null;
        }
    }
}
