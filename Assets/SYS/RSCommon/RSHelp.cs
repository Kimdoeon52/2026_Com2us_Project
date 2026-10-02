// RE:AL STEEL - 인스펙터 설명 표기 (공용)
//
//  [RSSummary("제목", "설명")]  클래스에 — 인스펙터 맨 위 요약 상자
//  [RSHelp("설명")]            필드에 — 그 필드 위에 도움말 상자 (항목 묶음 설명)
//  필드 하나하나의 설명은 유니티 기본 [Tooltip] (마우스를 올리면 뜬다)
//  [RSGroup("묶음")]           필드에 — 여기부터 그 묶음 (인스펙터에서 접히는 칸). [Header] 대신
//  [RSKey]                     필드에 — 자주 쓰는 칸, 맨 위에 늘 펼쳐 보여 줌 (컴포넌트당 3 ~ 5개)
//  [RSLook]                    필드에 — 룩 값 묶음(스테이지 룩 프로필로 옮길 수 있는 값). 안쪽 칸을 펼쳐 그림
//
// 설명 상자는 아무 RS 인스펙터 맨 위의 '설명 숨기기 · 설명 보기' 버튼으로 한꺼번에 끄고 켠다.
using System;
using UnityEngine;

namespace RealSteel.Common
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class RSHelpAttribute : PropertyAttribute
    {
        public readonly string text;
        public RSHelpAttribute(string text)
        {
            this.text = text;
            order = 1;   // [Header] 다음에 그린다
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class RSSummaryAttribute : Attribute
    {
        public readonly string title;
        public readonly string text;
        public RSSummaryAttribute(string title, string text)
        {
            this.title = title;
            this.text = text;
        }
    }

    /// <summary>
    /// 자주 만지는 칸. 인스펙터 맨 위 '자주 쓰는 것' 에 늘 펼쳐서 보여 준다 (나머지는 머리글별로 접힘).
    /// 컴포넌트마다 3 ~ 5 개만 붙인다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class RSKeyAttribute : Attribute { }

    /// <summary>
    /// 룩 값 묶음 (중첩 클래스 Look). 인스펙터가 안쪽 칸을 바로 펼쳐 그리고,
    /// 스테이지 룩 프로필이 연결돼 있으면 프로필 쪽 값을 대신 그린다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class RSLookAttribute : Attribute { }

    /// <summary>
    /// 인스펙터 묶음 (접히는 칸). [Header] 대신 쓴다 — RS 인스펙터가 머리글 대신 접기 버튼으로 그린다.
    /// open = true 면 처음에 펼쳐져 있다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class RSGroupAttribute : Attribute
    {
        public readonly string title;
        public readonly bool open;
        public RSGroupAttribute(string title, bool open = false) { this.title = title; this.open = open; }
    }
}
