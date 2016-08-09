// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace System
{
    // CONTRACT with Runtime
    // The Array type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type int

    public class Array
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // This field should be the first field in Array as the runtime/compilers depend on it
        private int _numComponents;
#if CORERT && BIT64
        //  The field '{blah}' is never used
#pragma warning disable 0169
        private int _padding;
#endif
#pragma warning restore

        public int Length
        {
            get
            {
                // NOTE: The compiler has assumptions about the implementation of this method.
                // Changing the implementation here (or even deleting this) will NOT have the desired impact
                return _numComponents;
            }
        }

#if CORERT
        private class RawSzArrayData : Array
        {
// Suppress bogus warning - remove once https://github.com/dotnet/roslyn/issues/10544 is fixed
#pragma warning disable 649
            public byte Data;
#pragma warning restore
        }

        internal ref byte GetRawSzArrayData()
        {
            return ref Unsafe.As<RawSzArrayData>(this).Data;
        }
#endif
    }

    // To accomodate class libraries that wish to implement generic interfaces on arrays, all class libraries
    // are now required to provide an Array<T> class that derives from Array.
    internal class Array<T> : Array
    {
    }
}
