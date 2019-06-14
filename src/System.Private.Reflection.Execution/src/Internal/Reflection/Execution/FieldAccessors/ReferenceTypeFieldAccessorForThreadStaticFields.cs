// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class ReferenceTypeFieldAccessorForThreadStaticFields : ThreadStaticFieldAccessor
    {
        public ReferenceTypeFieldAccessorForThreadStaticFields(IntPtr cctorContext, RuntimeTypeHandle declaringTypeHandle, int threadStaticsBlockOffset, int fieldOffset, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, declaringTypeHandle, threadStaticsBlockOffset, fieldOffset, fieldTypeHandle)
        {
        }

        protected sealed override Object GetFieldBypassCctor()
        {
            IntPtr fieldAddress = RuntimeAugments.GetThreadStaticFieldAddress(DeclaringTypeHandle, ThreadStaticsBlockOffset, FieldOffset);
            return RuntimeAugments.LoadReferenceTypeField(fieldAddress);
        }

        protected sealed override void UncheckedSetFieldBypassCctor(Object value)
        {
            IntPtr fieldAddress = RuntimeAugments.GetThreadStaticFieldAddress(DeclaringTypeHandle, ThreadStaticsBlockOffset, FieldOffset);
            RuntimeAugments.StoreReferenceTypeField(fieldAddress, value);
        }
    }
}
