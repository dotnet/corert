// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace System.Collections.Generic
{
    internal partial class ArraySortHelper<T>
    {
        // WARNING: We allow diagnostic tools to directly inspect this member (s_defaultArraySortHelper). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private static readonly ArraySortHelper<T> s_defaultArraySortHelper = new ArraySortHelper<T>();

        public static ArraySortHelper<T> Default
        {
            get
            {
                return s_defaultArraySortHelper;
            }
        }
    }

    internal partial class ArraySortHelper<TKey, TValue>
    {
        // WARNING: We allow diagnostic tools to directly inspect this member (s_defaultArraySortHelper). 
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        private static readonly ArraySortHelper<TKey, TValue> s_defaultArraySortHelper = new ArraySortHelper<TKey, TValue>();

        public static ArraySortHelper<TKey, TValue> Default
        {
            get
            {
                return s_defaultArraySortHelper;
            }
        }
    }
}