// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents an array (zero lower bound) that has no EEType.
    //
    internal sealed class RuntimeInspectionOnlyArrayType : RuntimeArrayType
    {
        internal RuntimeInspectionOnlyArrayType(RuntimeType elementType)
            : base(elementType, false, 1)
        {
        }

        internal RuntimeInspectionOnlyArrayType(RuntimeType elementType, int rank)
            : base(elementType, true, rank)
        {
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            return this.InternalRuntimeElementType.GetHashCode();
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return this.InternalRuntimeElementType.InternalIsOpen;
            }
        }
    }
}


