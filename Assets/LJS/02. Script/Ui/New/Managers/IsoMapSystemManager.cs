using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class IsoMapSystemManager : MonoBehaviour
{
    public static IsoMapSystemManager Instance;
    public static event Action OnMapClosed;

    [Header("카메라 세팅 (3D)")]
    public Camera mapCamera; // 맵 카메라

    [Header("UI Panels")]
    public GameObject mapCanvas;
    public GameObject confirmButton;
    public TextMeshProUGUI confirmText;

    [Header("예외 안내")]
    public GameObject currentLocationUI; // 현재 위치입니다 패널
    public float noticeDuration = 1f;

    private MapElement3D selectedElement; // 💡 3D 요소로 변경
    private MapElement3D currentHoveredElement; // 현재 마우스가 올라가 있는 요소
    private Tween noticeDt; // 숨김용 도트윈 변수

    private bool isMapOpen = false;
    public bool isConfirmOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else return; 

        mapCanvas.SetActive(false);
        confirmButton.SetActive(false);
        if (currentLocationUI != null) currentLocationUI.SetActive(false);
        if (mapCamera != null) mapCamera.gameObject.SetActive(false);
    }

    // 레이캐스트 / 마우스 위치를 확인하는 핵심 로직
    private void Update()
    {
        if (!isMapOpen) return;
        if (TimeSystemManager.Instance.currentState == TimeSystemManager.TimeState.Combat) return;
        if (EventSystem.current.IsPointerOverGameObject()) return;

        if (isConfirmOpen)
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnClickCancel();
            }
            return;
        }

        int layerMask = 1 << LayerMask.NameToLayer("Map"); // 맵만 적중용 마스크

        Ray ray = mapCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, layerMask))
        {
            // 광선에 맞은 오브젝트의 부모에게서 MapElement3D를 찾음
            MapElement3D hitElement = hit.collider.GetComponentInParent<MapElement3D>();
            if (hitElement != null) // 건물을 가리킴
            {
                if (currentHoveredElement != hitElement) // 새로운 건물에 마우스를 올렸다면
                {
                    ClearHover();
                    currentHoveredElement = hitElement;
                    currentHoveredElement.HoverOn(); // 하이라이트 켜기
                }

                if (Input.GetMouseButtonDown(0)) // 클릭했다면
                {
                    OpenConfirmPopup(currentHoveredElement);
                }
            }
            else ClearHover(); // 건물 외의 것(배경 Plane)을 가리킴
        }
        else ClearHover(); // 허공을 가리킴
    }

    private void OnDestroy()
    {
        // 오브젝트 파괴 시 실행 중인 트윈 정리
        noticeDt?.Kill();
    }

    private void ClearHover()
    {
        if (currentHoveredElement != null)
        {
            currentHoveredElement.HoverOff();
            currentHoveredElement = null;
        }
    }

    public void ToggleMapUI()
    {
        isMapOpen = !isMapOpen;

        if (mapCamera != null)
        {
            mapCamera.gameObject.SetActive(isMapOpen);
        }

        mapCanvas.SetActive(isMapOpen);

        if (isMapOpen) TimeSystemManager.Instance.BeginAction(this);
        else
        {
            TimeSystemManager.Instance.EndAction(this, 0);
            OnMapClosed?.Invoke();
        }

        confirmButton.SetActive(false);

        if (!isMapOpen)
        {
            isConfirmOpen = false;
            selectedElement = null;
            ClearHover();
            HideCurrentLocationUI();
        }
    }

    public void OpenConfirmPopup(MapElement3D element) // UI 팝업. 현재 지역이면 불가 
    {

        if (SceneManager.GetActiveScene().name == element.targetSceneName)
        {
            ClearHover();
            ShowCurrentLocationUI();
            return;
        }
        HideCurrentLocationUI();

        selectedElement = element;
        confirmText.text = $"'{element.elementName}'(으)로\n이동하시겠습니까?";
        confirmButton.SetActive(true);
        isConfirmOpen = true;

        ClearHover();
    }

    private void ShowCurrentLocationUI()
    {
        if (currentLocationUI == null) return;
        noticeDt?.Kill();
        currentLocationUI.SetActive(true);

        noticeDt = DOVirtual.DelayedCall(noticeDuration, () => // 변수 시간만큼 대기 후 ui 비활성화
        {
            if (currentLocationUI != null)
            {
                currentLocationUI.SetActive(false);
            }
        }).SetUpdate(true);
    }

    private void HideCurrentLocationUI()
    {
        noticeDt?.Kill();
        noticeDt = null;

        if (currentLocationUI != null)
        {
            currentLocationUI.SetActive(false);
        }
    }

    public void OnClickMove()
    {
        if (selectedElement != null)
        {
            // 불안정한 코어로는 전투 씬에 바로 들어갈 수 없다
            if (selectedElement.targetSceneName == "Battle" && InventoryHost.Instance != null && !InventoryHost.Instance.CanSortie)
            {
                confirmText.text = "코어가 불안정합니다.\n코어를 복구해야 출전할 수 있습니다.";
                return;
            }

            LoadingManager.Instance.LoadScene(selectedElement.targetSceneName); // 씬 이동
            TimeSystemManager.Instance.PassTime(selectedElement.useHours); // 
            ToggleMapUI();
        }
    }

    public void OnClickCancel()
    {
        confirmButton.SetActive(false);
        selectedElement = null;
        isConfirmOpen = false;
    }
}
