// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    public abstract partial class TypeSystemContext
    {

        //
        // Instantiated generic parameters
        //

        private struct InstantiatedGenericParameterKey
        {
            private readonly GenericParameterDesc _genericParam;
            private readonly Instantiation _typeInstantiation;
            private readonly Instantiation _methodInstantiation;

            public InstantiatedGenericParameterKey(GenericParameterDesc genericParam, Instantiation typeInstantiation, Instantiation methodInstantiation)
            {
                _genericParam = genericParam;
                _typeInstantiation = typeInstantiation;
                _methodInstantiation = methodInstantiation;
            }

            public GenericParameterDesc GenericParameter
            {
                get
                {
                    return _genericParam;
                }
            }

            public Instantiation TypeInstantiation
            {
                get
                {
                    return _typeInstantiation;
                }
            }

            public Instantiation MethodInstantiation
            {
                get
                {
                    return _methodInstantiation;
                }
            }

            public class InstantiatedGenericParameterKeyHashtable : LockFreeReaderHashtable<InstantiatedGenericParameterKey, InstantiatedGenericParameter>
            {
                protected override int GetKeyHashCode(InstantiatedGenericParameterKey key)
                {
                    return key._methodInstantiation.ComputeGenericInstanceHashCode(key._typeInstantiation.ComputeGenericInstanceHashCode(key._genericParam.GetTypeDefinition().GetHashCode()));
                }
                
                protected override int GetValueHashCode(InstantiatedGenericParameter value)
                {
                    return value.MethodInstantiation.ComputeGenericInstanceHashCode(value.TypeInstantiation.ComputeGenericInstanceHashCode(value.GetTypeDefinition().GetHashCode()));
                }
                
                protected override bool CompareKeyToValue(InstantiatedGenericParameterKey key, InstantiatedGenericParameter value)
                {
                    if (key._genericParam != value.GenericParameter)
                        return false;

                    if (key._typeInstantiation.Length != value.TypeInstantiation.Length)
                        return false;

                    if (key._methodInstantiation.Length != value.MethodInstantiation.Length)
                        return false;
                
                    for (int i = 0; i < key._typeInstantiation.Length; i++)
                    {
                        if (key._typeInstantiation[i] != value.TypeInstantiation[i])
                            return false;
                    }
                    return true;
                }
                
                protected override bool CompareValueToValue(InstantiatedGenericParameter value1, InstantiatedGenericParameter value2)
                {
                    if (value1.GetTypeDefinition() != value2.GetTypeDefinition())
                        return false;
                
                    Instantiation value1Instantiation = value1.Instantiation;
                    Instantiation value2Instantiation = value2.Instantiation;
                    
                    if (value1Instantiation.Length != value2Instantiation.Length)
                        return false;
                    
                    for (int i = 0; i < value1Instantiation.Length; i++)
                    {
                        if (value1Instantiation[i] != value2Instantiation[i])
                            return false;
                    }
                    
                    return true;
                }
                
                protected override InstantiatedGenericParameter CreateValueFromKey(InstantiatedGenericParameterKey key)
                {
                    return new InstantiatedGenericParameter(key.GenericParameter, key.TypeInstantiation, key.MethodInstantiation);
                }
            }
        }
        
        private InstantiatedGenericParameterKey.InstantiatedGenericParameterKeyHashtable _instantiatedGenericParams = new InstantiatedGenericParameterKey.InstantiatedGenericParameterKeyHashtable();
        
        internal InstantiatedGenericParameter GetInstantiatedGenericParameter(GenericParameterDesc genericParam, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            return _instantiatedGenericParams.GetOrCreateValue(new InstantiatedGenericParameterKey(genericParam, typeInstantiation, methodInstantiation));
        }
    }
}
