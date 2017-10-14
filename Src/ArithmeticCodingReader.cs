using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a read-only stream that can decompress data that was compressed using Arithmetic Coding.</summary>
    /// <seealso cref="ArithmeticCodingWriter"/>
    public class ArithmeticCodingReader
    {
        private ulong _high, _low, _code;
        private ArithmeticSymbolContext _context;
        private Stream _basestream;
        private int _curbyte;
        private bool _first = true;

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
            : this(basestr, frequencies == null ? new ArithmeticSymbolArrayContext(257) : new ArithmeticSymbolArrayContext(frequencies))
        {
        }

        public ArithmeticCodingReader(Stream basestr, ArithmeticSymbolContext context)
        {
            _basestream = basestr ?? throw new ArgumentNullException(nameof(basestr));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _high = 0xFFFF_FFFF;
            _low = 0;
            _curbyte = 0x10000;
            _code = 0;
        }

        private bool readBit()
        {
            if (_curbyte >= 0x10000)
                _curbyte = _basestream.ReadByte() | 0x100;
            _curbyte <<= 1;
            return (_curbyte & 0x100) != 0;
        }

        /// <summary>
        ///     Reads a single symbol. Use this if you are not using bytes as your symbol alphabet.</summary>
        /// <returns>
        ///     Symbol read.</returns>
        public int ReadSymbol()
        {
            ulong high = _high;
            ulong low = _low;
            ulong code = _code;
            int curbyte = _curbyte;

            ulong total = _context.GetTotal();

            if (_first)
            {
                code = (uint) _basestream.ReadByte() << 24;
                code |= (uint) _basestream.ReadByte() << 16;
                code |= (uint) _basestream.ReadByte() << 8;
                code |= (uint) _basestream.ReadByte();
                _first = false;
            }
            else
            {
                // While most significant bits match, shift them out
                while ((high & 0x8000_0000) == (low & 0x8000_0000))
                {
                    high = ((high << 1) & 0xFFFF_FFFF) | 1;
                    low = (low << 1) & 0xFFFF_FFFF;
                    code = (code << 1) & 0xFFFF_FFFF;
                    // inlined: if (readBit()) code++;
                    if (curbyte >= 0x10000)
                        curbyte = _basestream.ReadByte() | 0x100;
                    curbyte <<= 1;
                    if ((curbyte & 0x100) != 0)
                        code++;
                }

                // If underflow is imminent, shift it out
                while (((low & 0x4000_0000) != 0) && ((high & 0x4000_0000) == 0))
                {
                    high = ((high & 0x7FFF_FFFF) << 1) | 0x8000_0001;
                    low = (low << 1) & 0x7FFF_FFFF;
                    code = ((code & 0x7FFF_FFFF) ^ 0x4000_0000) << 1;
                    // inlined: if (readBit()) code++;
                    if (curbyte >= 0x10000)
                        curbyte = _basestream.ReadByte() | 0x100;
                    curbyte <<= 1;
                    if ((curbyte & 0x100) != 0)
                        code++;
                }
            }

            // Find out what the next symbol is from the contents of 'code'
            ulong pos = ((code - low + 1) * total - 1) / (high - low + 1);
            // Do a binary search of sorts to locate the symbol
            int symbol = 0;
            int inc = 1;
            while (pos >= _context.GetSymbolPos(symbol + 1))
            {
                symbol += inc;
                inc *= 2;
            }
            inc = inc / 2;
            while (inc != 0)
            {
                if (pos >= _context.GetSymbolPos(symbol + 1))
                    inc = (inc > 0 ? inc : -inc) / 2;
                else if (pos < _context.GetSymbolPos(symbol))
                    inc = (inc > 0 ? -inc : inc) / 2;
                else
                    break;
                symbol += inc;
            }
            pos = _context.GetSymbolPos(symbol);

            // Set high and low to the new values
            ulong newlow = (high - low + 1) * pos / total + low;
            high = (high - low + 1) * (pos + _context.GetSymbolFreq(symbol)) / total + low - 1;
            low = newlow;

            _high = high;
            _low = low;
            _code = code;
            _curbyte = curbyte;

            return symbol;
        }

        public void SetContext(ArithmeticSymbolContext context)
        {
            _context = context;
        }

        public void Finalize(bool closeBaseStream = false)
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
}
