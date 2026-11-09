using UnityEngine;

namespace RealSteel.Boss.Visual
{
    /// <summary>
    /// [보스전 전용 VFX 스폰 & 수명 관리 헬퍼 (BossVfxHelper)]
    /// 1. 지정 위치/각도에 이펙트 프리팹을 생성하고, ParticleSystem/Animator 재생 시간에 맞춰 자동 파괴(Destroy) 예약.
    /// 2. 2D 환경에서 보스나 배경 뒤로 가려지지 않도록 SortingOrder 및 레이어를 보정.
    /// 3. 프리팹/오브젝트 내 콜라이더 및 리지드바디를 비활성화하고 Ignore Raycast 레이어로 격리하여 전투 판정 오염 방지.
    /// 4. 인스펙터에 프리팹이 미할당(null)된 상태에서도 안전하게 무시(Null-Safe).
    /// </summary>
    public static class BossVfxHelper
    {
        /// <summary>
        /// [판정 완전 분리]: 대상 GameObject 및 하위 계층의 콜라이더/리지드바디를 비활성화하고,
        /// 태그를 "Untagged", 레이어를 "Ignore Raycast"로 설정하여 피격/공격 판정에 일체 간섭하지 않도록 격리함.
        /// </summary>
        public static void ConfigureVisualOnly(GameObject instance)
        {
            if (instance == null) return;

            // 1) 2D 및 3D 콜라이더 비활성화
            var colliders2D = instance.GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders2D)
            {
                if (col != null) col.enabled = false;
            }
            var colliders3D = instance.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders3D)
            {
                if (col != null) col.enabled = false;
            }

            // 2) 리지드바디 시뮬레이션 중단
            var rbs2D = instance.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var rb in rbs2D)
            {
                if (rb != null) rb.simulated = false;
            }

            // 3) 태그 및 레이어를 순수 연출용(Ignore Raycast / Untagged)으로 강제 격리
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer < 0) ignoreRaycastLayer = 2; // Unity Default Ignore Raycast = 2

            instance.tag = "Untagged";
            instance.layer = ignoreRaycastLayer;
            var allTransforms = instance.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t != null)
                {
                    t.gameObject.tag = "Untagged";
                    t.gameObject.layer = ignoreRaycastLayer;
                }
            }
        }

        public static GameObject SpawnVfx(
            GameObject vfxPrefab,
            Vector3 position,
            Quaternion rotation = default,
            Vector3? scale = null,
            float defaultLifetime = 2.0f,
            Transform parent = null,
            int sortingOrderOffset = 2)
        {
            if (vfxPrefab == null) return null;

            GameObject instance = Object.Instantiate(vfxPrefab, position, rotation, parent);
            if (instance == null) return null;

            if (scale.HasValue)
            {
                instance.transform.localScale = scale.Value;
            }

            // [판정 완전 분리]
            ConfigureVisualOnly(instance);

            // 1. 2D 렌더링 정렬 보정 및 HorizontalBillboard 최대 크기 제한 해제
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.sortingOrder <= 0)
                {
                    r.sortingOrder += sortingOrderOffset;
                }
                if (r is ParticleSystemRenderer psr && psr.renderMode == ParticleSystemRenderMode.HorizontalBillboard)
                {
                    psr.maxParticleSize = Mathf.Max(psr.maxParticleSize, 5.0f);
                }
            }

            // 2. 파티클 시스템 수명 자동 연산 및 3D 스케일/Burst 재시작 보장
            float duration = defaultLifetime;
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (particles != null && particles.Length > 0)
            {
                float maxParticleLifetime = 0f;
                bool needsRestart = scale.HasValue;
                foreach (var ps in particles)
                {
                    if (ps == null) continue;
                    var main = ps.main;
                    if (main.loop)
                    {
                        main.loop = false;
                    }

                    // 지면 원근 스케일(z > 1.5)을 사용하는 파티클은 3D Start Size 활성화
                    if (instance.transform.localScale.z > 1.5f && !main.startSize3D)
                    {
                        main.startSize3D = true;
                        needsRestart = true;
                    }

                    if (needsRestart)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.Play(true);
                    }
                    else if (!ps.isPlaying && main.playOnAwake)
                    {
                        ps.Play(true);
                    }

                    float psTime = main.duration + main.startLifetime.constantMax;
                    if (psTime > maxParticleLifetime)
                    {
                        maxParticleLifetime = psTime;
                    }
                }

                if (maxParticleLifetime > 0.05f)
                {
                    duration = maxParticleLifetime;
                }
            }
            else
            {
                // 파티클이 없으면 Animator 클립 길이 검사
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    var clips = animator.runtimeAnimatorController.animationClips;
                    if (clips != null && clips.Length > 0)
                    {
                        float maxClipLength = 0f;
                        foreach (var c in clips)
                        {
                            if (c != null && c.length > maxClipLength) maxClipLength = c.length;
                        }
                        if (maxClipLength > 0.05f) duration = maxClipLength;
                    }
                }
            }

            // 3. 자동 메모리 회수 예약
            Object.Destroy(instance, Mathf.Max(0.25f, duration));
            return instance;
        }
    }
}
