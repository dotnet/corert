// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using ILVerify;
using Internal.TypeSystem.Ecma;
using Xunit;
using System.Linq;
using System.Reflection;

namespace ILVerification.Tests
{
    public class ILInterfaceTester
    {
        [Fact]
        [Trait("MyTrait", "MyTrait")]
        public void InvalidClass()
        {
            string testFile = Path.GetFullPath(@"Tests\InterfaceTest.dll");
            var simpleNameToPathMap = new Dictionary<string, string>();
            simpleNameToPathMap.Add(Path.GetFileNameWithoutExtension(testFile), testFile);
            Assembly coreAssembly = typeof(object).GetTypeInfo().Assembly;
            simpleNameToPathMap.Add(coreAssembly.GetName().Name, coreAssembly.Location);
            Assembly systemRuntime = Assembly.Load(new AssemblyName("System.Runtime"));
            simpleNameToPathMap.Add(systemRuntime.GetName().Name, systemRuntime.Location);
            var resolver = new TestResolver(simpleNameToPathMap);
            var typeSystemContext = new ILVerifyTypeSystemContext(resolver);
            typeSystemContext.SetSystemModule(typeSystemContext.GetModule(resolver.Resolve(coreAssembly.GetName().Name)));
            Verifier verifier = new Verifier(typeSystemContext);
            var module = typeSystemContext.GetModule(resolver.Resolve(new AssemblyName(Path.GetFileNameWithoutExtension(testFile)).Name));
            var results = verifier.Verify(module.PEReader).ToArray();
            Assert.NotNull(results);
            Assert.NotEmpty(results);
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
            Dictionary<string, string> _simpleNameToPathMap;
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
