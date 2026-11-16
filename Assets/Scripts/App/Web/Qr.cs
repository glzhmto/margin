//---------------------------------------------------------------------
//
// QR Code Generator (C# port of the essential parts of "QR Code Generator for JavaScript",
// the library the web app uses at preview/vendor/qrcode.js)
//
// Copyright (c) 2009 Kazuhiko Arase
//
// URL: http://www.d-project.com/
//
// Licensed under the MIT license:
//  http://www.opensource.org/licenses/mit-license.php
//
// The word 'QR Code' is registered trademark of
// DENSO WAVE INCORPORATED
//  http://www.denso-wave.com/qrcode/faqpatent-e.html
//
//---------------------------------------------------------------------
//
// Only what the invite QR needs: byte mode, error correction level M, automatic version
// selection (versions 1 to 10, up to 213 bytes). Same steps and same mask choice as the
// web library, so both draw exactly the same matrix for the same text.
// The matrix part (Qr.Encode) has no Unity dependency; Qr.Texture renders it for UI Toolkit.
using System;
using System.Collections.Generic;
using System.Text;

namespace Margin.App.Web
{
    public static class Qr
    {
        const int PAD0 = 0xEC;
        const int PAD1 = 0x11;
        const int MODE_8BIT_BYTE = 1 << 2;
        /// <summary>Level M in the format bits (L = 1, M = 0, Q = 3, H = 2).</summary>
        const int LEVEL_M = 0;
        const int MaxVersion = 10;

        /// <summary>RS blocks for level M, versions 1 to 10: (count, totalCount, dataCount) groups.</summary>
        static readonly int[][] RS_BLOCK_M =
        {
            new[] { 1, 26, 16 },
            new[] { 1, 44, 28 },
            new[] { 1, 70, 44 },
            new[] { 2, 50, 32 },
            new[] { 2, 67, 43 },
            new[] { 4, 43, 27 },
            new[] { 4, 49, 31 },
            new[] { 2, 60, 38, 2, 61, 39 },
            new[] { 3, 58, 36, 2, 59, 37 },
            new[] { 4, 69, 43, 1, 70, 44 },
        };

        static readonly int[][] PATTERN_POSITION_TABLE =
        {
            new int[0],
            new[] { 6, 18 },
            new[] { 6, 22 },
            new[] { 6, 26 },
            new[] { 6, 30 },
            new[] { 6, 34 },
            new[] { 6, 22, 38 },
            new[] { 6, 24, 42 },
            new[] { 6, 26, 46 },
            new[] { 6, 28, 50 },
        };

        const int G15 = (1 << 10) | (1 << 8) | (1 << 5) | (1 << 4) | (1 << 2) | (1 << 1) | (1 << 0);
        const int G18 = (1 << 12) | (1 << 11) | (1 << 10) | (1 << 9) | (1 << 8) | (1 << 5) | (1 << 2) | (1 << 0);
        const int G15_MASK = (1 << 14) | (1 << 12) | (1 << 10) | (1 << 4) | (1 << 1);

        static readonly int[] EXP_TABLE = new int[256];
        static readonly int[] LOG_TABLE = new int[256];

        static Qr()
        {
            for (int i = 0; i < 8; i++) EXP_TABLE[i] = 1 << i;
            for (int i = 8; i < 256; i++) EXP_TABLE[i] = EXP_TABLE[i - 4] ^ EXP_TABLE[i - 5] ^ EXP_TABLE[i - 6] ^ EXP_TABLE[i - 8];
            for (int i = 0; i < 255; i++) LOG_TABLE[EXP_TABLE[i]] = i;
        }

        /// <summary>
        /// The QR matrix of the text ([row, col], true = dark), without the quiet zone.
        /// Throws ArgumentException when the text doesn't fit in version 10.
        /// </summary>
        public static bool[,] Encode(string text)
        {
            // The web library's default stringToBytes keeps the low byte of each char; for the ASCII
            // invite links both give the same bytes. UTF-8 is used so any other text still scans.
            var bytes = Encoding.UTF8.GetBytes(text ?? "");

            // Smallest version whose data capacity fits (the web's make() with typeNumber 0).
            int type = 1;
            for (; type <= MaxVersion; type++)
            {
                int bits = 4 + LengthInBits(type) + bytes.Length * 8;
                if (bits <= TotalDataCount(type) * 8) break;
            }
            if (type > MaxVersion) throw new ArgumentException("Text too long for a version 10 QR code.");

            var data = CreateData(type, bytes);
            var m = new Matrix(type);

            // getBestMaskPattern: try the 8 masks and keep the lowest penalty (first one on ties).
            double minLost = 0;
            int pattern = 0;
            for (int i = 0; i < 8; i++)
            {
                m.Make(true, i, data);
                double lost = LostPoint(m.Modules, m.Count);
                if (i == 0 || minLost > lost) { minLost = lost; pattern = i; }
            }
            m.Make(false, pattern, data);

            var r = new bool[m.Count, m.Count];
            for (int row = 0; row < m.Count; row++)
                for (int col = 0; col < m.Count; col++)
                    r[row, col] = m.Modules[row, col] == true;
            return r;
        }

        // ---------- Matrix ----------

        class Matrix
        {
            public readonly int Type;
            public readonly int Count;
            public bool?[,] Modules;

            public Matrix(int type) { Type = type; Count = type * 4 + 17; }

            public void Make(bool test, int maskPattern, int[] data)
            {
                Modules = new bool?[Count, Count];
                SetupPositionProbePattern(0, 0);
                SetupPositionProbePattern(Count - 7, 0);
                SetupPositionProbePattern(0, Count - 7);
                SetupPositionAdjustPattern();
                SetupTimingPattern();
                SetupTypeInfo(test, maskPattern);
                if (Type >= 7) SetupTypeNumber(test);
                MapData(data, maskPattern);
            }

            void SetupPositionProbePattern(int row, int col)
            {
                for (int r = -1; r <= 7; r++)
                {
                    if (row + r <= -1 || Count <= row + r) continue;
                    for (int c = -1; c <= 7; c++)
                    {
                        if (col + c <= -1 || Count <= col + c) continue;
                        Modules[row + r, col + c] =
                            (0 <= r && r <= 6 && (c == 0 || c == 6))
                            || (0 <= c && c <= 6 && (r == 0 || r == 6))
                            || (2 <= r && r <= 4 && 2 <= c && c <= 4);
                    }
                }
            }

            void SetupTimingPattern()
            {
                for (int r = 8; r < Count - 8; r++)
                {
                    if (Modules[r, 6] != null) continue;
                    Modules[r, 6] = r % 2 == 0;
                }
                for (int c = 8; c < Count - 8; c++)
                {
                    if (Modules[6, c] != null) continue;
                    Modules[6, c] = c % 2 == 0;
                }
            }

            void SetupPositionAdjustPattern()
            {
                var pos = PATTERN_POSITION_TABLE[Type - 1];
                for (int i = 0; i < pos.Length; i++)
                {
                    for (int j = 0; j < pos.Length; j++)
                    {
                        int row = pos[i], col = pos[j];
                        if (Modules[row, col] != null) continue;
                        for (int r = -2; r <= 2; r++)
                            for (int c = -2; c <= 2; c++)
                                Modules[row + r, col + c] = r == -2 || r == 2 || c == -2 || c == 2 || (r == 0 && c == 0);
                    }
                }
            }

            void SetupTypeNumber(bool test)
            {
                int bits = BCHTypeNumber(Type);
                for (int i = 0; i < 18; i++)
                {
                    bool mod = !test && ((bits >> i) & 1) == 1;
                    Modules[i / 3, i % 3 + Count - 8 - 3] = mod;
                }
                for (int i = 0; i < 18; i++)
                {
                    bool mod = !test && ((bits >> i) & 1) == 1;
                    Modules[i % 3 + Count - 8 - 3, i / 3] = mod;
                }
            }

            void SetupTypeInfo(bool test, int maskPattern)
            {
                int data = (LEVEL_M << 3) | maskPattern;
                int bits = BCHTypeInfo(data);

                // vertical
                for (int i = 0; i < 15; i++)
                {
                    bool mod = !test && ((bits >> i) & 1) == 1;
                    if (i < 6) Modules[i, 8] = mod;
                    else if (i < 8) Modules[i + 1, 8] = mod;
                    else Modules[Count - 15 + i, 8] = mod;
                }

                // horizontal
                for (int i = 0; i < 15; i++)
                {
                    bool mod = !test && ((bits >> i) & 1) == 1;
                    if (i < 8) Modules[8, Count - i - 1] = mod;
                    else if (i < 9) Modules[8, 15 - i - 1 + 1] = mod;
                    else Modules[8, 15 - i - 1] = mod;
                }

                // fixed module
                Modules[Count - 8, 8] = !test;
            }

            void MapData(int[] data, int maskPattern)
            {
                int inc = -1;
                int row = Count - 1;
                int bitIndex = 7;
                int byteIndex = 0;

                for (int col = Count - 1; col > 0; col -= 2)
                {
                    if (col == 6) col -= 1;
                    while (true)
                    {
                        for (int c = 0; c < 2; c++)
                        {
                            if (Modules[row, col - c] == null)
                            {
                                bool dark = false;
                                if (byteIndex < data.Length) dark = ((data[byteIndex] >> bitIndex) & 1) == 1;
                                if (Mask(maskPattern, row, col - c)) dark = !dark;
                                Modules[row, col - c] = dark;
                                bitIndex -= 1;
                                if (bitIndex == -1) { byteIndex += 1; bitIndex = 7; }
                            }
                        }
                        row += inc;
                        if (row < 0 || Count <= row)
                        {
                            row -= inc;
                            inc = -inc;
                            break;
                        }
                    }
                }
            }
        }

        // ---------- Data and error correction ----------

        static int LengthInBits(int type) => type < 10 ? 8 : 16; // byte mode

        static List<(int total, int data)> RSBlocks(int type)
        {
            var t = RS_BLOCK_M[type - 1];
            var list = new List<(int, int)>();
            for (int i = 0; i < t.Length; i += 3)
                for (int j = 0; j < t[i]; j++) list.Add((t[i + 1], t[i + 2]));
            return list;
        }

        static int TotalDataCount(int type)
        {
            int n = 0;
            foreach (var b in RSBlocks(type)) n += b.data;
            return n;
        }

        class BitBuffer
        {
            readonly List<int> _buffer = new List<int>();
            public int Length { get; private set; }
            public List<int> Buffer => _buffer;

            public void Put(int num, int length)
            {
                for (int i = 0; i < length; i++) PutBit(((num >> (length - i - 1)) & 1) == 1);
            }

            public void PutBit(bool bit)
            {
                int bufIndex = Length / 8;
                if (_buffer.Count <= bufIndex) _buffer.Add(0);
                if (bit) _buffer[bufIndex] |= 0x80 >> (Length % 8);
                Length += 1;
            }
        }

        static int[] CreateData(int type, byte[] bytes)
        {
            var rsBlocks = RSBlocks(type);
            var buffer = new BitBuffer();
            buffer.Put(MODE_8BIT_BYTE, 4);
            buffer.Put(bytes.Length, LengthInBits(type));
            foreach (var b in bytes) buffer.Put(b, 8);

            int totalDataCount = TotalDataCount(type);

            // end code
            if (buffer.Length + 4 <= totalDataCount * 8) buffer.Put(0, 4);

            // padding
            while (buffer.Length % 8 != 0) buffer.PutBit(false);

            // padding
            while (true)
            {
                if (buffer.Length >= totalDataCount * 8) break;
                buffer.Put(PAD0, 8);
                if (buffer.Length >= totalDataCount * 8) break;
                buffer.Put(PAD1, 8);
            }

            return CreateBytes(buffer, rsBlocks);
        }

        static int[] CreateBytes(BitBuffer buffer, List<(int total, int data)> rsBlocks)
        {
            int offset = 0, maxDcCount = 0, maxEcCount = 0;
            var dcdata = new int[rsBlocks.Count][];
            var ecdata = new int[rsBlocks.Count][];

            for (int r = 0; r < rsBlocks.Count; r++)
            {
                int dcCount = rsBlocks[r].data;
                int ecCount = rsBlocks[r].total - dcCount;
                maxDcCount = Math.Max(maxDcCount, dcCount);
                maxEcCount = Math.Max(maxEcCount, ecCount);

                dcdata[r] = new int[dcCount];
                for (int i = 0; i < dcCount; i++) dcdata[r][i] = 0xff & buffer.Buffer[i + offset];
                offset += dcCount;

                var rsPoly = ErrorCorrectPolynomial(ecCount);
                var rawPoly = new Poly(dcdata[r], rsPoly.Length - 1);
                var modPoly = rawPoly.Mod(rsPoly);
                ecdata[r] = new int[rsPoly.Length - 1];
                for (int i = 0; i < ecdata[r].Length; i++)
                {
                    int modIndex = i + modPoly.Length - ecdata[r].Length;
                    ecdata[r][i] = modIndex >= 0 ? modPoly[modIndex] : 0;
                }
            }

            int totalCodeCount = 0;
            foreach (var b in rsBlocks) totalCodeCount += b.total;

            var data = new int[totalCodeCount];
            int index = 0;
            for (int i = 0; i < maxDcCount; i++)
                for (int r = 0; r < rsBlocks.Count; r++)
                    if (i < dcdata[r].Length) data[index++] = dcdata[r][i];
            for (int i = 0; i < maxEcCount; i++)
                for (int r = 0; r < rsBlocks.Count; r++)
                    if (i < ecdata[r].Length) data[index++] = ecdata[r][i];
            return data;
        }

        static Poly ErrorCorrectPolynomial(int errorCorrectLength)
        {
            var a = new Poly(new[] { 1 }, 0);
            for (int i = 0; i < errorCorrectLength; i++) a = a.Multiply(new Poly(new[] { 1, Gexp(i) }, 0));
            return a;
        }

        static int Glog(int n)
        {
            if (n < 1) throw new ArgumentException("glog(" + n + ")");
            return LOG_TABLE[n];
        }

        static int Gexp(int n)
        {
            while (n < 0) n += 255;
            while (n >= 256) n -= 255;
            return EXP_TABLE[n];
        }

        /// <summary>qrPolynomial: coefficients over GF(256), leading zeros dropped, shifted by "shift" zeros.</summary>
        class Poly
        {
            readonly int[] _num;

            public Poly(int[] num, int shift)
            {
                int offset = 0;
                while (offset < num.Length && num[offset] == 0) offset++;
                _num = new int[num.Length - offset + shift];
                for (int i = 0; i < num.Length - offset; i++) _num[i] = num[i + offset];
            }

            public int this[int index] => _num[index];
            public int Length => _num.Length;

            public Poly Multiply(Poly e)
            {
                var num = new int[Length + e.Length - 1];
                for (int i = 0; i < Length; i++)
                    for (int j = 0; j < e.Length; j++)
                        num[i + j] ^= Gexp(Glog(this[i]) + Glog(e[j]));
                return new Poly(num, 0);
            }

            public Poly Mod(Poly e)
            {
                var p = this;
                while (p.Length - e.Length >= 0)
                {
                    int ratio = Glog(p[0]) - Glog(e[0]);
                    var num = new int[p.Length];
                    for (int i = 0; i < p.Length; i++) num[i] = p[i];
                    for (int i = 0; i < e.Length; i++) num[i] ^= Gexp(Glog(e[i]) + ratio);
                    p = new Poly(num, 0);
                }
                return p;
            }
        }

        // ---------- Format bits, masks and penalty ----------

        static int BCHDigit(int data)
        {
            int digit = 0;
            uint d = (uint)data;
            while (d != 0) { digit++; d >>= 1; }
            return digit;
        }

        static int BCHTypeInfo(int data)
        {
            int d = data << 10;
            while (BCHDigit(d) - BCHDigit(G15) >= 0) d ^= G15 << (BCHDigit(d) - BCHDigit(G15));
            return ((data << 10) | d) ^ G15_MASK;
        }

        static int BCHTypeNumber(int data)
        {
            int d = data << 12;
            while (BCHDigit(d) - BCHDigit(G18) >= 0) d ^= G18 << (BCHDigit(d) - BCHDigit(G18));
            return (data << 12) | d;
        }

        static bool Mask(int pattern, int i, int j)
        {
            switch (pattern)
            {
                case 0: return (i + j) % 2 == 0;
                case 1: return i % 2 == 0;
                case 2: return j % 3 == 0;
                case 3: return (i + j) % 3 == 0;
                case 4: return (i / 2 + j / 3) % 2 == 0;
                case 5: return (i * j) % 2 + (i * j) % 3 == 0;
                case 6: return ((i * j) % 2 + (i * j) % 3) % 2 == 0;
                case 7: return ((i * j) % 3 + (i + j) % 2) % 2 == 0;
                default: throw new ArgumentException("bad maskPattern:" + pattern);
            }
        }

        static double LostPoint(bool?[,] m, int n)
        {
            bool D(int r, int c) => m[r, c] == true;
            double lostPoint = 0;

            // LEVEL1
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    int sameCount = 0;
                    bool dark = D(row, col);
                    for (int r = -1; r <= 1; r++)
                    {
                        if (row + r < 0 || n <= row + r) continue;
                        for (int c = -1; c <= 1; c++)
                        {
                            if (col + c < 0 || n <= col + c) continue;
                            if (r == 0 && c == 0) continue;
                            if (dark == D(row + r, col + c)) sameCount++;
                        }
                    }
                    if (sameCount > 5) lostPoint += 3 + sameCount - 5;
                }
            }

            // LEVEL2
            for (int row = 0; row < n - 1; row++)
            {
                for (int col = 0; col < n - 1; col++)
                {
                    int count = 0;
                    if (D(row, col)) count++;
                    if (D(row + 1, col)) count++;
                    if (D(row, col + 1)) count++;
                    if (D(row + 1, col + 1)) count++;
                    if (count == 0 || count == 4) lostPoint += 3;
                }
            }

            // LEVEL3
            for (int row = 0; row < n; row++)
                for (int col = 0; col < n - 6; col++)
                    if (D(row, col) && !D(row, col + 1) && D(row, col + 2) && D(row, col + 3) && D(row, col + 4) && !D(row, col + 5) && D(row, col + 6))
                        lostPoint += 40;
            for (int col = 0; col < n; col++)
                for (int row = 0; row < n - 6; row++)
                    if (D(row, col) && !D(row + 1, col) && D(row + 2, col) && D(row + 3, col) && D(row + 4, col) && !D(row + 5, col) && D(row + 6, col))
                        lostPoint += 40;

            // LEVEL4
            int darkCount = 0;
            for (int col = 0; col < n; col++)
                for (int row = 0; row < n; row++)
                    if (D(row, col)) darkCount++;
            double ratio = Math.Abs(100.0 * darkCount / n / n - 50) / 5;
            lostPoint += ratio * 10;

            return lostPoint;
        }

#if !QR_NO_UNITY
        // ---------- Rendering ----------

        /// <summary>
        /// The QR code as a texture, one pixel per module plus the 4-module white quiet zone of the web
        /// (createDataURL(cell, cell * 4)). Point filtering keeps the modules sharp when scaled up.
        /// </summary>
        public static UnityEngine.Texture2D Texture(string text)
        {
            var m = Encode(text);
            int n = m.GetLength(0), size = n + 8;
            var tex = new UnityEngine.Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, false)
            {
                filterMode = UnityEngine.FilterMode.Point,
                wrapMode = UnityEngine.TextureWrapMode.Clamp,
                name = "QR",
            };
            var px = new UnityEngine.Color32[size * size];
            var white = new UnityEngine.Color32(255, 255, 255, 255);
            var black = new UnityEngine.Color32(0, 0, 0, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int row = y - 4, col = x - 4;
                    bool dark = row >= 0 && row < n && col >= 0 && col < n && m[row, col];
                    // Texture rows go bottom-up.
                    px[(size - 1 - y) * size + x] = dark ? black : white;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static readonly Dictionary<string, UnityEngine.Texture2D> _cache = new Dictionary<string, UnityEngine.Texture2D>();

        /// <summary>Texture(text), cached by text (screens rebuild often; the invite link rarely changes).</summary>
        public static UnityEngine.Texture2D Cached(string text)
        {
            text = text ?? "";
            if (_cache.TryGetValue(text, out var t) && t != null) return t;
            if (_cache.Count > 16) _cache.Clear();
            return _cache[text] = Texture(text);
        }
#endif
    }
}
