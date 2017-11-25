using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RT.ArithmeticCoding.Tests
{
    [TestClass]
    public sealed class ArithmeticCodingTests
    {
        private Random _rnd = new Random();

        private uint[] newArray(int length, Func<int, uint> initial)
        {
            var result = new uint[length];
            for (int i = 0; i < length; i++)
                result[i] = initial(i);
            return result;
        }

        private void writeInt(Stream stream, int value)
        {
            using (var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                bw.Write(value);
        }

        private int readInt(Stream stream)
        {
            using (var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                return br.ReadInt32();
        }

        public void TestSpeed()
        {
            // This is not a true unit test, but can be run from Program.cs to evaluate performance impact of a change
            testBasic(1000);
            // Test performance on a fixed context
            var start = DateTime.UtcNow;
            testBasic(10_000_000); // 3.92s  =>  5.7s  =>  3.13s
            Console.WriteLine((DateTime.UtcNow - start).TotalSeconds);
            // Test performance on a constantly updated context
            start = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
                TestAdvanced(); // 4.17s  =>  1.7s  =>  0.79s
            Console.WriteLine((DateTime.UtcNow - start).TotalSeconds);
        }

        [TestMethod]
        public void TestNormalByteSequence()
        {
            // The encoding of bytes under a 0..255 context with equal probability should end up outputting those bytes unchanged
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, new ArithmeticSymbolArrayContext(256));
            for (int i = 0; i <= 255; i++)
                encoder.WriteSymbol(i);
            encoder.Finalize(false);
            var result = ms.ToArray();
            for (int i = 0; i <= 255; i++)
                Assert.AreEqual(i, result[i]);

            ms = new MemoryStream(result);
            var decoder = new ArithmeticCodingReader(ms, new ArithmeticSymbolArrayContext(256));
            for (int i = 0; i <= 255; i++)
                Assert.AreEqual(i, decoder.ReadSymbol());
            decoder.Finalize(false);
        }

        [TestMethod]
        public void TestBasic1()
        {
            var freqs = new uint[] { 10, 30, 10 }; // Symbol 0 occurs 10x, symbol 1 occurs 30x, symbol 2 occurs 10x
            var ms = new MemoryStream();
            var context = new ArithmeticSymbolArrayContext(freqs);
            var encoder = new ArithmeticCodingWriter(ms, context);

            for (int i = 0; i < 10; i++)
            {
                encoder.WriteSymbol(1);
                encoder.WriteSymbol(0);
                encoder.WriteSymbol(1);
                encoder.WriteSymbol(2);
                encoder.WriteSymbol(1);
            }
            encoder.Finalize();
            ms.WriteByte(47);

            ms = new MemoryStream(ms.ToArray());
            var decoder = new ArithmeticCodingReader(ms, context);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(1, decoder.ReadSymbol());
                Assert.AreEqual(0, decoder.ReadSymbol());
                Assert.AreEqual(1, decoder.ReadSymbol());
                Assert.AreEqual(2, decoder.ReadSymbol());
                Assert.AreEqual(1, decoder.ReadSymbol());
            }
            decoder.Finalize();
            Assert.AreEqual(47, ms.ReadByte());
        }

        [TestMethod]
        public void TestBasic2()
        {
            for (int i = 0; i < 1000; i++)
                testBasic(i);
        }

        private void testBasic(int length)
        {
            var freqs = newArray(256, v => 256 - (uint) v);
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            for (int i = 0; i < length; i++)
                encoder.WriteSymbol(i % 256);
            encoder.Finalize(false);
            var expectedEnding = ms.Position;
            ms.Write(new byte[32], 0, 32);
            var bytes = ms.ToArray();

            ms = new MemoryStream(bytes);
            var decoder = new ArithmeticCodingReader(ms, freqs);
            for (int i = 0; i < length; i++)
            {
                var sym = decoder.ReadSymbol();
                Assert.AreEqual(i % 256, sym);
            }
            decoder.Finalize(false);
            Assert.AreEqual(expectedEnding, ms.Position);
        }

        [TestMethod]
        public void TestRandom()
        {
            var rnd = new Random(468);
            for (int t = 0; t < 2000; t++)
            {
                var freqs = newArray(rnd.Next(1, 300), _ => (uint) rnd.Next(1, 500));
                var symbols = newArray(rnd.Next(0, 1000), _ => (uint) rnd.Next(0, freqs.Length)).Select(e => (int) e).ToArray();
                var ms = new MemoryStream();
                var encoder = new ArithmeticCodingWriter(ms, freqs);
                for (int i = 0; i < symbols.Length; i++)
                    encoder.WriteSymbol(symbols[i]);
                encoder.Finalize();
                var expectedEnding = ms.Position;
                ms.WriteByte(47);
                var bytes = ms.ToArray();

                ms = new MemoryStream(bytes);
                var decoder = new ArithmeticCodingReader(ms, freqs);
                for (int i = 0; i < symbols.Length; i++)
                    Assert.AreEqual(symbols[i], decoder.ReadSymbol());
                decoder.Finalize();
                Assert.AreEqual(expectedEnding, ms.Position);
                Assert.AreEqual(47, ms.ReadByte());
            }
        }

        [TestMethod]
        public void TestAdvanced()
        {
            _rnd = new Random(12345);
            int max = 1000;
            var symbols = Enumerable.Range(1, 100_000).Select(_ => _rnd.Next(0, max)).ToArray();

            var mainContext = new ArithmeticSymbolArrayContext(max, _ => 1);
            var secondaryContext = new ArithmeticSymbolArrayContext(new uint[] { 3, 2, 1 });

            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, mainContext);
            writeInt(ms, 12345);
            for (int i = 0; i < symbols.Length; i++)
            {
                encoder.WriteSymbol(symbols[i]);
                mainContext.IncrementSymbolFrequency(symbols[i]);
                encoder.SetContext(mainContext);
                if (i % 1000 == 999)
                {
                    encoder.SetContext(secondaryContext);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(1);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(1);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(2);
                    encoder.SetContext(mainContext);
                }
            }
            encoder.Finalize(false);
            writeInt(ms, -54321); // to verify that the stream ends where we think it ends
            var encoded = ms.ToArray();


            ms = new MemoryStream(encoded);
            mainContext = new ArithmeticSymbolArrayContext(max, _ => 1); // reset frequencies
            Assert.AreEqual(12345, readInt(ms));
            var decoder = new ArithmeticCodingReader(ms, mainContext);
            for (int i = 0; i < symbols.Length; i++)
            {
                var sym = decoder.ReadSymbol();
                Assert.AreEqual(symbols[i], sym);
                mainContext.IncrementSymbolFrequency(sym);
                decoder.SetContext(mainContext);
                if (i % 1000 == 999)
                {
                    decoder.SetContext(secondaryContext);
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(1, decoder.ReadSymbol());
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(1, decoder.ReadSymbol());
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(2, decoder.ReadSymbol());
                    decoder.SetContext(mainContext);
                }
            }
            decoder.Finalize(false);
            Assert.AreEqual(-54321, readInt(ms));
        }

        [TestMethod]
        public void TestSingleSymbol()
        {
            var freqs = new uint[] { 1 };
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            for (int i = 0; i < 100; i++)
                encoder.WriteSymbol(0);
            encoder.Finalize(false);
            Assert.AreEqual(5, ms.Position);

            ms = new MemoryStream(ms.ToArray());
            var decoder = new ArithmeticCodingReader(ms, freqs);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(0, decoder.ReadSymbol());
            decoder.Finalize(false);
            Assert.AreEqual(5, ms.Position);
        }

        [TestMethod]
        public void TestArrayContext()
        {
            int count = 1000;
            var ctx = new ArithmeticSymbolArrayContext(count);
            Assert.AreEqual(0UL, ctx.GetSymbolFrequency(-1000));
            Assert.AreEqual(0UL, ctx.GetSymbolFrequency(-1));
            Assert.AreEqual(1UL, ctx.GetSymbolFrequency(0));
            Assert.AreEqual(1UL, ctx.GetSymbolFrequency(1));
            Assert.AreEqual(0UL, ctx.GetSymbolPosition(0));
            Assert.AreEqual(1UL, ctx.GetSymbolPosition(1));
            Assert.AreEqual(1UL, ctx.GetSymbolFrequency(count - 1));
            Assert.AreEqual(0UL, ctx.GetSymbolFrequency(count));
            Assert.AreEqual(0UL, ctx.GetSymbolFrequency(count + 1000));

            ctx = new ArithmeticSymbolArrayContext(new uint[] { 1, 1, 1, 1 });
            ctx.SetSymbolFrequency(1, 5);
            ctx.SetSymbolFrequency(3, 5);
            Assert.AreEqual(6UL, ctx.GetSymbolPosition(2));
        }

        [TestMethod]
        public void TestExtremeProbabilities1()
        {
            // This test encodes and decodes a sequence of N 1's, followed by a single 0, where the frequency of 1 is extremely high and the frequency of 0 is 1 (minimal).
            // For very large frequencies, the encoder overflows after a certain number of 1's.
            // This test verifies correct operation for all sequences of length 1..1000, then goes up in bigger increments until a sequence of ~10 million 1's.
            // A 100 billion long sequence has been tested manually but takes far too long for a unit test (encoded: FF FF FF FF FF FD 21 DB A1 79 + sync padding)

            // Maximum frequency vs first failure at 1's count:
            // 0xFFFF_FFFE: 2
            // 0xFFFF_FFF0: 16
            // 0xFFFF_FF00: 256
            // 0xFFFF_F000: 4096
            // 0xFFFF_0000: 65536
            // 0xF000_0000: 268,435,455
            // 0x8000_0001: 2,147,483,647
            // 0x8000_0000: correct to at least 2.2 billion
            // 0x7FFF_FFFF: correct to at least 100 billion

            var freqs = new[] { 1u, ArithmeticSymbolContext.MaxTotal - 1 };
            int count = 0;
            while (true)
            {
                var ms = new MemoryStream();
                var encoder = new ArithmeticCodingWriter(ms, freqs);
                for (int i = 0; i < count; i++)
                    encoder.WriteSymbol(1);
                encoder.WriteSymbol(0);
                encoder.Finalize();
                var expectedEnding = ms.Position;
                ms.WriteByte(47);
                var bytes = ms.ToArray();

                ms = new MemoryStream(bytes);
                var decoder = new ArithmeticCodingReader(ms, freqs);
                for (int i = 0; i < count; i++)
                    Assert.AreEqual(1, decoder.ReadSymbol());
                Assert.AreEqual(0, decoder.ReadSymbol());
                decoder.Finalize();
                Assert.AreEqual(expectedEnding, ms.Position);
                Assert.AreEqual(47, ms.ReadByte());

                if (count < 1000)
                    count++;
                else if (count < 10_000)
                    count += 997;
                else if (count < 100_000)
                    count = 9_999_991;
                else
                    break;
            }
        }

        [TestMethod]
        public void TestExtremeProbabilities2()
        {
            uint maxTotal = ArithmeticSymbolContext.MaxTotal;
            var rnd = new Random(123);
            for (int symbolCount = 2; symbolCount < 8; symbolCount++)
            {
                for (int commonSym = 0; commonSym < symbolCount; commonSym++)
                {
                    var freq = newArray(symbolCount, i => i == commonSym ? maxTotal - (uint) symbolCount + 1 : 1u);
                    var uncommonSyms = Enumerable.Range(0, symbolCount).Where(s => s != commonSym).ToArray();
                    for (int length = 1; length < 9 - symbolCount; length++)
                    {
                        for (int pattern = 0; pattern < Math.Pow(symbolCount, length); pattern++)
                        {
                            testExtremeProbs(freq, Enumerable.Range(0, length).Select(p => (pattern & (1 << p)) == 0 ? commonSym : uncommonSyms[rnd.Next(uncommonSyms.Length)]).ToArray());
                        }
                    }
                }
            }
        }

        private static void testExtremeProbs(uint[] freqs, int[] symbols)
        {
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            foreach (var symbol in symbols)
                encoder.WriteSymbol(symbol);
            encoder.Finalize();
            var expectedEnding = ms.Position;
            ms.WriteByte(47);
            var bytes = ms.ToArray();

            ms = new MemoryStream(bytes);
            var decoder = new ArithmeticCodingReader(ms, freqs);
            for (int i = 0; i < symbols.Length; i++)
                Assert.AreEqual(symbols[i], decoder.ReadSymbol());
            decoder.Finalize();
            Assert.AreEqual(expectedEnding, ms.Position);
            Assert.AreEqual(47, ms.ReadByte());
        }
    }
}
