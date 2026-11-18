using System.Reflection;
using UnityEngine;

namespace RealSteel.Dialogue
{
    // YJW CraftingUIManager의 제작 · 수리 창 열기 · 닫기 (YJW 코드 수정 없이 이름 · 리플렉션으로 호출)
    // 작업대 NPC(NpcChoiceMenu)와 토벌 신청 창의 정비하기(HuntDeparture)가 같이 씀
    public static class CraftingWindowBridge
    {
        public const string OwnerTypeName = "CraftingUIManager";
        public const string FixWindowField = "fixWindow";
        public const string FixOpenMethod = "OpenFixWindow";
        public const string FixCloseMethod = "CloseFixWindow";

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static MonoBehaviour FindOwner()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == OwnerTypeName) return behaviour;
            }
            return null;
        }

        // 창 관리자의 열기 함수를 부르고 창을 켬. 실패하면 null
        public static GameObject Open(MonoBehaviour owner, string windowField, string openMethod, string label)
        {
            if (owner == null) return null;

            System.Type type = owner.GetType();
            var window = type.GetField(windowField, AnyInstance)?.GetValue(owner) as GameObject;
            if (window == null)
            {
                Debug.LogWarning($"[Crafting] {type.Name}의 '{windowField}' 칸이 비어 있거나 이름이 바뀌어 {label} 창을 열 수 없습니다.", owner);
                return null;
            }

            MethodInfo open = type.GetMethod(openMethod, AnyInstance, null, System.Type.EmptyTypes, null);
            if (open != null) open.Invoke(owner, null);
            else Debug.LogWarning($"[Crafting] {type.Name}에 '{openMethod}' 함수가 없어 창만 켭니다.", owner);
            if (!window.activeSelf) window.SetActive(true);
            if (!window.activeInHierarchy) Debug.LogWarning($"[Crafting] '{window.name}'의 부모가 꺼져 있어 화면에 보이지 않습니다.", window);
            return window;
        }

        // 창 관리자의 닫기 함수를 우선 사용 (제작창은 올려 둔 재료 되돌리기 · 인벤토리 숨기기까지 해 줌)
        public static void Close(MonoBehaviour owner, string closeMethod, GameObject window)
        {
            MethodInfo close = owner != null ? owner.GetType().GetMethod(closeMethod, AnyInstance, null, System.Type.EmptyTypes, null) : null;
            if (close != null) close.Invoke(owner, null);
            if (window != null && window.activeSelf) window.SetActive(false);
        }
    }
}
