﻿using System;

namespace GeneralShare
{
    public class Random64
    {
        private const long MBIG = long.MaxValue;
        private const int MSEED = 161803398;
        private const int MZ = 0;

        private int inext;
        private int inextp;
        private readonly long[] _seedArray;

        public long Seed { get; }
        
        public Random64() : this(Environment.TickCount)
        {
        }

        public Random64(long seed)
        {
            Seed = seed;

            long sub = (seed == long.MinValue) ? long.MaxValue : Math.Abs(seed);
            long ii;
            long mj = MSEED - sub;
            long mk = 1;

            _seedArray = new long[56];
            _seedArray[55] = mj;
            for (int i = 1; i < 55; i++)
            {
                ii = (21 * i) % 55;
                _seedArray[ii] = mk;
                mk = mj - mk;
                if (mk < 0)
                    mk += MBIG;
                mj = _seedArray[ii];
            }
            for (int k = 1; k < 5; k++)
            {
                for (int i = 1; i < 56; i++)
                {
                    _seedArray[i] -= _seedArray[1 + (i + 30) % 55];
                    if (_seedArray[i] < 0)
                        _seedArray[i] += MBIG;
                }
            }
            inext = 0;
            inextp = 21;
        }

        private long LongSample()
        {
            long retVal;
            int locINext = inext;
            int locINextp = inextp;

            if (++locINext >= 56)
                locINext = 1;

            if (++locINextp >= 56)
                locINextp = 1;

            retVal = _seedArray[locINext] - _seedArray[locINextp];

            if (retVal == MBIG)
                retVal--;

            if (retVal < 0)
                retVal += MBIG;

            _seedArray[locINext] = retVal;

            inext = locINext;
            inextp = locINextp;

            return retVal;
        }

        public double NextDouble()
        {
            long result = LongSample();
            bool negative = (LongSample() % 2 == 0) ? true : false;
            if (negative)
                result = -result;

            double d = result;
            d += (long.MaxValue - 1);
            d /= 2 * (ulong)long.MaxValue - 1;
            return d;
        }

        public float NextSingle()
        {
            return (float)NextDouble(); 
        }

        public long NextInt64()
        {
            return LongSample();
        }

        public long NextInt64(long maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "Value must be positive.");

            return (long)(NextDouble() * maxValue);
        }

        public int NextInt32()
        {
            return (int)(NextDouble() * int.MaxValue);
        }

        public int NextInt32(int maxValue)
        {
            if (maxValue < 0)
                throw new ArgumentOutOfRangeException(nameof(maxValue), "Value must be positive.");

            return (int)(NextDouble() * maxValue);
        }

        public int NextInt32(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minValue), $"{nameof(minValue)} must be less than {nameof(maxValue)}.");
            }

            long range = (long)maxValue - minValue;
            if (range <= int.MaxValue)
            {
                return ((int)(NextDouble() * range) + minValue);
            }
            else
            {
                return (int)((long)(NextDouble() * range) + minValue);
            }
        }

        public void NextBytes(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            NextBytes(buffer, 0, buffer.Length);
        }

        public void NextBytes(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            for (int i = 0; i < count; i++)
            {
                buffer[i + offset] = (byte)(LongSample() % (byte.MaxValue + 1));
            }
        }

        public byte NextByte()
        {
            return (byte)(LongSample() % (byte.MaxValue + 1));
        }
    }
}
