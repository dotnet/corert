// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Internal.Runtime.Augments
{
#pragma warning disable 3003

    //
    // Abstract base for reflection-based information regarding an Enum type.
    //
    public abstract class EnumInfo
    {
        public abstract Type UnderlyingType { get; }
        
        /// <summary>
        /// Returns an array whose element type is the underlying enum type. Sorted the same way NamesAndValues is sorted.
        /// </summary>
        public abstract Array Values { get; }

        /// <summary>
        /// Sorted by performing a value-preserving cast of each value to long, (except in the case of a ulong in which case,
        /// a 2's complement conversion is done), then doing a 2's complement cast to ulong and sorting as ulong.
        /// </summary>
        public abstract KeyValuePair<String, ulong>[] NamesAndValues { get; }

        public abstract bool HasFlagsAttribute { get; }
    }
}

