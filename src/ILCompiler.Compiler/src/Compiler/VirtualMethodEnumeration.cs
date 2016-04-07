// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Virtual method enumeration algorithm for delegate types that injects a synthetic virtual
    /// method override into the type.
    /// </summary>
    public sealed class DelegateVirtualMethodEnumerationAlgorithm : VirtualMethodEnumerationAlgorithm
    {
        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            var context = (CompilerTypeSystemContext)type.Context;

            InstantiatedType instantiatedType = type as InstantiatedType;
            if (instantiatedType != null)
            {
                DelegateInfo info = context.GetDelegateInfo(type.GetTypeDefinition());
                yield return context.GetMethodForInstantiatedType(info.GetThunkMethod, instantiatedType);
            }
            else
            {
                DelegateInfo info = context.GetDelegateInfo(type);
                yield return info.GetThunkMethod;
            }
        }
    }
}