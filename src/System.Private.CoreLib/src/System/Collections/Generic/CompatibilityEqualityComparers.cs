// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace System.Collections.Generic
{
    // Comparers that exist for serialization compatibility with .NET Framework

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class ByteEqualityComparer : EqualityComparer<byte>
    {
        public override bool Equals(byte x, byte y)
        {
            return x == y;
        }

        public override int GetHashCode(byte obj)
        {
            return obj.GetHashCode();
        }

        // Equals method for the comparer itself.
        public override bool Equals(object obj) => obj != null && GetType() == obj.GetType();

        public override int GetHashCode() => GetType().GetHashCode();
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class SByteEnumEqualityComparer<T> : EnumEqualityComparer<T> where T : struct
    {
        private SByteEnumEqualityComparer(SerializationInfo information, StreamingContext context) { }
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class ShortEnumEqualityComparer<T> : EnumEqualityComparer<T> where T : struct
    {
        private ShortEnumEqualityComparer(SerializationInfo information, StreamingContext context) { }
    }

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class LongEnumEqualityComparer<T> : EnumEqualityComparer<T> where T : struct
    {
        private LongEnumEqualityComparer(SerializationInfo information, StreamingContext context) { }
    }
}
