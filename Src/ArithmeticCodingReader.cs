using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RT.Util.Streams
{
    public class ArithmeticCodingReader : Stream
    {
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        private UInt64 high, low, code;
        private UInt64[] probs;
        private UInt64 totalprob;
        private Stream basestream;
        private byte curbyte;
        private int curbit;

        public const int END_OF_STREAM = 256;

        public ArithmeticCodingReader(Stream basestr, UInt64[] probabilities)
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
            curbit = 8;
            code = 0;
            for (int i = 0; i < 32; i++)
            {
                code <<= 1;
                code |= ReadBit() ? (UInt64) 1 : (UInt64) 0;
            }
        }

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
            for (int i = 0; i < count; i++)
            {
                int symbol = ReadSymbol();
                if (symbol == END_OF_STREAM)
                    return i;
                else
                    buffer[offset+i] = (byte) symbol;
            }
            return count;
        }

        public override int ReadByte()
        {
            int symbol = ReadSymbol();
            return symbol == END_OF_STREAM ? -1 : symbol;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void SetLength(long value)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void Close()
        {
            basestream.Close();
            base.Close();
        }

        private bool ReadBit()
        {
            if (curbit > 7)
            {
                curbit = 0;
                curbyte = (byte) basestream.ReadByte();
            }
            bool ret = (curbyte & (1 << curbit)) != 0;
            curbit++;
            return ret;
        }

        private int ReadSymbol()
        {
            // Find out what the next symbol is from the contents of 'code'
            UInt64 pos = ((code-low+1) * totalprob - 1)/(high-low+1);
            int symbol = 0;
            UInt64 postmp = pos;
            while (postmp >= probs[symbol])
            {
                postmp -= probs[symbol];
                symbol++;
            }
            pos -= postmp;  // pos is now the symbol's lowest possible pos

            // Set high and low to the new values
            UInt64 newlow = (high-low+1) * pos / totalprob + low;
            high = (high-low+1) * (pos+probs[symbol]) / totalprob + low - 1;
            low = newlow;

            // While most significant bits match, shift them out
            while ((high & 0x80000000) == (low & 0x80000000))
            {
                high = ((high << 1) & 0xffffffff) | 1;
                low = (low << 1) & 0xffffffff;
                code = (code << 1) & 0xffffffff;
                if (ReadBit()) code++;
            }

            // If underflow is imminent, shift it out
            while (((low & 0x40000000) != 0) && ((high & 0x40000000) == 0))
            {
                high = ((high & 0x7fffffff) << 1) | 0x80000001;
                low = (low << 1) & 0x7fffffff;
                code = ((code & 0x7fffffff) ^ 0x40000000) << 1;
                if (ReadBit()) code++;
            }

            return symbol;
        }
    }
}
