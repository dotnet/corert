// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.Runtime;
using Internal.Runtime.Augments;

using Debug = System.Diagnostics.Debug;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class ReferenceTypeFieldAccessorForStaticFields : RegularStaticFieldAccessor
    {
        public ReferenceTypeFieldAccessorForStaticFields(IntPtr cctorContext, IntPtr staticsBase, int fieldOffset, FieldTableFlags fieldBase, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, staticsBase, fieldOffset, fieldBase, fieldTypeHandle)
        {
        }

        unsafe protected sealed override Object GetFieldBypassCctor()
        {
            if (FieldBase == FieldTableFlags.GCStatic)
            {
                // The _staticsBase variable points to a GC handle, which points at the GC statics base of the type.
                // We need to perform a double indirection in a GC-safe manner.
                object gcStaticsRegion = RuntimeAugments.LoadReferenceTypeField(*(IntPtr*)StaticsBase);
                return RuntimeAugments.LoadReferenceTypeField(gcStaticsRegion, FieldOffset);
            }
            else if (FieldBase == FieldTableFlags.NonGCStatic)
            {
                return RuntimeAugments.LoadReferenceTypeField(StaticsBase + FieldOffset);
            }

            Debug.Assert(FieldBase == FieldTableFlags.ThreadStatic);
            object threadStaticRegion = RuntimeAugments.GetThreadStaticBase(StaticsBase);
            return RuntimeAugments.LoadReferenceTypeField(threadStaticRegion, FieldOffset);
        }

        unsafe protected sealed override void UncheckedSetFieldBypassCctor(Object value)
        {
            if (FieldBase == FieldTableFlags.GCStatic)
            {
                // The _staticsBase variable points to a GC handle, which points at the GC statics base of the type.
                // We need to perform a double indirection in a GC-safe manner.
                object gcStaticsRegion = RuntimeAugments.LoadReferenceTypeField(*(IntPtr*)StaticsBase);
                RuntimeAugments.StoreReferenceTypeField(gcStaticsRegion, FieldOffset, value);
                return;
            }
            else if (FieldBase == FieldTableFlags.NonGCStatic)
            {
                RuntimeAugments.StoreReferenceTypeField(StaticsBase + FieldOffset, value);
            }
            else
            {
                Debug.Assert(FieldBase == FieldTableFlags.ThreadStatic);
                object threadStaticsRegion = RuntimeAugments.GetThreadStaticBase(StaticsBase);
                RuntimeAugments.StoreReferenceTypeField(threadStaticsRegion, FieldOffset, value);
            }
        }
    }
}
