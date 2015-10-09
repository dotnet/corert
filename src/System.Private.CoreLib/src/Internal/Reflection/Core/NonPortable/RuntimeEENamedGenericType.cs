// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

