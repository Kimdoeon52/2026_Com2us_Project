using UnityEngine;

/// <summary>
/// 총알·폭발처럼 "잠깐 생겼다 사라지는 스프라이트 오브젝트"를 만드는 자리 (View).
///
/// 프리팹을 따로 만들지 않고 코드로 조립하는 이유: 필요한 게 SpriteRenderer + Animator 두 개뿐인데,
/// 프리팹으로 두면 머티리얼·정렬·회전을 로봇과 매번 손으로 맞춰야 한다. 여기서는 그 값을 쏜 로봇
/// (styleSource)에서 그대로 복사해서, 2.5D 렌더 설정이 바뀌어도 이펙트가 자동으로 따라가게 한다.
/// </summary>
public static class SpriteEffect
{
    /// <summary>
    /// styleSource(보통 로봇)와 같은 렌더 설정으로 스프라이트 오브젝트를 만든다. 좌우는 RobotMover와 같은
    /// 규칙(localScale.x 부호)으로 뒤집는다 — 로봇과 같은 시트에서 나온 그림이라 같은 규칙이 맞다
    /// </summary>
    public static GameObject Create(string name, RuntimeAnimatorController controller, Vector3 position, Transform styleSource, bool facingRight)
    {
        var go = new GameObject(name);
        go.transform.position = position;

        float scale = 1f;
        var renderer = go.AddComponent<SpriteRenderer>();
        if (styleSource != null)
        {
            // 로봇이 Y축 180도 돌아간 채로 배치돼 있어도 같은 각도로 보이게 회전까지 복사한다
            go.transform.rotation = styleSource.rotation;
            scale = Mathf.Abs(styleSource.localScale.x);

            var sourceRenderer = styleSource.GetComponent<SpriteRenderer>();
            if (sourceRenderer != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                // 로봇보다 한 칸 앞 — 총알·폭발이 몸에 가려지지 않게 (§10 정렬은 sortingOrder로)
                renderer.sortingOrder = sourceRenderer.sortingOrder + 1;
            }
        }
        go.transform.localScale = new Vector3(facingRight ? scale : -scale, scale, scale);

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return go;
    }

    /// <summary>
    /// 한 번 재생하고 스스로 사라지는 이펙트(폭발 등). lifetimeSeconds가 0이면 스테이트 클립 길이만큼 보여준다.
    /// 판정에 안 쓰이는 순수 연출이라 CombatTick이 아니라 실제 시간(Destroy 타이머)으로 지운다 (§3 "렌더·UI는 틱 바깥")
    /// </summary>
    public static GameObject PlayOnce(RuntimeAnimatorController controller, string stateName, Vector3 position, Transform styleSource, float lifetimeSeconds = 0f)
    {
        if (controller == null || string.IsNullOrEmpty(stateName)) return null;

        GameObject go = Create($"Effect_{stateName}", controller, position, styleSource, true);
        var animator = go.GetComponent<Animator>();
        animator.Play(stateName, 0, 0f);
        animator.Update(0f); // 클립 길이를 바로 읽으려면 상태 전이를 즉시 확정해야 한다 (RobotView.PlayAction과 같은 이유)

        float lifetime = lifetimeSeconds > 0f ? lifetimeSeconds : animator.GetCurrentAnimatorStateInfo(0).length;
        Object.Destroy(go, lifetime);
        return go;
    }
}
