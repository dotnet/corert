// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// RuntimeInterfaces algorithm for types known to have no explicitly defined interfaces
    /// but which do have a base type. (For instance multidimensional arrays)
    /// </summary>
    public sealed class BaseTypeRuntimeInterfacesAlgorithm : RuntimeInterfacesAlgorithm
    {
        private static RuntimeInterfacesAlgorithm _singleton = new BaseTypeRuntimeInterfacesAlgorithm();

        private BaseTypeRuntimeInterfacesAlgorithm() { }

        public static RuntimeInterfacesAlgorithm Instance
        {
            get
            {
                return _singleton;
            }
        }

        public override DefType[] ComputeRuntimeInterfaces(TypeDesc _type)
        {
            return _type.BaseType.RuntimeInterfaces;
        }
    }
}
