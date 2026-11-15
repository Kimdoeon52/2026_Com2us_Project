using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Tutorial
{
    // 게임 전체 튜토리얼 목록. Resources/RealSteel/TutorialCatalog 에 두면 TutorialDirector가 게임 시작 때 읽어 자동으로 감시함
    [CreateAssetMenu(menuName = "RealSteel/Tutorial Catalog", fileName = "TutorialCatalog")]
    public class TutorialCatalog : ScriptableObject
    {
        public const string ResourcePath = "RealSteel/TutorialCatalog";

        [Tooltip("튜토리얼 화면 프리팹 (TutorialPanel이 붙은 캔버스)")]
        public GameObject panelPrefab;

        [Tooltip("목록 순서 = 같은 순간에 여러 개가 걸렸을 때 나오는 순서 = 도움말 목록 순서")]
        public List<TutorialGuide> guides = new List<TutorialGuide>();

        [Header("자동으로 열기")]
        [Tooltip("끄면 자동으로 열지 않음 (도움말에서 다시 보기는 그대로). 기획서 '튜토리얼 끄기 옵션' 자리")]
        public bool autoOpen = true;
        [Tooltip("씬에 들어온 직후 대화 시스템이 준비될 때까지 기다리는 최대 시간 (초)")]
        [Min(0f)] public float waitForDialogueSeconds = 2f;

        [Header("도움말 (본 튜토리얼 다시 보기)")]
        public Key helpKey = Key.F10;
    }
}
