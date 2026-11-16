using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 경매 중 플레이어 착석 + 이동 잠금 + 카메라 무대 정면 고정
// - Seat(): 플레이어를 좌석으로 순간이동, 이동 컴포넌트(RSSimpleCharacter)와 CharacterController를 끄고
//           카메라를 무대 정면의 넓은 시점으로 옮겨 고정
// - Release(): 전부 원래대로 (이동 가능, 카메라는 다시 캐릭터를 따라감)
// Auction이 없으면 자동으로 붙여서 기본값으로 동작. 인스펙터에서 미리 붙여두면 값을 조절할 수 있음
public class AuctionSeatLock : MonoBehaviour
{
    [Header("대상 (비우면 자동)")]
    [SerializeField] private Transform playerRoot; // 비우면 AuctionPlayer가 붙은 오브젝트
    [SerializeField] private Camera auctionCamera; // 비우면 캐릭터가 쓰는 카메라 → Main Camera

    [Header("카메라 (cameraPoint를 지정하면 그 위치/회전을 그대로 사용)")]
    [SerializeField] private Transform cameraPoint;
    [SerializeField] private Transform stageFocus; // 카메라가 바라볼 무대 지점. 비우면 무대 막(RSStageCurtain) 위치
    [SerializeField] private float stageFocusHeight = 1.5f; // 무대 막 기준 바라볼 높이
    [SerializeField] private float cameraBack = 7f; // 플레이어 좌석에서 뒤로 얼마나 떨어질지
    [SerializeField] private float cameraHeight = 3.5f; // 좌석 바닥 기준 카메라 높이
    [SerializeField] private float fieldOfView = 45f; // 넓게 보이도록 (기존 무대 카메라는 24)
    [SerializeField] private float cameraMoveTime = 0.8f;

    [Header("착석")]
    [SerializeField] private bool hidePlayerSprites = true; // 좌석에 플레이어 초상화가 따로 있으므로 캐릭터 스프라이트는 숨김

    public bool IsSeated { get; private set; }

    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    private CharacterController disabledController;
    private Camera usedCamera;
    private Vector3 savedCamPos;
    private Quaternion savedCamRot;
    private float savedFov;

    // 착석 중 유지할 상태 (다른 시스템이 이동/카메라를 다시 켜도 되돌림)
    private Transform seatedPlayer;
    private Vector3 seatPosition;
    private RSSimpleCharacter lockedMover;
    private RealSteel.Dialogue.PlayerInteractor lockedInteractor;
    private Vector3 camTargetPos;
    private Quaternion camTargetRot;
    private bool camHolding; // 카메라 이동 연출이 끝난 뒤 매 프레임 고정

    // 플레이어 찾기: 지정값 → 이동 캐릭터(RSSimpleCharacter)가 붙은 AuctionPlayer → 씬의 RSSimpleCharacter → AuctionPlayer
    private Transform FindPlayer()
    {
        if (playerRoot != null) return playerRoot;
        AuctionPlayer ap = AuctionPlayer.Instance;
        if (ap != null && ap.GetComponentInParent<RSSimpleCharacter>() != null) return ap.GetComponentInParent<RSSimpleCharacter>().transform;
        RSSimpleCharacter character = FindFirstObjectByType<RSSimpleCharacter>();
        if (character != null) return character.transform;
        return ap != null ? ap.transform : null;
    }

    // hideCharacter: 좌석 초상화가 있을 때만 걸어다니던 캐릭터를 숨김 (없으면 캐릭터를 그대로 보여줌)
    public void Seat(Transform seat, Transform keepVisible = null, bool hideCharacter = true)
    {
        if (IsSeated || seat == null) return;
        IsSeated = true;

        Transform player = FindPlayer();
        RSSimpleCharacter mover = player != null ? player.GetComponent<RSSimpleCharacter>() : null;
        if (player == null) Debug.LogWarning("[AuctionSeatLock] 플레이어를 못 찾았습니다. 인스펙터의 playerRoot에 플레이어를 넣어주세요.", this);

        // 1. 이동 잠금 (이동/카메라 추적 스크립트 + CharacterController + NPC 상호작용 끄기)
        if (player != null)
        {
            seatedPlayer = player;
            lockedMover = mover;
            if (mover != null)
            {
                mover.enabled = true; // 대화 브리지가 잠깐 꺼둔 상태여도 '원래 켜져 있던 것'으로 기록 → 해제 시 다시 켜짐
                Disable(mover);
            }
            if (player.TryGetComponent(out CharacterController cc)) Disable(cc);
            lockedInteractor = player.GetComponentInChildren<RealSteel.Dialogue.PlayerInteractor>();
            if (lockedInteractor == null) lockedInteractor = FindFirstObjectByType<RealSteel.Dialogue.PlayerInteractor>();
            if (lockedInteractor != null) Disable(lockedInteractor); // 경매 중 E키로 NPC 메뉴가 열리지 않게

            // 2. 좌석으로 순간이동 (좌석 바닥 = 초상화의 부모 슬롯)
            player.position = seat.position;
            seatPosition = seat.position;

            if (hidePlayerSprites && hideCharacter)
            {
                foreach (SpriteRenderer sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!sr.enabled) continue;
                    if (keepVisible != null && sr.transform.IsChildOf(keepVisible)) continue; // 말풍선은 유지
                    sr.enabled = false;
                    hiddenRenderers.Add(sr);
                }
            }
        }

        // 3. 카메라 무대 정면 고정
        usedCamera = auctionCamera != null ? auctionCamera : (mover != null && mover.cam != null ? mover.cam : Camera.main);
        if (usedCamera == null) return;
        Transform camT = usedCamera.transform;
        savedCamPos = camT.position;
        savedCamRot = camT.rotation;
        savedFov = usedCamera.fieldOfView;

        Vector3 targetPos;
        Quaternion targetRot;
        if (cameraPoint != null)
        {
            targetPos = cameraPoint.position;
            targetRot = cameraPoint.rotation;
        }
        else
        {
            Vector3 focus = GetStageFocus(seat);
            Vector3 toStage = focus - seat.position;
            toStage.y = 0f;
            Vector3 forward = toStage.sqrMagnitude > 0.01f ? toStage.normalized : Vector3.forward; // 무대 정면 방향
            targetPos = seat.position - forward * cameraBack + Vector3.up * cameraHeight;
            targetRot = Quaternion.LookRotation(focus - targetPos, Vector3.up);
        }

        camTargetPos = targetPos;
        camTargetRot = targetRot;
        camHolding = false;

        camT.DOKill();
        usedCamera.DOKill();
        camT.DOMove(targetPos, cameraMoveTime).SetEase(Ease.InOutSine).SetLink(usedCamera.gameObject)
            .OnComplete(() => camHolding = IsSeated);
        camT.DORotateQuaternion(targetRot, cameraMoveTime).SetEase(Ease.InOutSine).SetLink(usedCamera.gameObject);
        if (!usedCamera.orthographic)
            DOTween.To(() => usedCamera.fieldOfView, v => usedCamera.fieldOfView = v, fieldOfView, cameraMoveTime).SetLink(usedCamera.gameObject);
    }

    // 착석 중에는 다른 시스템(대화 입력 브리지 등)이 이동/카메라를 다시 켜도 즉시 되돌림
    private void LateUpdate()
    {
        if (!IsSeated) return;

        if (lockedMover != null && lockedMover.enabled)
        {
            lockedMover.enabled = false;
            if (!disabledBehaviours.Contains(lockedMover)) disabledBehaviours.Add(lockedMover);
        }
        if (lockedInteractor != null && lockedInteractor.enabled)
        {
            lockedInteractor.enabled = false;
            if (!disabledBehaviours.Contains(lockedInteractor)) disabledBehaviours.Add(lockedInteractor);
        }
        if (seatedPlayer != null && (seatedPlayer.position - seatPosition).sqrMagnitude > 0.0001f)
            seatedPlayer.position = seatPosition;

        if (camHolding && usedCamera != null)
        {
            Transform camT = usedCamera.transform;
            camT.SetPositionAndRotation(camTargetPos, camTargetRot);
            if (!usedCamera.orthographic) usedCamera.fieldOfView = fieldOfView;
        }
    }

    public void Release()
    {
        if (!IsSeated) return;
        IsSeated = false;
        camHolding = false;
        seatedPlayer = null;
        lockedMover = null;
        lockedInteractor = null;

        foreach (Renderer r in hiddenRenderers) if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
        if (disabledController != null) disabledController.enabled = true;
        disabledController = null;
        foreach (Behaviour b in disabledBehaviours) if (b != null) b.enabled = true; // 캐릭터가 다시 카메라를 따라잡음
        disabledBehaviours.Clear();

        if (usedCamera != null)
        {
            usedCamera.transform.DOKill();
            usedCamera.DOKill();
            if (!usedCamera.orthographic) usedCamera.fieldOfView = savedFov;
        }
    }

    private void Disable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabledBehaviours.Add(b);
    }

    private void Disable(CharacterController cc) // CharacterController는 Behaviour가 아니라 따로 보관
    {
        if (cc == null || !cc.enabled) return;
        cc.enabled = false;
        disabledController = cc;
    }

    private Vector3 GetStageFocus(Transform seat)
    {
        if (stageFocus != null) return stageFocus.position;
        var curtain = FindFirstObjectByType<RealSteel.Build.RSStageCurtain>();
        if (curtain != null) return curtain.transform.position + Vector3.up * stageFocusHeight;
        return seat.position + Vector3.forward * 6f + Vector3.up * stageFocusHeight; // 무대가 +Z 쪽에 있다고 가정
    }

    private void OnDisable()
    {
        Release();
    }
}
