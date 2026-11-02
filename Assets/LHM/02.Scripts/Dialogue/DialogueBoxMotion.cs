using System;
using System.Collections;
using Michsky.UI.MTP;
using UnityEngine;

namespace RealSteel.Dialogue
{
    public class DialogueBoxMotion : MonoBehaviour
    {
        private const string InState = "In";
        private const string OutState = "Out";
        private const string FinishedState = "Finished";

        [Header("연결")]
        [SerializeField] private StyleManager style;

        [Header("설정")]
        [SerializeField, Range(0.1f, 2.5f)] private float speed = 1f;
        [SerializeField, Min(0.1f)] private float maxCloseSeconds = 2f;

        private Animator animator;
        private Coroutine closing;

        public bool IsClosing => closing != null;


        private void Awake()
        {
            if (style == null) style = GetComponent<StyleManager>();
            animator = style != null ? (style.styleAnimator != null ? style.styleAnimator : style.GetComponent<Animator>()) : null;

            if (style == null || animator == null)
            {
                Debug.LogError("[BoxMotion] Style(StyleManager)과 Animator가 필요합니다. 대화창 모션 적용 메뉴를 다시 실행하세요.", this);
                enabled = false;
                return;
            }

            style.playOnEnable = false;
            style.playOutAnimation = false;
            style.disableOnOut = false;
            style.loopAnimations = false;
            style.UseUnscaledTime = true;
            style.AnimationSpeed = speed;
            animator.Play(FinishedState, 0, 0f);
        }

        public void PlayOpen()
        {
            if (!enabled) return;

            StopClosing();
            style.AnimationSpeed = speed;
            style.PlayIn();
            if (animator.isActiveAndEnabled)
            {
                animator.Play(InState, 0, 0f);
                animator.Update(0f);
            }
        }

        public void PlayClose(Action onHidden)
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                onHidden?.Invoke();
                return;
            }

            StopClosing();
            style.AnimationSpeed = speed;
            animator.Play(OutState, 0, 0f);
            closing = StartCoroutine(WaitClosed(onHidden));
        }

        private IEnumerator WaitClosed(Action onHidden)
        {
            float elapsed = 0f;
            yield return null;

            while (!animator.GetCurrentAnimatorStateInfo(0).IsName(FinishedState) && elapsed < maxCloseSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (elapsed >= maxCloseSeconds)
            {
                Debug.LogWarning($"[BoxMotion] 닫힘 모션이 {maxCloseSeconds}초 안에 끝나지 않아 바로 닫습니다.", this);
                animator.Play(FinishedState, 0, 0f);
            }

            closing = null;
            onHidden?.Invoke();
        }

        private void StopClosing()
        {
            if (closing == null) return;

            StopCoroutine(closing);
            closing = null;
        }
    }
}
