using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a write-only stream that can compress data using Arithmetic Coding.</summary>
    /// <seealso cref="ArithmeticCodingReader"/>
    public class ArithmeticCodingWriter
    {
        private ulong _high, _low;
        private int _underflow;
        private ArithmeticSymbolContext _context;
        private Stream _basestream;
        private int _curbyte;
        private bool _anyWrites = false;

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingWriter"/> instance given a base stream and a set of byte
        ///     frequencies.</summary>
        /// <param name="basestr">
        ///     The base stream to which the compressed data will be written.</param>
        /// <param name="frequencies">
        ///     The frequency of each byte occurring. Can be null, in which case all bytes are assumed to have the same
        ///     frequency. When reading the data back using an <see cref="ArithmeticCodingReader"/>, the set of frequencies
        ///     must be exactly the same.</param>
        /// <remarks>
        ///     The compressed data will not be complete until the stream is closed using <see cref="Close()"/>.</remarks>
        public ArithmeticCodingWriter(Stream basestr, ulong[] frequencies)
            : this(basestr, frequencies == null ? new ArithmeticSymbolArrayContext(257) : new ArithmeticSymbolArrayContext(frequencies))
        {
        }

        public ArithmeticCodingWriter(Stream basestr, ArithmeticSymbolContext context)
        {
            _basestream = basestr ?? throw new ArgumentNullException(nameof(basestr));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _high = 0xFFFF_FFFF;
            _low = 0;
            _curbyte = 1;
            _underflow = 0;
        }

        /// <summary>
        ///     Writes a single symbol. Use this if you are not using bytes as your symbol alphabet.</summary>
        /// <param name="p">
        ///     Symbol to write. Must be an integer between 0 and the length of the frequencies array passed in the
        ///     constructor.</param>
        public void WriteSymbol(int p)
        {
            ulong total = _context.GetTotal();
            ulong symbolFreq = _context.GetSymbolFreq(p);
            ulong symbolPos = _context.GetSymbolPos(p);
            if (symbolFreq == 0)
                throw new ArgumentException("Attempt to encode a symbol with zero frequency");
            if (symbolPos + symbolFreq > total)
                throw new InvalidOperationException("Attempt to encode a symbol for which the symbol context returns inconsistent results (pos+prob > total)");

            ulong high = _high;
            ulong low = _low;
            int curbyte = _curbyte;
            int underflow = _underflow;

            _anyWrites = true;

            // Set high and low to the new values
            ulong newlow = (high - low + 1) * symbolPos / total + low;
            high = (high - low + 1) * (symbolPos + symbolFreq) / total + low - 1;
            low = newlow;

            // While most significant bits match, shift them out and output them
            while ((high & 0x8000_0000) == (low & 0x8000_0000))
            {
                // inlined: outputBit((high & 0x8000_0000) != 0);
                curbyte <<= 1;
                if ((high & 0x8000_0000) != 0)
                    curbyte++;
                if (curbyte >= 0x100)
                {
                    _basestream.WriteByte((byte) curbyte);
                    curbyte = 1;
                }

                while (underflow > 0)
                {
                    // inlined: outputBit((high & 0x8000_0000) == 0);
                    curbyte <<= 1;
                    if ((high & 0x8000_0000) == 0)
                        curbyte++;
                    if (curbyte >= 0x100)
                    {
                        _basestream.WriteByte((byte) curbyte);
                        curbyte = 1;
                    }

                    underflow--;
                }
                high = ((high << 1) & 0xFFFF_FFFF) | 1;
                low = (low << 1) & 0xFFFF_FFFF;
            }

            // If underflow is imminent, shift it out
            while (((low & 0x4000_0000) != 0) && ((high & 0x4000_0000) == 0))
            {
                underflow++;
                high = ((high & 0x7FFF_FFFF) << 1) | 0x8000_0001;
                low = (low << 1) & 0x7FFF_FFFF;
            }

            _high = high;
            _low = low;
            _curbyte = curbyte;
            _underflow = underflow;
        }

        private void outputBit(bool p)
        {
            _curbyte <<= 1;
            if (p)
                _curbyte++;
            if (_curbyte >= 0x100)
            {
                _basestream.WriteByte((byte) _curbyte);
                _curbyte = 1;
            }
        }

        /// <summary>
        ///     Closes the stream, optionally writing an end-of-stream symbol first. The end-of-stream symbol has the numeric
        ///     value 257, which is useful only if you have 256 symbols or fewer. If you intend to use a larger symbol
        ///     alphabet, write your own end-of-stream symbol and then invoke Close(false).</summary>
        public void Close(bool closeBaseStream = false)
        {
            if (_anyWrites)
            {
                outputBit((_low & 0x4000_0000) != 0);
                _underflow++;
                while (_underflow > 0)
                {
                    outputBit((_low & 0x4000_0000) == 0);
                    _underflow--;
                }
                if (_curbyte != 1)
                {
                    while (_curbyte < 0x100)
                        _curbyte <<= 1;
                    _basestream.WriteByte((byte) _curbyte);
                }
                // The reader needs to look ahead by a few bytes, so pad the ending to keep them in sync. The reader and the writer
                // use a slightly different number of bytes so this sequence helps the reader finish in exactly the right place.
                _basestream.WriteByte(0x51);
                _basestream.WriteByte(0x51);
                _basestream.WriteByte(0x51);
                _basestream.WriteByte(0x50);
            }
            if (closeBaseStream)
                _basestream.Close();
        }

        /// <summary>
        ///     Changes the frequencies of the symbols. This can be used at any point in the middle of encoding, as long as
        ///     the same change is made at the same time when decoding using <see cref="ArithmeticCodingReader"/>.</summary>
        /// <param name="newFreqs"/>
        public void SetContext(ArithmeticSymbolContext context)
        {
            _context = context;
        }
    }
}
