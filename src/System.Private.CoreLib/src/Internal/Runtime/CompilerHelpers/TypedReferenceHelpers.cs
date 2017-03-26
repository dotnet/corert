// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to implement TypedReference-related instructions.
    /// </summary>
    internal static class TypedReferenceHelpers
    {
        public unsafe static RuntimeTypeHandle TypeHandleToRuntimeTypeMaybeNull(RuntimeTypeHandle typeHandle)
        {
            return typeHandle;
        }

        public unsafe static ref byte GetRefAny(RuntimeTypeHandle type, TypedReference typedRef)
        {
            if (!TypedReference.RawTargetTypeToken(typedRef).Equals(type))
            {
                throw new InvalidCastException();
            }

            return ref typedRef.Value;
        }
    }
}
