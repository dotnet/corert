// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal abstract class RegularStaticFieldAccessor : WritableStaticFieldAccessor
    {
        protected RegularStaticFieldAccessor(IntPtr cctorContext, IntPtr staticsBase, int fieldOffset, bool isGcStatic, RuntimeTypeHandle fieldTypeHandle)
            : base(cctorContext, fieldTypeHandle)
        {
            StaticsBase = staticsBase;
            IsGcStatic = isGcStatic;
            FieldOffset = fieldOffset;
        }

        protected IntPtr StaticsBase { get; }
        protected bool IsGcStatic { get; }
        protected int FieldOffset { get; }
    }
}
