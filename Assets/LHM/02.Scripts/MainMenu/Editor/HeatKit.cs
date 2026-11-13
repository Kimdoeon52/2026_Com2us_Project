using Michsky.UI.Heat;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RealSteel.MainMenu.EditorTools
{
    internal static class HeatKit
    {
        internal const string Root = "Assets/Heat - Complete Modern UI/";
        internal const string Prefabs = Root + "Prefabs/";
        internal const string Animations = Root + "Animations/";
        private const string HeatFontFolder = Root + "Fonts/";

        internal static bool IsInstalled()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "UI Elements/Canvas/Canvas.prefab") != null;
        }

        internal static GameObject Prefab(string relativePath, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + relativePath);
            if (asset == null) throw new System.IO.FileNotFoundException($"Heat 프리팹이 없습니다: {Prefabs + relativePath}");

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            return go;
        }

        internal static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        internal static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        internal static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        internal static Image Paint(GameObject go, Color color, Sprite sprite = null, bool raycast = false)
        {
            Image image = Add<Image>(go);
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static Animator UseAnimator(GameObject go, string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + controllerPath);
            if (controller == null) throw new System.IO.FileNotFoundException($"Heat 애니메이터가 없습니다: {Animations + controllerPath}");

            Animator animator = Add<Animator>(go);
            animator.runtimeAnimatorController = controller;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            return animator;
        }

        // GetComponent() ?? AddComponent()는 안 됨: 에디터에서 없는 컴포넌트가 진짜 null이 아닌 '가짜 null'로 와서 ??가 그냥 통과함
        internal static T Add<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
        }

        internal static T Find<T>(GameObject root, string path) where T : Component
        {
            Transform t = root.transform.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }

        internal static void Set(Object target, string property, Object value) => Edit(target, property, p => p.objectReferenceValue = value);
        internal static void Set(Object target, string property, bool value) => Edit(target, property, p => p.boolValue = value);
        internal static void Set(Object target, string property, int value) => Edit(target, property, p => p.intValue = value);
        internal static void Set(Object target, string property, string value) => Edit(target, property, p => p.stringValue = value);
        internal static void SetEnum(Object target, string property, int index) => Edit(target, property, p => p.enumValueIndex = index);

        private static void Edit(Object target, string property, System.Action<SerializedProperty> apply)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(property);
            if (p == null) throw new System.MissingFieldException(target.GetType().Name, property);
            apply(p);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static InputAction KeyAction(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            return action;
        }

        internal static void Listen(UnityEvent unityEvent, UnityAction call)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, call);
        }

        internal static void Listen<T>(UnityEvent<T> unityEvent, UnityAction<T> call)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(unityEvent, call);
        }

        internal static void Listen(UnityEvent unityEvent, UnityAction<string> call, string argument)
        {
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(unityEvent, call, argument);
        }

        internal static int BindUIManager(GameObject root, UIManager asset)
        {
            int count = 0;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                SerializedProperty p = so.FindProperty("UIManagerAsset");
                if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (p.objectReferenceValue == asset) continue;
                p.objectReferenceValue = asset;
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            return count;
        }

        internal static int ReplaceHeatFonts(GameObject root, UIManager asset)
        {
            int count = 0;
            foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font == null) continue;
                string path = AssetDatabase.GetAssetPath(text.font);
                if (!path.StartsWith(HeatFontFolder)) continue;

                TMP_FontAsset replacement = path.Contains("Light") ? asset.fontLight
                    : path.Contains("Bold") ? asset.fontBold
                    : asset.fontRegular;
                if (replacement == null || text.font == replacement) continue;

                text.font = replacement;
                count++;
            }
            return count;
        }

        internal static TextMeshProUGUI Caption(GameObject go, string value, float size, TextAlignmentOptions alignment, UIManager asset, UIManagerText.FontType fontType)
        {
            var text = Add<TextMeshProUGUI>(go);
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var managed = Add<UIManagerText>(go);
            managed.UIManagerAsset = asset;
            managed.fontType = fontType;
            managed.colorType = UIManagerText.ColorType.Primary;
            text.font = fontType == UIManagerText.FontType.Bold ? asset.fontBold : asset.fontRegular;
            return text;
        }
    }
}
