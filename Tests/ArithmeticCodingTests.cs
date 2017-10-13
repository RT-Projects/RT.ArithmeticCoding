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

        private ulong[] newArray(int length, Func<int, ulong> initial)
        {
            var result = new ulong[length];
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
        public void TestBasic()
        {
            for (int i = 0; i < 1000; i++)
                testBasic(i);
        }

        private void testBasic(int length)
        {
            var freqs = newArray(256, v => 256 - (ulong) v);
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            for (int i = 0; i < length; i++)
                encoder.WriteSymbol(i % 256);
            encoder.Close(false, false);
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
            decoder.Close(false);
            Assert.AreEqual(expectedEnding, ms.Position);
        }

        [TestMethod]
        public void TestAdvanced()
        {
            _rnd = new Random(12345);
            int max = 1000;
            var symbols = Enumerable.Range(1, 100_000).Select(_ => _rnd.Next(0, max)).ToArray();

            var mainContext = new ArithmeticSymbolArrayContext(max, _ => 1);
            var secondaryContext = new ArithmeticSymbolArrayContext(new ulong[] { 3, 2, 1 });

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
            encoder.Close(false, false);
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
            decoder.Close(false);
            Assert.AreEqual(-54321, readInt(ms));
        }

        [TestMethod]
        public void TestSingleSymbol()
        {
            var freqs = new ulong[] { 1 };
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            for (int i = 0; i < 100; i++)
                encoder.WriteSymbol(0);
            encoder.Close(false, false);
            Assert.AreEqual(5, ms.Position);

            ms = new MemoryStream(ms.ToArray());
            var decoder = new ArithmeticCodingReader(ms, freqs);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(0, decoder.ReadSymbol());
            decoder.Close(false);
            Assert.AreEqual(5, ms.Position);
        }
    }
}
