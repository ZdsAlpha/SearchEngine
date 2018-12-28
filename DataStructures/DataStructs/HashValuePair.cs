using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataStructs
{
    public class HashValuePair<ValueType> : IComparable<HashValuePair<ValueType>>
    {
        public readonly uint Hash;
        public ValueType Value;
        public HashValuePair() { }
        public HashValuePair(uint hash, ValueType value)
        {
            Hash = hash;
            Value = value;
        }
        public int CompareTo(HashValuePair<ValueType> other)
        {
            return Hash.CompareTo(other.Hash);
        }
    }
}
