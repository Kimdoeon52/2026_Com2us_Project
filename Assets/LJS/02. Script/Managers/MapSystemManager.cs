using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapSystemManager : MonoBehaviour
{
    public static MapSystemManager Instance;

    [Header("UI Panels")]
    public GameObject mapCanvas; // 전체 지도 UI 캔버스
    public GameObject confirmButton; // [이동/취소]가 있는 확인 팝업창
    public TextMeshProUGUI n_hourText; // "장소명(n시간 소요)" 텍스트

    [Header("Player & TargetPos")]
    public Transform playerTransform; // 플레이어 위치
    private MapElement selectedElement; // 선택된 위치(맵 아이콘)

    private bool isMapOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
        mapCanvas.SetActive(false); // 맵 ui 및 확인버튼 off
        confirmButton.SetActive(false);
    }

    private void Update()
    {
        // 
        /*if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Combat)
            return;
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleMapUI();
        }*/
    }

    public void ToggleMapUI()
    {
        isMapOpen = !isMapOpen;
        mapCanvas.SetActive(isMapOpen);

        if (isMapOpen) TimeSystemManager.Instance.BeginAction(); // 맵 켜면 시간 동결
        else TimeSystemManager.Instance.EndAction(0); // 맵 끄면 시간 흐르기(소모 0)
        confirmButton.SetActive(false);
    }

    public void OpenConfirmPopup(MapElement element) // 마우스를 호버링했을때 나타나는 UI
    {
        selectedElement = element;
        n_hourText.text = $"'{element.elementName}' ({element.useHours}시간 소요)";
        confirmButton.SetActive(true);
    }

    public void OnClickMove() // 이동버튼 누르면 해당 맵으로 순간이동
    {
        if (selectedElement != null)
        {
            playerTransform.position = selectedElement.teleportPoint.position;
            TimeSystemManager.Instance.EndAction(selectedElement.useHours);

            isMapOpen = false;
            mapCanvas.SetActive(false);
            confirmButton.SetActive(false);
            selectedElement = null;
            Debug.Log("이동 완료.");
        }
    }

    public void OnClickCancel() // 캔슬 버튼 누르면 캔슬
    {
        confirmButton.SetActive(false);
        selectedElement = null;
    }
}
