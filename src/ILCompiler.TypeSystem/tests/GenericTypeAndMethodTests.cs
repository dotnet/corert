// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Internal.TypeSystem;
using Xunit;

namespace TypeSystemTests
{
    public class GenericMethodTests
    {
        private TestTypeSystemContext _context;
        private ModuleDesc _testModule;

        public GenericMethodTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.Unknown);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        /// <summary>
        /// Testing proper instantiation of types and methods involving generic parameters in their signature.
        /// </summary>
        [Fact]
        public void TestInstantiation()
        {
            MetadataType t = _testModule.GetType("GenericTypes", "GenericClass`1");

            // Verify that we get just type definitions.
            Assert.NotNull(t);
            Assert.True(t.IsTypeDefinition);
            Assert.NotNull(t.Instantiation);
            Assert.Equal(t.Instantiation.Length, 1);
            Assert.True(t.Instantiation[0].IsTypeDefinition);

            // Verify that we got a method definition
            MethodDesc fooMethod = t.GetMethods().First(m => m.Name == "Foo");
            Assert.True(fooMethod.IsTypicalMethodDefinition);

            // Verify that instantiating a method definition has no effect
            MethodDesc instantiatedMethod = fooMethod.InstantiateSignature( new Instantiation(_context.GetWellKnownType(WellKnownType.Int32)), Instantiation.Empty);
            Assert.Same(fooMethod, instantiatedMethod);

            MetadataType instantiatedType = t.MakeInstantiatedType(_context.GetWellKnownType(WellKnownType.Int32));

            // Verify properties of the instantiated type
            Assert.NotNull(instantiatedType);
            Assert.False(instantiatedType.IsTypeDefinition);
            Assert.NotNull(instantiatedType.Instantiation);
            Assert.Equal(instantiatedType.Instantiation.Length, 1);
            Assert.Equal(instantiatedType.Instantiation[0], _context.GetWellKnownType(WellKnownType.Int32));

            // Verify that we get an instantiated method with the proper signature
            MethodDesc fooInstantiatedMethod = instantiatedType.GetMethods().First(m => m.Name == "Foo");
            Assert.False(fooInstantiatedMethod.IsTypicalMethodDefinition);
            Assert.Equal(fooInstantiatedMethod.Signature.ReturnType, _context.GetWellKnownType(WellKnownType.Int32));
            Assert.Same(fooInstantiatedMethod.GetTypicalMethodDefinition(), fooMethod);
            // This is not a generic method, so they should be the same
            Assert.Same(fooInstantiatedMethod.GetMethodDefinition(), fooInstantiatedMethod);

            // Verify that instantiating a type definition has no effect
            TypeDesc newType = t.InstantiateSignature(new Instantiation(_context.GetWellKnownType(WellKnownType.Int32)), Instantiation.Empty);
            Assert.NotNull(newType);
            Assert.Same(newType, t);
        }

        /// <summary>
        /// Testing lookup up of a method in an instantiated type.
        /// </summary>
        [Fact]
        [ActiveIssue(-1)]
        public void TestMethodLookup()
        {
            MetadataType t = _testModule.GetType("GenericTypes", "GenericClass`1").MakeInstantiatedType(_context.GetWellKnownType(WellKnownType.Int32));

            MethodSignature sig = new MethodSignature(MethodSignatureFlags.None, 0, t.Instantiation[0], new TypeDesc[0] { });
            MethodDesc fooMethod = t.GetMethod("Foo", sig);
            Assert.NotNull(fooMethod);
        }
    }
}
