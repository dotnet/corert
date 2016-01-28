// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Security;
using System.Diagnostics.Contracts;

namespace System.Globalization
{
    internal partial class FormatProvider
    {
        private partial class Number
        {
            // WARNING: Don't allocate these on the heap, the "digits" property will return an unmanaged pointer
            // to an interior character array.
            [System.Runtime.CompilerServices.StackOnly]
            [StructLayout(LayoutKind.Sequential)]
            internal unsafe struct NumberBuffer
            {
                public Int32 precision;
                public Int32 scale;
                public Boolean sign;

                // First character of an inline array of NumberMaxDigits characters.
                private char _char01;
                private char _char02;
                private char _char03;
                private char _char04;
                private char _char05;
                private char _char06;
                private char _char07;
                private char _char08;
                private char _char09;
                private char _char10;
                private char _char11;
                private char _char12;
                private char _char13;
                private char _char14;
                private char _char15;
                private char _char16;
                private char _char17;
                private char _char18;
                private char _char19;
                private char _char20;
                private char _char21;
                private char _char22;
                private char _char23;
                private char _char24;
                private char _char25;
                private char _char26;
                private char _char27;
                private char _char28;
                private char _char29;
                private char _char30;
                private char _char31;
                private char _char32;

                public char* digits
                {
                    get
                    {
                        // This is only safe if the caller allocated the NumberBuffer on the stack or pinned it.

#if CORERT
                        unsafe
                        {
                            fixed (char* p = &_char01)
                                return p;
                        }
#else
                        // using the ManagedPointer instead of fixed allows the compiler to inline this property 
                        // and thus make it more efficient.
                        System.Runtime.CompilerServices.ByReference<char> mp = System.Runtime.CompilerServices.ByReference<char>.FromRef(ref _char01);
                        return (char*)System.Runtime.CompilerServices.ByReference<char>.ToPointer(mp);
#endif
                    }
                }
            }
        }
    }
}

