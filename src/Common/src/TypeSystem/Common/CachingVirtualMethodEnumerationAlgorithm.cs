// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Provides an implementation of <see cref="VirtualMethodEnumerationAlgorithm"/> that is
    /// based on the metadata, as reported by the type through the <see cref="TypeDesc.GetMethods"/> method.
    /// The algorithm operates simliarly to <see cref="MetadataVirtualMethodEnumerationAlgorithm"/>, but
    /// maintains a cache of the results.
    /// </summary>
    public sealed class CachingVirtualMethodEnumerationAlgorithm : VirtualMethodEnumerationAlgorithm
    {
        private class VirtualMethodListHashTable : LockFreeReaderHashtable<TypeDesc, Tuple<TypeDesc, MethodDesc[]>>
        {
            protected override int GetKeyHashCode(TypeDesc key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(Tuple<TypeDesc, MethodDesc[]> value)
            {
                return value.Item1.GetHashCode();
            }
            protected override bool CompareKeyToValue(TypeDesc key, Tuple<TypeDesc, MethodDesc[]> value)
            {
                return key == value.Item1;
            }
            protected override bool CompareValueToValue(Tuple<TypeDesc, MethodDesc[]> value1, Tuple<TypeDesc, MethodDesc[]> value2)
            {
                return value1.Item1 == value2.Item1;
            }
            protected override Tuple<TypeDesc, MethodDesc[]> CreateValueFromKey(TypeDesc key)
            {
                ArrayBuilder<MethodDesc> virtualMethods = new ArrayBuilder<MethodDesc>();

                foreach (var method in key.GetMethods())
                {
                    if (method.IsVirtual)
                    {
                        virtualMethods.Add(method);
                    }
                }

                return new Tuple<TypeDesc, MethodDesc[]>(key, virtualMethods.ToArray());
            }
        }
        private VirtualMethodListHashTable _virtualMethodLists = new VirtualMethodListHashTable();

        public override IEnumerable<MethodDesc> ComputeAllVirtualMethods(TypeDesc type)
        {
            InstantiatedType instantiatedType = type as InstantiatedType;

            if (instantiatedType != null)
            {
                return ComputeAllVirtualMethodsForInstantiatedType(instantiatedType);
            }
            else
            {
                return _virtualMethodLists.GetOrCreateValue(type).Item2;
            }
        }

        private IEnumerable<MethodDesc> ComputeAllVirtualMethodsForInstantiatedType(InstantiatedType instantiatedType)
        {
            foreach (var typicalMethod in _virtualMethodLists.GetOrCreateValue(instantiatedType.GetTypeDefinition()).Item2)
            {
                yield return instantiatedType.Context.GetMethodForInstantiatedType(typicalMethod, instantiatedType);
            }
        }
    }
}