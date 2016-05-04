// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace Internal.TypeSystem
{
    // Api extensions for fields that allow keeping track of field layout

    public partial class FieldDesc
    {
        private int _offset = FieldAndOffset.InvalidOffset;

        public int Offset
        {
            get
            {
                if (_offset == FieldAndOffset.InvalidOffset)
                {
                    if (IsStatic)
                        OwningType.ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizesAndFields);
                    else
                        OwningType.ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields);

                    if (_offset == FieldAndOffset.InvalidOffset)
                    {
                        // Must be a field that doesn't participate in layout (literal or RVA mapped)
                        throw new BadImageFormatException();
                    }
                }
                return _offset;
            }
        }

        /// <summary>
        /// For static fields, represents whether or not the field is held in the GC or non GC statics region
        /// Does not apply to thread static fields.
        /// </summary>
        public virtual bool HasGCStaticBase
        {
            get
            {
                Debug.Assert(IsStatic);

                TypeDesc fieldType = FieldType;
                if (fieldType.IsValueType)
                    return ((DefType)fieldType).ContainsGCPointers;
                else
                    return fieldType.IsGCPointer;
            }
        }

        internal void InitializeOffset(int offset)
        {
            Debug.Assert(_offset == FieldAndOffset.InvalidOffset || _offset == offset);
            _offset = offset;
        }
    }
}
