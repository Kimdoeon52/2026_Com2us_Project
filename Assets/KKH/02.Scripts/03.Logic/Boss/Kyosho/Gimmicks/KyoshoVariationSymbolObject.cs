using System;
using UnityEngine;
using TMPro;
using RealSteel.Boss.Core;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [전장 기물: 변주곡 6종 음악 문양 타겟 오브젝트 (KyoshoVariationSymbolObject)]
    /// 플레이어의 근접/원거리 타격을 IPlayerHitTarget으로 수신하며,
    /// 같은 프레임 다중 히트 시 가장 가까운 1개만 판정되도록 KyoshoVariationPatternGimmick으로 후보를 전달함.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KyoshoVariationSymbolObject : MonoBehaviour, IPlayerHitTarget
    {
        private BoxCollider2D _collider;
        private SpriteRenderer _frameRenderer;
        private SpriteRenderer _fillRenderer;
        private TextMeshPro _glyphText;
        private TextMeshPro _subText;

        private Action<KyoshoVariationSymbolObject, Vector2> _onHitCandidate;
        private Vector3 _basePos;
        private float _moveDir = 1f;
        private float _moveSpeed;
        private float _minX;
        private float _maxX;
        private float _flashTimer;

        public int SymbolIndex { get; private set; }
        public bool IsConsumed { get; private set; }
        public bool IsHittable => gameObject.activeSelf && !IsConsumed;

        public void Initialize(
            int symbolIndex,
            Vector3 worldPos,
            bool enableHorizontalMove,
            float moveSpeed,
            float minX,
            float maxX,
            Action<KyoshoVariationSymbolObject, Vector2> onHitCandidate)
        {
            EnsureVisuals();
            SymbolIndex = Mathf.Clamp(symbolIndex, 0, 5);
            _basePos = worldPos;
            transform.position = worldPos;
            _moveSpeed = enableHorizontalMove ? moveSpeed : 0f;
            _minX = minX;
            _maxX = maxX;
            _moveDir = (SymbolIndex % 2 == 0) ? 1f : -1f;
            _onHitCandidate = onHitCandidate;
            IsConsumed = false;
            if (_collider != null) _collider.enabled = true;

            Color borderCol = KyoshoConstants.SymbolBorderColors[SymbolIndex];
            if (_frameRenderer != null) _frameRenderer.color = borderCol;
            if (_fillRenderer != null) _fillRenderer.color = new Color(0.08f, 0.10f, 0.16f, 0.88f);
            if (_glyphText != null)
            {
                _glyphText.text = KyoshoConstants.SymbolGlyphs[SymbolIndex];
                _glyphText.color = Color.white;
            }
            if (_subText != null)
            {
                _subText.text = $"[{SymbolIndex + 1}] {KyoshoConstants.SolfegeNames[SymbolIndex]}";
                _subText.color = borderCol;
            }
        }

        public void RearmForDuplicateStep()
        {
            IsConsumed = false;
            _flashTimer = 0.25f;
            if (_collider != null) _collider.enabled = true;
            gameObject.SetActive(true);
        }

        public void MarkConsumedVisual()
        {
            IsConsumed = true;
            if (_collider != null) _collider.enabled = false;
            gameObject.SetActive(false);
        }

        public void TakePlayerHit(float damage, Vector2 point)
        {
            if (!IsHittable) return;
            _onHitCandidate?.Invoke(this, point);
        }

        private void Update()
        {
            if (IsConsumed) return;

            if (_moveSpeed > 0.01f)
            {
                Vector3 pos = transform.position;
                pos.x += _moveDir * _moveSpeed * Time.deltaTime;
                if (pos.x <= _minX) { pos.x = _minX; _moveDir = 1f; }
                else if (pos.x >= _maxX) { pos.x = _maxX; _moveDir = -1f; }
                pos.y = _basePos.y + Mathf.Sin(Time.time * 3f + SymbolIndex) * 0.12f;
                transform.position = pos;
            }
            else
            {
                Vector3 pos = transform.position;
                pos.y = _basePos.y + Mathf.Sin(Time.time * 2.5f + SymbolIndex) * 0.10f;
                transform.position = pos;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float s = 1f + Mathf.Sin(_flashTimer * 25f) * 0.12f;
                transform.localScale = Vector3.one * s;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        private void EnsureVisuals()
        {
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _collider.size = new Vector2(1.30f, 1.35f);
            _collider.offset = Vector2.zero;

            if (_frameRenderer == null)
            {
                var frameGo = new GameObject("Frame");
                frameGo.transform.SetParent(transform, false);
                frameGo.transform.localScale = new Vector3(1.30f, 1.30f, 1f);
                _frameRenderer = frameGo.AddComponent<SpriteRenderer>();
                _frameRenderer.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                _frameRenderer.sortingOrder = 18;
            }

            if (_fillRenderer == null)
            {
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(transform, false);
                fillGo.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                fillGo.transform.localScale = new Vector3(1.12f, 1.12f, 1f);
                _fillRenderer = fillGo.AddComponent<SpriteRenderer>();
                _fillRenderer.sprite = KyoshoNanobotAttackHelper.GetSquareSprite();
                _fillRenderer.sortingOrder = 19;
            }

            if (_glyphText == null)
            {
                var glyphGo = new GameObject("Glyph");
                glyphGo.transform.SetParent(transform, false);
                glyphGo.transform.localPosition = new Vector3(0f, 0.14f, -0.05f);
                _glyphText = glyphGo.AddComponent<TextMeshPro>();
                _glyphText.alignment = TextAlignmentOptions.Center;
                _glyphText.fontSize = 6.2f;
                _glyphText.sortingOrder = 20;
            }

            if (_subText == null)
            {
                var subGo = new GameObject("Solfege");
                subGo.transform.SetParent(transform, false);
                subGo.transform.localPosition = new Vector3(0f, -0.38f, -0.05f);
                _subText = subGo.AddComponent<TextMeshPro>();
                _subText.alignment = TextAlignmentOptions.Center;
                _subText.fontSize = 3.0f;
                _subText.sortingOrder = 20;
            }
        }
    }
}
