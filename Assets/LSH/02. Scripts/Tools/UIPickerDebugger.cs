using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스 아래 UI를 화면 왼쪽 위에 보여주는 디버그 툴. F1로 켜고 끈다
// 씬에 배치할 필요 없이 에디터와 Development Build에서만 자동 생성된다
public class UIPickerDebugger : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F1;
    private const int MaxRows = 10; //목록 최대 줄 수
    private const float ScanInterval = 0.2f; //전체 UI 검사 주기 (매 프레임 찾으면 무거움)

    private readonly List<RaycastResult> _hits = new List<RaycastResult>(); //클릭을 받는 UI
    private readonly List<Graphic> _under = new List<Graphic>(); //레이캐스트 꺼진 것까지 포함한 마우스 아래 UI
    private readonly StringBuilder _text = new StringBuilder();
    private PointerEventData _pointer;
    private EventSystem _pointerOwner; //_pointer를 만든 EventSystem
    private GUIStyle _style;
    private Texture2D _pixel;
    private bool _visible = true;
    private float _nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild) return; // 일반 빌드에는 안 들어감 (에디터는 항상 true)

        GameObject go = new GameObject(nameof(UIPickerDebugger));
        DontDestroyOnLoad(go);
        go.AddComponent<UIPickerDebugger>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) _visible = !_visible;
        if (!_visible) return;

        Vector2 mouse = Input.mousePosition;
        _text.Clear();

        // 1. 실제로 클릭을 받는 UI (맨 위가 첫 줄)
        _hits.Clear();
        if (EventSystem.current != null)
        {
            if (_pointer == null || _pointerOwner != EventSystem.current) // 씬이 바뀌어 EventSystem이 달라지면 새로 만듦
            {
                _pointer = new PointerEventData(EventSystem.current);
                _pointerOwner = EventSystem.current;
            }
            _pointer.position = mouse;
            EventSystem.current.RaycastAll(_pointer, _hits);
        }

        _text.AppendLine($"<b>[{ToggleKey}] 클릭 받는 UI {_hits.Count}개</b> (첫 줄이 클릭 대상)");
        if (EventSystem.current == null) _text.AppendLine("  <color=red>EventSystem이 없음</color>");
        for (int i = 0; i < _hits.Count && i < MaxRows; i++)
        {
            Graphic graphic = _hits[i].gameObject.GetComponent<Graphic>();
            _text.AppendLine("  " + Describe(_hits[i].gameObject, graphic));
        }

        // 2. 마우스 아래 모든 UI (Raycast Target 꺼진 아이콘, 투명한 이미지 포함)
        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + ScanInterval;
            ScanGraphicsUnder(mouse);
        }

        _text.AppendLine($"<b>마우스 아래 모든 UI {_under.Count}개</b> (위에 그려지는 순)");
        for (int i = 0; i < _under.Count && i < MaxRows; i++)
        {
            if (_under[i] != null) _text.AppendLine("  " + Describe(_under[i].gameObject, _under[i]));
        }
    }

    private void ScanGraphicsUnder(Vector2 mouse)
    {
        _under.Clear();
        foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
        {
            if (!graphic.isActiveAndEnabled || graphic.canvas == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, mouse, CanvasCamera(graphic.canvas)))
                _under.Add(graphic);
        }

        // 캔버스 정렬 순서 → 캔버스 안 그리는 순서로, 위에 보이는 것부터
        _under.Sort((a, b) =>
        {
            int order = b.canvas.rootCanvas.sortingOrder.CompareTo(a.canvas.rootCanvas.sortingOrder);
            return order != 0 ? order : b.depth.CompareTo(a.depth);
        });
    }

    // 계층 경로와 이미지 상태를 한 줄로
    private string Describe(GameObject go, Graphic graphic)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        for (int depth = 0; parent != null && depth < 2; depth++, parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        if (graphic == null) return path;

        string info = $"a={graphic.color.a:0.##}";
        if (graphic is Image image)
            info = $"sprite={(image.sprite != null ? image.sprite.name : "<color=red>없음</color>")} " + info;
        else if (graphic is RawImage raw)
            info = $"texture={(raw.texture != null ? raw.texture.name : "<color=red>없음</color>")} " + info;

        if (graphic.material != null && graphic.material != graphic.defaultMaterial)
            info += $" mat={graphic.material.name}({graphic.material.shader.name})";
        if (!graphic.raycastTarget) info += " raycast=off";

        return $"{path}  <color=#9cf>{info}</color>";
    }

    private static Camera CanvasCamera(Canvas canvas)
    {
        Canvas root = canvas.rootCanvas;
        return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = false };
            _style.normal.textColor = Color.white;
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        // 클릭 대상 UI에 노란 테두리
        if (_hits.Count > 0 && _hits[0].gameObject != null)
            DrawOutline(_hits[0].gameObject.transform as RectTransform, Color.yellow);

        GUIContent content = new GUIContent(_text.ToString());
        Vector2 size = _style.CalcSize(content);
        Rect box = new Rect(10, 10, size.x + 16, size.y + 12);

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(box, _pixel);
        GUI.color = prev;
        GUI.Label(new Rect(box.x + 8, box.y + 6, size.x, size.y), content, _style);
    }

    private void DrawOutline(RectTransform rect, Color color)
    {
        if (rect == null) return;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null ? CanvasCamera(canvas) : null;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        // GUI 좌표는 위가 0이라 y를 뒤집음
        Rect r = Rect.MinMaxRect(min.x, Screen.height - max.y, max.x, Screen.height - min.y);
        const float t = 2f;

        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), _pixel);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), _pixel);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), _pixel);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), _pixel);
        GUI.color = prev;
    }

    private void OnDestroy()
    {
        if (_pixel != null) Destroy(_pixel);
    }
}
