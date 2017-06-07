// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Internal.IL;
using Internal.TypeSystem;

namespace ILVerify
{
    internal class SimpleArrayOfTRuntimeInterfacesAlgorithm : RuntimeInterfacesAlgorithm
    {
        private DefType[] _arrayRuntimeInterfaces;
        private MetadataType[] _genericRuntimeInterfaces;
        private ModuleDesc _systemModule;

        public SimpleArrayOfTRuntimeInterfacesAlgorithm(ModuleDesc systemModule)
        {
            _systemModule = systemModule;
            _arrayRuntimeInterfaces = _systemModule.GetKnownType("System", "Array").RuntimeInterfaces;
            _genericRuntimeInterfaces = new MetadataType[]
            {
                _systemModule.GetKnownType("System.Collections.Generic", "IEnumerable`1"),
                _systemModule.GetKnownType("System.Collections.Generic", "ICollection`1"),
                _systemModule.GetKnownType("System.Collections.Generic", "IList`1")
            };
        }

        public override DefType[] ComputeRuntimeInterfaces(TypeDesc type)
        {
            ArrayType arrayType = (ArrayType)type;
            TypeDesc elementType = arrayType.ElementType;
            Debug.Assert(arrayType.IsSzArray);

            // first copy runtime interfaces from System.Array
            var result = new DefType[_arrayRuntimeInterfaces.Length + _genericRuntimeInterfaces.Length];
            Array.Copy(_arrayRuntimeInterfaces, result, _arrayRuntimeInterfaces.Length);

            // then copy instantiated generic interfaces
            int offset = _arrayRuntimeInterfaces.Length;
            for (int i = 0; i < _genericRuntimeInterfaces.Length; ++i)
                result[i + offset] = _genericRuntimeInterfaces[i].MakeInstantiatedType(elementType);

            return result;
        }
    }
}
