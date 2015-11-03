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

        static public DefType GetClosestDefType(this TypeDesc type)
        {
            if (type is DefType)
                return (DefType)type;
            else
                return type.BaseType;
        }

        static public MetadataType GetClosestMetadataType(this TypeDesc type)
        {
            if (type is MetadataType)
                return (MetadataType)type;
            else
                return type.BaseType.GetClosestMetadataType();
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

        /// <summary>
        /// Returns method as defined on a non-generic base class or on a base
        /// instantiation.
        /// For example, If Foo&lt;T&gt; : Bar&lt;T&gt; and overrides method M,
        /// if method is Bar&lt;string&gt;.M(), then this returns Bar&lt;T&gt;.M()
        /// but if Foo : Bar&lt;string&gt;, then this returns Bar&lt;string&gt;.M()
        /// </summary>
        /// <param name="typeExamine">A potentially derived type</param>
        /// <param name="method">A base class's virtual method</param>
        static public MethodDesc FindMethodOnTypeWithMatchingTypicalMethod(this TypeDesc targetType, MethodDesc method)
        {
            // If method is nongeneric and on a nongeneric type, then it is the matching method
            if (!method.HasInstantiation && !method.OwningType.HasInstantiation)
            {
                return method;
            }

            // Since method is an instantiation that may or may not be the same as typeExamine's hierarchy,
            // find a matching base class on an open type and then work from the instantiation in typeExamine's
            // hierarchy
            TypeDesc typicalTypeOfTargetMethod = method.GetTypicalMethodDefinition().OwningType;
            TypeDesc targetOrBase = targetType;
            do
            {
                TypeDesc openTargetOrBase = targetOrBase;
                if (openTargetOrBase is InstantiatedType)
                {
                    openTargetOrBase = openTargetOrBase.GetTypeDefinition();
                }
                if (openTargetOrBase == typicalTypeOfTargetMethod)
                {
                    // Found an open match. Now find an equivalent method on the original target typeOrBase
                    MethodDesc matchingMethod = targetOrBase.FindMethodOnExactTypeWithMatchingTypicalMethod(method);
                    return matchingMethod;
                }
                targetOrBase = targetOrBase.BaseType;
            } while (targetOrBase != null);

            Debug.Assert(false, "method has no related type in the type hierarchy of type");
            return null;
        }
    }
}
