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

using global::Internal.Metadata.NativeFormat;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class ValueTypeFieldAccessorForThreadStaticFields : WritableStaticFieldAccessor
    {
        private IntPtr _cookie;
        private RuntimeTypeHandle _declaringTypeHandle;

        public ValueTypeFieldAccessorForThreadStaticFields(IntPtr cctorContext, RuntimeTypeHandle declaringTypeHandle, IntPtr cookie, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, fieldTypeHandle)
        {
            _cookie = cookie;
            _declaringTypeHandle = declaringTypeHandle;
        }

        protected sealed override Object GetFieldBypassCctor()
        {
            IntPtr fieldAddress = RuntimeAugments.GetThreadStaticFieldAddress(_declaringTypeHandle, _cookie);
            return RuntimeAugments.LoadValueTypeField(fieldAddress, FieldTypeHandle);
        }

        protected sealed override void UncheckedSetFieldBypassCctor(Object value)
        {
            IntPtr fieldAddress = RuntimeAugments.GetThreadStaticFieldAddress(_declaringTypeHandle, _cookie);
            RuntimeAugments.StoreValueTypeField(fieldAddress, value, FieldTypeHandle);
        }
    }
}
