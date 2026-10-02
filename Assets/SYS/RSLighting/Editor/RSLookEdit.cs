// RE:AL STEEL - 룩 값 고치기 도우미 (에디터)
//
// 룩 값(Look)은 스테이지 룩 프로필이 연결돼 있으면 프로필 에셋에, 아니면 컴포넌트에 저장된다.
// 되돌리기(Undo) · 저장 표시(SetDirty)를 맞는 쪽에 걸어 준다.
using UnityEditor;
using UnityEngine;

namespace RealSteel.Lighting.EditorTools
{
    public static class RSLookEdit
    {
        /// <summary>룩 값이 실제로 저장되는 오브젝트 (프로필 에셋 또는 컴포넌트)</summary>
        public static Object Owner(Object component, bool usesStageLook)
        {
            var p = RSStageLook.Current;
            return usesStageLook && p != null ? (Object)p : component;
        }

        public static void Record(Object component, bool usesStageLook, string label)
        {
            Undo.RecordObject(Owner(component, usesStageLook), label);
        }

        /// <summary>컴포넌트 자체 값과 룩 값을 같이 고칠 때 (둘 다 되돌리기에 넣음)</summary>
        public static void RecordBoth(Object component, bool usesStageLook, string label)
        {
            var o = Owner(component, usesStageLook);
            if (o == component) Undo.RecordObject(component, label);
            else Undo.RecordObjects(new[] { component, o }, label);
        }

        public static void DirtyBoth(Object component, bool usesStageLook)
        {
            EditorUtility.SetDirty(component);
            Dirty(component, usesStageLook);
        }

        public static void Dirty(Object component, bool usesStageLook)
        {
            var o = Owner(component, usesStageLook);
            EditorUtility.SetDirty(o);
            if (o is RSStageLook p) p.NotifyChanged();
        }
    }
}
