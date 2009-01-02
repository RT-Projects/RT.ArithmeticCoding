using System;
using System.IO;

namespace RT.Util.Streams
{
    /// <summary>
    /// Provides a write-only stream that can compress data using Arithmetic Coding.
    /// </summary>
    /// <seealso cref="ArithmeticCodingReader"/>
    public class ArithmeticCodingWriter : Stream
    {
        private UInt64 _high, _low;
        private int _underflow;
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
        /// Initialises an <see cref="ArithmeticCodingWriter"/> instance given a base stream and a set of byte probabilities.
        /// </summary>
        /// <param name="basestr">The base stream to which the compressed data will be written.</param>
        /// <param name="probabilities">The probability of each byte occurring. Can be null, in which 
        /// case all bytes are assumed to have the same probability. When reading the data back using
        /// an <see cref="ArithmeticCodingReader"/>, the set of probabilities must be exactly the same.</param>
        /// <remarks>The compressed data will not be complete until the stream is closed using <see cref="Close()"/>.</remarks>
        public ArithmeticCodingWriter(Stream basestr, UInt64[] probabilities)
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
            _curbit = 0;
            _underflow = 0;
        }

#pragma warning disable 1591    // Missing XML comment for publicly visible type or member

        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

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
            throw new Exception("This is ArithmeticCodingWriter! You can't read from it.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new Exception("The method ArithmeticCodingWriter.Seek() is not implemented.");
        }

        public override void SetLength(long value)
        {
            throw new Exception("The method ArithmeticCodingWriter.SetLength() is not implemented.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = offset; (i < offset + count) && (i < buffer.Length); i++)
                WriteSymbol(buffer[i]);
        }

        /// <summary>
        /// Writes a single symbol. Use this if you are not using bytes as your symbol alphabet.
        /// </summary>
        /// <param name="p">Symbol to write. Must be an integer between 0 and the length of the probabilities array passed in the constructor.</param>
        public void WriteSymbol(int p)
        {
            if (p >= _probs.Length)
                throw new Exception("Attempt to encode non-existent symbol");

            UInt64 pos = 0;
            for (int i = 0; i < p; i++)
                pos += _probs[i];

            // Set high and low to the new values
            UInt64 newlow = (_high - _low + 1) * pos / _totalprob + _low;
            _high = (_high - _low + 1) * (pos + _probs[p]) / _totalprob + _low - 1;
            _low = newlow;

            // While most significant bits match, shift them out and output them
            while ((_high & 0x80000000) == (_low & 0x80000000))
            {
                OutputBit((_high & 0x80000000) != 0);
                while (_underflow > 0)
                {
                    OutputBit((_high & 0x80000000) == 0);
                    _underflow--;
                }
                _high = ((_high << 1) & 0xffffffff) | 1;
                _low = (_low << 1) & 0xffffffff;
            }

            // If underflow is imminent, shift it out
            while (((_low & 0x40000000) != 0) && ((_high & 0x40000000) == 0))
            {
                _underflow++;
                _high = ((_high & 0x7fffffff) << 1) | 0x80000001;
                _low = (_low << 1) & 0x7fffffff;
            }
        }

        private void OutputBit(bool p)
        {
            if (p) _curbyte |= (byte) (1 << _curbit);
            if (_curbit >= 7)
            {
                _basestream.WriteByte(_curbyte);
                _curbit = 0;
                _curbyte = 0;
            }
            else
                _curbit++;
        }

        public override void Close()
        {
            Close(true);
        }

#pragma warning restore 1591    // Missing XML comment for publicly visible type or member

        /// <summary>
        /// Closes the stream, optionally writing an end-of-stream symbol first. The end-of-stream symbol
        /// has the numeric value 257, which is useful only if you have 256 symbols or fewer. If you intend
        /// to use a larger symbol alphabet, write your own end-of-stream symbol and then invoke Close(false).
        /// </summary>
        /// <param name="writeEndOfStreamSymbol">Determines whether to write the end-of-stream symbol or not.</param>
        public void Close(bool writeEndOfStreamSymbol)
        {
            if (writeEndOfStreamSymbol)
                WriteSymbol(END_OF_STREAM);
            OutputBit((_low & 0x40000000) != 0);
            _underflow++;
            while (_underflow > 0)
            {
                OutputBit((_low & 0x40000000) == 0);
                _underflow--;
            }
            _basestream.WriteByte(_curbyte);
            _basestream.Close();
            base.Close();
        }
    }
}
