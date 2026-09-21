using TMPro;
using UnityEngine;

// 상호작용 반경 안에 들어왔을 때만 뜨는 안내. 상호작용 키는 Interactor에서 가져온다
public class ScrapInteractPrompt : MonoBehaviour
{
    [Tooltip("상호작용 가능 여부와 키를 가져올 곳")]
    [SerializeField] private ScrapInteractor _interactor;

    [Tooltip("켜고 끌 안내 루트")]
    [SerializeField] private GameObject _root;

    [Tooltip("안내 문구. {0} 자리에 키 이름이 들어간다")]
    [SerializeField] private string _format = "[{0}] 뒤지기";

    [SerializeField] private TMP_Text _label;

    private bool _shown = true;

    private void Start()
    {
        if (_label != null && _interactor != null)
            _label.text = string.Format(_format, _interactor.InteractKey);

        Apply(false);
    }

    private void Update()
    {
        bool canInteract = _interactor != null && _interactor.CanInteract;
        if (canInteract != _shown)
            Apply(canInteract);
    }

    private void Apply(bool shown)
    {
        _shown = shown;

        if (_root != null)
            _root.SetActive(shown);
    }
}
