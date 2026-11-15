using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RealSteel.Dialogue
{
    // SYS 캐릭터(RSSimpleCharacter)는 Q/E를 '카메라 45° 돌리기'로도 써서, E로 상호작용할 때마다 카메라가 돌아감
    // → 상호작용 대상(NPC · 문)이 범위 안에 있는 동안 그 회전 각도(rotateStep)를 0으로 막고, 아무도 막지 않으면 원래 값으로 돌려놓음
    // 여러 곳(PlayerInteractor · DoorInteractable)이 동시에 막아도 원래 값이 꼬이지 않게 막는 쪽을 세어 둠
    // SYS 파일은 고치지 않고, 이름으로 찾아서 없으면 아무 일도 안 함
    public static class CameraStepBlock
    {
        private const string FieldName = "rotateStep";

        private sealed class Entry
        {
            public Component owner;
            public FieldInfo field;
            public float saved;
            public readonly HashSet<object> holders = new HashSet<object>();
        }

        private static readonly Dictionary<GameObject, Entry> entries = new Dictionary<GameObject, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            entries.Clear();
        }

        public static void Set(object holder, GameObject character, bool block)
        {
            if (holder == null || character == null) return;

            if (!entries.TryGetValue(character, out Entry entry))
            {
                entry = Create(character);
                entries[character] = entry;
            }
            if (entry.field == null || entry.owner == null) return;

            if (block)
            {
                if (entry.holders.Count == 0)
                {
                    entry.saved = (float)entry.field.GetValue(entry.owner);
                    entry.field.SetValue(entry.owner, 0f);
                }
                entry.holders.Add(holder);
            }
            else if (entry.holders.Remove(holder) && entry.holders.Count == 0)
            {
                entry.field.SetValue(entry.owner, entry.saved);
            }
        }

        private static Entry Create(GameObject character)
        {
            var entry = new Entry();
            foreach (MonoBehaviour behaviour in character.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;

                FieldInfo field = behaviour.GetType().GetField(FieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || field.FieldType != typeof(float)) continue;

                entry.owner = behaviour;
                entry.field = field;
                Debug.Log($"[CameraStepBlock] {behaviour.GetType().Name}의 E/Q 카메라 회전을 상호작용 범위 안에서는 막습니다 (E 키 충돌 방지).", behaviour);
                break;
            }
            return entry;
        }
    }
}
