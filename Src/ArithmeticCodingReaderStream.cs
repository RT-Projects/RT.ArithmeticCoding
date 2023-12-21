namespace RT.ArithmeticCoding;

/// <summary>
///     Provides a read-only stream which decodes data that was encoded using <see cref="ArithmeticCodingWriterStream"/>.</summary>
/// <seealso cref="ArithmeticCodingWriter"/>
public class ArithmeticCodingReaderStream : Stream
{
    private ArithmeticCodingReader _reader;
    private bool _ended = false;

    /// <summary>
    ///     Initialises an <see cref="ArithmeticCodingReaderStream"/> instance given an input stream and a set of byte
    ///     frequencies.</summary>
    /// <param name="inputStream">
    ///     The input stream from which the encoded data will be read.</param>
    /// <param name="frequencies">
    ///     The frequency of each byte occurring, plus the frequency of the end of stream symbol (written automatically when
    ///     the stream is closed). Must be 257 elements long. The set of frequencies must be exactly the same as the one used
    ///     when the data was written using <see cref="ArithmeticCodingWriterStream"/>.</param>
    public ArithmeticCodingReaderStream(Stream inputStream, uint[] frequencies)
        : this(inputStream, new ArithmeticSymbolArrayContext(frequencies))
    {
        if (frequencies.Length != 257)
            throw new ArgumentException("The frequencies array must be 257 elements long.", nameof(frequencies));
    }

    /// <summary>
    ///     Initialises an <see cref="ArithmeticCodingReaderStream"/> instance given an input stream and a set of byte
    ///     frequencies.</summary>
    /// <param name="inputStream">
    ///     The input stream from which the encoded data will be read.</param>
    /// <param name="context">
    ///     The context used for determining the relative frequencies of input bytes. The caller may make changes to the
    ///     context instance it passed in; such changes will take effect immediately. See also <see cref="SetContext"/>.</param>
    public ArithmeticCodingReaderStream(Stream inputStream, ArithmeticSymbolContext context)
    {
        _reader = new ArithmeticCodingReader(
            inputStream ?? throw new ArgumentNullException(nameof(inputStream)),
            context ?? throw new ArgumentNullException(nameof(context))
        );
    }

    /// <summary>Returns <c>true</c>.</summary>
    public override bool CanRead { get { return true; } }
    /// <summary>Returns <c>false</c>.</summary>
    public override bool CanSeek { get { return false; } }
    /// <summary>Returns <c>false</c>.</summary>
    public override bool CanWrite { get { return false; } }

    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override long Length => throw new NotSupportedException();
    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override void Flush() => throw new NotSupportedException();
    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override void SetLength(long value) => throw new NotSupportedException();
    /// <summary>Throws <c>NotSupportedException</c>.</summary>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>
    ///     Decodes no more than <paramref name="count"/> bytes into the specified <paramref name="buffer"/>, populating the
    ///     array from <paramref name="offset"/> onwards.</summary>
    /// <returns>
    ///     The number of bytes read. This can be less than <paramref name="count"/>. Returns 0 if and only if there are no
    ///     further bytes to be decoded.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_ended)
            return 0;
        for (int i = 0; i < count; i++)
        {
            int symbol = _reader.ReadSymbol();
            if (symbol == ArithmeticCodingWriterStream.END_OF_STREAM)
            {
                _ended = true;
                return i;
            }
            else
                buffer[offset + i] = (byte) symbol;
        }
        return count;
    }

    /// <summary>Decodes the next byte from the input stream. Returns -1 if there are no more bytes to be decoded.</summary>
    public override int ReadByte()
    {
        if (_ended)
            return -1;
        int symbol = _reader.ReadSymbol();
        if (symbol == ArithmeticCodingWriterStream.END_OF_STREAM)
            _ended = true;
        return _ended ? -1 : symbol;
    }

    /// <summary>
    ///     Changes the symbol context. See Remarks.</summary>
    /// <remarks>
    ///     The context instance may be modified after it's been applied by <see cref="SetContext"/> (or in the initial
    ///     constructor call) with immediate effect. It is not necessary to call this method after modifying an already
    ///     applied context..</remarks>
    public void SetContext(ArithmeticSymbolContext context)
    {
        _reader.SetContext(context);
    }

    /// <summary>
    ///     Finalizes the decoding and closes the input stream. This method must be called if you intend to read additional
    ///     data from the input stream, but it is optional otherwise. Disposing of this stream closes it automatically.</summary>
    public override void Close()
    {
        Close(true);
        base.Close();
    }

    /// <summary>
    ///     Finalizes the decoding and optionally closes the input stream. This method must be called if you intend to read
    ///     additional data from the input stream, but it is optional otherwise.</summary>
    public void Close(bool closeBaseStream = true)
    {
        _reader.Finalize(closeBaseStream);
    }
}
