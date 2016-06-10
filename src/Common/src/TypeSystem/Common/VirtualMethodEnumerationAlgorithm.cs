// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Pluggable virtual method computation algorithm. Provides an abstraction to compute the list
    /// of all virtual methods defined on a type.
    /// </summary>
    /// <remarks>
    /// The algorithms are expected to be directly used by <see cref="TypeSystemContext"/> derivatives
    /// only. The most obvious implementation of this algorithm that uses type's metadata to
    /// compute the answers is in <see cref="MetadataVirtualMethodEnumerationAlgorithm"/>.
    /// </remarks>
    public abstract class VirtualMethodEnumerationAlgorithm
    {
        /// <summary>
        /// Enumerates all virtual methods introduced or overriden by '<paramref name="type"/>'.
        /// </summary>
        public abstract IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type);
    }
}
