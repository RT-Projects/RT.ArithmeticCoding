using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a write-only stream that can compress data using Arithmetic Coding.</summary>
    /// <seealso cref="ArithmeticCodingReader"/>
    public class ArithmeticCodingWriterStream : Stream
    {
        private ArithmeticCodingWriter _writer;

        /// <summary>Encapsulates a symbol that represents the end of the stream. All other symbols are byte values.</summary>
        internal const int END_OF_STREAM = 256;

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
        public ArithmeticCodingWriterStream(Stream basestr, ulong[] frequencies)
            : this(basestr, frequencies == null ? new ArithmeticSymbolArrayContext(257) : new ArithmeticSymbolArrayContext(frequencies))
        {
        }

        public ArithmeticCodingWriterStream(Stream basestr, ArithmeticSymbolContext context)
        {
            _writer = new ArithmeticCodingWriter(
                basestr ?? throw new ArgumentNullException(nameof(basestr)),
                context ?? throw new ArgumentNullException(nameof(context))
            );
        }

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Length => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = offset; (i < offset + count) && (i < buffer.Length); i++)
                _writer.WriteSymbol(buffer[i]);
        }

        public override void Close()
        {
            Close(true);
        }

        /// <summary>
        ///     Closes the stream, optionally writing an end-of-stream symbol first. The end-of-stream symbol has the numeric
        ///     value 257, which is useful only if you have 256 symbols or fewer. If you intend to use a larger symbol
        ///     alphabet, write your own end-of-stream symbol and then invoke Close(false).</summary>
        /// <param name="writeEndOfStreamSymbol">
        ///     Determines whether to write the end-of-stream symbol or not.</param>
        public void Close(bool writeEndOfStreamSymbol, bool closeBaseStream = true)
        {
            if (writeEndOfStreamSymbol)
                _writer.WriteSymbol(END_OF_STREAM);
            _writer.Close(closeBaseStream);
            base.Close();
        }

        /// <summary>
        ///     Changes the frequencies of the symbols. This can be used at any point in the middle of encoding, as long as
        ///     the same change is made at the same time when decoding using <see cref="ArithmeticCodingReader"/>.</summary>
        /// <param name="newFreqs"/>
        public void SetContext(ArithmeticSymbolContext context)
        {
            _writer.SetContext(context);
        }
    }
}
