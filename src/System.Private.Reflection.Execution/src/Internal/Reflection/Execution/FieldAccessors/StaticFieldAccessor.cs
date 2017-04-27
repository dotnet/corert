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
    internal abstract class StaticFieldAccessor : FieldAccessor
    {
        protected RuntimeTypeHandle FieldTypeHandle { get; private set; }

        private IntPtr _cctorContext;

        public StaticFieldAccessor(IntPtr cctorContext, RuntimeTypeHandle fieldTypeHandle)
        {
            FieldTypeHandle = fieldTypeHandle;
            _cctorContext = cctorContext;
        }

        public sealed override Object GetField(Object obj)
        {
            if (_cctorContext != IntPtr.Zero)
            {
                RuntimeAugments.EnsureClassConstructorRun(_cctorContext);
            }
            return GetFieldBypassCctor();
        }

        // GetValueDirect() can be used on static fields though this seems like a silly thing to do.
        public sealed override object GetFieldDirect(TypedReference typedReference) => GetField(null);

        public sealed override void SetField(Object obj, Object value, BinderBundle binderBundle)
        {
            if (_cctorContext != IntPtr.Zero)
            {
                RuntimeAugments.EnsureClassConstructorRun(_cctorContext);
            }
            SetFieldBypassCctor(value, binderBundle);
        }

        // SetValueDirect() can be used on static fields though this seems like a silly thing to do.
        // Note that the argument coercion rules are different from SetValue.
        public sealed override void SetFieldDirect(TypedReference typedReference, object value)
        {
            if (_cctorContext != IntPtr.Zero)
            {
                RuntimeAugments.EnsureClassConstructorRun(_cctorContext);
            }
            SetFieldDirectBypassCctor(value);
        }

        public sealed override int Offset
        {
            get
            {
                Debug.Fail("Cannot call Offset on a static field.");
                throw new InvalidOperationException();
            }
        }

        protected abstract Object GetFieldBypassCctor();
        protected abstract void SetFieldBypassCctor(Object value, BinderBundle binderBundle);
        protected abstract void SetFieldDirectBypassCctor(object value);
    }
}
