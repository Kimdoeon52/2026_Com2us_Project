// RE:AL STEEL - 나노봇 군집: 리그 가이드 + 리그가 쓰는 도우미
//
// 리그(RSNanoRig)에 붙은 봇은 slot = 부위 번호, cell = 그 부위의 격자 자리.
// 목표 자리 · 느낌 · 판정 · 발광은 리그가 매 프레임 채우는 LimbSim 을 잡이 읽어 정한다.
using System.Collections.Generic;
using UnityEngine;

namespace RealSteel.NanoBoss
{
    public sealed class RigGuide : SwarmGuide
    {
        public RSNanoRig rig;

        public RigGuide() { name = "리그"; }

        public RSNanoLimb Limb(int slot) => rig ? rig.LimbAt(slot) : null;

        public bool Danger(int slot)
        {
            var l = Limb(slot);
            return l && l.danger;
        }
    }

    public partial class RSNanoSwarm
    {
        /// <summary>탄으로 꺼내 쓸 봇 — 후광 → 덩어리 순, 원점에 가까운 것부터 (리그 · 기사 몸은 건드리지 않는다)</summary>
        public List<int> Ammo(int count, Vector3 near, List<int> into)
        {
            into.Clear();
            if (count <= 0) return into;
            foreach (int g in AmmoOrder)
            {
                if (into.Count >= count) break;
                Collect(g, near, count - into.Count, ammoTmp);
                into.AddRange(ammoTmp);
            }
            return into;
        }
        static readonly int[] AmmoOrder = { G_Halo, G_Blob };
        readonly List<int> ammoTmp = new List<int>();

        /// <summary>봇 하나를 가이드로 돌려보낸다 (후광이면 자리 좌표를 새로)</summary>
        public void SendHome(int i, int homeGuide)
        {
            if (bots[i].mode == BotMode.Free) { bots[i].home = homeGuide; return; }
            if (bots[i].mode != BotMode.Bound) return;
            if (homeGuide == G_Halo) bots[i].v = new Vector3(R(), R(), R());
            Bind(i, homeGuide);
        }

        public System.Random Rng => rng;
    }
}
