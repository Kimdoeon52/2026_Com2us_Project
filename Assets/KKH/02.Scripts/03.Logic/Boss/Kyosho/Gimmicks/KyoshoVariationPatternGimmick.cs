using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RealSteel.Dialogue;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [패턴 6: 변주곡 ① · ② (KyoshoVariationPatternGimmick)]
    /// - 변주곡 ① (HP 60%): 전 난이도, 1페 ➔ 2페 전환 기믹
    /// - 변주곡 ② (HP 30%): 보통 이상 전용, 실패 시 파괴된 악기 섹션 전원 100% 복구!
    /// - 시퀀스: 중앙 무적 이동(1마디) ➔ 문양 기물 4/6개 생성(1박) ➔ 3연속 문양 변신 & 도~라 3음 예고 ➔ 4마디 입력(공격 인스턴스당 1개 문양만 판정)
    /// - 실패 페널티: 안 A (불협화음 광역 폭발 코어 피해 240, 파츠 내구도 0)
    /// </summary>
    public class KyoshoVariationPatternGimmick : KyoshoPatternGimmickBase
    {
        [Header("변주곡 설정")]
        [SerializeField] private KyoshoRhythmTableSO rhythmTable;
        [SerializeField] private int dissonanceFailDamage = 240;
        [SerializeField] private float hardSymbolMoveSpeed = 1.5f;

        private RSNanoLimb _morphSpriteLimb;
        private readonly RSNanoSpriteShape[] _fallbackShapes = new RSNanoSpriteShape[6];
        private readonly List<KyoshoVariationSymbolObject> _spawnedSymbols = new List<KyoshoVariationSymbolObject>();
        private readonly List<(KyoshoVariationSymbolObject sym, Vector2 hitPoint)> _frameHitCandidates = new List<(KyoshoVariationSymbolObject, Vector2)>();

        private readonly List<int> _targetSequence = new List<int>(3);
        private int _currentStep;
        private int _mistakesCount;
        private bool _isInputWindowActive;
        private bool _puzzleCompleted;
        private bool _puzzleFailed;
        private int _lastProcessedHitFrame = -1;
        private bool _isSecondVariation;

        public void Configure(RSNanoLimb morphLimb, KyoshoRhythmTableSO table)
        {
            _morphSpriteLimb = morphLimb;
            rhythmTable = table;
            if (_morphSpriteLimb != null)
            {
                _morphSpriteLimb.type = LimbType.Sprite;
                _morphSpriteLimb.amount = 0f;
            }
        }

        public void SetVariationMode(bool isSecondVariation)
        {
            _isSecondVariation = isSecondVariation;
        }

        protected override async UniTask ExecuteActionAsync(CancellationToken token)
        {
            if (Kyosho == null || Swarm == null || Clock == null) return;

            if (rhythmTable == null)
            {
                rhythmTable = ScriptableObject.CreateInstance<KyoshoRhythmTableSO>();
            }
            rhythmTable.EnsureDefaults();

            var diff = Kyosho.SelectedDifficulty;
            int symbolCount = KyoshoConstants.GetVariationSymbolCount(diff);
            float morphBeats = KyoshoConstants.GetVariationMorphBeats(diff);
            bool allowDuplicates = diff == BossDifficulty.Hell;
            bool moveSymbols = diff >= BossDifficulty.Hard;
            bool fireHellStaccato = diff == BossDifficulty.Hell;
            bool allowRetryOnce = diff <= BossDifficulty.Normal;

            Kyosho.IsInvincible = true;
            Kyosho.SetPhaseState(_isSecondVariation ? KyoshoPhaseState.Variation2 : KyoshoPhaseState.Variation1);
            Kyosho.ScorePreviewHUD?.ShowVariationStatus(_isSecondVariation ? "변주곡 ② — 상단 카드의 3개 순서를 기억하세요!" : "변주곡 ① — 상단 3개 순서를 기억하세요!");
            Kyosho.ScorePreviewHUD?.ResetVariationSlots(3);

            Kyosho.GetArenaBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            float centerY = Kyosho.ArenaFloorY + 2.35f;

            // 1. 중앙 무적 이동 (1마디 = 4박)
            Vector3 startBlobCenter = Swarm.Blob.center;
            Vector3 targetCenter = new Vector3(centerX, centerY, 0f);
            double moveStart = Clock.TotalBeats;
            while (Clock.TotalBeats < moveStart + 4.0)
            {
                token.ThrowIfCancellationRequested();
                float t = Mathf.Clamp01((float)((Clock.TotalBeats - moveStart) / 4.0));
                Swarm.Blob.center = Vector3.Lerp(startBlobCenter, targetCenter, t * t * (3f - 2f * t));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            Swarm.Blob.center = targetCenter;

            // 2. 문양 기물 4 또는 6개 생성 (1박)
            SpawnSymbolObjects(symbolCount, moveSymbols, minX, maxX);
            await Clock.WaitBeatsAsync(1f, token);

            // 3. 3연속 문양 변신 및 3음 멜로디 예고
            PickTargetSequence(symbolCount, allowDuplicates);

            for (int step = 0; step < _targetSequence.Count; step++)
            {
                token.ThrowIfCancellationRequested();
                int symIdx = _targetSequence[step];
                ApplyBossMorphSprite(symIdx, targetCenter);
                KyoshoSfx.PlaySolfegeNote(symIdx, targetCenter);
                Kyosho.ScorePreviewHUD?.RevealVariationSlot(step, symIdx);
                Kyosho.ScorePreviewHUD?.ShowVariationStatus($"변주곡 출제 ({step + 1}/3) : {KyoshoConstants.SymbolGlyphs[symIdx]} ({KyoshoConstants.SolfegeNames[symIdx]} · {KyoshoConstants.SymbolNames[symIdx]})");

                await Clock.WaitBeatsAsync(morphBeats, token);
            }

            ClearBossMorphSprite();

            // 4. 플레이어 입력 (4마디 = 16박)
            _currentStep = 0;
            _mistakesCount = 0;
            _puzzleCompleted = false;
            _puzzleFailed = false;
            _isInputWindowActive = true;
            _frameHitCandidates.Clear();
            _lastProcessedHitFrame = -1;

            Kyosho.ScorePreviewHUD?.UpdateVariationInputProgress(0);
            Kyosho.ScorePreviewHUD?.ShowVariationStatus($"순서대로 타격하세요! (0 / 3)  {(allowRetryOnce ? "[1회 오답 재도전 가능]" : "[즉시 판정]")}");

            double inputStart = Clock.TotalBeats;
            double nextHellStaccatoBeat = inputStart + 1.0;
            var staccato = Kyosho.GetGimmick<KyoshoStaccatoPatternGimmick>(KyoshoConstants.PatternStaccatoID);

            while (Clock.TotalBeats < inputStart + 16.0 && !_puzzleCompleted && !_puzzleFailed)
            {
                token.ThrowIfCancellationRequested();

                if (fireHellStaccato && staccato != null)
                {
                    staccato.StepActiveBullets(Time.deltaTime);
                    if (Clock.TotalBeats >= nextHellStaccatoBeat)
                    {
                        nextHellStaccatoBeat += 2.0;
                        staccato.FireSingleImmediateNote(KyoshoNoteType.Eighth);
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            _isInputWindowActive = false;
            ClearSpawnedSymbols();
            Kyosho.ScorePreviewHUD?.ShowVariationStatus(null);
            Kyosho.ScorePreviewHUD?.HideVariationSequenceSlots();
            Kyosho.IsInvincible = false;

            // 5. 결과 처리
            if (_puzzleCompleted)
            {
                KyoshoSfx.PlayCue(KyoshoSfx.Data?.variationCorrect, "variation_ok", targetCenter);
                Debug.Log($"<color=green>[KyoshoVariation] 변주곡 {(_isSecondVariation ? "②" : "①")} 파훼 성공! 4박 그로기 진입!</color>");

                // 1페 -> 2페 전환이 필요한 경우 그로기 종료 시점에 2페 개막 (또는 즉시 페이즈 상태 갱신)
                if (!_isSecondVariation)
                {
                    Kyosho.PreparePhase2AfterVariation1();
                }

                Kyosho.EnterBeatGroggy(4f, 1.5f);
            }
            else
            {
                // 안 A: 불협화음 광역 폭발 피해 (코어 전용 240, Space 회피 가능)
                KyoshoSfx.PlayCue(KyoshoSfx.Data?.variationFailDissonance, "variation_fail", targetCenter);
                KyoshoSfx.DuckBgm(0.35f, 0.35f);
                SpawnDissonanceShockwave(targetCenter);

                var player = FindPlayerObject();
                if (player != null)
                {
                    ApplyCoreOnlyHit(player, dissonanceFailDamage, respectSpaceDodge: true);
                }

                if (_isSecondVariation)
                {
                    // 변주곡 ② 실패 시: 파괴했던 악기 섹션 전원 100% 복구!
                    Kyosho.SectionManager?.RestoreAllSections();
                }
                else
                {
                    Kyosho.PreparePhase2AfterVariation1();
                }

                Debug.Log($"<color=red>[KyoshoVariation] 변주곡 {(_isSecondVariation ? "②" : "①")} 실패 ➔ 불협화음 광역 폭발!</color>");
            }
        }

        /// <summary>
        /// 같은 프레임에 플레이어 범위 공격이 여러 문양 콜라이더에 겹칠 경우
        /// LateUpdate에서 히트 지점과 가장 가까운 단 1개의 문양만 판정함 (§2.3).
        /// </summary>
        public void ReportSymbolHitCandidate(KyoshoVariationSymbolObject symbol, Vector2 hitPoint)
        {
            if (!_isInputWindowActive || _puzzleCompleted || _puzzleFailed || symbol == null) return;
            _frameHitCandidates.Add((symbol, hitPoint));
        }

        private void LateUpdate()
        {
            if (!_isInputWindowActive || _frameHitCandidates.Count == 0) return;

            if (_lastProcessedHitFrame == Time.frameCount)
            {
                _frameHitCandidates.Clear();
                return;
            }
            _lastProcessedHitFrame = Time.frameCount;

            KyoshoVariationSymbolObject closest = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < _frameHitCandidates.Count; i++)
            {
                var (sym, pt) = _frameHitCandidates[i];
                if (sym == null || !sym.IsHittable) continue;
                float dSq = ((Vector2)sym.transform.position - pt).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    closest = sym;
                }
            }
            _frameHitCandidates.Clear();

            if (closest != null)
            {
                ProcessSingleSymbolHit(closest);
            }
        }

        private void ProcessSingleSymbolHit(KyoshoVariationSymbolObject hitSym)
        {
            if (_currentStep >= _targetSequence.Count) return;

            int expected = _targetSequence[_currentStep];
            if (hitSym.SymbolIndex == expected)
            {
                KyoshoSfx.PlaySolfegeNote(hitSym.SymbolIndex, hitSym.transform.position, 1.05f);
                var bark = FindAnyObjectByType<BattleBarkPlayer>();
                bark?.NotifyBossPartBroken(KyoshoConstants.BarkVariationSymbol);

                // 헬 난이도에서 뒤쪽 단계에 같은 문양이 한 번 더 출제되었다면 즉시 소멸하지 않고 재무장
                bool neededAgainLater = false;
                for (int k = _currentStep + 1; k < _targetSequence.Count; k++)
                {
                    if (_targetSequence[k] == hitSym.SymbolIndex) { neededAgainLater = true; break; }
                }

                if (neededAgainLater) hitSym.RearmForDuplicateStep();
                else hitSym.MarkConsumedVisual();

                _currentStep++;
                Kyosho.ScorePreviewHUD?.UpdateVariationInputProgress(_currentStep);
                Kyosho.ScorePreviewHUD?.ShowVariationStatus($"순서대로 타격하세요! ({_currentStep} / 3)");

                if (_currentStep >= _targetSequence.Count)
                {
                    _puzzleCompleted = true;
                }
            }
            else
            {
                _mistakesCount++;
                bool allowRetryOnce = Kyosho.SelectedDifficulty <= BossDifficulty.Normal;
                if (allowRetryOnce && _mistakesCount == 1)
                {
                    // 아주쉬움·쉬움·보통: 1회 오답 시 현재 진행 리셋 & 문양 복원 후 재도전 기회
                    KyoshoSfx.PlayCue(KyoshoSfx.Data?.variationFailDissonance, "variation_fail", hitSym.transform.position, 1f, 0.55f);
                    _currentStep = 0;
                    foreach (var s in _spawnedSymbols) s?.RearmForDuplicateStep();
                    Kyosho.ScorePreviewHUD?.UpdateVariationInputProgress(0);
                    Kyosho.ScorePreviewHUD?.ShowVariationStatus("오답! 리셋되었습니다 — 처음부터 다시 타격하세요! (0 / 3)");
                }
                else
                {
                    _puzzleFailed = true;
                }
            }
        }

        private void SpawnSymbolObjects(int count, bool enableMove, float minX, float maxX)
        {
            ClearSpawnedSymbols();
            float floorY = Kyosho.ArenaFloorY;
            float left = minX + 1.15f;
            float right = maxX - 1.15f;

            // 좌·우 공간으로 문양 배치 (중앙 보스 본체 양옆)
            var indices = new List<int>(count);
            for (int i = 0; i < count; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0.5f;
                float x = Mathf.Lerp(left, right, t);
                float y = floorY + (i % 2 == 0 ? 1.10f : 1.45f);

                var go = new GameObject($"VariationSymbol_{indices[i]}");
                var sym = go.AddComponent<KyoshoVariationSymbolObject>();
                sym.Initialize(indices[i], new Vector3(x, y, -0.05f), enableMove, hardSymbolMoveSpeed, left, right, ReportSymbolHitCandidate);
                _spawnedSymbols.Add(sym);
            }
        }

        private void PickTargetSequence(int symbolCount, bool allowDuplicates)
        {
            _targetSequence.Clear();
            if (allowDuplicates)
            {
                for (int i = 0; i < 3; i++) _targetSequence.Add(Random.Range(0, symbolCount));
                return;
            }

            var pool = new List<int>(symbolCount);
            for (int i = 0; i < symbolCount; i++) pool.Add(i);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int pickIdx = Random.Range(0, pool.Count);
                _targetSequence.Add(pool[pickIdx]);
                pool.RemoveAt(pickIdx);
            }
        }

        private void ApplyBossMorphSprite(int symbolIndex, Vector3 centerPos)
        {
            if (_morphSpriteLimb == null) return;
            var def = rhythmTable.GetSymbolDef(symbolIndex);
            var shape = def.spriteShape != null ? def.spriteShape : GetOrCreateFallbackSymbolShape(symbolIndex);

            _morphSpriteLimb.transform.position = centerPos;
            _morphSpriteLimb.picture = shape;
            _morphSpriteLimb.artColor = true;
            _morphSpriteLimb.tint = def.borderColor;
            _morphSpriteLimb.glow = 1.8f;
            _morphSpriteLimb.amount = 1f;
            Swarm.Blob.bulge = 0f;
        }

        private void ClearBossMorphSprite()
        {
            if (_morphSpriteLimb != null)
            {
                _morphSpriteLimb.amount = 0f;
                _morphSpriteLimb.glow = 0f;
            }
        }

        private RSNanoSpriteShape GetOrCreateFallbackSymbolShape(int idx)
        {
            if (_fallbackShapes[idx] != null) return _fallbackShapes[idx];
            _fallbackShapes[idx] = KyoshoNanobotAttackHelper.CreateRuntimeFallbackShape($"FallbackSymbol_{idx}", 24, 24, (x, y) =>
            {
                float nx = (x - 11.5f) / 10f;
                float ny = (y - 11.5f) / 10f;
                bool draw = idx switch
                {
                    0 => Mathf.Abs(nx) < 0.16f || (ny > 0.15f && Mathf.Abs(Mathf.Sqrt(nx * nx + (ny - 0.45f) * (ny - 0.45f)) - 0.35f) < 0.15f)
                         || (ny <= 0.15f && Mathf.Abs(Mathf.Sqrt(nx * nx + (ny + 0.2f) * (ny + 0.2f)) - 0.42f) < 0.16f),
                    1 => Mathf.Abs(Mathf.Sqrt((nx + 0.15f) * (nx + 0.15f) + ny * ny) - 0.48f) < 0.16f && nx > -0.2f
                         || (Mathf.Abs(nx - 0.52f) < 0.14f && (Mathf.Abs(ny - 0.25f) < 0.14f || Mathf.Abs(ny + 0.25f) < 0.14f)),
                    2 => Mathf.Abs(nx - 0.25f) < 0.12f || Mathf.Abs(nx + 0.25f) < 0.12f || Mathf.Abs(ny - 0.25f - nx * 0.2f) < 0.12f || Mathf.Abs(ny + 0.25f - nx * 0.2f) < 0.12f,
                    3 => (Mathf.Abs(nx + 0.25f) < 0.14f && ny > -0.65f) || (ny < 0.1f && Mathf.Abs(Mathf.Sqrt(nx * nx + (ny + 0.25f) * (ny + 0.25f)) - 0.38f) < 0.16f && nx > -0.2f),
                    4 => Mathf.Abs(ny - nx * 1.4f) < 0.22f && Mathf.Abs(ny) < 0.75f,
                    _ => (ny > -0.1f && Mathf.Abs(Mathf.Sqrt(nx * nx + (ny + 0.1f) * (ny + 0.1f)) - 0.72f) < 0.16f) || (nx * nx + (ny + 0.25f) * (ny + 0.25f) < 0.06f)
                };
                if (!draw) return null;
                Color c = KyoshoConstants.SymbolBorderColors[idx];
                return new Color32((byte)(c.r * 255), (byte)(c.g * 255), (byte)(c.b * 255), 255);
            });
            return _fallbackShapes[idx];
        }

        private void ClearSpawnedSymbols()
        {
            for (int i = 0; i < _spawnedSymbols.Count; i++)
            {
                if (_spawnedSymbols[i] != null) Destroy(_spawnedSymbols[i].gameObject);
            }
            _spawnedSymbols.Clear();
        }

        private void SpawnDissonanceShockwave(Vector3 center)
        {
            var go = new GameObject("KyoshoDissonanceExplosion");
            go.transform.position = center;
            go.transform.localScale = Vector3.one * 18f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KyoshoNanobotAttackHelper.GetCircleSprite();
            sr.color = new Color(1f, 0.22f, 0.35f, 0.65f);
            sr.sortingOrder = 25;
            Destroy(go, 0.35f);
        }

        protected override void CleanupEffects()
        {
            base.CleanupEffects();
            _isInputWindowActive = false;
            ClearBossMorphSprite();
            ClearSpawnedSymbols();
            Kyosho?.ScorePreviewHUD?.ShowVariationStatus(null);
            Kyosho?.ScorePreviewHUD?.HideVariationSequenceSlots();
            if (Kyosho != null) Kyosho.IsInvincible = false;
        }
    }
}
