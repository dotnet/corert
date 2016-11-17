// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Internal.Text
{
    public struct Utf8String : IEquatable<Utf8String>
    {
        private byte[] _value;

        public Utf8String(byte[] underlyingArray)
        {
            _value = underlyingArray;
        }

        public Utf8String(string s)
        {
            _value = Encoding.UTF8.GetBytes(s);
        }

        // TODO: This should return ReadOnlySpan<byte> instead once available
        public byte[] UnderlyingArray => _value;
        public int Length => _value.Length;

        // For now, define implicit conversions between string and Utf8String to aid the transition
        // These conversions will be removed eventually
        public static implicit operator Utf8String(string s)
        {
            return new Utf8String(s);
        }

        public override string ToString()
        {
            return Encoding.UTF8.GetString(_value);
        }

        public override bool Equals(object obj)
        {
            return (obj is Utf8String) && Equals((Utf8String)obj);
        }

        public override int GetHashCode()
        {
            return ((IStructuralEquatable)_value).GetHashCode(EqualityComparer<byte>.Default);
        }

        public bool Equals(Utf8String other)
        {
            return ((IStructuralEquatable)_value).Equals(other._value, EqualityComparer<byte>.Default);
        }
    }
}
