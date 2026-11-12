using System.Collections.Generic;
using UnityEngine;
using RealSteel.NanoBoss;

namespace RealSteel.Boss.Kyosho
{
    /// <summary>
    /// [2페이즈 악기 섹션 총괄 매니저 (KyoshoSectionManager)]
    /// 현악(좌)·타악(우)·금관(중앙 상단) 생성, 파괴 상태 추적,
    /// 변주곡 ② 실패 시 전원 복구, 전부 파괴 시 본체 피격반경 +30% & 패턴 후딜 +1박 보너스를 관리함.
    /// </summary>
    public class KyoshoSectionManager : MonoBehaviour
    {
        private readonly List<KyoshoSectionPartGimmick> _sections = new List<KyoshoSectionPartGimmick>(3);
        private KyoshoBossController _kyosho;
        private KyoshoSectionStatusHUD _statusHud;

        public KyoshoSectionPartGimmick StringsSection { get; private set; }
        public KyoshoSectionPartGimmick PercussionSection { get; private set; }
        public KyoshoSectionPartGimmick BrassSection { get; private set; }

        public bool IsStringsAlive => StringsSection != null && StringsSection.HasEverSpawned && !StringsSection.IsBroken;
        public bool IsPercussionAlive => PercussionSection != null && PercussionSection.HasEverSpawned && !PercussionSection.IsBroken;
        public bool IsBrassAlive => BrassSection != null && BrassSection.HasEverSpawned && !BrassSection.IsBroken;

        public bool AreAllActiveSectionsBroken
        {
            get
            {
                if (_kyosho == null || _kyosho.Snapshot == null || _kyosho.Snapshot.currentPhase < 2) return false;
                if (StringsSection == null || !StringsSection.HasEverSpawned) return false;
                if (!StringsSection.IsBroken || !PercussionSection.IsBroken) return false;

                if (KyoshoConstants.SupportsBrassSection(_kyosho.SelectedDifficulty))
                {
                    return BrassSection != null && BrassSection.HasEverSpawned && BrassSection.IsBroken;
                }
                return true;
            }
        }

        public void Initialize(
            KyoshoBossController kyosho,
            RSNanoSwarm swarm,
            RSNanoLimb stringsLimb,
            RSNanoLimb percussionLimb,
            RSNanoLimb brassLimb,
            KyoshoSectionStatusHUD statusHud)
        {
            _kyosho = kyosho;
            _statusHud = statusHud;
            _sections.Clear();

            int sectionMaxHp = GetSectionMaxHp();
            _kyosho.GetArenaBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            float floorY = _kyosho.ArenaFloorY;

            StringsSection = CreateOrGetSection("Section_Strings", KyoshoSectionType.Strings, stringsLimb, swarm, sectionMaxHp,
                new Vector3(minX + 2.5f, floorY + 0.95f, 0f));
            PercussionSection = CreateOrGetSection("Section_Percussion", KyoshoSectionType.Percussion, percussionLimb, swarm, sectionMaxHp,
                new Vector3(maxX - 2.5f, floorY + 0.95f, 0f));
            BrassSection = CreateOrGetSection("Section_Brass", KyoshoSectionType.Brass, brassLimb, swarm, sectionMaxHp,
                new Vector3(centerX, floorY + 2.85f, 0f));

            _sections.Add(StringsSection);
            _sections.Add(PercussionSection);
            _sections.Add(BrassSection);

            foreach (var sec in _sections)
            {
                _kyosho.RegisterGimmick(sec);
            }

            _statusHud?.SetVisible(false);
        }

        private KyoshoSectionPartGimmick CreateOrGetSection(
            string goName,
            KyoshoSectionType type,
            RSNanoLimb limb,
            RSNanoSwarm swarm,
            int maxHp,
            Vector3 worldPos)
        {
            var existing = transform.Find(goName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(goName);
            go.transform.SetParent(transform, false);
            var comp = go.GetComponent<KyoshoSectionPartGimmick>() ?? go.AddComponent<KyoshoSectionPartGimmick>();
            comp.Configure(_kyosho, type, limb, swarm, maxHp, worldPos, OnSectionBroken, RefreshHud);
            return comp;
        }

        public void ActivatePhase2InitialSections()
        {
            int hp = GetSectionMaxHp();
            _kyosho.GetArenaBounds(out float minX, out float maxX);
            float floorY = _kyosho.ArenaFloorY;

            StringsSection?.SpawnSection(hp, new Vector3(minX + 2.5f, floorY + 0.95f, 0f));
            PercussionSection?.SpawnSection(hp, new Vector3(maxX - 2.5f, floorY + 0.95f, 0f));
            RefreshHud();
        }

        public void ActivateBrassSection()
        {
            if (_kyosho == null || !KyoshoConstants.SupportsBrassSection(_kyosho.SelectedDifficulty)) return;
            if (BrassSection == null || BrassSection.HasEverSpawned) return;

            int hp = GetSectionMaxHp();
            _kyosho.GetArenaBounds(out float minX, out float maxX);
            float centerX = (minX + maxX) * 0.5f;
            float floorY = _kyosho.ArenaFloorY;

            BrassSection.SpawnSection(hp, new Vector3(centerX, floorY + 2.85f, 0f));
            RefreshHud();
            Debug.Log("<color=magenta>[KyoshoSectionManager] 첫 페르마타 종료 ➔ 금관 섹션(SECTION_BRASS) 조립 완료!</color>");
        }

        public void RestoreAllSections()
        {
            int hp = GetSectionMaxHp();
            StringsSection?.RestoreSection(hp);
            PercussionSection?.RestoreSection(hp);
            if (_kyosho != null && KyoshoConstants.SupportsBrassSection(_kyosho.SelectedDifficulty))
            {
                BrassSection?.RestoreSection(hp);
            }
            RefreshHud();
            Debug.Log("<color=orange>[KyoshoSectionManager] 변주곡 ② 실패 페널티 ➔ 악기 섹션 전원 100% 복구!</color>");
        }

        public void SetTemporarilyGatheredForFinale(bool gathered)
        {
            foreach (var sec in _sections)
            {
                sec?.SetTemporarilyGathered(gathered);
            }
        }

        private int GetSectionMaxHp()
        {
            int bossMax = (_kyosho != null && _kyosho.Snapshot != null) ? _kyosho.Snapshot.maxHp : 10000;
            return Mathf.Max(100, Mathf.RoundToInt(bossMax * 0.08f));
        }

        private void OnSectionBroken(KyoshoSectionPartGimmick broken)
        {
            if (AreAllActiveSectionsBroken)
            {
                Debug.Log("<color=yellow>[KyoshoSectionManager] ▶ 모든 악기 섹션 파괴 완료! 본체 피격 반경 +30% & 패턴 간격 +1박 보너스 활성화!</color>");
            }
            RefreshHud();
        }

        public void RefreshHud()
        {
            _statusHud?.Refresh(_sections, AreAllActiveSectionsBroken);
        }
    }
}
