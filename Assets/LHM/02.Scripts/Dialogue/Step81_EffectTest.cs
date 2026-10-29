using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RealSteel.Dialogue
{
    public class Step81_EffectTest : MonoBehaviour
    {
        [SerializeField] private DialogueDirector director;
        [SerializeField] private DialoguePortrait portrait;
        [SerializeField] private string soundId = "sfx.impact";
        [SerializeField] private string speakerId = "auctioneer";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Start()
        {
            if (director == null)
            {
                Debug.LogError("[Step81] Director가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            director.BeginDialogue();
            Debug.Log("[Step81] Z 확대 / Shift+Z 원래대로 / X 흔들림 / C 페이드아웃 / V 페이드인 / B 효과음 / Shift+B 긴 대기 시험 / N 초상화 default / M 초상화 smile / Shift+M 숨김 / Enter 대화 종료 복귀");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool shift = keyboard.shiftKey.isPressed;

            if (keyboard.zKey.wasPressedThisFrame) Run(DialogueEffectTypes.Zoom, shift ? 1f : 1.3f, null, false);
            else if (keyboard.xKey.wasPressedThisFrame) Run(DialogueEffectTypes.Shake, 0.4f, null, false);
            else if (keyboard.cKey.wasPressedThisFrame) Run(DialogueEffectTypes.FadeOut, 1f, null, true);
            else if (keyboard.vKey.wasPressedThisFrame) Run(DialogueEffectTypes.FadeIn, 1f, null, true);
            else if (keyboard.bKey.wasPressedThisFrame) Run(shift ? DialogueEffectTypes.FadeOut : DialogueEffectTypes.Sound, shift ? 10f : 0f, soundId, true);
            else if (keyboard.nKey.wasPressedThisFrame && portrait != null) portrait.Show(speakerId, "default");
            else if (keyboard.mKey.wasPressedThisFrame && portrait != null)
            {
                if (shift) portrait.Hide();
                else portrait.Show(speakerId, "smile");
            }
            else if (keyboard.enterKey.wasPressedThisFrame)
            {
                director.EndDialogue();
                director.BeginDialogue();
                Debug.Log("[Step81] 대화 종료 복귀 실행");
            }
        }

        private void Run(string type, float value, string id, bool wait)
        {
            var effect = new EffectData { type = type, value = value, id = id, wait = wait };
            float waitTime = director.Play(new List<EffectData> { effect });
            Debug.Log($"[Step81] {effect} → 대기 {waitTime:0.00}초");
        }
#endif
    }
}
