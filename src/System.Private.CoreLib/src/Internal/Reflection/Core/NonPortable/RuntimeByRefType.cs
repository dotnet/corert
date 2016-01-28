// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents a byref type. These types never have EETypes.
    // 
    internal sealed class RuntimeByRefType : RuntimeHasElementType
    {
        internal RuntimeByRefType(RuntimeType targetType)
            : base(targetType)
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

        public sealed override bool IsByRef
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
                return this.InternalRuntimeElementType.InternalIsOpen;
            }
        }

        protected sealed override String Suffix
        {
            get
            {
                return "&";
            }
        }
    }
}
