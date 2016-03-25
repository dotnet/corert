// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Virtual method algorithm for types that don't introduce any new virtual methods.
    /// </summary>
    public sealed class BaseTypeVirtualMethodAlgorithm : VirtualMethodAlgorithm
    {
        private static BaseTypeVirtualMethodAlgorithm _singleton = new BaseTypeVirtualMethodAlgorithm();

        public static VirtualMethodAlgorithm Instance
        {
            get
            {
                return _singleton;
            }
        }

        private BaseTypeVirtualMethodAlgorithm()
        {
        }

        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            return Array.Empty<MethodDesc>();
        }

        public override IEnumerable<MethodDesc> ComputeAllVirtualSlots(TypeDesc type)
        {
            return type.BaseType.EnumAllVirtualSlots();
        }

        public override MethodDesc FindVirtualFunctionTargetMethodOnObjectType(MethodDesc targetMethod, TypeDesc objectType)
        {
            return objectType.BaseType.FindVirtualFunctionTargetMethodOnObjectType(targetMethod);
        }

        public override bool TryResolveInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType, out MethodDesc resolvedMethod)
        {
            resolvedMethod = null;
            return false;
        }
    }
}
