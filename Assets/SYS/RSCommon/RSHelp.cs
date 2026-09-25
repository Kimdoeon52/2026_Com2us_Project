// RE:AL STEEL - 인스펙터 설명 표기 (공용)
//
//  [RSSummary("제목", "설명")]  클래스에 — 인스펙터 맨 위 요약 상자
//  [RSHelp("설명")]            필드에 — 그 필드 위에 도움말 상자 (항목 묶음 설명)
//  필드 하나하나의 설명은 유니티 기본 [Tooltip] (마우스를 올리면 뜬다)
//
// 설명 상자는 메뉴 Tools → RE_AL STEEL → Stage → 인스펙터 설명 보이기 · 숨기기 로 한꺼번에 끄고 켠다.
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
}
