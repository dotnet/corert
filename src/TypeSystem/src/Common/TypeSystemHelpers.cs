// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Internal.TypeSystem
{
    static public class TypeSystemHelpers
    {
        static public InstantiatedType MakeInstantiatedType(this MetadataType typeDef, Instantiation instantiation)
        {
            return typeDef.Context.GetInstantiatedType(typeDef, instantiation);
        }

        static public InstantiatedMethod MakeInstantiatedMethod(this MethodDesc methodDef, Instantiation instantiation)
        {
            return methodDef.Context.GetInstantiatedMethod(methodDef, instantiation);
        }

        static public TypeDesc MakeArrayType(this TypeDesc type)
        {
            return type.Context.GetArrayType(type);
        }

        static public TypeDesc MakeByRefType(this TypeDesc type)
        {
            return type.Context.GetByRefType(type);
        }

        static public TypeDesc MakePointerType(this TypeDesc type)
        {
            return type.Context.GetPointerType(type);
        }

        static public MetadataType GetClosestDefType(this TypeDesc type)
        {
            if (type is MetadataType)
                return (MetadataType)type;
            else
                return type.BaseType;
        }

        static public int GetElementSize(this TypeDesc type)
        {
            if (type.IsValueType)
            {
                return ((MetadataType)type).InstanceFieldSize;
            }
            else
            {
                return type.Context.Target.PointerSize;
            }
        }

        static private MethodDesc FindMethodOnExactTypeWithMatchingTypicalMethod(this TypeDesc type, MethodDesc method)
        {
            // Assert that either type is instantiated and its type definition is the type that defines the typical
            // method definition of method, or that the owning type of the method typical definition is exactly type
            Debug.Assert((type is InstantiatedType) ? 
                ((InstantiatedType)type).GetTypeDefinition() == method.GetTypicalMethodDefinition().OwningType :
                type == method.GetTypicalMethodDefinition().OwningType);

            MethodDesc methodTypicalDefinition = method.GetTypicalMethodDefinition();

            foreach (MethodDesc methodToExamine in type.GetMethods())
            {
                if (methodToExamine.GetTypicalMethodDefinition() == methodTypicalDefinition)
                    return methodToExamine;
            }

            Debug.Assert(false, "Behavior of typical type not as expected.");
            return null;
        }

        static public MethodDesc FindMethodOnTypeWithMatchingTypicalMethod(this TypeDesc typeExamine, MethodDesc method)
        {
            TypeDesc typicalTypeInHierarchyOfTargetMethod = method.GetTypicalMethodDefinition().OwningType;
            TypeDesc typeInHierarchyOfTypeExamine = typeExamine;
            do
            {
                TypeDesc typicalTypeInHierarchyOfTypeExamine = typeInHierarchyOfTypeExamine;
                if (typicalTypeInHierarchyOfTypeExamine is InstantiatedType)
                {
                    typicalTypeInHierarchyOfTypeExamine = typicalTypeInHierarchyOfTypeExamine.GetTypeDefinition();
                }
                if (typicalTypeInHierarchyOfTypeExamine == typicalTypeInHierarchyOfTargetMethod)
                {
                    // set targetMethod to method on 
                    return typeInHierarchyOfTypeExamine.FindMethodOnTypeWithMatchingTypicalMethod(method);
                }
                typeInHierarchyOfTypeExamine = typeInHierarchyOfTypeExamine.BaseType;
            } while (typeInHierarchyOfTypeExamine != null);

            Debug.Assert(false, "method has no related type in the type hierarchy of type");
            return null;
        }
    }
}
