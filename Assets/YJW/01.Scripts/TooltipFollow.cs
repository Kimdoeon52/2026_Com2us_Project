using UnityEngine;

public class TooltipFollow : MonoBehaviour
{
    [SerializeField] private Vector2 offset = new Vector2(15f, 15f); // 마우스와의 간격(픽셀)

    private RectTransform rect;
    private Canvas canvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void OnEnable() => UpdatePosition(); // 켜지는 순간 한 프레임 튀는 것 방지
    private void LateUpdate() => UpdatePosition();

    private void UpdatePosition()
    {
        Vector2 mouse = Input.mousePosition;

        // 마우스가 화면 오른쪽/위쪽에 있으면 툴팁을 반대편으로 뒤집어서 화면 밖으로 안 나가게
        float px = mouse.x > Screen.width * 0.5f ? 1f : 0f;
        float py = mouse.y > Screen.height * 0.5f ? 1f : 0f;
        rect.pivot = new Vector2(px, py);

        Vector2 o = new Vector2(px == 0f ? offset.x : -offset.x,
                                py == 0f ? offset.y : -offset.y);

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)canvas.transform, mouse + o, cam, out Vector3 worldPos);

        rect.position = worldPos;
    }
}