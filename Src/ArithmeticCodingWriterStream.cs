using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a write-only stream which encodes the bytes written using arithmetic coding.</summary>
    /// <seealso cref="ArithmeticCodingReader"/>
    public class ArithmeticCodingWriterStream : Stream
    {
        private ArithmeticCodingWriter _writer;

        internal const int END_OF_STREAM = 256;

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingWriterStream"/> instance given an output stream and a set of byte
        ///     frequencies.</summary>
        /// <param name="outputStream">
        ///     The output stream to which the encoded data will be written.</param>
        /// <param name="frequencies">
        ///     The frequency of each byte occurring, plus the frequency of the end of stream symbol (written automatically
        ///     when the stream is closed). Must be 257 elements long. When reading the data back using an <see
        ///     cref="ArithmeticCodingReaderStream"/>, the set of frequencies must be exactly the same.</param>
        /// <remarks>
        ///     The encoded data will not be complete until the stream is closed using <see cref="Close()"/>.</remarks>
        public ArithmeticCodingWriterStream(Stream outputStream, ulong[] frequencies)
            : this(outputStream, new ArithmeticSymbolArrayContext(frequencies))
        {
            if (frequencies.Length != 257)
                throw new ArgumentException("The frequencies array must be 257 elements long.", nameof(frequencies));
        }

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingWriterStream"/> instance given an output stream and a set of byte
        ///     frequencies.</summary>
        /// <param name="outputStream">
        ///     The output stream to which the encoded data will be written.</param>
        /// <param name="context">
        ///     The context used for determining the relative frequencies of input bytes. The caller may make changes to the
        ///     context instance it passed in; such changes will take effect immediately. See also <see cref="SetContext"/>.</param>
        /// <remarks>
        ///     The encoded data will not be complete until the stream is closed using <see cref="Close()"/>.</remarks>
        public ArithmeticCodingWriterStream(Stream outputStream, ArithmeticSymbolContext context)
        {
            _writer = new ArithmeticCodingWriter(
                outputStream ?? throw new ArgumentNullException(nameof(outputStream)),
                context ?? throw new ArgumentNullException(nameof(context))
            );
        }

        /// <summary>Returns <c>false</c>.</summary>
        public override bool CanRead { get { return false; } }
        /// <summary>Returns <c>false</c>.</summary>
        public override bool CanSeek { get { return false; } }
        /// <summary>Returns <c>true</c>.</summary>
        public override bool CanWrite { get { return true; } }

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
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        /// <summary>Encodes the bytes in the specified section of the buffer.</summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = offset; (i < offset + count) && (i < buffer.Length); i++)
                _writer.WriteSymbol(buffer[i]);
        }

        /// <summary>Encodes the specified byte.</summary>
        public override void WriteByte(byte value)
        {
            _writer.WriteSymbol(value);
        }

        /// <summary>
        ///     Changes the symbol context. See Remarks.</summary>
        /// <remarks>
        ///     The context instance may be modified after it's been applied by <see cref="SetContext"/> (or in the initial
        ///     constructor call) with immediate effect. It is not necessary to call this method after modifying an already
        ///     applied context. Symbol contexts may be changed arbitrarily between calls to <see cref="Write"/>, as long as
        ///     the same changes are made during decoding between calls to <see cref="ArithmeticCodingReaderStream.Read"/>.</remarks>
        public void SetContext(ArithmeticSymbolContext context)
        {
            _writer.SetContext(context);
        }

        /// <summary>Finalizes the encoding and closes the output stream.</summary>
        public override void Close()
        {
            Close(true);
        }

        /// <summary>
        ///     Finalizes and closes the stream. One last symbol is encoded into the stream, so make sure a suitable symbol
        ///     context is applied.</summary>
        /// <param name="closeOutputStream">
        ///     Specifies whether to close the output stream.</param>
        public void Close(bool closeOutputStream = true)
        {
            _writer.WriteSymbol(END_OF_STREAM);
            _writer.Finalize(closeOutputStream);
            base.Close();
        }
    }
}
