using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.Dialogue.Unity
{
    /// <summary>
    /// Game scope 변수의 공급원. 게임 시스템 → 대화 시스템 방향의 단방향 창구.
    ///
    /// 두 가지 방식 지원:
    ///  - Pull(권장): Register("record.wins", () => DVal.Int(record.Wins))
    ///      → 대화가 조건을 평가하는 순간의 실제 값을 읽으므로 "값 갱신을 깜빡"하는 버그가 없다.
    ///  - Push: Set("battle.lastResult", DVal.Str("Lose"))
    ///      → 이벤트성 값(직전 경기 결과 등)에 사용
    ///
    /// 대화 데이터는 이 값을 읽기만 한다(효과로 쓰기 불가 - 로더에서 차단).
    /// </summary>
    public sealed class GameStateBlackboard : MonoBehaviour, IGameStateProvider
    {
        private readonly Dictionary<string, Func<DVal>> _getters = new Dictionary<string, Func<DVal>>(StringComparer.Ordinal);
        private readonly Dictionary<string, DVal> _values = new Dictionary<string, DVal>(StringComparer.Ordinal);

        public void Register(string key, Func<DVal> getter) => _getters[key] = getter;
        public void Unregister(string key) => _getters.Remove(key);

        public void Set(string key, DVal value) => _values[key] = value;
        public void Clear(string key) => _values.Remove(key);

        public bool TryGet(string key, out DVal value)
        {
            if (_getters.TryGetValue(key, out var g))
            {
                try { value = g(); return true; }
                catch (Exception e)
                {
                    // 게임 쪽 getter 예외가 대화 시스템 전체를 죽이지 않도록 격리
                    Debug.LogException(e, this);
                    value = default;
                    return false;
                }
            }
            return _values.TryGetValue(key, out value);
        }
    }
}
