namespace RT.ArithmeticCoding;

/// <summary>
///     Implements an arithmetic coding decoder. See <see cref="ArithmeticCodingWriter"/> for further details.</summary>
/// <seealso cref="ArithmeticCodingWriter"/>
/// <seealso cref="ArithmeticCodingReaderStream"/>
public class ArithmeticCodingReader
{
    private ulong _high, _low, _code;
    private ArithmeticSymbolContext _context;
    private Stream _stream;
    private int _curbyte;
    private bool _first = true;

    /// <summary>
    ///     Initialises an <see cref="ArithmeticCodingReader"/> instance. See <see cref="ArithmeticCodingWriter"/> for further
    ///     details.</summary>
    /// <param name="stream">
    ///     The stream from which the encoded data will be read for decoding.</param>
    /// <param name="frequencies">
    ///     The frequency of each symbol occurring. Must match the frequencies used by <see cref="ArithmeticCodingWriter"/>
    ///     for encoding the data.</param>
    public ArithmeticCodingReader(Stream stream, uint[] frequencies)
        : this(stream, new ArithmeticSymbolArrayContext(frequencies ?? throw new ArgumentNullException(nameof(frequencies))))
    {
    }

    /// <summary>
    ///     Initialises an <see cref="ArithmeticCodingReader"/> instance. See <see cref="ArithmeticCodingWriter"/> for further
    ///     details.</summary>
    /// <param name="stream">
    ///     The stream from which the encoded data will be read for decoding.</param>
    /// <param name="context">
    ///     The context used for determining the relative frequencies of encoded symbols. The caller may make changes to the
    ///     context instance it passed in; such changes will take effect immediately. See also <see cref="SetContext"/>.</param>
    public ArithmeticCodingReader(Stream stream, ArithmeticSymbolContext context)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _high = 0xFFFF_FFFF;
        _low = 0;
        _curbyte = 0x10000;
        _code = 0;
    }

    /// <summary>
    ///     Changes the symbol context. See Remarks.</summary>
    /// <remarks>
    ///     The context instance may be modified after it's been applied by <see cref="SetContext"/> (or in the initial
    ///     constructor call) with immediate effect. It is not necessary to call this method after modifying a context that's
    ///     already been set using this method. Symbol context changes during reading must match exactly those made during
    ///     writing.</remarks>
    public void SetContext(ArithmeticSymbolContext context)
    {
        _context = context;
    }

    /// <summary>Decodes a single symbol.</summary>
    public int ReadSymbol()
    {
        ulong high = _high;
        ulong low = _low;
        ulong code = _code;
        int curbyte = _curbyte;

        ulong total = _context.GetTotal();

        if (_first)
        {
            code = (uint) _stream.ReadByte() << 24;
            code |= (uint) _stream.ReadByte() << 16;
            code |= (uint) _stream.ReadByte() << 8;
            code |= (uint) _stream.ReadByte();
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
                    curbyte = _stream.ReadByte() | 0x100;
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
                    curbyte = _stream.ReadByte() | 0x100;
                curbyte <<= 1;
                if ((curbyte & 0x100) != 0)
                    code++;
            }
        }

        // Find out what the next symbol is from the contents of 'code'
        ulong pos = checked(((code - low + 1) * total - 1) / (high - low + 1));
        // Do a binary search of sorts to locate the symbol
        int symbol = 0;
        int inc = 1;
        while (pos >= _context.GetSymbolPosition(symbol + 1))
        {
            symbol += inc;
            inc *= 2;
        }
        inc /= 2;
        while (inc != 0)
        {
            if (pos >= _context.GetSymbolPosition(symbol + 1))
                inc = (inc > 0 ? inc : -inc) / 2;
            else if (pos < _context.GetSymbolPosition(symbol))
                inc = (inc > 0 ? -inc : inc) / 2;
            else
                break;
            symbol += inc;
        }
        pos = _context.GetSymbolPosition(symbol);

        // Set high and low to the new values
        ulong newlow = (high - low + 1) * pos / total + low;
        high = (high - low + 1) * (pos + _context.GetSymbolFrequency(symbol)) / total + low - 1;
        low = newlow;

        _high = high;
        _low = low;
        _code = code;
        _curbyte = curbyte;

        return symbol;
    }

    /// <summary>
    ///     Finalizes the stream by reading synchronization padding appended by the writer. This call is optional; it is only
    ///     required if further data will be read from the same input stream.</summary>
    /// <param name="closeStream">
    ///     Specifies whether the input stream should be closed. Optional; defaults to <c>false</c>.</param>
    public void Finalize(bool closeStream = false)
    {
        if (!_first)
        {
            // Transfer the remainder of curbyte into code
            while (_curbyte < 0x10000)
            {
                _code = (_code << 1) & 0xFFFF_FFFF;
                _curbyte <<= 1;
                if ((_curbyte & 0x100) != 0)
                    _code++;
            }
            // Expect to see a sequence of 0x51, 0x51, 0x51, 0x50 bytes, of which up to all 4 might have been read into _code already.
            // This sequence is here to guarantee that reader and writer use the same number of bytes. There is no way to use it to detect the last symbol.
            if (_code != 0x51515150)
            {
                for (int i = 0; i < 5; i++)
                {
                    if (_stream.ReadByte() == 0x50)
                        break;
                    if (i == 4)
                        throw new InvalidOperationException("The stream did not end properly");
                }
            }
        }
        if (closeStream)
            _stream.Close();
    }

    private bool readBit()
    {
        if (_curbyte >= 0x10000)
            _curbyte = _stream.ReadByte() | 0x100;
        _curbyte <<= 1;
        return (_curbyte & 0x100) != 0;
    }
}
