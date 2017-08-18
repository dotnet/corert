// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal abstract class ThreadStaticFieldAccessor : WritableStaticFieldAccessor
    {
        protected ThreadStaticFieldAccessor(IntPtr cctorContext, RuntimeTypeHandle declaringTypeHandle, int threadStaticsBlockOffset, int fieldOffset, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, fieldTypeHandle)
        {
            ThreadStaticsBlockOffset = threadStaticsBlockOffset;
            FieldOffset = fieldOffset;
            DeclaringTypeHandle = declaringTypeHandle;
        }

        protected int ThreadStaticsBlockOffset { get; }
        protected int FieldOffset { get; }
        protected RuntimeTypeHandle DeclaringTypeHandle { get; }
    }
}
