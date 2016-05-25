// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Internal.Metadata.NativeFormat.Writer;
using ILCompiler.Metadata;

using Cts = Internal.TypeSystem;
using NativeFormat = Internal.Metadata.NativeFormat;

using Xunit;

namespace MetadataTransformTests
{
    public class ExplicitScopeTests
    {
        private TestTypeSystemContext _context;
        private Cts.Ecma.EcmaModule _systemModule;

        public ExplicitScopeTests()
        {
            _context = new TestTypeSystemContext();
            _systemModule = _context.CreateModuleForSimpleName("PrimaryMetadataAssembly");
            _context.SetSystemModule(_systemModule);
        }

        public ScopeDefinition GetScopeDefinitionOfType(TypeDefinition typeDefinition)
        {
            Assert.NotNull(typeDefinition);
            ScopeDefinition scope = null;
            NamespaceDefinition currentNamespaceDefinition = typeDefinition.NamespaceDefinition;

            while (scope == null)
            {
                Assert.NotNull(currentNamespaceDefinition);
                scope = currentNamespaceDefinition.ParentScopeOrNamespace as ScopeDefinition;
                currentNamespaceDefinition = currentNamespaceDefinition.ParentScopeOrNamespace as NamespaceDefinition;
            }

            return scope;
        }

        public ScopeReference GetScopeReferenceOfType(TypeReference typeReference)
        {
            Assert.NotNull(typeReference);
            ScopeReference scope = null;
            NamespaceReference currentNamespaceReference = typeReference.ParentNamespaceOrType as NamespaceReference;

            while (scope == null)
            {
                Assert.NotNull(currentNamespaceReference);
                scope = currentNamespaceReference.ParentScopeOrNamespace as ScopeReference;
                currentNamespaceReference = currentNamespaceReference.ParentScopeOrNamespace as NamespaceReference;
            }

            return scope;
        }

        public void CheckTypeDefinitionForProperWinRTHome(TypeDefinition typeDefinition, string module)
        {
            ScopeDefinition scope = GetScopeDefinitionOfType(typeDefinition);
            Assert.Equal(module, scope.Name.Value);
            int windowsRuntimeFlag = ((int)System.Reflection.AssemblyContentType.WindowsRuntime << 9);
            Assert.True((((int)scope.Flags) & windowsRuntimeFlag) == windowsRuntimeFlag);
        }


        public void CheckTypeReferenceForProperWinRTHome(TypeReference typeReference, string module)
        {
            ScopeReference scope = GetScopeReferenceOfType(typeReference);
            Assert.Equal(module, scope.Name.Value);
            int windowsRuntimeFlag = ((int)System.Reflection.AssemblyContentType.WindowsRuntime << 9);
            Assert.True((((int)scope.Flags) & windowsRuntimeFlag) == windowsRuntimeFlag);
        }

        [Fact]
        public void TestExplicitScopeAttributesForWinRTSingleFilePolicy()
        {
            // Test that custom attributes referring to blocked types don't show up in metadata

            var sampleMetadataModule = _context.GetModuleForSimpleName("SampleMetadataAssembly");
            var sampleWinRTMetadataModule = _context.GetModuleForSimpleName("SampleWinRTMetadataAssembly");
            var windowsWinRTMetadataModule = _context.GetModuleForSimpleName("WindowsWinRTMetadataAssembly");

            Cts.MetadataType controlType = windowsWinRTMetadataModule.GetType("Windows", "Control");
            Cts.MetadataType derivedFromControl = sampleWinRTMetadataModule.GetType("SampleMetadataWinRT", "DerivedFromControl");
            Cts.MetadataType derivedFromControlInCustomScope = sampleWinRTMetadataModule.GetType("SampleMetadataWinRT", "DerivedFromControlAndInCustomScope");

            var policy = new SingleFileMetadataPolicy();

            var transformResult = MetadataTransform.Run(policy,
                new[] { _systemModule, sampleMetadataModule, sampleWinRTMetadataModule, windowsWinRTMetadataModule });

            var controlTypeMetadata = transformResult.GetTransformedTypeDefinition(controlType);
            var derivedFromControlMetadata = transformResult.GetTransformedTypeDefinition(derivedFromControl);
            var derivedFromControlInCustomScopeMetadata = transformResult.GetTransformedTypeDefinition(derivedFromControlInCustomScope);

            CheckTypeDefinitionForProperWinRTHome(controlTypeMetadata, "Windows");
            ScopeDefinition scopeDefOfDerivedFromControlType = GetScopeDefinitionOfType(derivedFromControlMetadata);
            Assert.Equal("SampleWinRTMetadataAssembly", scopeDefOfDerivedFromControlType.Name.Value);
            CheckTypeDefinitionForProperWinRTHome(derivedFromControlInCustomScopeMetadata, "SampleMetadataWinRT");
        }


        [Fact]
        public void TestExplicitScopeAttributesForWinRTMultiFilePolicy()
        {
            // Test that custom attributes referring to blocked types don't show up in metadata

            var sampleMetadataModule = _context.GetModuleForSimpleName("SampleMetadataAssembly");
            var sampleWinRTMetadataModule = _context.GetModuleForSimpleName("SampleWinRTMetadataAssembly");
            var windowsWinRTMetadataModule = _context.GetModuleForSimpleName("WindowsWinRTMetadataAssembly");

            Cts.MetadataType controlType = windowsWinRTMetadataModule.GetType("Windows", "Control");
            Cts.MetadataType derivedFromControl = sampleWinRTMetadataModule.GetType("SampleMetadataWinRT", "DerivedFromControl");
            Cts.MetadataType derivedFromControlInCustomScope = sampleWinRTMetadataModule.GetType("SampleMetadataWinRT", "DerivedFromControlAndInCustomScope");

            var policy = new MultifileMetadataPolicy(sampleMetadataModule, sampleWinRTMetadataModule);

            var transformResult = MetadataTransform.Run(policy,
                new[] { _systemModule, sampleMetadataModule, sampleWinRTMetadataModule, windowsWinRTMetadataModule });

            var controlTypeMetadata = transformResult.GetTransformedTypeReference(controlType);
            var derivedFromControlMetadata = transformResult.GetTransformedTypeDefinition(derivedFromControl);
            var derivedFromControlInCustomScopeMetadata = transformResult.GetTransformedTypeDefinition(derivedFromControlInCustomScope);

            CheckTypeReferenceForProperWinRTHome(controlTypeMetadata, "Windows");
            ScopeDefinition scopeDefOfDerivedFromControlType = GetScopeDefinitionOfType(derivedFromControlMetadata);
            Assert.Equal("SampleWinRTMetadataAssembly", scopeDefOfDerivedFromControlType.Name.Value);
            CheckTypeDefinitionForProperWinRTHome(derivedFromControlInCustomScopeMetadata, "SampleMetadataWinRT");
        }
    }
}
