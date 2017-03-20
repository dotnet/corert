// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class StructLayoutAttribute : Attribute
    {
        internal LayoutKind _val;

        public StructLayoutAttribute(LayoutKind layoutKind)
        {
            _val = layoutKind;
        }

        public StructLayoutAttribute(short layoutKind)
            : this((LayoutKind)layoutKind)
        {
        }

        public LayoutKind Value { get { return _val; } }
        public int Pack;
        public int Size;
        public CharSet CharSet;
    }
}
