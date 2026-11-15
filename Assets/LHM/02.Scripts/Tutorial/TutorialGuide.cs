using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace RealSteel.Tutorial
{
    [CreateAssetMenu(menuName = "RealSteel/Tutorial Guide", fileName = "TutorialGuide")]
    public class TutorialGuide : ScriptableObject
    {
        [Tooltip("본 기록을 저장할 때 쓰는 이름. 튜토리얼마다 달라야 함")]
        public string guideId = "basic";
        public string title = "이용 방법";
        public List<TutorialStep> steps = new List<TutorialStep>();

        [Header("자동으로 열기 (처음 한 번 · TutorialDirector가 감시)")]
        public TutorialTrigger trigger = TutorialTrigger.Manual;
        [Tooltip("SceneName: 씬 이름에 들어간 글자 (쉼표로 여러 개)\n" +
                 "ComponentInScene: 씬에 있으면 여는 컴포넌트 이름 (예: BattleManager)\n" +
                 "WindowOpened: '컴포넌트 이름' = 그 오브젝트가 켜질 때 / '컴포넌트.칸' = bool 칸이 true가 될 때 · GameObject 칸이 켜질 때\n" +
                 "Event: TutorialDirector.Notify(\"키\")로 부르는 키")]
        public string triggerTarget = "";

        [Header("화면 전에 나올 인물 안내 대사 (대사 ID · 비우면 바로 화면)")]
        [Tooltip("tutorial 대사 풀의 대화 ID. 대화창으로 먼저 설명한 뒤 이 화면 튜토리얼이 열림 (기획서 xxi)")]
        public string introDialogueId = "";
    }

    public enum TutorialTrigger
    {
        Manual,
        SceneName,
        ComponentInScene,
        WindowOpened,
        Event,
    }

    // Unity는 GIF를 재생하지 못하므로 mp4(clip)로 바꾸거나 프레임 스프라이트(frames)로 잘라서 넣는다.
    [Serializable]
    public class TutorialStep
    {
        public string title;
        [TextArea(2, 5)] public string description;

        [Header("영상 (하나만 채우면 됨 · 위에 있는 것 우선 · 모두 비우면 '영상 준비 중')")]
        public VideoClip clip;
        public Sprite[] frames;
        [Min(1f)] public float framesPerSecond = 12f;
        public Sprite image;
    }
}
