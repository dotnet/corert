// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;

namespace ILCompiler
{
    public class RootingHelpers
    {
        public static bool TryRootType(IRootingServiceProvider rootProvider, TypeDesc type, string reason)
        {
            try
            {
                RootType(rootProvider, type, reason);
                return true;
            }
            catch (TypeSystemException)
            {
                return false;
            }
        }

        public static void RootType(IRootingServiceProvider rootProvider, TypeDesc type, string reason)
        {
            rootProvider.AddCompilationRoot(type, reason);

            // Instantiate generic types over something that will be useful at runtime
            if (type.IsGenericDefinition)
            {
                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(type.Instantiation, allowCanon: true);
                if (inst.IsNull)
                    return;

                type = ((MetadataType)type).MakeInstantiatedType(inst);

                rootProvider.AddCompilationRoot(type, reason);
            }

            // Also root base types. This is so that we make methods on the base types callable.
            // This helps in cases like "class Foo : Bar<int> { }" where we discover new
            // generic instantiations.
            TypeDesc baseType = type.BaseType;
            if (baseType != null)
            {
                RootType(rootProvider, baseType.NormalizeInstantiation(), reason);
            }

            if (type.IsDefType)
            {
                bool hasFinalizer = type.HasFinalizer || type.IsObject;

                foreach (var method in type.GetMethods())
                {
                    // We don't root finalizers because they're not directly callable and they will get
                    // generated if needed. We also need to prevent a VirtualMethodUse of Object::Finalize
                    // from entering the system.
                    if (hasFinalizer && method.IsFinalizer)
                        continue;

                    if (method.HasInstantiation)
                    {
                        // Generic methods on generic types could end up as Foo<object>.Bar<__Canon>(),
                        // so for simplicity, we just don't handle them right now to make this more
                        // predictable.
                        if (!method.OwningType.HasInstantiation)
                        {
                            Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(method.Instantiation, allowCanon: false);
                            if (!inst.IsNull)
                            {
                                TryRootMethod(rootProvider, method.MakeInstantiatedMethod(inst), reason);
                            }
                        }
                    }
                    else
                    {
                        TryRootMethod(rootProvider, method, reason);
                    }
                }
            }
        }

        public static bool TryRootMethod(IRootingServiceProvider rootProvider, MethodDesc method, string reason)
        {
            try
            {
                RootMethod(rootProvider, method, reason);
                return true;
            }
            catch (TypeSystemException)
            {
                return false;
            }
        }

        public static void RootMethod(IRootingServiceProvider rootProvider, MethodDesc method, string reason)
        {
            // Make sure we're not putting something into the graph that will crash later.
            LibraryRootProvider.CheckCanGenerateMethod(method);

            // Virtual methods should be rooted as if they were called virtually
            if (method.IsVirtual)
                rootProvider.RootVirtualMethodForReflection(method, reason);

            if (!method.IsAbstract)
                rootProvider.AddCompilationRoot(method, reason);
        }
    }
}
