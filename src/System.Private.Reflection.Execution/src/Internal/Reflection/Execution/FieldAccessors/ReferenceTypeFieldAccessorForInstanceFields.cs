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
    internal sealed class ReferenceTypeFieldAccessorForInstanceFields : InstanceFieldAccessor
    {
        private int _offset;

        public ReferenceTypeFieldAccessorForInstanceFields(int offset, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle)
            : base(declaringTypeHandle, fieldTypeHandle)
        {
            _offset = offset;
        }

        public sealed override int Offset => _offset - RuntimeAugments.ObjectHeaderSize;

        protected sealed override Object UncheckedGetField(Object obj)
        {
            return RuntimeAugments.LoadReferenceTypeField(obj, _offset);
        }

        protected sealed override object UncheckedGetFieldDirectFromValueType(TypedReference typedReference)
        {
            return RuntimeAugments.LoadReferenceTypeFieldValueFromValueType(typedReference, this.Offset);
        }

        protected sealed override void UncheckedSetField(Object obj, Object value)
        {
            RuntimeAugments.StoreReferenceTypeField(obj, _offset, value);
        }

        protected sealed override void UncheckedSetFieldDirectIntoValueType(TypedReference typedReference, object value)
        {
            RuntimeAugments.StoreReferenceTypeFieldValueIntoValueType(typedReference, this.Offset, value);
        }
    }
}
