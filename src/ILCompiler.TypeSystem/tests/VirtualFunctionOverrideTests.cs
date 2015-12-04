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
        DefType _stringType;
        DefType _voidType;

        public VirtualFunctionOverrideTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;

            _stringType = _context.GetWellKnownType(WellKnownType.String);
            _voidType = _context.GetWellKnownType(WellKnownType.Void);
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

        [Fact]
        public void TestVirtualDispatchOnGenericType()
        {
            // Verifies that virtual dispatch to a non-generic method on a generic instantiation works
            DefType objectType = _context.GetWellKnownType(WellKnownType.Object);
            MethodSignature toStringSig = new MethodSignature(MethodSignatureFlags.None, 0, _stringType, Array.Empty<TypeDesc>());
            MethodDesc objectToString = objectType.GetMethod("ToString", toStringSig);
            Assert.NotNull(objectToString);
            MetadataType openTestType = _testModule.GetType("VirtualFunctionOverride", "SimpleGeneric`1");
            InstantiatedType testInstance = openTestType.MakeInstantiatedType(new Instantiation(new TypeDesc[] { objectType }));
            MethodDesc targetOnInstance = testInstance.GetMethod("ToString", toStringSig);

            MethodDesc targetMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(objectToString, testInstance);
            Assert.Equal(targetOnInstance, targetMethod);        
        }

        [Fact]
        public void TestVirtualDispatchOnGenericTypeWithOverload()
        {
            MetadataType openDerived = _testModule.GetType("VirtualFunctionOverride", "DerivedGenericWithOverload`1");
            MetadataType derivedInstance = openDerived.MakeInstantiatedType(new Instantiation(new TypeDesc[] { _stringType }));
            MetadataType baseInstance = (MetadataType)derivedInstance.BaseType;

            MethodDesc baseNongenericOverload = baseInstance.GetMethod("MyMethod", new MethodSignature(MethodSignatureFlags.None, 0, _voidType, new TypeDesc[] { _stringType }));
            MethodDesc derivedNongenericOverload = derivedInstance.GetMethod("MyMethod", new MethodSignature(MethodSignatureFlags.None, 0, _voidType, new TypeDesc[] { _stringType }));
            MethodDesc nongenericTargetOverload = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(baseNongenericOverload, derivedInstance);
            Assert.Equal(derivedNongenericOverload, nongenericTargetOverload);

            MethodDesc baseGenericOverload = baseInstance.GetMethod("MyMethod", new MethodSignature(MethodSignatureFlags.None, 0, _voidType, new TypeDesc[] { _context.GetSignatureVariable(0, false) }));
            MethodDesc derivedGenericOverload = derivedInstance.GetMethod("MyMethod", new MethodSignature(MethodSignatureFlags.None, 0, _voidType, new TypeDesc[] { _context.GetSignatureVariable(0, false) }));
            MethodDesc genericTargetOverload = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(baseGenericOverload, derivedInstance);
            Assert.Equal(derivedGenericOverload, genericTargetOverload);
        }

        [Fact]
        public void TestFinalizeOverrideChecking()
        {
            MetadataType classWithFinalizer = _testModule.GetType("VirtualFunctionOverride", "ClassWithFinalizer");
            DefType objectType = _testModule.Context.GetWellKnownType(WellKnownType.Object);
            MethodDesc finalizeMethod = objectType.GetMethod("Finalize", new MethodSignature(MethodSignatureFlags.None, 0, _voidType, new TypeDesc[] { }));

            MethodDesc actualFinalizer = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(finalizeMethod, classWithFinalizer);
            Assert.NotNull(actualFinalizer);
            Assert.NotEqual(actualFinalizer, finalizeMethod);
        }
    }
}
