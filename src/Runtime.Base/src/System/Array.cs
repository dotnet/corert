// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System
{
    // CONTRACT with Runtime
    // The Array type is one of the primitives understood by the compilers and runtime
    // Data Contract: Single field of type int

    public partial class Array
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // This field should be the first field in Array as the runtime/compilers depend on it
        private int _numComponents;
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
    }

    // To accommodate class libraries that wish to implement generic interfaces on arrays, all class libraries
    // are now required to provide an Array<T> class that derives from Array.
    internal class Array<T> : Array
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    internal class RawArrayData
    {
        public uint Length; // Array._numComponents padded to IntPtr
#if BIT64
        public uint Padding;
#endif
        public byte Data;
    }
}
