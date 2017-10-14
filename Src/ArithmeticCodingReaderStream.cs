using System;
using System.IO;

namespace RT.ArithmeticCoding
{
    /// <summary>
    ///     Provides a read-only stream that can decompress data that was compressed using Arithmetic Coding.</summary>
    /// <seealso cref="ArithmeticCodingWriter"/>
    public class ArithmeticCodingReaderStream : Stream
    {
        private ArithmeticCodingReader _reader;
        private bool _ended = false;

        /// <summary>Encapsulates a symbol that represents the end of the stream. All other symbols are byte values.</summary>
        internal const int END_OF_STREAM = ArithmeticCodingWriterStream.END_OF_STREAM;

        /// <summary>
        ///     Initialises an <see cref="ArithmeticCodingReader"/> instance given a base stream and a set of byte
        ///     frequencies.</summary>
        /// <param name="basestr">
        ///     The base stream to which the compressed data will be written.</param>
        /// <param name="frequencies">
        ///     The frequency of each byte occurring. Can be null, in which case all bytes are assumed to have the same
        ///     frequency. The set of frequencies must be exactly the same as the one used when the data was written using
        ///     <see cref="ArithmeticCodingWriter"/>.</param>
        public ArithmeticCodingReaderStream(Stream basestr, ulong[] frequencies)
            : this(basestr, frequencies == null ? new ArithmeticSymbolArrayContext(257) : new ArithmeticSymbolArrayContext(frequencies))
        {
        }

        public ArithmeticCodingReaderStream(Stream basestr, ArithmeticSymbolContext context)
        {
            _reader = new ArithmeticCodingReader(
                basestr ?? throw new ArgumentNullException(nameof(basestr)),
                context ?? throw new ArgumentNullException(nameof(context))
            );
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override long Length => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_ended)
                return 0;
            for (int i = 0; i < count; i++)
            {
                int symbol = _reader.ReadSymbol();
                if (symbol == END_OF_STREAM)
                    return i;
                else
                    buffer[offset + i] = (byte) symbol;
            }
            return count;
        }

        public override int ReadByte()
        {
            int symbol = _reader.ReadSymbol();
            return symbol == END_OF_STREAM ? -1 : symbol;
        }

        public void SetContext(ArithmeticSymbolContext context)
        {
            _reader.SetContext(context);
        }

        public override void Close()
        {
            Close(true);
            base.Close();
        }

        public void Close(bool closeBaseStream = true)
        {
            _reader.Finalize(closeBaseStream);
        }
    }
}
