// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

using Internal.Runtime.CompilerServices;

namespace System
{
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public partial class String
    {
#if BIT64
        private const int POINTER_SIZE = 8;
#else
        private const int POINTER_SIZE = 4;
#endif
        //                                        m_pEEType    + _stringLength
        internal const int FIRST_CHAR_OFFSET = POINTER_SIZE + sizeof(int);

        // CS0169: The private field '{blah}' is never used
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 169, 649

#if PROJECTN
        [Bound]
#endif
        // WARNING: We allow diagnostic tools to directly inspect these two members (_stringLength, _firstChar)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        [NonSerialized]
        private int _stringLength;
        [NonSerialized]
        private char _firstChar;

#pragma warning restore

        public static readonly String Empty = "";

        // Gets the character at a specified position.
        //
        // Spec#: Apply the precondition here using a contract assembly.  Potential perf issue.
        [System.Runtime.CompilerServices.IndexerName("Chars")]
        public unsafe char this[int index]
        {
#if PROJECTN
            [BoundsChecking]
            get
            {
                return Unsafe.Add(ref _firstChar, index);
            }
#else
            [Intrinsic]
            get
            {
                if ((uint)index >= _stringLength)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return Unsafe.Add(ref _firstChar, index);
            }
#endif
        }

        internal static String FastAllocateString(int length)
        {
            // We allocate one extra char as an interop convenience so that our strings are null-
            // terminated, however, we don't pass the extra +1 to the string allocation because the base
            // size of this object includes the _firstChar field.
            string newStr = RuntimeImports.RhNewString(EETypePtr.EETypePtrOf<string>(), length);
            Debug.Assert(newStr._stringLength == length);
            return newStr;
        }
    }
}
