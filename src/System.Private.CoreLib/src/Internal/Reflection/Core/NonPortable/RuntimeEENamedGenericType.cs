// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents *generic* types that have a typedef token in the ECMA metadata model (that is, "Foo<>" but not "Foo<int>")
    // and are backed up by a Redhawk artifact.
    //
    internal sealed class RuntimeEENamedGenericType : RuntimeEENamedType
    {
        internal RuntimeEENamedGenericType(EETypePtr eeType)
            : base(new RuntimeTypeHandle(eeType))
        {
        }

        public sealed override Type GetGenericTypeDefinition()
        {
            return this;
        }

        public sealed override bool InternalIsGenericTypeDefinition
        {
            get
            {
                return true;
            }
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return true;
            }
        }
    }
}

