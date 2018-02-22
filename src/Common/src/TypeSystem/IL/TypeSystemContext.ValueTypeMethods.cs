// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.IL.Stubs;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    public abstract partial class TypeSystemContext
    {
        private class ValueTypeMethodHashtable : LockFreeReaderHashtable<DefType, MethodDesc>
        {
            protected override int GetKeyHashCode(DefType key) => key.GetHashCode();
            protected override int GetValueHashCode(MethodDesc value) => value.OwningType.GetHashCode();
            protected override bool CompareKeyToValue(DefType key, MethodDesc value) => key == value.OwningType;
            protected override bool CompareValueToValue(MethodDesc v1, MethodDesc v2) => v1.OwningType == v2.OwningType;

            protected override MethodDesc CreateValueFromKey(DefType key)
            {
                return new ValueTypeGetFieldHelperMethodOverride(key);
            }
        }

        private ValueTypeMethodHashtable _valueTypeMethodHashtable = new ValueTypeMethodHashtable();

        protected virtual IEnumerable<MethodDesc> GetAllMethodsForValueType(TypeDesc valueType)
        {
            TypeDesc valueTypeDefinition = valueType.GetTypeDefinition();
            MethodDesc getFieldHelperMethod = _valueTypeMethodHashtable.GetOrCreateValue((DefType)valueTypeDefinition);

            if (valueType != valueTypeDefinition)
            {
                yield return GetMethodForInstantiatedType(getFieldHelperMethod, (InstantiatedType)valueType);
            }
            else
            {
                yield return getFieldHelperMethod;
            }

            foreach (MethodDesc method in valueType.GetMethods())
                yield return method;
        }
    }
}
