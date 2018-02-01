// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class ReferenceTypeFieldAccessorForStaticFields : RegularStaticFieldAccessor
    {
        public ReferenceTypeFieldAccessorForStaticFields(IntPtr cctorContext, IntPtr staticsBase, int fieldOffset, bool isGcStatic, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, staticsBase, fieldOffset, isGcStatic, fieldTypeHandle)
        {
        }

        unsafe protected sealed override Object GetFieldBypassCctor()
        {
#if !PROJECTN
            if (IsGcStatic)
            {
                // The _staticsBase variable points to a GC handle, which points at the GC statics base of the type.
                // We need to perform a double indirection in a GC-safe manner.
                object gcStaticsRegion = RuntimeAugments.LoadReferenceTypeField(*(IntPtr*)StaticsBase);
                return RuntimeAugments.LoadReferenceTypeField(gcStaticsRegion, FieldOffset);
            }
#endif
            return RuntimeAugments.LoadReferenceTypeField(StaticsBase + FieldOffset);
        }

        unsafe protected sealed override void UncheckedSetFieldBypassCctor(Object value)
        {
#if !PROJECTN
            if (IsGcStatic)
            {
                // The _staticsBase variable points to a GC handle, which points at the GC statics base of the type.
                // We need to perform a double indirection in a GC-safe manner.
                object gcStaticsRegion = RuntimeAugments.LoadReferenceTypeField(*(IntPtr*)StaticsBase);
                RuntimeAugments.StoreReferenceTypeField(gcStaticsRegion, FieldOffset, value);
                return;
            }
#endif
            RuntimeAugments.StoreReferenceTypeField(StaticsBase + FieldOffset, value);
        }
    }
}
