﻿using System;
using System.Runtime.InteropServices;

namespace SharpLearning.Containers.Views
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Interval1D : IEquatable<Interval1D>
    {
        public readonly int FromInclusive;
        public readonly int ToExclusive;
        public readonly int Length;

        /// <summary>
        /// Creates a 1D interval as specified from inclusive to exclusive
        /// </summary>
        /// <param name="fromInclusive"></param>
        /// <param name="toExclusive"></param>
        public Interval1D(int fromInclusive, int toExclusive)
        {
            if (fromInclusive >= toExclusive) { throw new ArgumentException(); }
            FromInclusive = fromInclusive;
            ToExclusive = toExclusive;
            Length = toExclusive - fromInclusive;
        }

        /// <summary>
        /// Creates a 1D interval as specified from inclusive to exclusive
        /// </summary>
        /// <param name="fromInclusive"></param>
        /// <param name="toExclusive"></param>
        public static Interval1D Create(int fromInclusive, int toExclusive)
        {
            return new Interval1D(fromInclusive, toExclusive);
        }

        public bool Equals(Interval1D other)
        {
            return (this.FromInclusive == other.FromInclusive) && 
                   (this.ToExclusive == other.ToExclusive) &&
                   (this.Length == other.Length);
        }

        public override bool Equals(object other)
        {
            if (other is Interval1D)
                return this.Equals((Interval1D)other);
            else
                return false;
        }

        public override int GetHashCode()
        {
            return this.FromInclusive.GetHashCode() ^ this.ToExclusive.GetHashCode() ^ this.Length.GetHashCode();
        }
    }
}
