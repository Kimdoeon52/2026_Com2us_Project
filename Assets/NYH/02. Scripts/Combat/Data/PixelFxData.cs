using UnityEngine;

/// <summary>
/// 도트 플립북 이펙트 한 종류 (1층 · 데이터). PixelFx가 이것만 읽고 재생한다.
///
/// 2026-11-05 추가 — pixelfx(github.com/patch-gg/pixelfx, MIT)로 뽑은 스프라이트 시트를 쓰려고 만들었다.
/// 시트는 프로젝트 밖 pixelfx 폴더에서 렌더해 PNG + JSON으로 넣고, 메뉴 NYH/이펙트/pixelfx 시트 가져오기가
/// PNG를 칸마다 자르고 이 에셋을 만든다(frames·fps·loop 채움). 다시 가져와도 아래 "조정" 값은 그대로 둔다.
///
/// 순수 연출이다 — 판정·데미지와 무관하고, 재생도 전투 틱이 아니라 실제 시간으로 돈다(§3 "렌더·이펙트는 틱 바깥").
/// </summary>
[CreateAssetMenu(menuName = "NYH/Combat/Pixel Fx Data", fileName = "NewPixelFxData")]
public class PixelFxData : ScriptableObject
{
    [Header("시트 (가져오기 메뉴가 채움)")]
    public Sprite[] frames;

    [Min(1f)] public float fps = 30f;

    [Tooltip("켜면 반복 (PixelFx.Stop 또는 오브젝트를 지울 때까지)")]
    public bool loop;

    [Tooltip("원본 pixelfx 이펙트 id·옵션 (다시 렌더할 때 참고용)")]
    public string source;

    [Header("조정 (다시 가져와도 유지)")]
    [Tooltip("크기 배율. 기본 PPU 45 = 쿄다이 임시 그림(18ppu × 보스 크기 0.4)과 같은 도트 크기")]
    [Min(0.01f)] public float scale = 1f;

    [Tooltip("마지막 몇 프레임 동안 투명해지며 사라짐 — 시트 꼬리를 잘라서 끝이 툭 끊기는 걸 가린다")]
    [Min(0)] public int fadeOutFrames = 8;

    [Tooltip("재생 속도 배율")]
    [Min(0.01f)] public float speed = 1f;

    public Color tint = Color.white;

    [Tooltip("그림 순서 — 쿄다이 손 5, 레이저 8")]
    public int sortingOrder = 9;

    [Tooltip("앞에서부터 이 프레임까지만 재생 (0 = 전부). 시트 뒤쪽 긴 꼬리(오래 남는 연기 등)를 자르고 fadeOutFrames로 흐리게 끝낸다 " +
             "(2026-11-09: 주먹 충격 연기가 3초씩 남아 연속 공격 때 플레이어를 가렸다)")]
    [Min(0)] public int maxFrames;

    /// <summary>실제로 재생하는 프레임 수 (maxFrames 반영)</summary>
    public int FrameCount => frames == null ? 0 : (maxFrames > 0 ? Mathf.Min(maxFrames, frames.Length) : frames.Length);

    public float Duration => FrameCount == 0 ? 0f : FrameCount / (fps * speed);
}
