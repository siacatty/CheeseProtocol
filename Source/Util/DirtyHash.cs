using System;
using System.Collections.Generic;
using UnityEngine;

namespace CheeseProtocol
{
    public struct DirtyHash
    {
        private int h;

        public DirtyHash(int seed = 17) => h = seed;

        public int Value => h;

        public void Add(int v)
        {
            unchecked { h = h * 31 + v; }
        }

        public void Add(bool v) => Add(v ? 1 : 0);

        public void Add(float v, int scale = 10000)
        {
            // float 흔들림 방지: quantize
            Add(Mathf.RoundToInt(v * scale));
        }

        public void Add(string s)
        {
            unchecked { h = h * 31 + (s?.GetHashCode() ?? 0); }
        }

        public void AddEnum<T>(T e) where T : unmanaged, Enum
            => Add(Convert.ToInt32(e));

        public void AddListUnordered(List<string> list)
        {
            // 순서 의미 없을 때: 안정적 (정렬 비용 없이)
            // 원소 해시를 XOR/합산으로 섞어서 순서 무관하게
            if (list == null) { Add(0); return; }

            unchecked
            {
                int count = list.Count;
                int acc1 = 0;
                int acc2 = 0;

                for (int i = 0; i < count; i++)
                {
                    int x = list[i]?.GetHashCode() ?? 0;
                    acc1 ^= x;
                    acc2 += x * 16777619;
                }

                Add(count);
                Add(acc1);
                Add(acc2);
            }
        }

        public void AddListOrdered(List<string> list)
        {
            if (list == null) { Add(0); return; }
            Add(list.Count);
            for (int i = 0; i < list.Count; i++)
                Add(list[i]);
        }
    }
    public static class DirtyHashExt
    {
        public static void AddRange(this ref DirtyHash dh, QualityRange r)
        {
            dh.Add(r.qMin);
            dh.Add(r.qMax);
        }
    }
}