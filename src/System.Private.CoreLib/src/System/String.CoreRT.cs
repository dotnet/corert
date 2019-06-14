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
    // STRING LAYOUT
    // -------------
    // Strings are null-terminated for easy interop with native, but the value returned by String.Length 
    // does NOT include this null character in its count.  As a result, there's some trickiness here in the 
    // layout and allocation of strings that needs explanation...
    //
    // String is allocated like any other array, using the RhNewArray API.  It is essentially a very special 
    // char[] object.  In order to be an array, the String EEType must have an 'array element size' of 2, 
    // which is setup by a special case in the binder.  Strings must also have a typical array instance 
    // layout, which means that the first field after the m_pEEType field is the 'number of array elements' 
    // field.  However, here, it is called _stringLength because it contains the number of characters in the
    // string (NOT including the terminating null element) and, thus, directly represents both the array 
    // length and String.Length.
    //
    // As with all arrays, the GC calculates the size of an object using the following formula:  
    //
    //      obj_size = align(base_size + (num_elements * element_size), sizeof(void*))
    //
    // The values 'base_size' and 'element_size' are both stored in the EEType for String and 'num_elements'
    // is _stringLength.
    //
    // Our base_size is the size of the fixed portion of the string defined below.  It, therefore, contains 
    // the size of the _firstChar field in it.  This means that, since our string data actually starts 
    // inside the fixed 'base_size' area, and our num_elements is equal to String.Length, we end up with one 
    // extra character at the end.  This is how we get our extra null terminator which allows us to pass a 
    // pinned string out to native code as a null-terminated string.  This is also why we don't increment the
    // requested string length by one before passing it to RhNewArray.  There is no need to allocate an extra
    // array element, it is already allocated here in the fixed layout of the String.
    //
    // Typically, the base_size of an array type is aligned up to the nearest pointer size multiple (so that
    // array elements start out aligned in case they need alignment themselves), but we don't want to do that 
    // with String because we are allocating String.Length components with RhNewArray and the overall object 
    // size will then need another alignment, resulting in wasted space.  So the binder specially shrinks the
    // base_size of String, leaving it unaligned in order to allow the use of that otherwise wasted space.  
    //
    // One more note on base_size -- on 64-bit, the base_size ends up being 22 bytes, which is less than the
    // min_obj_size of (3 * sizeof(void*)).  This is OK because our array allocator will still align up the
    // overall object size, so a 0-length string will end up with an object size of 24 bytes, which meets the
    // min_obj_size requirement.
    //
    // NOTE: This class is marked EagerStaticClassConstruction because McgCurrentModule class being eagerly
    // constructed itself depends on this class also being eagerly constructed. Plus, it's nice to have this
    // eagerly constructed to avoid the cost of defered ctors. I can't imagine any app that doesn't use string
    //
    [StructLayout(LayoutKind.Sequential)]
    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
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

        [Intrinsic]
        public static readonly string Empty = "";

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

        public int Length
        {
            get { return _stringLength; }
        }

        internal static string FastAllocateString(int length)
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
