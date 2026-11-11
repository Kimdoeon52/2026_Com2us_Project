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
