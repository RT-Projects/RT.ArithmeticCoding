// This stand-alone program showcases the power of arithmetic coding when coupled with a somewhat good symbol predictor,
// and is able to beat 7-Zip LZMA (its default algorithm) on ultra, at least on text files. It approaches PPMd compression ratios.
// It is, of course, infinitely slower than either of these 7-Zip implementations, but is also comparatively tiny.

namespace Beat7zipStandalone;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3 || (args[0] != "c" && args[0] != "d"))
            throw new Exception("Usage: program.exe [c|d] <input-file> <output-file>");

        new Program().Process(compress: args[0] == "c", args[1], args[2], historyLength: 8);
    }

    private ulong _high = 0xFFFF_FFFF;
    private ulong _low = 0;
    private ulong _code;
    private int _underflow = 0;
    private int _curbyte = 1;

    private FixedByteQueue _history;
    private Node _root = new Node();
    private uint[] _frequencies = new uint[257];
    private uint[] _positions = new uint[257];

    private Stream _inputStream, _outputStream;

    public void Process(bool compress, string inputFile, string outputFile, int historyLength)
    {
        using (_inputStream = File.Open(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (_outputStream = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            _history = new FixedByteQueue(historyLength);
            int symbol;

            if (compress)
            {
                while ((symbol = _inputStream.ReadByte()) >= 0)
                {
                    writeSymbol(symbol);
                    symbolSeen((byte) symbol);
                }
                writeSymbol(256);
                writeLast();
            }
            else
            {
                readFirst();
                while ((symbol = readSymbol()) != 256)
                {
                    _outputStream.WriteByte(checked((byte) symbol));
                    symbolSeen((byte) symbol);
                }
            }
        }
    }

    // --- Write to arithmetic coded stream --- //

    private void writeSymbol(int symbol)
    {
        recomputeSymbolFrequencies();

        ulong total = _positions[256] + _frequencies[256];
        ulong newlow = checked((_high - _low + 1) * _positions[symbol] / total + _low);
        _high = checked((_high - _low + 1) * (_positions[symbol] + _frequencies[symbol]) / total + _low - 1);
        _low = newlow;

        while ((_high & 0x8000_0000) == (_low & 0x8000_0000))
        {
            outputBit((_high & 0x8000_0000) != 0);
            while (_underflow > 0)
            {
                outputBit((_high & 0x8000_0000) == 0);
                _underflow--;
            }
            _high = ((_high << 1) & 0xFFFF_FFFF) | 1;
            _low = (_low << 1) & 0xFFFF_FFFF;
        }

        while (((_low & 0x4000_0000) != 0) && ((_high & 0x4000_0000) == 0))
        {
            _underflow++;
            _high = ((_high & 0x7FFF_FFFF) << 1) | 0x8000_0001;
            _low = (_low << 1) & 0x7FFF_FFFF;
        }
    }

    private void writeLast()
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
            _outputStream.WriteByte((byte) _curbyte);
        }
        _outputStream.WriteByte(0x51);
        _outputStream.WriteByte(0x51);
        _outputStream.WriteByte(0x51);
        _outputStream.WriteByte(0x50);
    }

    private void outputBit(bool p)
    {
        _curbyte <<= 1;
        if (p)
            _curbyte++;
        if (_curbyte >= 0x100)
        {
            _outputStream.WriteByte((byte) _curbyte);
            _curbyte = 1;
        }
    }

    // --- Read from arithmetic coded stream --- //

    private void readFirst()
    {
        _curbyte = 0x10000;
        _code = (uint) _inputStream.ReadByte() << 24;
        _code |= (uint) _inputStream.ReadByte() << 16;
        _code |= (uint) _inputStream.ReadByte() << 8;
        _code |= (uint) _inputStream.ReadByte();
    }

    private int readSymbol()
    {
        recomputeSymbolFrequencies();
        ulong total = _positions[256] + _frequencies[256];

        while ((_high & 0x8000_0000) == (_low & 0x8000_0000))
        {
            _high = ((_high << 1) & 0xFFFF_FFFF) | 1;
            _low = (_low << 1) & 0xFFFF_FFFF;
            _code = (_code << 1) & 0xFFFF_FFFF;
            if (readBit()) _code++;
        }

        while (((_low & 0x4000_0000) != 0) && ((_high & 0x4000_0000) == 0))
        {
            _high = ((_high & 0x7FFF_FFFF) << 1) | 0x8000_0001;
            _low = (_low << 1) & 0x7FFF_FFFF;
            _code = ((_code & 0x7FFF_FFFF) ^ 0x4000_0000) << 1;
            if (readBit()) _code++;
        }

        ulong pos = checked(((_code - _low + 1) * total - 1) / (_high - _low + 1));
        int symbol = 0;
        ulong postmp = pos;
        while (postmp >= _frequencies[symbol])
        {
            postmp -= _frequencies[symbol];
            symbol++;
        }
        pos -= postmp;

        ulong newlow = (_high - _low + 1) * pos / total + _low;
        _high = (_high - _low + 1) * (pos + _frequencies[symbol]) / total + _low - 1;
        _low = newlow;

        return symbol;
    }

    private bool readBit()
    {
        if (_curbyte >= 0x10000)
            _curbyte = _inputStream.ReadByte() | 0x100;
        _curbyte <<= 1;
        return (_curbyte & 0x100) != 0;
    }

    // --- Symbol frequency tracking / probability prediction --- //

    private void symbolSeen(byte symbol)
    {
        _history.Enqueue(symbol);
        for (int i = 0; i < _history.Length; i++)
            _root.SequenceSeen(_history, i, null);
    }

    private void recomputeSymbolFrequencies()
    {
        for (int i = 0; i < _frequencies.Length; i++)
            _frequencies[i] = 1;
        for (int length = 0; length < _history.Length; length++)
            addHistoryFrequencies(length);

        _positions[0] = 0;
        for (int i = 1; i < _positions.Length; i++)
            _positions[i] = checked(_positions[i - 1] + _frequencies[i - 1]);
    }

    private void addHistoryFrequencies(int length)
    {
        var node = _root;
        for (int i = _history.Length - length; i < _history.Length; i++)
        {
            if (!node.Children.ContainsKey(_history[i]))
                return;
            node = node.Children[_history[i]];
        }
        foreach (var child in node.Children)
            _frequencies[child.Key] += (uint) (20_000.0 * child.Value.Frequency / node.TotalChildren * (0.17 + 2 * length));
    }

    class FixedByteQueue
    {
        private byte[] _entries;
        private int _next = 0;
        public int Length => _entries.Length;
        public FixedByteQueue(int length) { _entries = new byte[length]; }
        public void Enqueue(byte entry) { _entries[_next] = entry; _next = (_next + 1) % _entries.Length; }
        public byte this[int index] => _entries[(_next + index) % _entries.Length];
    }

    class Node
    {
        public uint Frequency;
        public uint TotalChildren;
        public SortedDictionary<byte, Node> Children = new SortedDictionary<byte, Node>();

        public void SequenceSeen(FixedByteQueue sequence, int offset, Node parent)
        {
            if (offset == sequence.Length)
            {
                Frequency++;
                parent.TotalChildren++;
                return;
            }
            if (!Children.ContainsKey(sequence[offset]))
                Children.Add(sequence[offset], new Node());
            Children[sequence[offset]].SequenceSeen(sequence, offset + 1, this);
        }
    }
}
