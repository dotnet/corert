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
    internal sealed class ValueTypeFieldAccessorForInstanceFields : InstanceFieldAccessor
    {
        int _offset;

        public ValueTypeFieldAccessorForInstanceFields(int offset, RuntimeTypeHandle declaringTypeHandle, RuntimeTypeHandle fieldTypeHandle)
            : base(declaringTypeHandle, fieldTypeHandle)
        {
            _offset = offset;
        }

        protected sealed override Object UncheckedGetField(Object obj)
        {
            return RuntimeAugments.LoadValueTypeField(obj, _offset, this.FieldTypeHandle);
        }

        protected sealed override void UncheckedSetField(Object obj, Object value)
        {
            RuntimeAugments.StoreValueTypeField(obj, _offset, value, this.FieldTypeHandle);
        }
    }
}
