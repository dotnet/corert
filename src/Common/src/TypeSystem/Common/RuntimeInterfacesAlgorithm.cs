// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Pluggable RuntimeInterfaces computation algorithm
    /// </summary>
    public abstract class RuntimeInterfacesAlgorithm
    {
        /// <summary>
        /// Compute the RuntimeInterfaces for a TypeDesc, is permitted to depend on 
        /// RuntimeInterfaces of base type, but must not depend on any other
        /// details of the base type.
        /// </summary>
        public abstract DefType[] ComputeRuntimeInterfaces(TypeDesc type);
    }
}
