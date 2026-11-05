using UnityEngine;

/// <summary>
/// PixelFxData 플립북을 한 번 재생하고 스스로 사라지는 이펙트 오브젝트 (View, 2026-11-05).
/// SpriteEffect(Animator 기반 — 로봇 시트용)와 달리 Animator·클립 없이 스프라이트 배열을 직접 넘긴다 — 시트를 가져오면 바로 쓴다.
/// 순수 연출이라 전투 틱이 아니라 실제 시간(Update)으로 돈다(§3). Time.timeScale = 0이면 같이 멈춘다.
/// </summary>
public class PixelFx : MonoBehaviour
{
    private PixelFxData data;
    private SpriteRenderer spriteRenderer;
    private float age;
    private Transform follow;
    private Vector3 followOffset;

    /// <summary>pos에 이펙트 하나. scale은 data.scale에 곱해진다. data가 비었으면 null</summary>
    public static PixelFx Play(PixelFxData data, Vector3 pos, float scale = 1f, bool flipX = false)
    {
        if (data == null || data.frames == null || data.frames.Length == 0) return null;
        var go = new GameObject($"Fx_{data.name}");
        go.transform.position = pos;
        float s = data.scale * scale;
        go.transform.localScale = new Vector3(flipX ? -s : s, s, 1f);

        var fx = go.AddComponent<PixelFx>();
        fx.data = data;
        fx.spriteRenderer = go.AddComponent<SpriteRenderer>();
        fx.spriteRenderer.sortingOrder = data.sortingOrder;
        fx.Apply();
        return fx;
    }

    /// <summary>반복 이펙트를 끝낸다 (남은 페이드 없이 바로)</summary>
    public void Stop() => Destroy(gameObject);

    /// <summary>target을 따라 움직인다 (지금 자리와의 차이 유지). 회전·크기는 안 따라간다 — 찌그러짐이 이펙트에 묻지 않게</summary>
    public void Follow(Transform target)
    {
        follow = target;
        if (target != null) followOffset = transform.position - target.position;
    }

    private void Update()
    {
        if (follow != null) transform.position = follow.position + followOffset;
        age += Time.deltaTime;
        if (!data.loop && age >= data.Duration) { Destroy(gameObject); return; }
        Apply();
    }

    private void Apply()
    {
        int count = data.frames.Length;
        int index = Mathf.FloorToInt(age * data.fps * data.speed);
        index = data.loop ? index % count : Mathf.Min(index, count - 1);
        spriteRenderer.sprite = data.frames[index];

        Color c = data.tint;
        if (!data.loop && data.fadeOutFrames > 0)
            c.a *= Mathf.Clamp01((count - index) / (float)data.fadeOutFrames);
        spriteRenderer.color = c;
    }
}
