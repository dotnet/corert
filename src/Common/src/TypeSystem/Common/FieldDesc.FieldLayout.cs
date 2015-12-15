// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
                        OwningType.ComputeStaticFieldLayout();
                    else
                        OwningType.ComputeInstanceFieldLayout();

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
        public bool HasGCStaticBase
        {
            get
            {
                if (!FieldType.IsValueType)
                    return true;

                DefType fieldType = FieldType as DefType;
                return fieldType != null && fieldType.ContainsPointers;
            }
        }

        internal void InitializeOffset(int offset)
        {
            Debug.Assert(_offset == FieldAndOffset.InvalidOffset || _offset == offset);
            _offset = offset;
        }
    }
}
