// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

