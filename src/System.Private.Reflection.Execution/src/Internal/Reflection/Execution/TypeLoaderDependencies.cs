// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Internal.Reflection.Core.Execution;

// This file contains System.Private.TypeLoader dependencies. Since we're not porting that for now,
// we declare the dependencies here.

namespace Internal.TypeSystem
{
    public enum CanonicalFormKind
    {
        Specific,
        Universal,
    }
}

namespace Internal.Runtime.TypeLoader
{
    using Internal.TypeSystem;

    public struct CanonicallyEquivalentEntryLocator
    {
        RuntimeTypeHandle _typeToFind;

        public CanonicallyEquivalentEntryLocator(RuntimeTypeHandle typeToFind, CanonicalFormKind kind)
        {
            _typeToFind = typeToFind;
        }

        public int LookupHashCode
        {
            get
            {
                return _typeToFind.GetHashCode();
            }
        }

        public bool IsCanonicallyEquivalent(RuntimeTypeHandle other)
        {
            return _typeToFind.Equals(other);
        }
    }
}
