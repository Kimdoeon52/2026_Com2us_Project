// RE:AL STEEL - 나노봇 리그 포즈 모음
//
// 픽셀 애니메이션의 '키 포즈' 처럼, 리그 전체 모양(관절 위치 · 회전 · 크기, 부위 두께 · 크기 …)을 이름 붙여 저장한다.
// 나노봇 동작 창 → 포즈 탭에서 저장하고, '지금 시점에 키' 로 불러오면 달라진 값만 키가 잡힌다.
// 경로는 리그 원점 기준이라 구조가 같은 다른 리그에도 쓸 수 있다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    [CreateAssetMenu(menuName = "RE_AL STEEL/나노봇 포즈 모음", fileName = "나노봇 포즈")]
    public class RSNanoPoseLibrary : ScriptableObject
    {
        [Serializable]
        public class TransformPose
        {
            public string path;
            public Vector3 position, euler, scale;
        }

        [Serializable]
        public class LimbPose
        {
            public string path;
            public float radius, grow, wobble, bind;
            public Vector3 size;
            public Vector2 extent;
        }

        [Serializable]
        public class JointPose
        {
            public string path;
            public float thickness;
        }

        [Serializable]
        public class Pose
        {
            public string name = "포즈";
            public List<TransformPose> transforms = new List<TransformPose>();
            public List<LimbPose> limbs = new List<LimbPose>();
            public List<JointPose> joints = new List<JointPose>();
            [Tooltip("썸네일 선분 (리그 원점 기준 x0, y0, x1, y1)")]
            public List<Vector4> sketch = new List<Vector4>();
        }

        public List<Pose> poses = new List<Pose>();
    }
}
