using RT.ArithmeticCoding;

// Same as Beat7zipStandalone but uses  RT.ArithmeticCoding

namespace Beat7zip;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3 || (args[0] != "c" && args[0] != "d"))
            throw new Exception("Usage: test.exe [c|d] <input-file> <output-file>");

        var start = DateTime.UtcNow;
        using (var input = File.Open(args[1], FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var output = File.Open(args[2], FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            var context = new HistoryContext(8);
            int symbol;

            if (args[0] == "c")
            {
                var encoder = new ArithmeticCodingWriter(output, context);
                while ((symbol = input.ReadByte()) >= 0)
                {
                    encoder.WriteSymbol(symbol);
                    context.SymbolSeen((byte) symbol);
                }
                encoder.WriteSymbol(256);
                encoder.Finalize();
            }
            else
            {
                var decoder = new ArithmeticCodingReader(input, context);
                while ((symbol = decoder.ReadSymbol()) != 256)
                {
                    output.WriteByte(checked((byte) symbol));
                    context.SymbolSeen((byte) symbol);
                }
            }
        }
        Console.WriteLine((DateTime.UtcNow - start).TotalSeconds);
    }
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

class HistoryContext : ArithmeticSymbolContext
{
    private FixedByteQueue _history;
    private Node _root = new Node();

    private bool _computed = false;
    private uint[] _frequencies = new uint[257];
    private uint[] _positions = new uint[257];

    public HistoryContext(int historyLengthLimit)
    {
        _history = new FixedByteQueue(historyLengthLimit);
    }

    public void SymbolSeen(byte symbol)
    {
        _computed = false;
        _history.Enqueue(symbol);
        for (int i = 0; i < _history.Length; i++)
            _root.SequenceSeen(_history, i, null);
    }

    public override uint GetSymbolFrequency(int symbol)
    {
        if (symbol < 0 || symbol > 256)
            return 0;
        if (!_computed)
            compute();
        return _frequencies[symbol];
    }

    public override uint GetSymbolPosition(int symbol)
    {
        if (symbol <= 0)
            return 0;
        if (symbol > 256)
            return GetTotal();
        if (!_computed)
            compute();
        return _positions[symbol];
    }

    public override uint GetTotal()
    {
        if (!_computed)
            compute();
        return _positions[256] + _frequencies[256];
    }

    private void compute()
    {
        for (int i = 0; i < _frequencies.Length; i++)
            _frequencies[i] = 1;
        for (int length = 0; length < _history.Length; length++)
            addHistoryFrequencies(length);

        _positions[0] = 0;
        for (int i = 1; i < _positions.Length; i++)
            _positions[i] = checked(_positions[i - 1] + _frequencies[i - 1]);

        _computed = true;
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
}
