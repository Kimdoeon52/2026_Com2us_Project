using UnityEngine;

// 미니게임 중 플레이어 조작 스크립트를 끈다.
// 어떤 구현이든 인스펙터로 꽂으므로 플레이어 클래스를 참조하지 않는다
public class ScrapInputGateRelay : MonoBehaviour
{
    [Tooltip("미니게임 중 끌 컴포넌트. 플레이어 이동·카메라 스크립트")]
    [SerializeField] private Behaviour[] _targets;

    private bool _blocked;

    private void Update()
    {
        if (_blocked == ScrapInputGate.IsBlocked)
            return;

        _blocked = ScrapInputGate.IsBlocked;

        for (int i = 0; i < _targets.Length; i++)
        {
            if (_targets[i] != null)
                _targets[i].enabled = !_blocked;
        }
    }
}
