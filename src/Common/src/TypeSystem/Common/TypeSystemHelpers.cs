// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        static public ArrayType MakeArrayType(this TypeDesc type)
        {
            return type.Context.GetArrayType(type);
        }

        static public ByRefType MakeByRefType(this TypeDesc type)
        {
            return type.Context.GetByRefType(type);
        }

        static public PointerType MakePointerType(this TypeDesc type)
        {
            return type.Context.GetPointerType(type);
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

        static public MethodDesc GetDefaultConstructor(this TypeDesc type)
        {
            // TODO: Do we want check for specialname/rtspecialname? Maybe add another overload on GetMethod?
            var sig = new MethodSignature(0, 0, type.Context.GetWellKnownType(WellKnownType.Void), Array.Empty<TypeDesc>());
            return type.GetMethod(".ctor", sig);
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

        /// <summary>
        /// Attempts to resolve constrained call to <paramref name="interfaceMethod"/> into a concrete non-unboxing
        /// method on <paramref name="constrainedType"/>.
        /// The ability to resolve constraint methods is affected by the degree of code sharing we are performing
        /// for generic code.
        /// </summary>
        /// <returns>The resolved method or null if the constraint couldn't be resolved.</returns>
        static public MethodDesc TryResolveConstraintMethodApprox(this MetadataType constrainedType, TypeDesc interfaceType, MethodDesc interfaceMethod, out bool forceRuntimeLookup)
        {
            forceRuntimeLookup = false;

            // We can't resolve constraint calls effectively for reference types, and there's
            // not a lot of perf. benefit in doing it anyway.
            if (!constrainedType.IsValueType)
            {
                return null;
            }

            // Non-virtual methods called through constraints simply resolve to the specified method without constraint resolution.
            if (!interfaceMethod.IsVirtual)
            {
                return null;
            }

            MetadataType canonMT = constrainedType;

            MethodDesc method;

            MethodDesc genInterfaceMethod = interfaceMethod.GetMethodDefinition();
            if (genInterfaceMethod.OwningType.IsInterface)
            {
                // Sometimes (when compiling shared generic code)
                // we don't have enough exact type information at JIT time
                // even to decide whether we will be able to resolve to an unboxed entry point...
                // To cope with this case we always go via the helper function if there's any
                // chance of this happening by checking for all interfaces which might possibly
                // be compatible with the call (verification will have ensured that
                // at least one of them will be)

                // Enumerate all potential interface instantiations

                // TODO: this code assumes no shared generics
                Debug.Assert(interfaceType == interfaceMethod.OwningType);

                method = VirtualFunctionResolution.ResolveInterfaceMethodToVirtualMethodOnType(genInterfaceMethod, constrainedType);
            }
            else if (genInterfaceMethod.IsVirtual)
            {
                method = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(genInterfaceMethod, constrainedType);
            }
            else
            {
                // The method will be null if calling a non-virtual instance 
                // methods on System.Object, i.e. when these are used as a constraint.
                method = null;
            }

            if (method == null)
            {
                // Fall back to VSD
                return null;
            }

            //#TryResolveConstraintMethodApprox_DoNotReturnParentMethod
            // Only return a method if the value type itself declares the method, 
            // otherwise we might get a method from Object or System.ValueType
            if (!method.OwningType.IsValueType)
            {
                // Fall back to VSD
                return null;
            }

            // We've resolved the method, ignoring its generic method arguments
            // If the method is a generic method then go and get the instantiated descriptor
            if (interfaceMethod.HasInstantiation)
            {
                method = method.InstantiateSignature(interfaceType.Instantiation, interfaceMethod.Instantiation);
            }

            Debug.Assert(method != null);
            //assert(!pMD->IsUnboxingStub());

            return method;
        }

        /// <summary>
        /// Retrieves the namespace qualified name of a <see cref="MetadataType"/>.
        /// </summary>
        public static string GetFullName(this MetadataType metadataType)
        {
            string ns = metadataType.Namespace;
            return ns.Length > 0 ? String.Concat(ns, ".", metadataType.Name) : metadataType.Name;
        }
    }
}
