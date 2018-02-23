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
        private MethodDesc _objectGetHashCodeMethod;
        private MethodDesc _objectEqualsMethod;

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

            if (RequiresGetFieldHelperMethod((MetadataType)valueTypeDefinition))
            {
                MethodDesc getFieldHelperMethod = _valueTypeMethodHashtable.GetOrCreateValue((DefType)valueTypeDefinition);

                // Check that System.ValueType has the method we're overriding.
                Debug.Assert(valueTypeDefinition.BaseType.GetMethod(getFieldHelperMethod.Name, null) != null);

                if (valueType != valueTypeDefinition)
                {
                    yield return GetMethodForInstantiatedType(getFieldHelperMethod, (InstantiatedType)valueType);
                }
                else
                {
                    yield return getFieldHelperMethod;
                }
            }

            foreach (MethodDesc method in valueType.GetMethods())
                yield return method;
        }

        private bool RequiresGetFieldHelperMethod(MetadataType valueType)
        {
            if (_objectGetHashCodeMethod == null)
                _objectGetHashCodeMethod = GetWellKnownType(WellKnownType.Object).GetMethod("GetHashCode", null);

            if (_objectEqualsMethod == null)
                _objectEqualsMethod = GetWellKnownType(WellKnownType.Object).GetMethod("Equals", null);

            // If the classlib doesn't have Object.Equals/Object.GetHashCode, we don't need this.
            if (_objectEqualsMethod == null && _objectGetHashCodeMethod == null)
                return false;

            // Byref-like valuetypes cannot be boxed.
            if (valueType.IsByRefLike)
                return false;

            // Enums get their overrides from System.Enum.
            if (valueType.IsEnum)
                return false;

            // Optimization: if Equals/GetHashCode are overriden, we don't need the helper
            // TODO: not stricly correct because user code can do
            // public override int GetHashCode() => base.GetHashCode
            // and cause us to not be able to provide the implementation anymore
            // We should probably scope this down to e.g. only framework code.
            bool overridesEquals = valueType.GetMethod("Equals", _objectEqualsMethod.Signature) != null;
            bool overridesGetHashCode = valueType.GetMethod("GetHashCode", _objectGetHashCodeMethod.Signature) != null;
            if (overridesEquals && overridesGetHashCode)
                return false;

            return !CanCompareValueTypeBits(valueType);
        }

        private bool CanCompareValueTypeBits(MetadataType type)
        {
            Debug.Assert(type.IsValueType);

            if (type.ContainsGCPointers)
                return false;

            // TODO: what we're shooting for is overlapping fields
            // or gaps between fields
            if (type.IsExplicitLayout || type.GetClassLayout().Size != 0)
                return false;

            bool result = true;
            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType.IsPointer || fieldType.IsFunctionPointer)
                {
                    TypeFlags category = fieldType.UnderlyingType.Category;
                    if (category == TypeFlags.Single || category == TypeFlags.Double)
                    {
                        // Double/Single have weird behaviors around negative/positive zero
                        result = false;
                        break;
                    }
                }
                else if (fieldType.IsSignatureVariable)
                {
                    return false;
                }
                else
                {
                    // Would be a suprise if this wasn't a valuetype. We checked ContainsGCPointers above.
                    Debug.Assert(fieldType.IsValueType);

                    // If the field overrides Equals/GetHashCode, we can't use the fast helper because we need to call the method.
                    if (fieldType.FindVirtualFunctionTargetMethodOnObjectType(_objectEqualsMethod) != null ||
                        fieldType.FindVirtualFunctionTargetMethodOnObjectType(_objectGetHashCodeMethod) != null)
                    {
                        result = false;
                        break;
                    }

                    if (!CanCompareValueTypeBits((MetadataType)fieldType))
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }
    }
}
