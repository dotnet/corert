// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Virtual method algorithm for for array types which are similar to a generic type.
    /// </summary>
    public sealed class ArrayOfTVirtualMethodAlgorithm : VirtualMethodAlgorithm
    {
        private MetadataType _arrayOfTType;

        public ArrayOfTVirtualMethodAlgorithm(MetadataType arrayOfTType)
        {
            _arrayOfTType = arrayOfTType;
            Debug.Assert(!(arrayOfTType is InstantiatedType));
        }

        private InstantiatedType GetMatchingArrayOfT(TypeDesc type)
        {
            ArrayType arrayType = (ArrayType)type;
            Debug.Assert(arrayType.IsSzArray);
            Instantiation arrayInstantiation = new Instantiation(new TypeDesc[] { arrayType.ElementType });
            return _arrayOfTType.Context.GetInstantiatedType(_arrayOfTType, arrayInstantiation);
        }

        private static MethodDesc Reparent(MethodDesc method, TypeDesc oldType, TypeDesc newType)
        {
            if (method == null)
                return null;

            if (method.OwningType == oldType)
            {
                return method.Context.GetReparentedMethod(newType, method);
            }
            else
                return method;
        }

        private static IEnumerable<MethodDesc> Reparent(IEnumerable<MethodDesc> methods, TypeDesc oldType, TypeDesc newType)
        {
            foreach (var method in methods)
                yield return Reparent(method, oldType, newType);
        }

        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            var arrayOfT = GetMatchingArrayOfT(type);
            return Reparent(arrayOfT.GetAllVirtualMethods(), arrayOfT, type);
        }

        public override IEnumerable<MethodDesc> ComputeAllVirtualSlots(TypeDesc type)
        {
            var arrayOfT = GetMatchingArrayOfT(type);
            return Reparent(arrayOfT.EnumAllVirtualSlots(), arrayOfT, type);
        }

        public override MethodDesc FindVirtualFunctionTargetMethodOnObjectType(MethodDesc targetMethod, TypeDesc objectType)
        {
            var arrayOfT = GetMatchingArrayOfT(objectType);

            ReparentedMethodDesc reparentedTarget = targetMethod as ReparentedMethodDesc;
            if (reparentedTarget != null)
                targetMethod = reparentedTarget.ShadowMethod;

            return Reparent(arrayOfT.FindVirtualFunctionTargetMethodOnObjectType(targetMethod), arrayOfT, objectType);
        }

        public override MethodDesc ResolveInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType)
        {
            var arrayOfT = GetMatchingArrayOfT(currentType);
            return Reparent(arrayOfT.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod), arrayOfT, currentType);
        }
    }
}
