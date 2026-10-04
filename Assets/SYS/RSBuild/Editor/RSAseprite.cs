// RE:AL STEEL - Aseprite 파일 읽기 · 쓰기 (RGBA, 첫 프레임)
//
// 쓰기: 레이어 여러 장 (가이드 = 참조 레이어 · 반투명, 그림 = 보통 레이어)
// 읽기: 보이는 보통 레이어를 순서대로 겹친 그림 (참조 레이어 · 숨긴 레이어 제외). 사용자가 레이어를 더해도 된다
// 픽셀 배열은 위에서 아래로 (Aseprite 순서). 유니티 텍스처(아래에서 위)로 바꾸는 건 부르는 쪽에서.
// 형식: https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RealSteel.Build.EditorTools
{
    public struct RGBA
    {
        public byte r, g, b, a;
        public RGBA(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static readonly RGBA Clear = new RGBA(0, 0, 0, 0);
    }

    public class AseLayer
    {
        public string name;
        public bool visible = true;
        public bool reference;
        public byte opacity = 255;
        public RGBA[] pixels;   // width × height, 위에서 아래
    }

    public static class RSAseprite
    {
        const ushort FileMagic = 0xA5E0, FrameMagic = 0xF1FA;
        const ushort ChunkLayer = 0x2004, ChunkCel = 0x2005, ChunkPalette = 0x2019, ChunkColorProfile = 0x2007;

        // ─────────────────────────────────────────────────────────────
        // 쓰기
        // ─────────────────────────────────────────────────────────────

        public static void Write(string path, int width, int height, IList<AseLayer> layers)
        {
            var chunks = new List<byte[]>();
            chunks.Add(ColorProfileChunk());
            chunks.Add(PaletteChunk());
            foreach (var l in layers) chunks.Add(LayerChunk(l));
            for (int i = 0; i < layers.Count; i++)
            {
                var cel = CelChunk(i, width, height, layers[i].pixels);
                if (cel != null) chunks.Add(cel);
            }

            int frameBytes = 16;
            foreach (var c in chunks) frameBytes += c.Length;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                // 헤더 128 바이트
                w.Write((uint)(128 + frameBytes));
                w.Write(FileMagic);
                w.Write((ushort)1);            // 프레임 수
                w.Write((ushort)width);
                w.Write((ushort)height);
                w.Write((ushort)32);           // RGBA
                w.Write((uint)1);              // 레이어 불투명도 유효
                w.Write((ushort)100);          // (옛) 속도
                w.Write((uint)0); w.Write((uint)0);
                w.Write((byte)0);              // 투명 팔레트 번호
                w.Write(new byte[3]);
                w.Write((ushort)1);            // 색 수
                w.Write((byte)1); w.Write((byte)1);   // 픽셀 비율
                w.Write((short)0); w.Write((short)0); // 격자 위치
                w.Write((ushort)1); w.Write((ushort)1); // 격자 크기 (1 픽셀)
                w.Write(new byte[84]);
                // 프레임
                w.Write((uint)frameBytes);
                w.Write(FrameMagic);
                w.Write((ushort)Math.Min(chunks.Count, 0xFFFF));
                w.Write((ushort)100);
                w.Write(new byte[2]);
                w.Write((uint)chunks.Count);
                foreach (var c in chunks) w.Write(c);
                w.Flush();
                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        static byte[] Chunk(ushort type, byte[] data)
        {
            var b = new byte[6 + data.Length];
            BitConverter.GetBytes((uint)b.Length).CopyTo(b, 0);
            BitConverter.GetBytes(type).CopyTo(b, 4);
            data.CopyTo(b, 6);
            return b;
        }

        static byte[] ColorProfileChunk()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)1);   // sRGB
                w.Write((ushort)0);   // 플래그
                w.Write((uint)0);     // 감마 (고정 소수)
                w.Write(new byte[8]);
                return Chunk(ChunkColorProfile, ms.ToArray());
            }
        }

        static byte[] PaletteChunk()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((uint)1); w.Write((uint)0); w.Write((uint)0);
                w.Write(new byte[8]);
                w.Write((ushort)0);
                w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)255);
                return Chunk(ChunkPalette, ms.ToArray());
            }
        }

        static byte[] LayerChunk(AseLayer l)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                ushort flags = 0;
                if (l.visible) flags |= 1;
                flags |= 2;                       // 편집 가능
                if (l.reference) flags |= 64;     // 참조 레이어 (그림 · 내보내기에서 빠짐)
                w.Write(flags);
                w.Write((ushort)0);   // 보통 레이어
                w.Write((ushort)0);   // 깊이
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)0);   // 섞기: 보통
                w.Write(l.opacity);
                w.Write(new byte[3]);
                var name = Encoding.UTF8.GetBytes(l.name ?? "");
                w.Write((ushort)name.Length);
                w.Write(name);
                return Chunk(ChunkLayer, ms.ToArray());
            }
        }

        static byte[] CelChunk(int layer, int width, int height, RGBA[] px)
        {
            if (px == null) return null;
            bool any = false;
            foreach (var c in px) if (c.a != 0) { any = true; break; }
            if (!any) return null;   // 빈 레이어는 칸 없이
            var raw = new byte[width * height * 4];
            for (int i = 0; i < px.Length && i * 4 + 3 < raw.Length; i++)
            {
                raw[i * 4] = px[i].r; raw[i * 4 + 1] = px[i].g; raw[i * 4 + 2] = px[i].b; raw[i * 4 + 3] = px[i].a;
            }
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((ushort)layer);
                w.Write((short)0); w.Write((short)0);
                w.Write((byte)255);
                w.Write((ushort)2);      // 압축 이미지
                w.Write((short)0);       // z-index
                w.Write(new byte[5]);
                w.Write((ushort)width);
                w.Write((ushort)height);
                w.Write(Zlib(raw));
                return Chunk(ChunkCel, ms.ToArray());
            }
        }

        static byte[] Zlib(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x9C);
                using (var d = new DeflateStream(ms, CompressionLevel.Optimal, true)) d.Write(data, 0, data.Length);
                uint a = 1, b = 0;
                foreach (byte x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
                uint adler = (b << 16) | a;
                ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16)); ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 읽기
        // ─────────────────────────────────────────────────────────────

        /// <summary>보이는 보통 레이어를 겹친 그림 (첫 프레임). 실패하면 예외</summary>
        public static RGBA[] ReadComposite(string path, out int width, out int height)
        {
            var bytes = File.ReadAllBytes(path);
            using (var ms = new MemoryStream(bytes))
            using (var r = new BinaryReader(ms))
            {
                r.ReadUInt32();
                if (r.ReadUInt16() != FileMagic) throw new InvalidDataException("Aseprite 파일이 아닙니다");
                int frames = r.ReadUInt16();
                width = r.ReadUInt16();
                height = r.ReadUInt16();
                int depth = r.ReadUInt16();
                if (depth != 32) throw new InvalidDataException("색 모드가 RGBA 가 아닙니다 (Aseprite: 스프라이트 → 색 모드 → RGB)");
                ms.Position = 128;

                var layers = new List<AseLayer>();
                var outPx = new RGBA[width * height];
                if (frames < 1) return outPx;

                long frameStart = ms.Position;
                uint frameBytes = r.ReadUInt32();
                if (r.ReadUInt16() != FrameMagic) throw new InvalidDataException("프레임 머리가 이상합니다");
                int oldCount = r.ReadUInt16();
                r.ReadUInt16(); r.ReadBytes(2);
                uint newCount = r.ReadUInt32();
                long count = newCount != 0 ? newCount : oldCount;

                var cels = new List<(int layer, int x, int y, int op, int w, int h, byte[] data)>();
                for (long c = 0; c < count && ms.Position < frameStart + frameBytes; c++)
                {
                    long cs = ms.Position;
                    uint size = r.ReadUInt32();
                    ushort type = r.ReadUInt16();
                    if (type == ChunkLayer)
                    {
                        ushort flags = r.ReadUInt16();
                        ushort ltype = r.ReadUInt16();
                        r.ReadUInt16(); r.ReadUInt16(); r.ReadUInt16();
                        r.ReadUInt16();
                        byte op = r.ReadByte();
                        r.ReadBytes(3);
                        int nl = r.ReadUInt16();
                        string name = Encoding.UTF8.GetString(r.ReadBytes(nl));
                        layers.Add(new AseLayer { name = name, visible = (flags & 1) != 0 && ltype == 0, reference = (flags & 64) != 0, opacity = op });
                    }
                    else if (type == ChunkCel)
                    {
                        int li = r.ReadUInt16();
                        int x = r.ReadInt16(), y = r.ReadInt16();
                        int op = r.ReadByte();
                        int ctype = r.ReadUInt16();
                        r.ReadInt16(); r.ReadBytes(5);
                        if (ctype == 0 || ctype == 2)
                        {
                            int w = r.ReadUInt16(), h = r.ReadUInt16();
                            int rest = (int)(cs + size - ms.Position);
                            var data = r.ReadBytes(rest);
                            byte[] rawPx = ctype == 2 ? Unzlib(data, w * h * 4) : data;
                            cels.Add((li, x, y, op, w, h, rawPx));
                        }
                    }
                    ms.Position = cs + size;
                }

                // 레이어 순서대로 겹치기 (뒤 레이어가 위)
                for (int li = 0; li < layers.Count; li++)
                {
                    var L = layers[li];
                    if (!L.visible || L.reference) continue;
                    foreach (var cel in cels)
                    {
                        if (cel.layer != li) continue;
                        float op = (L.opacity / 255f) * (cel.op / 255f);
                        for (int yy = 0; yy < cel.h; yy++)
                        {
                            int ty = cel.y + yy;
                            if (ty < 0 || ty >= height) continue;
                            for (int xx = 0; xx < cel.w; xx++)
                            {
                                int tx = cel.x + xx;
                                if (tx < 0 || tx >= width) continue;
                                int si = (yy * cel.w + xx) * 4;
                                if (si + 3 >= cel.data.Length) continue;
                                float sa = cel.data[si + 3] / 255f * op;
                                if (sa <= 0f) continue;
                                ref RGBA d = ref outPx[ty * width + tx];
                                float da = d.a / 255f;
                                float oa = sa + da * (1f - sa);
                                if (oa <= 0f) continue;
                                d.r = (byte)Math.Round((cel.data[si] * sa + d.r * da * (1f - sa)) / oa);
                                d.g = (byte)Math.Round((cel.data[si + 1] * sa + d.g * da * (1f - sa)) / oa);
                                d.b = (byte)Math.Round((cel.data[si + 2] * sa + d.b * da * (1f - sa)) / oa);
                                d.a = (byte)Math.Round(oa * 255f);
                            }
                        }
                    }
                }
                return outPx;
            }
        }

        static byte[] Unzlib(byte[] data, int expected)
        {
            using (var ms = new MemoryStream(data, 2, data.Length - 2))
            using (var d = new DeflateStream(ms, CompressionMode.Decompress))
            using (var o = new MemoryStream(expected))
            {
                d.CopyTo(o);
                return o.ToArray();
            }
        }
    }
}
