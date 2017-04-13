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
    internal sealed class ValueTypeFieldAccessorForInstanceFields : InstanceFieldAccessor
    {
        private int _offset;

        public ValueTypeFieldAccessorForInstanceFields(int offset, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle)
            : base(declaringTypeHandle, fieldTypeHandle)
        {
            _offset = offset;
        }

        public sealed override int Offset => _offset - RuntimeAugments.ObjectHeaderSize;

        protected sealed override Object UncheckedGetField(Object obj)
        {
            return RuntimeAugments.LoadValueTypeField(obj, _offset, this.FieldTypeHandle);
        }

        protected sealed override object UncheckedGetFieldDirectFromValueType(TypedReference typedReference)
        {
            return RuntimeAugments.LoadValueTypeFieldValueFromValueType(typedReference, this.Offset, this.FieldTypeHandle);
        }

        protected sealed override void UncheckedSetField(Object obj, Object value)
        {
            RuntimeAugments.StoreValueTypeField(obj, _offset, value, this.FieldTypeHandle);
        }

        protected sealed override void UncheckedSetFieldDirectIntoValueType(TypedReference typedReference, object value)
        {
            RuntimeAugments.StoreValueTypeFieldValueIntoValueType(typedReference, this.Offset, value, this.FieldTypeHandle);
        }
    }
}
