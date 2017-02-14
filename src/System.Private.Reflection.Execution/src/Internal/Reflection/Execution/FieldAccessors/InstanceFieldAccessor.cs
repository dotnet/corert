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

using TargetException = System.ArgumentException;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal abstract class InstanceFieldAccessor : FieldAccessor
    {
        public InstanceFieldAccessor(RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle)
        {
            this.DeclaringTypeHandle = declaringTypeHandle;
            this.FieldTypeHandle = fieldTypeHandle;
        }

        public sealed override Object GetField(Object obj)
        {
            if (obj == null)
                throw new TargetException(SR.RFLCT_Targ_StatFldReqTarg);
            if (!RuntimeAugments.IsAssignable(obj, this.DeclaringTypeHandle))
                throw new ArgumentException();
            return UncheckedGetField(obj);
        }

        public sealed override void SetField(Object obj, Object value, BinderBundle binderBundle)
        {
            if (obj == null)
                throw new TargetException(SR.RFLCT_Targ_StatFldReqTarg);
            if (!RuntimeAugments.IsAssignable(obj, this.DeclaringTypeHandle))
                throw new ArgumentException();
            value = RuntimeAugments.CheckArgument(value, this.FieldTypeHandle, binderBundle);
            UncheckedSetField(obj, value);
        }

        protected abstract Object UncheckedGetField(Object obj);
        protected abstract void UncheckedSetField(Object obj, Object value);

        protected RuntimeTypeHandle DeclaringTypeHandle { get; private set; }
        protected RuntimeTypeHandle FieldTypeHandle { get; private set; }
    }
}
