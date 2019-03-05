// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using AssemblyName = System.Reflection.AssemblyName;

namespace ILCompiler
{
    /// <summary>
    /// Compilation root provider that provides roots based on the RD.XML file format.
    /// Only supports a subset of the Runtime Directives configuration file format.
    /// </summary>
    /// <remarks>https://msdn.microsoft.com/en-us/library/dn600639(v=vs.110).aspx</remarks>
    internal class RdXmlRootProvider : ICompilationRootProvider
    {
        private XElement _documentRoot;
        private TypeSystemContext _context;

        public RdXmlRootProvider(TypeSystemContext context, string rdXmlFileName)
        {
            _context = context;
            _documentRoot = XElement.Load(rdXmlFileName);
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            var libraryOrApplication = _documentRoot.Elements().Single();

            if (libraryOrApplication.Name.LocalName != "Library" && libraryOrApplication.Name.LocalName != "Application")
                throw new Exception();

            if (libraryOrApplication.Attributes().Any())
                throw new NotSupportedException();

            foreach (var element in libraryOrApplication.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Assembly":
                        ProcessAssemblyDirective(rootProvider, element);
                        break;

                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private void ProcessAssemblyDirective(IRootingServiceProvider rootProvider, XElement assemblyElement)
        {
            var assemblyNameAttribute = assemblyElement.Attribute("Name");
            if (assemblyNameAttribute == null)
                throw new Exception();

            ModuleDesc assembly = _context.ResolveAssembly(new AssemblyName(assemblyNameAttribute.Value));

            rootProvider.RootModuleMetadata(assembly, "RD.XML root");

            var dynamicDegreeAttribute = assemblyElement.Attribute("Dynamic");
            if (dynamicDegreeAttribute != null)
            {
                if (dynamicDegreeAttribute.Value != "Required All")
                    throw new NotSupportedException();

                foreach (TypeDesc type in ((EcmaModule)assembly).GetAllTypes())
                {
                    RootType(rootProvider, type, "RD.XML root");
                }
            }

            foreach (var element in assemblyElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Type":
                        ProcessTypeDirective(rootProvider, assembly, element);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private void ProcessTypeDirective(IRootingServiceProvider rootProvider, ModuleDesc containingModule, XElement typeElement)
        {
            var typeNameAttribute = typeElement.Attribute("Name");
            if (typeNameAttribute == null)
                throw new Exception();
            string typeName = typeNameAttribute.Value;
            TypeDesc type = containingModule.GetTypeByCustomAttributeTypeName(typeName);

            var dynamicDegreeAttribute = typeElement.Attribute("Dynamic");
            if (dynamicDegreeAttribute != null)
            {
                if (dynamicDegreeAttribute.Value != "Required All")
                    throw new NotSupportedException();

                RootType(rootProvider, type, "RD.XML root");
            }

            foreach (var element in typeElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Method":
                        ProcessMethodDirective(rootProvider, containingModule, type, element);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        private void ProcessMethodDirective(IRootingServiceProvider rootProvider, ModuleDesc containingModule, TypeDesc containingType, XElement methodElement)
        {
            var methodNameAttribute = methodElement.Attribute("Name");
            if (methodNameAttribute == null)
                throw new Exception();
            string methodName = methodNameAttribute.Value;
            MethodDesc method = containingType.GetMethod(methodName, null);

            var instArgs = new List<TypeDesc>();
            foreach (var element in methodElement.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "GenericArgument":
                        string instArgName = element.Attribute("Name").Value;
                        instArgs.Add(containingModule.GetTypeByCustomAttributeTypeName(instArgName));
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            if (instArgs.Count != method.Instantiation.Length)
                throw new Exception();

            if (instArgs.Count > 0)
            {
                var methodInst = new Instantiation(instArgs.ToArray());
                method = method.MakeInstantiatedMethod(methodInst);
            }

            RootMethod(rootProvider, method, "RD.XML root");
        }

        public static void RootType(IRootingServiceProvider rootProvider, TypeDesc type, string reason)
        {
            rootProvider.AddCompilationRoot(type, reason);

            // Instantiate generic types over something that will be useful at runtime
            if (type.IsGenericDefinition)
            {
                Instantiation inst = GetInstantiationThatMeetsConstraints(type.Instantiation);
                if (inst.IsNull)
                    return;

                type = ((MetadataType)type).MakeInstantiatedType(inst);

                rootProvider.AddCompilationRoot(type, reason);
            }

            // Also root base types. This is so that we make methods on the base types callable.
            // This helps in cases like "class Foo : Bar<int> { }" where we discover new
            // generic instantiations.
            DefType baseType = type.BaseType;
            while (baseType != null)
            {
                RootType(rootProvider, baseType, reason);
                baseType = baseType.BaseType;
            }

            if (type.IsDefType)
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.HasInstantiation)
                    {
                        // Generic methods on generic types could end up as Foo<object>.Bar<__Canon>(),
                        // so for simplicity, we just don't handle them right now to make this more
                        // predictable.
                        if (!method.OwningType.HasInstantiation)
                        {
                            Instantiation inst = GetInstantiationThatMeetsConstraints(method.Instantiation);
                            if (!inst.IsNull)
                            {
                                RootMethod(rootProvider, method.MakeInstantiatedMethod(inst), reason);
                            }
                        }
                    }
                    else
                    {
                        RootMethod(rootProvider, method, reason);
                    }
                }
            }
        }

        private static void RootMethod(IRootingServiceProvider rootProvider, MethodDesc method, string reason)
        {
            try
            {
                LibraryRootProvider.CheckCanGenerateMethod(method);

                // Virtual methods should be rooted as if they were called virtually
                if (method.IsVirtual)
                    rootProvider.RootVirtualMethodForReflection(method, reason);

                if (!method.IsAbstract)
                    rootProvider.AddCompilationRoot(method, reason);
            }
            catch (TypeSystemException)
            {
                // TODO: fail compilation if a switch was passed

                // Individual methods can fail to load types referenced in their signatures.
                // Skip them in library mode since they're not going to be callable.
                return;

                // TODO: Log as a warning
            }
        }

        private static Instantiation GetInstantiationThatMeetsConstraints(Instantiation inst)
        {
            TypeDesc[] resultArray = new TypeDesc[inst.Length];
            for (int i = 0; i < inst.Length; i++)
            {
                TypeDesc instArg = GetTypeThatMeetsConstraints((GenericParameterDesc)inst[i]);
                if (instArg == null)
                    return default(Instantiation);
                resultArray[i] = instArg;
            }

            return new Instantiation(resultArray);
        }

        private static TypeDesc GetTypeThatMeetsConstraints(GenericParameterDesc genericParam)
        {
            TypeSystemContext context = genericParam.Context;

            // Universal canon is the best option if it's supported
            if (context.SupportsUniversalCanon)
                return context.UniversalCanonType;

            // Try normal canon next
            if (!context.SupportsCanon)
                return null;

            // Not nullable type is the only thing where reference canon doesn't make sense.
            GenericConstraints constraints = genericParam.Constraints;
            if ((constraints & GenericConstraints.NotNullableValueTypeConstraint) != 0)
                return null;

            foreach (var c in genericParam.TypeConstraints)
            {
                // Could be e.g. "where T : U"
                // We could try to dig into the U and solve it, but that just opens us up to
                // recursion and it's just not worth it.
                if (c.IsSignatureVariable)
                    return null;

                if (!c.IsGCPointer)
                    return null;
            }

            return genericParam.Context.CanonType;
        }
    }
}
