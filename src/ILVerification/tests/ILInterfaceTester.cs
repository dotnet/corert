// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ILVerify;
using Xunit;

namespace ILVerification.Tests
{
    public class ILInterfaceTester
    {
        [Fact]
        [Trait("MyTrait", "MyTrait")]
        public void Test()
        {
            string testFile = Path.GetFullPath(@"Tests\InterfaceImplementation.dll");
            var simpleNameToPathMap = new Dictionary<string, string>
            {
                { Path.GetFileNameWithoutExtension(testFile), testFile }
            };
            Assembly coreAssembly = typeof(object).GetTypeInfo().Assembly;
            simpleNameToPathMap.Add("InterfaceDefinition" , @"Tests\InterfaceDefinition.dll");
            simpleNameToPathMap.Add(coreAssembly.GetName().Name, coreAssembly.Location);
            Assembly systemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
            simpleNameToPathMap.Add(systemRuntime.GetName().Name, systemRuntime.Location);
            var resolver = new TestResolver(simpleNameToPathMap);
            var typeSystemContext = new ILVerifyTypeSystemContext(resolver);
            typeSystemContext.SetSystemModule(typeSystemContext.GetModule(resolver.Resolve(coreAssembly.GetName().Name)));
            Verifier verifier = new Verifier(typeSystemContext);
            Internal.TypeSystem.Ecma.EcmaModule module = typeSystemContext.GetModule(resolver.Resolve(new AssemblyName(Path.GetFileNameWithoutExtension(testFile)).Name));

            List<VerificationResult> vr = new List<VerificationResult>();
            foreach (TypeDefinitionHandle typeHandle in module.PEReader.GetMetadataReader().TypeDefinitions)
            {
                vr.AddRange(verifier.VerifyInterface(module.PEReader, typeHandle).ToArray());
            }

            Assert.NotEmpty(vr);
        }

        private static IEnumerable<string> GetAllTestDlls(string assemblyPath)
        {
            foreach (var item in Directory.GetFiles(assemblyPath))
            {
                if (item.ToLower().EndsWith(".dll"))
                {
                    yield return Path.GetFileName(item);
                }
            }
        }

        private sealed class TestResolver : ResolverBase
        {
            private Dictionary<string, string> _simpleNameToPathMap;
            public TestResolver(Dictionary<string, string> simpleNameToPathMap)
            {
                _simpleNameToPathMap = simpleNameToPathMap;
            }

            protected override PEReader ResolveCore(string simpleName)
            {
                if (_simpleNameToPathMap.TryGetValue(simpleName, out string path))
                {
                    return new PEReader(File.OpenRead(path));
                }

                return null;
            }
        }
    }
}
