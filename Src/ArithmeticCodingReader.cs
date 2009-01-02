using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace RT.Util.Streams
{
    /// <summary>
    /// Provides a read-only stream that can decompress data that was compressed using Arithmetic Coding.
    /// </summary>
    /// <seealso cref="ArithmeticCodingWriter"/>
    public class ArithmeticCodingReader : Stream
    {
        private UInt64 _high, _low, _code;
        private UInt64[] _probs;
        private UInt64 _totalprob;
        private Stream _basestream;
        private byte _curbyte;
        private int _curbit;

        /// <summary>
        /// Encapsulates a symbol that represents the end of the stream. All other symbols are byte values.
        /// </summary>
        public const int END_OF_STREAM = 256;

        /// <summary>
        /// Initialises an <see cref="ArithmeticCodingReader"/> instance given a base stream and a set of byte probabilities.
        /// </summary>
        /// <param name="basestr">The base stream to which the compressed data will be written.</param>
        /// <param name="probabilities">The probability of each byte occurring. Can be null, in which
        /// case all bytes are assumed to have the same probability. The set of probabilities must be
        /// exactly the same as the one used when the data was written using <see cref="ArithmeticCodingWriter"/>.</param>
        public ArithmeticCodingReader(Stream basestr, UInt64[] probabilities)
        {
            _basestream = basestr;
            _high = 0xffffffff;
            _low = 0;
            if (probabilities == null)
            {
                _probs = new UInt64[257];
                for (int i = 0; i < 257; i++)
                    _probs[i] = 1;
                _totalprob = 257;
            }
            else
            {
                _probs = probabilities;
                _totalprob = 0;
                for (int i = 0; i < _probs.Length; i++)
                    _totalprob += _probs[i];
            }
            _curbyte = 0;
            _curbit = 8;
            _code = 0;
            for (int i = 0; i < 32; i++)
            {
                _code <<= 1;
                _code |= ReadBit() ? (UInt64) 1 : (UInt64) 0;
            }
        }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        public override void Flush()
        {
            _basestream.Flush();
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
            _basestream.Close();
            base.Close();
        }

        private bool ReadBit()
        {
            if (_curbit > 7)
            {
                _curbit = 0;
                _curbyte = (byte) _basestream.ReadByte();
            }
            bool ret = (_curbyte & (1 << _curbit)) != 0;
            _curbit++;
            return ret;
        }

        /// <summary>
        /// Reads a single symbol. Use this if you are not using bytes as your symbol alphabet.
        /// </summary>
        /// <returns>Symbol read.</returns>
        public int ReadSymbol()
        {
            // Find out what the next symbol is from the contents of 'code'
            UInt64 pos = ((_code-_low+1) * _totalprob - 1)/(_high-_low+1);
            int symbol = 0;
            UInt64 postmp = pos;
            while (postmp >= _probs[symbol])
            {
                postmp -= _probs[symbol];
                symbol++;
            }
            pos -= postmp;  // pos is now the symbol's lowest possible pos

            // Set high and low to the new values
            UInt64 newlow = (_high-_low+1) * pos / _totalprob + _low;
            _high = (_high-_low+1) * (pos+_probs[symbol]) / _totalprob + _low - 1;
            _low = newlow;

            // While most significant bits match, shift them out
            while ((_high & 0x80000000) == (_low & 0x80000000))
            {
                _high = ((_high << 1) & 0xffffffff) | 1;
                _low = (_low << 1) & 0xffffffff;
                _code = (_code << 1) & 0xffffffff;
                if (ReadBit()) _code++;
            }

            // If underflow is imminent, shift it out
            while (((_low & 0x40000000) != 0) && ((_high & 0x40000000) == 0))
            {
                _high = ((_high & 0x7fffffff) << 1) | 0x80000001;
                _low = (_low << 1) & 0x7fffffff;
                _code = ((_code & 0x7fffffff) ^ 0x40000000) << 1;
                if (ReadBit()) _code++;
            }

            return symbol;
        }
    }
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member
}
