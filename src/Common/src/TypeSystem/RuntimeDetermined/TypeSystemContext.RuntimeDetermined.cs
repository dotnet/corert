// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Internal.TypeSystem
{
    partial class TypeSystemContext
    {
        private struct RuntimeDeterminedTypeKey
        {
            private DefType _plainCanonType;
            private GenericParameterDesc _detailsType;

            public RuntimeDeterminedTypeKey(DefType plainCanonType, GenericParameterDesc detailsType)
            {
                _plainCanonType = plainCanonType;
                _detailsType = detailsType;
            }

            public class RuntimeDeterminedTypeKeyHashtable : LockFreeReaderHashtable<RuntimeDeterminedTypeKey, RuntimeDeterminedType>
            {
                protected override int GetKeyHashCode(RuntimeDeterminedTypeKey key)
                {
                    return key._detailsType.GetHashCode() ^ key._plainCanonType.GetHashCode();
                }

                protected override int GetValueHashCode(RuntimeDeterminedType value)
                {
                    return value.RuntimeDeterminedDetailsType.GetHashCode() ^ value.CanonicalType.GetHashCode();
                }

                protected override bool CompareKeyToValue(RuntimeDeterminedTypeKey key, RuntimeDeterminedType value)
                {
                    return key._detailsType == value.RuntimeDeterminedDetailsType && key._plainCanonType == value.CanonicalType;
                }

                protected override bool CompareValueToValue(RuntimeDeterminedType value1, RuntimeDeterminedType value2)
                {
                    return value1.RuntimeDeterminedDetailsType == value2.RuntimeDeterminedDetailsType
                        && value1.CanonicalType == value2.CanonicalType;
                }

                protected override RuntimeDeterminedType CreateValueFromKey(RuntimeDeterminedTypeKey key)
                {
                    return new RuntimeDeterminedType(key._plainCanonType, key._detailsType);
                }
            }
        }

        private RuntimeDeterminedTypeKey.RuntimeDeterminedTypeKeyHashtable _runtimeDeterminedTypes = new RuntimeDeterminedTypeKey.RuntimeDeterminedTypeKeyHashtable();

        public RuntimeDeterminedType GetRuntimeDeterminedType(DefType plainCanonType, GenericParameterDesc detailsType)
        {
            return _runtimeDeterminedTypes.GetOrCreateValue(new RuntimeDeterminedTypeKey(plainCanonType, detailsType));
        }

        protected internal virtual TypeDesc ConvertToCanon(TypeDesc typeToConvert, ref CanonicalFormKind kind)
        {
            throw new NotSupportedException();
        }
    }
}
