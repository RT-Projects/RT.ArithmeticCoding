using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RT.Util.Streams
{
    /// <summary>
    /// Provides a write-only stream that can compress data using Arithmetic Coding.
    /// </summary>
    /// <seealso cref="ArithmeticCodingReader"/>
    public class ArithmeticCodingWriter : Stream
    {
        private UInt64 high, low;
        private int underflow;
        private UInt64[] probs;
        private UInt64 totalprob;
        private Stream basestream;
        private byte curbyte;
        private int curbit;

        /// <summary>
        /// Encapsulates a symbol that represents the end of the stream. All other symbols are byte values.
        /// </summary>
        public const int END_OF_STREAM = 256;

        /// <summary>
        /// Initialises an <see cref="ArithmeticCodingWriter"/> instance given a base stream and a set of byte probabilities.
        /// </summary>
        /// <param name="basestr">The base stream to which the compressed data will be written.</param>
        /// <param name="probabilities">The probability of each byte occurring. Can be null, in which 
        /// case all bytes are assumed to have the same probability. When reading the data back using
        /// an <see cref="ArithmeticCodingReader"/>, the set of probabilities must be exactly the same.</param>
        /// <remarks>The compressed data will not be complete until the stream is closed using <see cref="Close"/>.</remarks>
        public ArithmeticCodingWriter(Stream basestr, UInt64[] probabilities)
        {
            basestream = basestr;
            high = 0xffffffff;
            low = 0;
            if (probabilities == null)
            {
                probs = new UInt64[257];
                for (int i = 0; i < 257; i++)
                    probs[i] = 1;
                totalprob = 257;
            }
            else
            {
                probs = probabilities;
                totalprob = 0;
                for (int i = 0; i < probs.Length; i++)
                    totalprob += probs[i];
            }
            curbyte = 0;
            curbit = 0;
            underflow = 0;
        }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override void Flush()
        {
            basestream.Flush();
        }

        public override long Length
        {
            get { throw new Exception("The method or operation is not implemented."); }
        }

        public override long Position
        {
            get
            {
                throw new Exception("The method or operation is not implemented.");
            }
            set
            {
                throw new Exception("The method or operation is not implemented.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new Exception("This is ArithmeticCodingWriter! You can't read from it.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new Exception("The method ArithmeticCodingWriter.Seek() is not implemented.");
        }

        public override void SetLength(long value)
        {
            throw new Exception("The method ArithmeticCodingWriter.SetLength() is not implemented.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = offset; (i < offset+count) && (i < buffer.Length); i++)
                WriteSymbol(buffer[i]);
        }

        private void WriteSymbol(int p)
        {
            if (p >= probs.Length)
                throw new Exception("Attempt to encode non-existent symbol");

            UInt64 pos = 0;
            for (int i = 0; i < p; i++)
                pos += probs[i];

            // Set high and low to the new values
            UInt64 newlow = (high-low+1) * pos / totalprob + low;
            high = (high-low+1) * (pos+probs[p]) / totalprob + low - 1;
            low = newlow;

            // While most significant bits match, shift them out and output them
            while ((high & 0x80000000) == (low & 0x80000000))
            {
                OutputBit((high & 0x80000000) != 0);
                while (underflow > 0)
                {
                    OutputBit((high & 0x80000000) == 0);
                    underflow--;
                }
                high = ((high << 1) & 0xffffffff) | 1;
                low = (low << 1) & 0xffffffff;
            }

            // If underflow is imminent, shift it out
            while (((low & 0x40000000) != 0) && ((high & 0x40000000) == 0))
            {
                underflow++;
                high = ((high & 0x7fffffff) << 1) | 0x80000001;
                low = (low << 1) & 0x7fffffff;
            }
        }

        private void OutputBit(bool p)
        {
            if (p) curbyte |= (byte) (1 << curbit);
            if (curbit >= 7)
            {
                basestream.WriteByte(curbyte);
                curbit = 0;
                curbyte = 0;
            }
            else
                curbit++;
        }

        public override void Close()
        {
            WriteSymbol(END_OF_STREAM);
            OutputBit((low & 0x40000000) != 0);
            underflow++;
            while (underflow > 0)
            {
                OutputBit((low & 0x40000000) == 0);
                underflow--;
            }
            basestream.WriteByte(curbyte);

            basestream.Close();
            base.Close();
        }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

    }
}
