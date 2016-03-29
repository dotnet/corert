// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Provides an implementation of <see cref="VirtualMethodEnumerationAlgorithm"/> that is
    /// based on the metadata, as reported by the type through the <see cref="TypeDesc.GetMethods"/> method.
    /// </summary>
    public sealed class MetadataVirtualMethodEnumerationAlgorithm : VirtualMethodEnumerationAlgorithm
    {
        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            foreach (var method in type.GetMethods())
            {
                if (method.IsVirtual)
                    yield return method;
            }
        }
    }
}