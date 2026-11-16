using System.Collections;
using System.Reflection;
using UnityEngine;

namespace RealSteel.Dialogue
{
    // 등록직원 NPC 선택지 → LSH 경매(Auction) 연결. 그쪽 코드는 수정하지 않고 이름(리플렉션)으로 부름
    //  - 일반 · 주먹 경매: '경매 시작' 버튼을 누른 것처럼 시작 → 모드 선택 창이 뜨면 고른 모드를 대신 누름 (첫 회차)
    //  - 옵션: 경매장 판매 창(shopPanel)을 열고, 닫을 때는 그쪽 닫기 함수(OnCloseShop)를 씀
    // 이름이 바뀌면 콘솔에 안내만 하고 아무것도 하지 않음
    public static class AuctionBridge
    {
        public const string AuctionTypeName = "Auction";
        public const string CloseOptionMethod = "OnCloseShop";

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float ModeSelectTimeout = 180f;
        private const float FieldReadyTimeout = 5f;

        public enum Mode
        {
            Normal,
            Fist,
        }

        public static MonoBehaviour Find()
        {
            foreach (MonoBehaviour behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (behaviour != null && behaviour.GetType().Name == AuctionTypeName) return behaviour;
            }
            return null;
        }

        // 경매 시작 + 모드 자동 선택. runner는 기다리는 코루틴을 돌릴 오브젝트
        public static bool StartAuction(Mode mode, MonoBehaviour runner)
        {
            MonoBehaviour auction = Find();
            if (auction == null)
            {
                Debug.LogWarning($"[경매 연결] 이 씬에 경매 관리자({AuctionTypeName})가 없어 경매를 시작할 수 없습니다.");
                return false;
            }

            System.Type type = auction.GetType();
            object waiting = type.GetField("startAuctionTcs", AnyInstance)?.GetValue(auction);
            bool isFinished = type.GetProperty("IsAuctioningFin", AnyInstance)?.GetValue(auction) as bool? ?? true;
            if (waiting == null && !isFinished)
            {
                Debug.LogWarning("[경매 연결] 이미 경매가 진행 중입니다.", auction);
                return false;
            }

            MethodInfo start = type.GetMethod("OnStartAuctionButton", AnyInstance, null, System.Type.EmptyTypes, null);
            if (start == null)
            {
                Debug.LogWarning($"[경매 연결] {type.Name}에 'OnStartAuctionButton' 함수가 없어 경매를 시작할 수 없습니다.", auction);
                return false;
            }

            runner.StartCoroutine(StartWhenFieldReady(auction, start, mode));
            return true;
        }

        // 대화 연출(클로즈업 카메라 복귀)과 입력 잠금이 다 풀린 뒤에 경매 시작
        //  - 대화 중에는 FieldInputBridge가 캐릭터 이동(RSSimpleCharacter)을 꺼 둠 → 그때 착석하면 AuctionSeatLock이
        //    '이미 꺼져 있음'으로 보고 기록하지 않고, 대화가 끝나면 브리지가 다시 켜서 카메라가 캐릭터를 따라가 버림
        private static IEnumerator StartWhenFieldReady(MonoBehaviour auction, MethodInfo start, Mode mode)
        {
            float waited = 0f;
            while (auction != null && waited < FieldReadyTimeout && !IsFieldReady())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (auction == null) yield break;
            if (waited >= FieldReadyTimeout) Debug.LogWarning("[경매 연결] 대화 화면이 다 닫히지 않았지만 경매를 시작합니다. 카메라 고정이 풀릴 수 있습니다.", auction);

            start.Invoke(auction, null);
            Debug.Log($"[경매 연결] 경매 시작 → 모드 선택 창이 뜨면 '{(mode == Mode.Normal ? "일반 경매" : "주먹 경매")}'를 고름");
            yield return PickModeWhenAsked(auction, mode);
        }

        private static bool IsFieldReady()
        {
            if (UiStack.Current != null && UiStack.Current.Count > 0) return false;
            foreach (FieldInputBridge bridge in Object.FindObjectsByType<FieldInputBridge>(FindObjectsSortMode.None))
            {
                if (bridge.isActiveAndEnabled && bridge.IsBlocking) return false;
            }
            return true;
        }

        private static IEnumerator PickModeWhenAsked(MonoBehaviour auction, Mode mode)
        {
            System.Type type = auction.GetType();
            FieldInfo selecting = type.GetField("isSelectingMode", AnyInstance);
            MethodInfo choose = type.GetMethod(mode == Mode.Normal ? "OnSelectNormalMode" : "OnSelectFistMode", AnyInstance, null, System.Type.EmptyTypes, null);
            if (selecting == null || choose == null)
            {
                Debug.LogWarning($"[경매 연결] {type.Name}의 모드 선택 함수 이름이 바뀌어 자동 선택을 못 합니다. 화면의 모드 버튼을 직접 누르세요.", auction);
                yield break;
            }

            float waited = 0f;
            while (auction != null && waited < ModeSelectTimeout)
            {
                if (selecting.GetValue(auction) is bool asked && asked)
                {
                    choose.Invoke(auction, null);
                    yield break;
                }
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (auction != null) Debug.LogWarning("[경매 연결] 모드 선택 창이 뜨지 않아 자동 선택을 그만둡니다.", auction);
        }

        // 경매장 판매 옵션 창 열기. 연 창과 그 주인(닫기 함수용)을 돌려줌
        public static GameObject OpenOption(out MonoBehaviour owner)
        {
            owner = Find();
            if (owner == null)
            {
                Debug.LogWarning($"[경매 연결] 이 씬에 경매 관리자({AuctionTypeName})가 없어 옵션 창을 열 수 없습니다.");
                return null;
            }

            var window = owner.GetType().GetField("shopPanel", AnyInstance)?.GetValue(owner) as GameObject;
            if (window == null)
            {
                Debug.LogWarning($"[경매 연결] {owner.GetType().Name}의 'shopPanel' 칸이 비어 있거나 이름이 바뀌어 옵션 창을 열 수 없습니다.", owner);
                return null;
            }

            window.SetActive(true);
            return window;
        }
    }
}
