// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This is essentially a workaround for the fact that neither IntPtr or RuntimeTypeHandle implements IEquatable<>.
    // Note that for performance, this key implements equality as a IntPtr compare rather than calling RuntimeImports.AreTypesEquivalent().
    // That is, this key is designed for use in caches, not unifiers.
    //
    internal struct RawRuntimeTypeHandleKey : IEquatable<RawRuntimeTypeHandleKey>
    {
        public RawRuntimeTypeHandleKey(RuntimeTypeHandle runtimeTypeHandle)
        {
            _runtimeTypeHandle = runtimeTypeHandle;
        }

        public RuntimeTypeHandle RuntimeTypeHandle
        {
            get
            {
                return _runtimeTypeHandle;
            }
        }

        public override bool Equals(Object obj)
        {
            if (!(obj is RawRuntimeTypeHandleKey))
                return false;
            return Equals((RawRuntimeTypeHandleKey)obj);
        }

        public bool Equals(RawRuntimeTypeHandleKey other)
        {
            //
            // Note: This compares the handle as raw IntPtr's. This is NOT equivalent to testing for semantic type identity. Redhawk can
            // and does create multiple EETypes for the same type identity. This is only considered ok here because 
            // this key is designed for caching Object.GetType() and Type.GetTypeFromHandle() results, not for establishing
            // a canonical Type instance for a given semantic type identity.
            //
            return this.RuntimeTypeHandle.RawValue == other.RuntimeTypeHandle.RawValue;
        }

        public override int GetHashCode()
        {
            //
            // Note: This treats the handle as raw IntPtr's. This is NOT equivalent to testing for semantic type identity. Redhawk can
            // and does create multiple EETypes for the same type identity. This is only considered ok here because 
            // this key is designed for caching Object.GetType() and Type.GetTypeFromHandle() results, not for establishing
            // a canonical Type instance for a given semantic type identity.
            //
            return this.RuntimeTypeHandle.RawValue.GetHashCode();
        }

        private RuntimeTypeHandle _runtimeTypeHandle;
    }
}

