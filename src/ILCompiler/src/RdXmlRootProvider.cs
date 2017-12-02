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
                    RootType(rootProvider, type);
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
                
                RootType(rootProvider, type);
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

            RootMethod(rootProvider, method);
        }

        private void RootType(IRootingServiceProvider rootProvider, TypeDesc type)
        {
            rootProvider.AddCompilationRoot(type, "RD.XML root");

            if (type.IsGenericDefinition)
                return;
            
            if (type.IsDefType)
            {
                foreach (var method in type.GetMethods())
                {
                    // We don't know what to instantiate generic methods over
                    if (method.HasInstantiation)
                        continue;

                    RootMethod(rootProvider, method);
                }
            }
        }

        private void RootMethod(IRootingServiceProvider rootProvider, MethodDesc method)
        {
            try
            {
                LibraryRootProvider.CheckCanGenerateMethod(method);

                // Virtual methods should be rooted as if they were called virtually
                if (method.IsVirtual)
                {
                    MethodDesc slotMethod = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method);
                    rootProvider.RootVirtualMethodForReflection(slotMethod, "RD.XML root");
                }

                if (!method.IsAbstract)
                    rootProvider.AddCompilationRoot(method, "RD.XML root");
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
    }
}
