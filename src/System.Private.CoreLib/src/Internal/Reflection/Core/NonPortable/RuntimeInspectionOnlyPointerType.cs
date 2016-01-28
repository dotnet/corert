// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents a pointer that has no EEType.
    //
    internal sealed class RuntimeInspectionOnlyPointerType : RuntimePointerType
    {
        internal RuntimeInspectionOnlyPointerType(RuntimeType runtimeElementType)
            : base(runtimeElementType)
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

