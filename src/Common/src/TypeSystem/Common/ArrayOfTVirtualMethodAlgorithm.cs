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

        private static ResolvedVirtualMethod Reparent(ResolvedVirtualMethod method, TypeDesc oldType, TypeDesc newType)
        {
            if (method.OwningType == oldType)
            {
                return new ResolvedVirtualMethod(newType, method.Target);
            }
            else
                return method;
        }

        private static IEnumerable<ResolvedVirtualMethod> Reparent(IEnumerable<ResolvedVirtualMethod> methods, TypeDesc oldType, TypeDesc newType)
        {
            foreach (var method in methods)
                yield return Reparent(method, oldType, newType);
        }

        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            var arrayOfT = GetMatchingArrayOfT(type);
            return arrayOfT.GetAllVirtualMethods();
        }

        public override IEnumerable<ResolvedVirtualMethod> ComputeAllVirtualSlots(TypeDesc type)
        {
            var arrayOfT = GetMatchingArrayOfT(type);
            return Reparent(arrayOfT.EnumAllVirtualSlots(), arrayOfT, type);
        }

        public override ResolvedVirtualMethod FindVirtualFunctionTargetMethodOnObjectType(MethodDesc targetMethod, TypeDesc objectType)
        {
            var arrayOfT = GetMatchingArrayOfT(objectType);
            return Reparent(arrayOfT.FindVirtualFunctionTargetMethodOnObjectType(targetMethod), arrayOfT, objectType);
        }

        public override bool TryResolveInterfaceMethodToVirtualMethodOnType(MethodDesc interfaceMethod, TypeDesc currentType, out ResolvedVirtualMethod resolvedMethod)
        {
            var arrayOfT = GetMatchingArrayOfT(currentType);
            ResolvedVirtualMethod resolvedMethodOnArrayOfT;
            if (!arrayOfT.TryResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod, out resolvedMethodOnArrayOfT))
            {
                resolvedMethod = default(ResolvedVirtualMethod);
                return false;
            }

            resolvedMethod = Reparent(resolvedMethodOnArrayOfT, arrayOfT, currentType);
            return true;
        }
    }
}
