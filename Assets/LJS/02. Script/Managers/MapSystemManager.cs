using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSystemManager : MonoBehaviour
{
    public static MapSystemManager Instance;
    public static event Action OnMapClosed; // 맵 닫힐때의 이벤트

    [Header("UI Panels")]
    public GameObject mapCanvas; // 전체 지도 UI 캔버스

    [Header("Confirm Popup")]
    public GameObject confirmButton; // 이동 확인 버튼
    public TextMeshProUGUI confirmText; // 이동 확인창 내부 텍스트

    [Header("Player & TargetPos")]
    public Transform playerTransform; // 플레이어 위치
    private MapElement selectedElement; // 선택된 위치(맵 아이콘)

    private bool isMapOpen = false; // 맵이 켜졌는지
    public bool isConfirmOpen = false; // 선택창이 켜졌는지

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        mapCanvas.SetActive(false); // 맵 ui 및 확인버튼, 안내 툴팁 숨기기
        confirmButton.SetActive(false);
    }

    // 구독 함수들
    private void OnEnable() { MapElement.OnClicked += OpenConfirmPopup; }
    private void OnDisable() { MapElement.OnClicked -= OpenConfirmPopup; }

    public void ToggleMapUI()
    {
        isMapOpen = !isMapOpen;
        mapCanvas.SetActive(isMapOpen);

        if (isMapOpen) TimeSystemManager.Instance.BeginAction();
        else
        {
            TimeSystemManager.Instance.EndAction(0);
            OnMapClosed?.Invoke();
        }

        confirmButton.SetActive(false);
    }

    // 마우스를 클릭했을 때 실행되는 함수 (이동 확인창)
    public void OpenConfirmPopup(MapElement element)
    {
        selectedElement = element;
        confirmText.text = $"'{element.elementName}'(으)로 이동하시겠습니까?";
        confirmButton.SetActive(true);

        isConfirmOpen = true;
    }

    public void OnClickMove() // 이동버튼 누르면 해당 맵으로 순간이동
    {
        if (selectedElement != null)
        {
            playerTransform.position = selectedElement.teleportPoint.position;
            TimeSystemManager.Instance.EndAction(selectedElement.useHours);

            mapCanvas.SetActive(false);
            isMapOpen = false;
            confirmButton.SetActive(false);
            isConfirmOpen = false;
            selectedElement = null;
            Debug.Log("이동 완료.");
        }
    }

    public void OnClickCancel() // 캔슬 버튼 누르면 캔슬
    {
        confirmButton.SetActive(false);
        selectedElement = null;
        isConfirmOpen = false;
    }
}
