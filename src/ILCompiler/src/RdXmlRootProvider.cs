// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

            var dynamicDegreeAttribute = typeElement.Attribute("Dynamic");
            if (dynamicDegreeAttribute != null)
            {
                if (dynamicDegreeAttribute.Value != "Required All")
                    throw new NotSupportedException();
            }

            string typeName = typeNameAttribute.Value;
            RootType(rootProvider, containingModule.GetTypeByCustomAttributeTypeName(typeName));
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

                    try
                    {
                        LibraryRootProvider.CheckCanGenerateMethod(method);
                        
                        // Virtual methods should be rooted as if they were called virtually
                        if (method.IsVirtual)
                        {
                            MethodDesc slotMethod = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method);
                            rootProvider.RootMethodForReflection(slotMethod, "RD.XML root");
                        }
                        
                        if (!method.IsAbstract)
                            rootProvider.AddCompilationRoot(method, "RD.XML root");
                    }
                    catch (TypeSystemException)
                    {
                        // TODO: fail compilation if a switch was passed

                        // Individual methods can fail to load types referenced in their signatures.
                        // Skip them in library mode since they're not going to be callable.
                        continue;

                        // TODO: Log as a warning
                    }
                }
            }
        }
    }
}
