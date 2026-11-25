using System;
using UnityEngine;

/// <summary>
/// 마에스트로 모션 연결표 (2026-11-25 모션 편집기) — "이 몸짓을 걸면 코드 키 대신 이 모션을 쓴다".
/// 플레이 중에 편집기에서 연결해도 남도록 컴포넌트가 아니라 에셋에 둔다.
/// 위치: `Assets/NYH/04. SO/Boss/Maestro/Resources/Maestro_MotionSet.asset` — MaestroLook이 비어 있으면 Resources에서 찾는다.
/// </summary>
[CreateAssetMenu(fileName = "Maestro_MotionSet", menuName = "NYH/Boss/Maestro Motion Set")]
public class MaestroMotionSet : ScriptableObject
{
    public const string ResourcePath = "Maestro_MotionSet";

    [Serializable]
    public class Link
    {
        public MaestroGesture gesture;
        [Tooltip("비우면 코드에 있는 원래 몸짓")]
        public MaestroMotion motion;
    }

    [Tooltip("몸짓 → 모션. 패턴이 MaestroLook.Play / PlayTimed로 이 몸짓을 걸면 모션이 대신 나온다")]
    public Link[] links = Array.Empty<Link>();

    public MaestroMotion Find(MaestroGesture g)
    {
        if (links == null) return null;
        foreach (var l in links) if (l != null && l.gesture == g && l.motion != null) return l.motion;
        return null;
    }

    public void Set(MaestroGesture g, MaestroMotion m)
    {
        if (links != null)
            foreach (var l in links)
                if (l != null && l.gesture == g) { l.motion = m; return; }
        var list = new System.Collections.Generic.List<Link>(links ?? Array.Empty<Link>()) { new Link { gesture = g, motion = m } };
        links = list.ToArray();
    }
}
