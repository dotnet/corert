// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Need to use "extern alias", because both the writer and reader define types with same names.
extern alias writer;

using System.IO;
using System.Linq;

using Xunit;

using Reader = Internal.Metadata.NativeFormat;
using Writer = writer.Internal.Metadata.NativeFormat.Writer;

using TypeAttributes = System.Reflection.TypeAttributes;
using MetadataTypeHashingAlgorithms = Internal.Metadata.NativeFormat.MetadataTypeHashingAlgorithms;
using TypeHashingAlgorithms = Internal.NativeFormat.TypeHashingAlgorithms;

namespace System.Private.Reflection.Metadata.Tests
{
    public class HashCodeTests
    {
        private static Writer.ScopeDefinition BuildSimpleTestDefinitionData()
        {
            // Scope for System.Runtime, 4.0.0.0
            var systemRuntimeScope = new Writer.ScopeDefinition
            {
                Name = (Writer.ConstantStringValue)"System.Runtime",
                MajorVersion = 4,
            };

            // Root namespace (".")
            var rootNamespaceDefinition = new Writer.NamespaceDefinition
            {
                ParentScopeOrNamespace = systemRuntimeScope,
            };
            systemRuntimeScope.RootNamespaceDefinition = rootNamespaceDefinition;

            // The <Module> type
            var moduleTypeDefinition = new Writer.TypeDefinition
            {
                Flags = TypeAttributes.Abstract | TypeAttributes.Public,
                Name = (Writer.ConstantStringValue)"<Module>",
                NamespaceDefinition = rootNamespaceDefinition,
            };
            rootNamespaceDefinition.TypeDefinitions.Add(moduleTypeDefinition);

            // System namespace
            var systemNamespaceDefinition = new Writer.NamespaceDefinition
            {
                Name = (Writer.ConstantStringValue)"System",
                ParentScopeOrNamespace = rootNamespaceDefinition,
            };
            rootNamespaceDefinition.NamespaceDefinitions.Add(systemNamespaceDefinition);

            // System.Object type
            var objectType = new Writer.TypeDefinition
            {
                Flags = TypeAttributes.Public | TypeAttributes.SequentialLayout,
                Name = (Writer.ConstantStringValue)"Object",
                NamespaceDefinition = systemNamespaceDefinition
            };
            systemNamespaceDefinition.TypeDefinitions.Add(objectType);

            // System.Object+Nested type
            var nestedType = new Writer.TypeDefinition
            {
                Flags = TypeAttributes.SequentialLayout | TypeAttributes.NestedPublic,
                Name = (Writer.ConstantStringValue)"Nested",
                NamespaceDefinition = null,
                EnclosingType = objectType
            };
            objectType.NestedTypes.Add(nestedType);

            // System.Object+Nested+ReallyNested type
            var reallyNestedType = new Writer.TypeDefinition
            {
                Flags = TypeAttributes.SequentialLayout | TypeAttributes.NestedPublic,
                Name = (Writer.ConstantStringValue)"ReallyNested",
                NamespaceDefinition = null,
                EnclosingType = nestedType
            };
            nestedType.NestedTypes.Add(reallyNestedType);

            return systemRuntimeScope;
        }

        [Fact]
        public unsafe void TestDefinitionHashCodes()
        {
            var wr = new Writer.MetadataWriter();
            wr.ScopeDefinitions.Add(BuildSimpleTestDefinitionData());
            var ms = new MemoryStream();
            wr.Write(ms);

            fixed (byte* pBuffer = ms.ToArray())
            {
                var rd = new Reader.MetadataReader((IntPtr)pBuffer, (int)ms.Length);

                Reader.ScopeDefinitionHandle scopeHandle = rd.ScopeDefinitions.Single();
                Reader.ScopeDefinition systemRuntimeScope = scopeHandle.GetScopeDefinition(rd);

                // Validate root type hash code
                Reader.NamespaceDefinition rootNamespace = systemRuntimeScope.RootNamespaceDefinition.GetNamespaceDefinition(rd);
                Reader.TypeDefinitionHandle moduleTypeHandle = rootNamespace.TypeDefinitions.Single();
                Assert.Equal(TypeHashingAlgorithms.ComputeNameHashCode("<Module>"), MetadataTypeHashingAlgorithms.ComputeHashCode(moduleTypeHandle, rd));

                // Validate namespace type hashcode
                Reader.NamespaceDefinition systemNamespace = rootNamespace.NamespaceDefinitions.Single().GetNamespaceDefinition(rd);
                Reader.TypeDefinitionHandle objectTypeHandle = systemNamespace.TypeDefinitions.Single();
                int objectHashCode = TypeHashingAlgorithms.ComputeNameHashCode("System.Object");
                Assert.Equal(objectHashCode, MetadataTypeHashingAlgorithms.ComputeHashCode(objectTypeHandle, rd));

                // Validate nested type hashcode
                Reader.TypeDefinitionHandle nestedTypeHandle = objectTypeHandle.GetTypeDefinition(rd).NestedTypes.Single();
                int nestedHashCode = TypeHashingAlgorithms.ComputeNestedTypeHashCode(objectHashCode, TypeHashingAlgorithms.ComputeNameHashCode("Nested"));
                Assert.Equal(nestedHashCode, MetadataTypeHashingAlgorithms.ComputeHashCode(nestedTypeHandle, rd));

                // Validate really nested type hashcode
                Reader.TypeDefinitionHandle reallyNestedTypeHandle = nestedTypeHandle.GetTypeDefinition(rd).NestedTypes.Single();
                int reallyNestedHashCode = TypeHashingAlgorithms.ComputeNestedTypeHashCode(nestedHashCode, TypeHashingAlgorithms.ComputeNameHashCode("ReallyNested"));
                Assert.Equal(reallyNestedHashCode, MetadataTypeHashingAlgorithms.ComputeHashCode(reallyNestedTypeHandle, rd));
            }
        }

        [Fact]
        public unsafe void TestReferenceHashCodes()
        {
            var wr = new Writer.MetadataWriter();

            var systemRuntimeScopeRecord = new Writer.ScopeReference
            {
                Name = (Writer.ConstantStringValue)"System.Runtime",
                MajorVersion = 4,
            };

            var rootNamespaceRecord = new Writer.NamespaceReference
            {
                Name = null,
                ParentScopeOrNamespace = systemRuntimeScopeRecord,
            };

            var fooTypeRecord = new Writer.TypeReference
            {
                ParentNamespaceOrType = rootNamespaceRecord,
                TypeName = (Writer.ConstantStringValue)"Foo",
            };

            var nestedTypeRecord = new Writer.TypeReference
            {
                ParentNamespaceOrType = fooTypeRecord,
                TypeName = (Writer.ConstantStringValue)"Nested",
            };

            var reallyNestedTypeRecord = new Writer.TypeReference
            {
                ParentNamespaceOrType = nestedTypeRecord,
                TypeName = (Writer.ConstantStringValue)"ReallyNested",
            };

            var systemNamespaceRecord = new Writer.NamespaceReference
            {
                Name = (Writer.ConstantStringValue)"System",
                ParentScopeOrNamespace = rootNamespaceRecord,
            };

            var objectTypeRecord = new Writer.TypeReference
            {
                ParentNamespaceOrType = systemNamespaceRecord,
                TypeName = (Writer.ConstantStringValue)"Object",
            };

            wr.AdditionalRootRecords.Add(objectTypeRecord);
            wr.AdditionalRootRecords.Add(fooTypeRecord);
            wr.AdditionalRootRecords.Add(nestedTypeRecord);
            wr.AdditionalRootRecords.Add(reallyNestedTypeRecord);
            var ms = new MemoryStream();
            wr.Write(ms);

            fixed (byte* pBuffer = ms.ToArray())
            {
                var rd = new Reader.MetadataReader((IntPtr)pBuffer, (int)ms.Length);

                var fooTypeHandle = new Reader.TypeReferenceHandle(wr.GetRecordHandle(fooTypeRecord));
                var fooTypeHashCode = TypeHashingAlgorithms.ComputeNameHashCode("Foo");
                Assert.Equal(fooTypeHashCode, MetadataTypeHashingAlgorithms.ComputeHashCode(fooTypeHandle, rd));

                var objectTypeHandle = new Reader.TypeReferenceHandle(wr.GetRecordHandle(objectTypeRecord));
                Assert.Equal(TypeHashingAlgorithms.ComputeNameHashCode("System.Object"), MetadataTypeHashingAlgorithms.ComputeHashCode(objectTypeHandle, rd));

                var nestedTypeHandle = new Reader.TypeReferenceHandle(wr.GetRecordHandle(nestedTypeRecord));
                var nestedTypeHashCode = TypeHashingAlgorithms.ComputeNestedTypeHashCode(fooTypeHashCode, TypeHashingAlgorithms.ComputeNameHashCode("Nested"));
                Assert.Equal(nestedTypeHashCode , MetadataTypeHashingAlgorithms.ComputeHashCode(nestedTypeHandle, rd));

                var reallyNestedTypeHandle = new Reader.TypeReferenceHandle(wr.GetRecordHandle(reallyNestedTypeRecord));
                var reallyNestedTypeHashCode = TypeHashingAlgorithms.ComputeNestedTypeHashCode(nestedTypeHashCode, TypeHashingAlgorithms.ComputeNameHashCode("ReallyNested"));
                Assert.Equal(reallyNestedTypeHashCode, MetadataTypeHashingAlgorithms.ComputeHashCode(reallyNestedTypeHandle, rd));
            }
        }
    }
}
