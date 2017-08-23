// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution.FieldAccessors
{
    internal sealed class PointerTypeFieldAccessorForInstanceFields : InstanceFieldAccessor
    {
        public PointerTypeFieldAccessorForInstanceFields(int offsetPlusHeader, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle)
            : base(declaringTypeHandle, fieldTypeHandle, offsetPlusHeader)
        {
        }

        public sealed override int Offset => OffsetPlusHeader - RuntimeAugments.ObjectHeaderSize;

        protected sealed override Object UncheckedGetField(Object obj)
        {
            return RuntimeAugments.LoadPointerTypeField(obj, OffsetPlusHeader, this.FieldTypeHandle);
        }

        protected sealed override object UncheckedGetFieldDirectFromValueType(TypedReference typedReference)
        {
            return RuntimeAugments.LoadPointerTypeFieldValueFromValueType(typedReference, this.Offset, this.FieldTypeHandle);
        }

        protected sealed override void UncheckedSetField(Object obj, Object value)
        {
            RuntimeAugments.StoreValueTypeField(obj, OffsetPlusHeader, value, typeof(IntPtr).TypeHandle);
        }

        protected sealed override void UncheckedSetFieldDirectIntoValueType(TypedReference typedReference, object value)
        {
            RuntimeAugments.StoreValueTypeFieldValueIntoValueType(typedReference, this.Offset, value, typeof(IntPtr).TypeHandle);
        }
    }
}
