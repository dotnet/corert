// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Threading;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;

using global::Internal.Runtime.Augments;
using global::Internal.Reflection.Execution;
using global::Internal.Reflection.Core.Execution;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class ReferenceTypeFieldAccessorForUniversalThreadStaticFields : WritableStaticFieldAccessor
    {
        private int _fieldOffset;
        private RuntimeTypeHandle _declaringTypeHandle;

        public ReferenceTypeFieldAccessorForUniversalThreadStaticFields(IntPtr cctorContext, RuntimeTypeHandle declaringTypeHandle, int fieldOffset, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, fieldTypeHandle)
        {
            _fieldOffset = fieldOffset;
            _declaringTypeHandle = declaringTypeHandle;
        }

        protected sealed override Object GetFieldBypassCctor()
        {
            IntPtr tlsFieldsStartAddress = RuntimeAugments.GetThreadStaticFieldAddress(_declaringTypeHandle, IntPtr.Zero);
            IntPtr fieldAddress = tlsFieldsStartAddress + _fieldOffset;
            return RuntimeAugments.LoadReferenceTypeField(fieldAddress);
        }

        protected sealed override void UncheckedSetFieldBypassCctor(Object value)
        {
            IntPtr tlsFieldsStartAddress = RuntimeAugments.GetThreadStaticFieldAddress(_declaringTypeHandle, IntPtr.Zero);
            IntPtr fieldAddress = tlsFieldsStartAddress + _fieldOffset;
            RuntimeAugments.StoreReferenceTypeField(fieldAddress, value);
        }
    }
}
