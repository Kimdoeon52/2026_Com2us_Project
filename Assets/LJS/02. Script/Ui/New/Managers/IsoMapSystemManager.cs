using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class IsoMapSystemManager : MonoBehaviour
{
    public static IsoMapSystemManager Instance;
    public static event Action OnMapClosed;

    [Header("카메라 세팅 (3D)")]
    public Camera mainCamera; // 메인 카메라(복귀용)
    public Camera mapCamera; // 맵 카메라

    [Header("UI Panels")]
    public GameObject mapCanvas;
    public GameObject confirmButton;
    public TextMeshProUGUI confirmText;

    private MapElement3D selectedElement; // 💡 3D 요소로 변경
    private MapElement3D currentHoveredElement; // 현재 마우스가 올라가 있는 요소

    private bool isMapOpen = false;
    public bool isConfirmOpen = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else return; 

        if (mainCamera == null) mainCamera = Camera.main; // 카메라 자동 할당

        mapCanvas.SetActive(false);
        confirmButton.SetActive(false);
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

        Ray ray = mapCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f))
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

        if (mapCamera != null && mainCamera != null)
        {
            mapCamera.gameObject.SetActive(isMapOpen);
            mainCamera.gameObject.SetActive(!isMapOpen);
        }

        mapCanvas.SetActive(isMapOpen);

        if (isMapOpen) TimeSystemManager.Instance.BeginAction();
        else
        {
            TimeSystemManager.Instance.EndAction(0);
            OnMapClosed?.Invoke();
        }

        confirmButton.SetActive(false);

        if (!isMapOpen)
        {
            isConfirmOpen = false;
            selectedElement = null;
            ClearHover();
        }
    }

    public void OpenConfirmPopup(MapElement3D element)
    {
        selectedElement = element;
        confirmText.text = $"'{element.elementName}'(으)로\n이동하시겠습니까?";
        confirmButton.SetActive(true);
        isConfirmOpen = true;

        ClearHover();
    }

    public void OnClickMove()
    {
        if (selectedElement != null)
        {
            LoadingManager.Instance.LoadScene(selectedElement.targetSceneName); // 씬 이동
            TimeSystemManager.Instance.PassTime(selectedElement.useHours); // 
            ToggleMapUI();
            /*mapCanvas.SetActive(false);
            isMapOpen = false;
            confirmButton.SetActive(false);
            isConfirmOpen = false;
            selectedElement = null;*/
        }
    }

    public void OnClickCancel()
    {
        confirmButton.SetActive(false);
        selectedElement = null;
        isConfirmOpen = false;
    }
}
