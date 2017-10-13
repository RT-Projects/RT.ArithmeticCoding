using System;

namespace RT.ArithmeticCoding
{
    public abstract class ArithmeticSymbolContext
    {
        public abstract ulong GetTotal();
        public abstract ulong GetSymbolFreq(int symbol);
        public abstract ulong GetSymbolPos(int symbol);
    }

    public class ArithmeticSymbolArrayContext : ArithmeticSymbolContext
    {
        private ulong[] _frequencies;
        private ulong[] _positions;
        private ulong _total;
        private int _positionsValidUntil;

        public ArithmeticSymbolArrayContext(int length, Func<int, ulong> initializer = null)
        {
            initializer = initializer ?? (_ => 1UL);
            _frequencies = new ulong[length];
            _positions = new ulong[length];
            _positionsValidUntil = -1;
            _total = 0;
            for (int i = 0; i < length; i++)
            {
                _frequencies[i] = initializer(i);
                _total += _frequencies[i];
            }
        }

        public ArithmeticSymbolArrayContext(ulong[] frequencies)
        {
            _frequencies = frequencies;
            _positions = new ulong[frequencies.Length];
            _positionsValidUntil = -1;
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
        }

        public override ulong GetSymbolPos(int symbol)
        {
            if (symbol < 0)
                return 0;
            if (symbol >= _frequencies.Length)
                return _total;

            if (_positionsValidUntil >= symbol)
                return _positions[symbol];

            ulong pos = _positionsValidUntil < 0 ? 0 : (_positions[_positionsValidUntil] + _frequencies[_positionsValidUntil]);
            for (int i = _positionsValidUntil + 1; i <= symbol; i++)
            {
                _positions[i] = pos;
                pos += _frequencies[i];
            }
            _positionsValidUntil = symbol;
            return _positions[symbol];
        }

        public override ulong GetSymbolFreq(int symbol)
        {
            if (symbol < 0 || symbol >= _frequencies.Length)
                return 0;
            return _frequencies[symbol];
        }

        public override ulong GetTotal()
        {
            return _total;
        }

        public void UpdateFrequencies(Action<ulong[]> updater)
        {
            updater(_frequencies);
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
            _positionsValidUntil = -1;
        }

        public void UpdateFrequencies(Func<ulong[], ulong[]> updater)
        {
            _frequencies = updater(_frequencies);
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
            _positionsValidUntil = -1;
        }

        public void SetSymbolFrequency(int symbol, ulong newFrequency)
        {
            if (symbol < 0 || symbol >= _frequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol is out of range.");
            var oldProbability = _frequencies[symbol];
            _frequencies[symbol] = newFrequency;
            _total = _total - oldProbability + newFrequency;
            _positionsValidUntil = symbol;
        }

        public void IncrementSymbolFrequency(int symbol, int incrementBy = 1)
        {
            if (symbol < 0 || symbol >= _frequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol is out of range.");
            if (incrementBy == 0)
                return;
            if (incrementBy < 0 && _frequencies[symbol] < (ulong) -incrementBy)
                throw new ArgumentException($"Symbol {symbol} has a probability of {_frequencies[symbol]}; decrementing it by {-incrementBy} would make it less than 0.");
            SetSymbolFrequency(symbol, incrementBy > 0 ? (_frequencies[symbol] + (uint) incrementBy) : (_frequencies[symbol] - (uint) (-incrementBy)));
        }
    }
}
