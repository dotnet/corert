// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    internal abstract class StaticFieldAccessor : FieldAccessor
    {
        protected RuntimeTypeHandle FieldTypeHandle { get; private set; }

        IntPtr _cctorContext;

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
            return GetFieldBypassCctor(obj);
        }

        public sealed override void SetField(Object obj, Object value)
        {
            if (_cctorContext != IntPtr.Zero)
            {
                RuntimeAugments.EnsureClassConstructorRun(_cctorContext);
            }
            SetFieldBypassCctor(obj, value);
        }

        protected abstract Object GetFieldBypassCctor(Object obj);
        protected abstract void SetFieldBypassCctor(Object obj, Object value);
    }
}
