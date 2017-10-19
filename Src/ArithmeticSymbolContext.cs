using System;

namespace RT.ArithmeticCoding
{
    /// <summary>Provides information about the relative frequency of arithmetic coding symbols.</summary>
    public abstract class ArithmeticSymbolContext
    {
        /// <summary>
        ///     Returns the sum of all symbol frequencies. When overridden, must equal exactly the sum of <see
        ///     cref="GetSymbolFreq"/> over all possible symbols.</summary>
        public abstract ulong GetTotal();
        /// <summary>
        ///     Returns the frequency of the specified symbol. When overridden, must return a value for every possible input;
        ///     return 0 if the symbol is out of the range of possible symbols.</summary>
        public abstract ulong GetSymbolFreq(int symbol);
        /// <summary>
        ///     Returns the sum of the frequencies of all symbols less than <paramref name="symbol"/>. When overridden, must
        ///     return a value for every possible input: 0 for all symbols less than the first valid symbol, and a value equal
        ///     to <see cref="GetTotal"/> for all symbols greater than the last valid symbol.</summary>
        public abstract ulong GetSymbolPos(int symbol);
    }

    /// <summary>
    ///     Implements a symbol context best suited for relatively small ranges of valid symbols. Supports efficient updates
    ///     of individual symbol frequencies. The smallest symbol supported is 0, and the memory usage goes up linearly with
    ///     the largest valid symbol.</summary>
    public class ArithmeticSymbolArrayContext : ArithmeticSymbolContext
    {
        private ulong[] _frequencies;
        private ulong[] _positions;
        private ulong _total;
        private int _positionsValidUntil;

        /// <summary>
        ///     Initializes a new instance of <see cref="ArithmeticSymbolArrayContext"/>.</summary>
        /// <param name="length">
        ///     How many symbols to keep track of. Valid symbols are in the range of 0 .. <paramref name="length"/>. All
        ///     symbols outside of this range have a frequency of 0.</param>
        /// <param name="initializer">
        ///     A function which returns the initial value for each symbol's frequency. Optional; if omitted, every symbol
        ///     starts with a frequency of 1.</param>
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

        /// <summary>
        ///     Initializes a new instance of <see cref="ArithmeticSymbolArrayContext"/>.</summary>
        /// <param name="frequencies">
        ///     Initial frequencies of all symbols. The length of this array determines the maximum representable symbol; all
        ///     other symbols have a frequency of 0.</param>
        public ArithmeticSymbolArrayContext(ulong[] frequencies)
        {
            _frequencies = frequencies;
            _positions = new ulong[frequencies.Length];
            _positionsValidUntil = -1;
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
        }
        /// <summary>Returns the sum of the frequencies of all symbols less than <paramref name="symbol"/>.</summary>
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

        /// <summary>Returns the frequency of the specified symbol.</summary>
        public override ulong GetSymbolFreq(int symbol)
        {
            if (symbol < 0 || symbol >= _frequencies.Length)
                return 0;
            return _frequencies[symbol];
        }

        /// <summary>Returns the sum of all symbol frequencies.</summary>
        public override ulong GetTotal()
        {
            return _total;
        }

        /// <summary>
        ///     Updates the frequencies of all symbols. Use this method to update a large number of frequencies. Use <see
        ///     cref="SetSymbolFrequency(int, ulong)"/> to update a small number of frequencies more efficiently.</summary>
        /// <param name="updater">
        ///     A method which receives the current array of symbol frequencies. This method can modify the array arbitrarily,
        ///     but cannot replace it with an entirely new array. To change the length of the frequencies array, see <see
        ///     cref="UpdateFrequencies(Func{ulong[], ulong[]})"/>.</param>
        public void UpdateFrequencies(Action<ulong[]> updater)
        {
            updater(_frequencies);
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
            _positionsValidUntil = -1;
        }

        /// <summary>
        ///     Updates the frequencies of all symbols. Use this method to update a large number of frequencies. Use <see
        ///     cref="SetSymbolFrequency(int, ulong)"/> to update a small number of frequencies more efficiently.</summary>
        /// <param name="updater">
        ///     A function which receives the current array of symbol frequencies. This function can modify the array
        ///     arbitrarily and return it, or construct and return an entirely different array.</param>
        public void UpdateFrequencies(Func<ulong[], ulong[]> updater)
        {
            _frequencies = updater(_frequencies);
            _total = 0;
            for (int i = 0; i < _frequencies.Length; i++)
                _total += _frequencies[i];
            _positionsValidUntil = -1;
        }

        /// <summary>
        ///     Updates the frequency of the specified symbol. To update a large number of frequencies in one go more
        ///     efficiently, use <see cref="UpdateFrequencies(Action{ulong[]})"/>.</summary>
        public void SetSymbolFrequency(int symbol, ulong newFrequency)
        {
            if (symbol < 0 || symbol >= _frequencies.Length)
                throw new ArgumentOutOfRangeException(nameof(symbol), "Symbol is out of range.");
            var oldProbability = _frequencies[symbol];
            _frequencies[symbol] = newFrequency;
            _total = _total - oldProbability + newFrequency;
            _positionsValidUntil = symbol;
        }

        /// <summary>
        ///     Updates the frequency of the specified symbol by adding <paramref name="incrementBy"/> to it.</summary>
        /// <param name="symbol">
        ///     The symbol whose frequency is to be updated.</param>
        /// <param name="incrementBy">
        ///     The value to be added to the current frequency. This can be negative. If the symbol frequency becomes
        ///     negative, an <see cref="ArgumentException"/> is thrown.</param>
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
