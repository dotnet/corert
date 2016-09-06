// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Core.NonPortable
{
    internal static partial class RuntimeTypeUnifier
    {
        //
        // TypeTable mapping raw RuntimeTypeHandles (normalized or otherwise) to Types.
        //
        // Unlike most unifier tables, RuntimeTypeHandleToRuntimeTypeCache exists for fast lookup, not unification. It hashes and compares 
        // on the raw IntPtr value of the RuntimeTypeHandle. Because Redhawk can and does create multiple EETypes for the same 
        // semantically identical type, the same RuntimeType can legitimately appear twice in this table. The factory, however, 
        // does a second lookup in the true unifying tables rather than creating the Type itself.
        // Thus, the one-to-one relationship between Type reference identity and Type semantic identity is preserved.
        //
        private sealed class RuntimeTypeHandleToTypeCache : ConcurrentUnifierW<RawRuntimeTypeHandleKey, Type>
        {
            private RuntimeTypeHandleToTypeCache() { }

            protected sealed override Type Factory(RawRuntimeTypeHandleKey rawRuntimeTypeHandleKey)
            {
                RuntimeTypeHandle runtimeTypeHandle = rawRuntimeTypeHandleKey.RuntimeTypeHandle;

                // Desktop compat: Allows Type.GetTypeFromHandle(default(RuntimeTypeHandle)) to map to null.
                if (runtimeTypeHandle.RawValue == (IntPtr)0)
                    return null;
                EETypePtr eeType = runtimeTypeHandle.ToEETypePtr();
                ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.Callbacks;

                if (eeType.IsDefType)
                {
                    if (eeType.IsGenericTypeDefinition)
                    {
                        return callbacks.GetNamedTypeForHandle(runtimeTypeHandle, isGenericTypeDefinition: true);
                    }
                    else if (eeType.IsGeneric)
                    {
                        return callbacks.GetConstructedGenericTypeForHandle(runtimeTypeHandle);
                    }
                    else
                    {
                        return callbacks.GetNamedTypeForHandle(runtimeTypeHandle, isGenericTypeDefinition: false);
                    }
                }
                else if (eeType.IsArray)
                {
                    if (!eeType.IsSzArray)
                        return callbacks.GetMdArrayTypeForHandle(runtimeTypeHandle, eeType.ArrayRank);
                    else
                        return callbacks.GetArrayTypeForHandle(runtimeTypeHandle);
                }
                else if (eeType.IsPointer)
                {
                    return callbacks.GetPointerTypeForHandle(runtimeTypeHandle);
                }
                else
                {
                    throw new ArgumentException(SR.Arg_InvalidRuntimeTypeHandle);
                }
            }

            public static readonly RuntimeTypeHandleToTypeCache Table = new RuntimeTypeHandleToTypeCache();
        }
    }
}

