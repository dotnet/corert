// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem.Ecma;
using Internal.TypeSystem;

using Xunit;


namespace TypeSystemTests
{
    public class VirtualFunctionOverrideTests
    {
        TestTypeSystemContext _context;
        EcmaModule _testModule;

        public VirtualFunctionOverrideTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        [Fact]
        public void TestGenericMethodInterfaceMethodImplOverride()
        {
            //
            // Ensure MethodImpl based overriding works for MethodSpecs
            //

            MetadataType interfaceType = _testModule.GetType("VirtualFunctionOverride", "IIFaceWithGenericMethod");
            MethodDesc interfaceMethod = null;

            foreach(MethodDesc m in interfaceType.GetMethods())
            {
                if (m.Name == "GenMethod")
                {
                    interfaceMethod = m;
                    break;
                }
            }
            Assert.NotNull(interfaceMethod);

            MetadataType objectType = _testModule.GetType("VirtualFunctionOverride", "HasMethodInterfaceOverrideOfGenericMethod");
            MethodDesc expectedVirtualMethod = null;
            foreach (MethodDesc m in objectType.GetMethods())
            {
                if (m.Name.Contains("GenMethod"))
                {
                    expectedVirtualMethod = m;
                    break;
                }
            }
            Assert.NotNull(expectedVirtualMethod);

            Assert.Equal(expectedVirtualMethod, VirtualFunctionResolution.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod, objectType));
        }
    }
}
