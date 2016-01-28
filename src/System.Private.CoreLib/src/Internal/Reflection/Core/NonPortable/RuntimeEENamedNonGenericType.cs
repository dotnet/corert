// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents *non-generic* types that have a typedef token in the ECMA metadata model (that is, "Foo" but not "Foo<>" or "Foo<int>").
    // RuntimeEENamedNonGenericType have guaranteed EETypes (those that don't get wrapped by RuntimeInspectionOnlyNamedType).
    //
    internal sealed class RuntimeEENamedNonGenericType : RuntimeEENamedType
    {
        internal RuntimeEENamedNonGenericType(EETypePtr eeType)
            : base(new RuntimeTypeHandle(eeType))
        {
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return false;  // Anything that has an EEType cannot be open.
            }
        }
    }
}

