// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal abstract class WritableStaticFieldAccessor : StaticFieldAccessor
    {
        protected WritableStaticFieldAccessor(IntPtr cctorContext, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, fieldTypeHandle)
        {
        }

        protected abstract override object GetFieldBypassCctor();

        protected sealed override void SetFieldBypassCctor(object value, BinderBundle binderBundle)
        {
            value = RuntimeAugments.CheckArgument(value, FieldTypeHandle, binderBundle);
            UncheckedSetFieldBypassCctor(value);
        }

        protected sealed override void SetFieldDirectBypassCctor(object value)
        {
            value = RuntimeAugments.CheckArgumentForDirectFieldAccess(value, FieldTypeHandle);
            UncheckedSetFieldBypassCctor(value);
        }

        protected abstract void UncheckedSetFieldBypassCctor(object value);
    }
}
