using System.IO;
using System.Linq;
using NUnit.Framework;
using RT.Util.ExtensionMethods;

namespace RT.Util.Streams
{
    [TestFixture]
    public sealed class ArithmeticCodingTests
    {
        [Test]
        public void TestBasic()
        {
            var freqs = Ut.NewArray(256, _ => 1UL);
            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(ms, freqs);
            for (int i = 0; i < 256; i++)
                encoder.WriteSymbol(i);
            encoder.Close(false);
            var bytes = ms.ToArray();

            ms = new MemoryStream(bytes);
            var decoder = new ArithmeticCodingReader(ms, freqs);
            for (int i = 0; i < 256; i++)
            {
                var sym = decoder.ReadSymbol();
                Assert.AreEqual(i, sym);
            }
            Assert.AreEqual(bytes.Length, ms.Position);
        }

        [Test]
        public void TestAdvanced()
        {
            Rnd.Reset(12345);
            int max = 1000;
            var symbols = Enumerable.Range(1, 100_000).Select(_ => Rnd.Next(0, max)).ToArray();

            var mainFreqs = Ut.NewArray(max, _ => 1UL);
            var secondaryFreqs = new ulong[] { 3, 2, 1 };

            var ms = new MemoryStream();
            var encoder = new ArithmeticCodingWriter(new DoNotCloseStream(ms), mainFreqs);
            ms.WriteInt64Optim(12345);
            for (int i = 0; i < symbols.Length; i++)
            {
                encoder.WriteSymbol(symbols[i]);
                mainFreqs[symbols[i]]++;
                encoder.TweakProbabilities(mainFreqs);
                if (i % 1000 == 999)
                {
                    encoder.TweakProbabilities(secondaryFreqs);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(1);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(1);
                    encoder.WriteSymbol(0);
                    encoder.WriteSymbol(2);
                    encoder.TweakProbabilities(mainFreqs);
                }
            }
            encoder.Close(false);
            //ms.WriteInt64Optim(-54321); // to verify that the stream ends where we think it ends
            var encoded = ms.ToArray();


            ms = new MemoryStream(encoded);
            mainFreqs = Ut.NewArray(max, _ => 1UL); // reset frequencies
            Assert.AreEqual(12345, ms.ReadInt64Optim());
            var decoder = new ArithmeticCodingReader(ms, mainFreqs);
            for (int i = 0; i < symbols.Length; i++)
            {
                var sym = decoder.ReadSymbol();
                Assert.AreEqual(symbols[i], sym);
                mainFreqs[sym]++;
                decoder.TweakProbabilities(mainFreqs);
                if (i % 1000 == 999)
                {
                    decoder.TweakProbabilities(secondaryFreqs);
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(1, decoder.ReadSymbol());
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(1, decoder.ReadSymbol());
                    Assert.AreEqual(0, decoder.ReadSymbol());
                    Assert.AreEqual(2, decoder.ReadSymbol());
                    decoder.TweakProbabilities(mainFreqs);
                }
            }
#warning TODO: this fails at the moment because the reader reads past what the writer wrote
            //Assert.AreEqual(-54321, ms.ReadInt64Optim());
        }
    }
}
