// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Pluggable virtual method computation algorithm.
    /// </summary>
    public abstract class VirtualMethodAlgorithm
    {
        /// <summary>
        /// Resolves interface method '<paramref name="interfaceMethod"/>' to a method on '<paramref name="type"/>'
        /// that implements the the method.
        /// </summary>
        public abstract bool TryResolveInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType, out ResolvedVirtualMethod resolvedMethod);

        /// <summary>
        /// Resolves a virtual method call.
        /// </summary>
        public abstract ResolvedVirtualMethod FindVirtualFunctionTargetMethodOnObjectType(MethodDesc targetMethod, TypeDesc objectType);

        /// <summary>
        /// Enumerates all virtual methods introduced or overriden by '<paramref name="type"/>'.
        /// </summary>
        public abstract IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type);

        /// <summary>
        /// Enumerates all virtual slots on '<paramref name="type"/>'.
        /// </summary>
        public abstract IEnumerable<ResolvedVirtualMethod> ComputeAllVirtualSlots(TypeDesc type);
    }


    public struct ResolvedVirtualMethod
    {
        public readonly TypeDesc OwningType;
        public readonly MethodDesc Target;

        public ResolvedVirtualMethod(TypeDesc owningType, MethodDesc target)
        {
            OwningType = owningType;
            Target = target;
        }

        public ResolvedVirtualMethod(MethodDesc target)
            : this(target.OwningType, target)
        {
        }
    }
}
