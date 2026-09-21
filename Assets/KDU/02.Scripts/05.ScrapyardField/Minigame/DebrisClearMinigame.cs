using System.Collections.Generic;
using UnityEngine;

// 부품을 덮은 고물 조각을 창 밖으로 끌어내 전부 치우면 성공.
// 제한 시간은 베이스의 Time Limit에서 설정한다
public class DebrisClearMinigame : ScrapMinigameBase
{
    [Header("참조")]
    [Tooltip("이 영역 밖에서 조각을 놓으면 치워진다")]
    [SerializeField] private RectTransform _boundsRect;

    [Tooltip("조각이 생성될 영역")]
    [SerializeField] private RectTransform _spawnArea;

    [Tooltip("조각 프리팹")]
    [SerializeField] private DraggableDebris _debrisPrefab;

    [Tooltip("드래그 좌표 보정용 캔버스")]
    [SerializeField] private Canvas _canvas;

    [Header("배치")]
    [Tooltip("조각 개수")]
    [Min(1)]
    [SerializeField] private int _debrisCount = 8;

    [Tooltip("생성 영역 가장자리 여백")]
    [Min(0f)]
    [SerializeField] private float _edgePadding = 24f;

    [Tooltip("조각 회전 범위(도). 0이면 회전하지 않는다")]
    [Min(0f)]
    [SerializeField] private float _rotationRange = 25f;

    [Tooltip("난수 시드. 0이면 실행할 때마다 배치가 달라진다")]
    [SerializeField] private int _seed;

    private readonly List<DraggableDebris> _spawned = new List<DraggableDebris>();
    private Rng _rng;
    private int _removedCount;

    protected override void OnBegin()
    {
        if (_rng == null)
            _rng = new Rng(_seed != 0 ? _seed : System.Environment.TickCount);

        _removedCount = 0;
        Spawn();
    }

    protected override float PerformanceOnTimeout()
    {
        return _spawned.Count == 0 ? 0f : (float)_removedCount / _spawned.Count;
    }

    private void Spawn()
    {
        if (_debrisPrefab == null || _spawnArea == null)
            return;

        // 남은 인스턴스를 재사용하고 모자란 만큼만 만든다
        while (_spawned.Count < _debrisCount)
            _spawned.Add(Instantiate(_debrisPrefab, _spawnArea));

        for (int i = 0; i < _spawned.Count; i++)
        {
            DraggableDebris debris = _spawned[i];

            if (i >= _debrisCount)
            {
                debris.gameObject.SetActive(false);
                continue;
            }

            var rect = (RectTransform)debris.transform;
            rect.anchoredPosition = RandomPoint();
            rect.localRotation = Quaternion.Euler(0f, 0f, _rng.Range(-_rotationRange, _rotationRange));

            debris.Setup(_boundsRect, _canvas, OnDebrisRemoved);
        }
    }

    private Vector2 RandomPoint()
    {
        Rect area = _spawnArea.rect;
        float halfWidth = Mathf.Max(0f, (area.width * 0.5f) - _edgePadding);
        float halfHeight = Mathf.Max(0f, (area.height * 0.5f) - _edgePadding);

        return new Vector2(_rng.Range(-halfWidth, halfWidth), _rng.Range(-halfHeight, halfHeight));
    }

    private void OnDebrisRemoved(DraggableDebris debris)
    {
        _removedCount++;

        if (_removedCount >= _debrisCount)
            Finish(ScrapMinigameResult.Win(1f));
    }
}
