using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a read-only stream that can decompress data that was compressed using Arithmetic Coding.</summary>
    /// <seealso cref="ArithmeticCodingWriter"/>
    public sealed class ArithmeticCodingReader : Stream
    {
        private ulong _high, _low, _code;
        private ulong[] _freqs;
        private ulong _totalfreq;
        private Stream _basestream;
        private byte _curbyte;
        private int _curbit;
        private bool _ended = false;
        private bool _first = true;

        /// <summary>Encapsulates a symbol that represents the end of the stream. All other symbols are byte values.</summary>
        public const int END_OF_STREAM = 256;

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingReader"/> instance given a base stream and a set of byte
        ///     frequencies.</summary>
        /// <param name="basestr">
        ///     The base stream to which the compressed data will be written.</param>
        /// <param name="frequencies">
        ///     The frequency of each byte occurring. Can be null, in which case all bytes are assumed to have the same
        ///     frequency. The set of frequencies must be exactly the same as the one used when the data was written using
        ///     <see cref="ArithmeticCodingWriter"/>.</param>
        public ArithmeticCodingReader(Stream basestr, ulong[] frequencies)
        {
            _basestream = basestr;
            _high = 0xFFFF_FFFF;
            _low = 0;
            if (frequencies == null)
            {
                _freqs = new ulong[257];
                for (int i = 0; i < 257; i++)
                    _freqs[i] = 1;
                _totalfreq = 257;
            }
            else
            {
                _freqs = frequencies;
                _totalfreq = 0;
                for (int i = 0; i < _freqs.Length; i++)
                    _totalfreq += _freqs[i];
            }
            _curbyte = 0;
            _curbit = 8;
            _code = 0;
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
            if (_ended)
                return 0;
            for (int i = 0; i < count; i++)
            {
                int symbol = ReadSymbol();
                if (symbol == END_OF_STREAM)
                    return i;
                else
                    buffer[offset + i] = (byte) symbol;
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
            Close(true);
            base.Close();
        }

        private bool readBit()
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
        ///     Reads a single symbol. Use this if you are not using bytes as your symbol alphabet.</summary>
        /// <returns>
        ///     Symbol read.</returns>
        public int ReadSymbol()
        {
            if (_first)
            {
                for (int i = 0; i < 32; i++)
                {
                    _code <<= 1;
                    _code |= readBit() ? 1UL : 0UL;
                }
                _first = false;
            }
            else
            {
                // While most significant bits match, shift them out
                while ((_high & 0x8000_0000) == (_low & 0x8000_0000))
                {
                    _high = ((_high << 1) & 0xFFFF_FFFF) | 1;
                    _low = (_low << 1) & 0xFFFF_FFFF;
                    _code = (_code << 1) & 0xFFFF_FFFF;
                    if (readBit()) _code++;
                }

                // If underflow is imminent, shift it out
                while (((_low & 0x4000_0000) != 0) && ((_high & 0x4000_0000) == 0))
                {
                    _high = ((_high & 0x7FFF_FFFF) << 1) | 0x8000_0001;
                    _low = (_low << 1) & 0x7FFF_FFFF;
                    _code = ((_code & 0x7FFF_FFFF) ^ 0x4000_0000) << 1;
                    if (readBit()) _code++;
                }
            }

            // Find out what the next symbol is from the contents of 'code'
            ulong pos = ((_code - _low + 1) * _totalfreq - 1) / (_high - _low + 1);
            int symbol = 0;
            ulong postmp = pos;
            while (postmp >= _freqs[symbol])
            {
                postmp -= _freqs[symbol];
                symbol++;
            }
            pos -= postmp;  // pos is now the symbol's lowest possible pos

            // Set high and low to the new values
            ulong newlow = (_high - _low + 1) * pos / _totalfreq + _low;
            _high = (_high - _low + 1) * (pos + _freqs[symbol]) / _totalfreq + _low - 1;
            _low = newlow;

            if (symbol == END_OF_STREAM)
                _ended = true;

            return symbol;
        }

        public void TweakProbabilities(ulong[] newFreqs)
        {
            _freqs = newFreqs;
            _totalfreq = 0;
            for (int i = 0; i < _freqs.Length; i++)
                _totalfreq += _freqs[i];
        }

        public void Close(bool closeBaseStream = true)
        {
            if (!_first)
            {
                // Expect to see a sequence of 0x51, 0x51, 0x51, 0x50 bytes, with the first 1 to 3 potentially cut off
                // This sequence is here to guarantee that reader and writer use the same number of bytes. There is no way to use it to detect the last symbol.
                var b = _basestream.ReadByte();
                if (b == 0x51)
                    b = _basestream.ReadByte();
                if (b == 0x51)
                    b = _basestream.ReadByte();
                if (b == 0x51)
                    b = _basestream.ReadByte();
                if (b != 0x50)
                    throw new InvalidOperationException("The stream did not end properly");
            }
            if (closeBaseStream)
                _basestream.Close();
        }
    }
#pragma warning restore 1591    // Missing XML comment for publicly visible type or member
}
