using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Implements an arithmetic coding encoder. See Remarks.</summary>
    /// <remarks>
    ///     <para>
    ///         The writer accepts a sequence of symbols as inputs via <see cref="WriteSymbol(int)"/>, encodes them and writes
    ///         the encoded bytes to the specified stream. A symbol is an integer; the same integer will be returned by <see
    ///         cref="ArithmeticCodingReader.ReadSymbol"/> when the encoded stream is decoded. Symbols are encoded according
    ///         to the current <see cref="ArithmeticSymbolContext"/>, which describes the relative frequencies of all symbols.</para>
    ///     <para>
    ///         Symbols are permitted to have a frequency of zero, but it is illegal to attempt to encode a zero-frequency
    ///         symbol. The symbol context does not have to remain unchanged; the context may be modified arbitrarily between
    ///         calls to <see cref="WriteSymbol(int)"/>, and an entirely different context may be applied using <see
    ///         cref="SetContext"/>. The only requirement is that identical contexts are in place before every <see
    ///         cref="WriteSymbol"/> call and before its corresponding <see cref="ArithmeticCodingReader.ReadSymbol"/> call.</para>
    ///     <para>
    ///         This class does not offer any built-in means for the reader to detect the last symbol written. The caller must
    ///         know when to stop calling <see cref="ArithmeticCodingReader.ReadSymbol"/>. Where this cannot be deduced from
    ///         context, you can dedicate an extra symbol (with a suitable frequency) to mark end of stream, or write total
    ///         symbol count to the stream separately.</para>
    ///     <para>
    ///         The reader and the writer support operation on a stream which has other data before and/or after the
    ///         arithmetic-coded section. The reader's <see cref="ArithmeticCodingReader.Finalize"/> method ensures that the
    ///         input stream is left with exactly the correct number of bytes consumed.</para>
    ///     <para>
    ///         Arithmetic encoding uses fewer bits for more frequent symbols. The number of bits used per symbol is not
    ///         necessarily an integer, and there is no pre-determined output bit pattern corresponding to a given symbol.
    ///         Arithmetic coding is not data compression per se; it is an entropy coding algorithm. Arithmetic coding
    ///         requires an accurate prediction of each symbol's probability to be supplied by the caller in order to be
    ///         effective, and is only as good as the caller's modelling of the sequence of symbols being passed in.</para></remarks>
    /// <seealso cref="ArithmeticCodingReader"/>
    /// <seealso cref="ArithmeticCodingWriterStream"/>
    public class ArithmeticCodingWriter
    {
        private ulong _high, _low;
        private int _underflow;
        private ArithmeticSymbolContext _context;
        private Stream _stream;
        private int _curbyte;
        private bool _anyWrites = false;

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingWriter"/> instance. See Remarks.</summary>
        /// <param name="stream">
        ///     The stream to which the encoded data will be written.</param>
        /// <param name="frequencies">
        ///     The frequency of each symbol occurring. When reading the data back using an <see
        ///     cref="ArithmeticCodingReader"/>, the set of frequencies must be exactly the same.</param>
        /// <remarks>
        ///     The encoded data will not be complete until the writer is finalized using <see cref="Finalize"/>.</remarks>
        public ArithmeticCodingWriter(Stream stream, ulong[] frequencies)
            : this(stream, new ArithmeticSymbolArrayContext(frequencies ?? throw new ArgumentNullException(nameof(frequencies))))
        {
        }

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingWriter"/> instance. See Remarks.</summary>
        /// <param name="stream">
        ///     The stream to which the encoded data will be written.</param>
        /// <param name="context">
        ///     The context used for determining the relative frequencies of encoded symbols. The caller may make changes to
        ///     the context instance it passed in; such changes will take effect immediately. See also <see
        ///     cref="SetContext"/>.</param>
        /// <remarks>
        ///     The encoded data will not be complete until the writer is finalized using <see cref="Finalize"/>.</remarks>
        public ArithmeticCodingWriter(Stream stream, ArithmeticSymbolContext context)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _high = 0xFFFF_FFFF;
            _low = 0;
            _curbyte = 1;
            _underflow = 0;
        }

        /// <summary>
        ///     Encodes a single symbol.</summary>
        /// <param name="symbol">
        ///     Symbol to write. Must be a non-negative integer with a non-zero frequency in the current context.</param>
        public void WriteSymbol(int symbol)
        {
            if (_context == null)
                throw new InvalidOperationException("The writer has already been finalized; no further symbol writes are permitted.");
            ulong total = _context.GetTotal();
            ulong symbolFreq = _context.GetSymbolFreq(symbol);
            ulong symbolPos = _context.GetSymbolPos(symbol);
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
                    _stream.WriteByte((byte) curbyte);
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
                        _stream.WriteByte((byte) curbyte);
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
                _stream.WriteByte((byte) _curbyte);
                _curbyte = 1;
            }
        }

        /// <summary>
        ///     Finalizes the stream by flushing any remaining buffered data and writing the synchronization padding required
        ///     by the reader. This call is mandatory; the stream will not be readable in full if this method is not called.
        ///     This method does not write enough information to the stream for the reader to detect that there are no further
        ///     symbols; see Remarks on <see cref="ArithmeticCodingWriter"/> for further info.</summary>
        /// <param name="closeStream">
        ///     Specifies whether the output stream should be closed. Optional; defaults to <c>false</c>.</param>
        public void Finalize(bool closeStream = false)
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
                    _stream.WriteByte((byte) _curbyte);
                }
                // The reader needs to look ahead by a few bytes, so pad the ending to keep them in sync. The reader and the writer
                // use a slightly different number of bytes so this sequence helps the reader finish in exactly the right place.
                _stream.WriteByte(0x51);
                _stream.WriteByte(0x51);
                _stream.WriteByte(0x51);
                _stream.WriteByte(0x50);
            }
            if (closeStream)
                _stream.Close();
            _context = null; // prevent further symbol writes
        }

        /// <summary>
        ///     Changes the symbol context. See Remarks.</summary>
        /// <remarks>
        ///     The context instance may be modified after it's been applied by <see cref="SetContext"/> (or in the initial
        ///     constructor call) with immediate effect. It is not necessary to call this method after modifying an already
        ///     applied context. Symbol contexts may be changed arbitrarily between calls to <see cref="WriteSymbol"/>, as
        ///     long as the same changes are made during decoding between calls to <see
        ///     cref="ArithmeticCodingReader.ReadSymbol"/>.</remarks>
        public void SetContext(ArithmeticSymbolContext context)
        {
            if (_context == null)
                throw new InvalidOperationException("The writer has already been finalized; no further context changes are permitted.");
            _context = context;
        }
    }
}
