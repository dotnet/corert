// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Internal.Metadata.NativeFormat.Writer;
using ILCompiler.Metadata;

using Cts = Internal.TypeSystem;

using Xunit;

namespace MetadataTransformTests
{
    public class SimpleTests
    {
        private TestTypeSystemContext _context;
        private Cts.Ecma.EcmaModule _systemModule;

        public SimpleTests()
        {
            _context = new TestTypeSystemContext();
            _systemModule = _context.CreateModuleForSimpleName("PrimaryMetadataAssembly");
            _context.SetSystemModule(_systemModule);
        }

        [Fact]
        public void TestAllTypes()
        {
            var policy = new SingleFileMetadataPolicy();
            var transformResult = MetadataTransform.Run(policy, new[] { _systemModule });

            Assert.Equal(1, transformResult.Scopes.Count());
            
            Assert.Equal(
                _systemModule.GetAllTypes().Count(x => !policy.IsBlocked(x)),
                transformResult.Scopes.Single().GetAllTypes().Count());
        }

        [Fact]
        public void TestBlockedInterface()
        {
            // __ComObject implements ICastable, which is a metadata blocked type and should not show
            // up in the __ComObject interface list.

            var policy = new SingleFileMetadataPolicy();
            var transformResult = MetadataTransform.Run(policy, new[] { _systemModule });

            Cts.MetadataType icastable = _systemModule.GetType("System.Private.CompilerServices", "ICastable");
            Cts.MetadataType comObject = _systemModule.GetType("System", "__ComObject");
            Assert.Equal(1, comObject.ExplicitlyImplementedInterfaces.Length);
            Assert.Equal(icastable, comObject.ExplicitlyImplementedInterfaces[0]);

            Assert.Null(transformResult.GetTransformedTypeDefinition(icastable));
            Assert.Null(transformResult.GetTransformedTypeReference(icastable));

            TypeDefinition comObjectRecord = transformResult.GetTransformedTypeDefinition(comObject);
            Assert.NotNull(comObjectRecord);
            Assert.Equal(comObject.Name, comObjectRecord.Name.Value);
            Assert.Equal(0, comObjectRecord.Interfaces.Count);
        }

        [Fact]
        public void TestStandaloneSignatureGeneration()
        {
            var transformResult = MetadataTransform.Run(new SingleFileMetadataPolicy(), new[] { _systemModule });

            var stringRecord = transformResult.GetTransformedTypeDefinition(
                (Cts.MetadataType)_context.GetWellKnownType(Cts.WellKnownType.String));
            var singleRecord = transformResult.GetTransformedTypeDefinition(
                (Cts.MetadataType)_context.GetWellKnownType(Cts.WellKnownType.Single));

            var sig = new Cts.MethodSignature(
                0, 0, _context.GetWellKnownType(Cts.WellKnownType.String),
                new[] { _context.GetWellKnownType(Cts.WellKnownType.Single) });

            var sigRecord = transformResult.Transform.HandleMethodSignature(sig);

            // Verify the signature is connected to the existing transformResult world
            Assert.Same(stringRecord, sigRecord.ReturnType.Type);
            Assert.Equal(1, sigRecord.Parameters.Count);
            Assert.Same(singleRecord, sigRecord.Parameters[0].Type);
        }

        [Fact]
        public void TestSampleMetadataGeneration()
        {
            var policy = new SingleFileMetadataPolicy();
            var sampleMetadataModule = _context.GetModuleForSimpleName("SampleMetadataAssembly");
            var transformResult = MetadataTransform.Run(policy,
                new[] { _systemModule, sampleMetadataModule });

            Assert.Equal(2, transformResult.Scopes.Count);

            var systemScope = transformResult.Scopes.Single(s => s.Name.Value == "PrimaryMetadataAssembly");
            var sampleScope = transformResult.Scopes.Single(s => s.Name.Value == "SampleMetadataAssembly");

            Assert.Equal(_systemModule.GetAllTypes().Count(t => !policy.IsBlocked(t)), systemScope.GetAllTypes().Count());
            Assert.Equal(sampleMetadataModule.GetAllTypes().Count(t => !policy.IsBlocked(t)), sampleScope.GetAllTypes().Count());

            // TODO: check individual types
        }

        [Fact]
        public void TestMultifileSanity()
        {
            var sampleMetadataModule = _context.GetModuleForSimpleName("SampleMetadataAssembly");
            var policy = new MultifileMetadataPolicy(sampleMetadataModule);
            var transformResult = MetadataTransform.Run(policy,
                new[] { _systemModule, sampleMetadataModule });

            Assert.Equal(1, transformResult.Scopes.Count);

            var sampleScope = transformResult.Scopes.Single();
            Assert.Equal(sampleMetadataModule.GetAllTypes().Count(t => !policy.IsBlocked(t)), sampleScope.GetAllTypes().Count());

            var objectType = (Cts.MetadataType)_context.GetWellKnownType(Cts.WellKnownType.Object);
            var objectRecord = transformResult.GetTransformedTypeReference(objectType);
            Assert.Equal("Object", objectRecord.TypeName.Value);

            var stringType = (Cts.MetadataType)_context.GetWellKnownType(Cts.WellKnownType.String);
            var stringRecord = transformResult.GetTransformedTypeReference(stringType);
            Assert.Equal("String", stringRecord.TypeName.Value);

            Assert.Same(objectRecord.ParentNamespaceOrType, stringRecord.ParentNamespaceOrType);
            Assert.IsType<NamespaceReference>(objectRecord.ParentNamespaceOrType);

            var parentNamespace = objectRecord.ParentNamespaceOrType as NamespaceReference;
            Assert.Equal("System", parentNamespace.Name.Value);

            Assert.Null(transformResult.GetTransformedTypeDefinition(objectType));
            Assert.Null(transformResult.GetTransformedTypeDefinition(stringType));
        }

        [Fact]
        public void TestNestedTypeReference()
        {
            // A type reference nested under a type that has a definition record. The transform is required
            // to create a type reference for the containing type because a type *definition* can't be a parent
            // to a type *reference*.

            var sampleMetadataModule = _context.GetModuleForSimpleName("SampleMetadataAssembly");
            Cts.MetadataType genericOutside = sampleMetadataModule.GetType("SampleMetadata", "GenericOutside`1");
            Cts.MetadataType inside = genericOutside.GetNestedType("Inside");

            {
                MockPolicy policy = new MockPolicy(
                    type =>
                    {
                        return type == genericOutside;
                    });

                var result = MetadataTransform.Run(policy, new[] { sampleMetadataModule });

                Assert.Equal(1, result.Scopes.Count);
                Assert.Equal(1, result.Scopes.Single().GetAllTypes().Count());

                var genericOutsideDefRecord = result.GetTransformedTypeDefinition(genericOutside);
                Assert.NotNull(genericOutsideDefRecord);

                Assert.Null(result.GetTransformedTypeReference(inside));

                var insideRecord = result.Transform.HandleType(inside);
                Assert.IsType<TypeReference>(insideRecord);

                var genericOutsideRefRecord = ((TypeReference)insideRecord).ParentNamespaceOrType as TypeReference;
                Assert.NotNull(genericOutsideRefRecord);
                Assert.Equal(genericOutside.Name, genericOutsideRefRecord.TypeName.Value);

                Assert.Same(genericOutsideDefRecord, result.GetTransformedTypeDefinition(genericOutside));
            }

        }
    }
}
