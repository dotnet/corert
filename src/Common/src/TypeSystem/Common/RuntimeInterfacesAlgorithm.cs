// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
