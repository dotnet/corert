// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Pluggable virtual method computation algorithm.
    /// </summary>
    public abstract class VirtualMethodEnumerationAlgorithm
    {
        /// <summary>
        /// Enumerates all virtual methods introduced or overriden by '<paramref name="type"/>'.
        /// </summary>
        public abstract IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type);
    }
}
