// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Reflection.Core.NonPortable;

namespace Internal.Runtime.Augments
{
#pragma warning disable 3003

    //
    // Abstract base for reflection-based information regarding an Enum type.
    //
    public abstract class EnumInfo
    {
        public abstract Type UnderlyingType { get; }
        public abstract Array Values { get; }
        public abstract KeyValuePair<String, ulong>[] NamesAndValues { get; }
        public abstract bool HasFlagsAttribute { get; }
    }
}

