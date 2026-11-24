using System;
using UnityEngine;

/// <summary>음표 종류 — 모양 = 속도 (보스3_월광_기획 1.4). 새 값은 맨 뒤에만 추가할 것 — 에셋에 숫자로 저장된다</summary>
public enum MaestroNoteKind
{
    Whole,   // 온음표 — 가장 느림, 큼
    Quarter, // 4분음표 — 보통
    Eighth   // 8분음표 — 가장 빠름, 작음
}

/// <summary>
/// 마에스트로 「음표 공격」 수치 (보스3_월광_기획 1.4, 2026-11-19 2단계).
/// 기획서에 있는 값: 음표 속도(3/5/8 m/s), 난이도별 음표 수(3~7)·간격(1.0~0.6초)·구성(문장).
/// 기획서에 없는 값(휘두르기 길이, 판정 반경, 궤적 크기 등)은 전부 TODO 임시값.
/// </summary>
[CreateAssetMenu(fileName = "Maestro_NoteData", menuName = "NYH/Boss/Maestro Note Data")]
public class MaestroNoteData : ScriptableObject
{
    [Serializable]
    public class KindSpec
    {
        [Tooltip("비행 속도 (m/s) — 기획서 1.4 임시값: 온음표 3 / 4분 5 / 8분 8")]
        [Min(0.1f)] public float speed = 5f;

        [Tooltip("TODO: 기획 수치 없음. 판정 반경 (m) — 음표 머리 중심 기준")]
        [Min(0.05f)] public float hitRadius = 0.25f;

        [Tooltip("그림(실루엣) 배율 — 1 = 기본 모양 크기")]
        [Min(0.2f)] public float visualScale = 1f;
    }

    [Serializable]
    public class DifficultyRow
    {
        [Tooltip("휘두르기 1번에 나오는 음표를 순서대로 — 개수 = 음표 수 (기획서: 3/4/5/6/7)")]
        public MaestroNoteKind[] notes = Array.Empty<MaestroNoteKind>();

        [Tooltip("음표 사이 간격 (초) — 기획서: 1.0 / 0.9 / 0.8 / 0.7 / 0.6")]
        [Min(0.05f)] public float interval = 0.8f;
    }

    [Header("음표 종류 (모양 = 속도)")]
    public KindSpec whole = new KindSpec { speed = 3f, hitRadius = 0.35f, visualScale = 1f };
    public KindSpec quarter = new KindSpec { speed = 5f, hitRadius = 0.25f, visualScale = 1f };
    public KindSpec eighth = new KindSpec { speed = 8f, hitRadius = 0.18f, visualScale = 1f };

    [Header("난이도별 (아주 쉬움 · 쉬움 · 보통 · 어려움 · 헬 순서)")]
    [Tooltip("구성은 기획서 문장을 옮긴 해석값 — 아주 쉬움 '온음표 위주'를 전부 온음표로 둠(TODO 확인)")]
    public DifficultyRow[] difficulty =
    {
        new DifficultyRow { interval = 1.0f, notes = new[] { MaestroNoteKind.Whole, MaestroNoteKind.Whole, MaestroNoteKind.Whole } },
        new DifficultyRow { interval = 0.9f, notes = new[] { MaestroNoteKind.Whole, MaestroNoteKind.Quarter, MaestroNoteKind.Whole, MaestroNoteKind.Quarter } },
        new DifficultyRow { interval = 0.8f, notes = new[] { MaestroNoteKind.Quarter, MaestroNoteKind.Quarter, MaestroNoteKind.Eighth, MaestroNoteKind.Quarter, MaestroNoteKind.Quarter } },
        new DifficultyRow { interval = 0.7f, notes = new[] { MaestroNoteKind.Quarter, MaestroNoteKind.Eighth, MaestroNoteKind.Quarter, MaestroNoteKind.Eighth, MaestroNoteKind.Quarter, MaestroNoteKind.Quarter } },
        new DifficultyRow { interval = 0.6f, notes = new[] { MaestroNoteKind.Eighth, MaestroNoteKind.Eighth, MaestroNoteKind.Quarter, MaestroNoteKind.Eighth, MaestroNoteKind.Eighth, MaestroNoteKind.Quarter, MaestroNoteKind.Eighth } },
    };

    [Header("① 흐름 · ② 형태: 휘두르기 (TODO 전부 임시값)")]
    [Tooltip("휘두르기 1번 길이 (초) — 이 동안 궤적을 따라 음표가 차례로 생긴다 ('천천히, 크게')")]
    [Min(0.1f)] public float swingSeconds = 1.2f;

    [Tooltip("궤적(호) 반지름 (m) — 몸 중심 기준")]
    [Min(0.5f)] public float arcRadius = 2.6f;

    [Tooltip("궤적 중심을 몸 중심에서 위로 (m)")]
    public float arcLift = 0.5f;

    [Tooltip("휘두르기 시작·끝 각도 (도). 0 = 플레이어 쪽 수평, 90 = 바로 위")]
    public Vector2 arcAngles = new Vector2(120f, 0f);

    [Tooltip("음표가 바닥에서 최소 이만큼 위에 생긴다 (m)")]
    [Min(0f)] public float minSpawnHeight = 0.6f;

    [Header("③ 예고 · ④ 공격 (TODO 임시값)")]
    [Tooltip("휘두르기가 끝나고 첫 음이 울리기까지 (초)")]
    [Min(0f)] public float firstSoundDelay = 0.3f;

    [Tooltip("음이 울리고(반짝임) 실제로 날아가기까지 (초). 조준은 음이 울린 순간의 플레이어 위치 — 0이면 울리는 순간 바로 발사")]
    [Min(0f)] public float launchDelay = 0.15f;

    [Tooltip("조준점 = 플레이어 위치 + 이 높이 (m) — '몸 높이로 날아오는 직선탄'")]
    public float aimHeight = 0.6f;

    [Tooltip("최대 비행 시간 (초) — 아무것도 안 닿아도 이 뒤엔 흩어진다")]
    [Min(0.5f)] public float maxFlightSeconds = 6f;

    [Header("⑤ 붕괴")]
    [Tooltip("TODO: 임시값. 흩어진 모래가 몸으로 재흡수되기 시작하기까지 (초)")]
    [Min(0f)] public float reabsorbDelay = 1.0f;

    [Header("그림")]
    [Tooltip("음표 봇 발광 (판정 입자 구분 — 기획서 0.2)")]
    [Range(0f, 4f)] public float glow = 1.5f;

    [Header("소리 (월광 1악장 — 셋잇단 아르페지오)")]
    [Tooltip("음표 순서대로 울릴 음 (MIDI 번호, 60 = 가운데 도). 음표 수보다 짧으면 처음부터 반복. 기본 = G#3 C#4 E4")]
    public int[] melody = { 56, 61, 64 };

    [Tooltip("녹음된 음 (melody와 같은 순서). 비우면 임시 합성음(사인파)을 쓴다 — 자체 편곡 음원이 나오면 넣을 것")]
    public AudioClip[] noteClips = Array.Empty<AudioClip>();

    [Range(0f, 1f)] public float volume = 0.6f;

    public KindSpec Spec(MaestroNoteKind kind) => kind switch
    {
        MaestroNoteKind.Whole => whole,
        MaestroNoteKind.Eighth => eighth,
        _ => quarter,
    };

    public DifficultyRow Row(BossDifficulty d)
    {
        if (difficulty == null || difficulty.Length == 0) return null;
        return difficulty[Mathf.Clamp((int)d, 0, difficulty.Length - 1)];
    }
}
