// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// RuntimeInterfaces algorithm for for array types which are similar to a generic type
    /// </summary>
    public sealed class ArrayOfTRuntimeInterfacesAlgorithm : RuntimeInterfacesAlgorithm
    {
        /// <summary>
        /// Open type to instantiate to get the interfaces associated with an array.
        /// </summary>
        private MetadataType _arrayOfTType;

        /// <summary>
        /// RuntimeInterfaces algorithm for for array types which are similar to a generic type
        /// </summary>
        /// <param name="arrayOfTType">Open type to instantiate to get the interfaces associated with an array.</param>
        public ArrayOfTRuntimeInterfacesAlgorithm(MetadataType arrayOfTType)
        {
            _arrayOfTType = arrayOfTType;
            Debug.Assert(!(arrayOfTType is InstantiatedType));
        }

        public override DefType[] ComputeRuntimeInterfaces(TypeDesc _type)
        {
            ArrayType arrayType = (ArrayType)_type;
            Debug.Assert(arrayType.IsSzArray);
            Instantiation arrayInstantiation = new Instantiation(new TypeDesc[] { arrayType.ElementType });
            TypeDesc arrayOfTInstantiation = _arrayOfTType.Context.GetInstantiatedType(_arrayOfTType, arrayInstantiation);

            return arrayOfTInstantiation.RuntimeInterfaces;
        }
    }
}
