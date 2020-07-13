// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Need to use "extern alias", because both the writer and reader define types with same names.
extern alias writer;

using System;
using System.IO;
using System.Linq;

using Xunit;

using Reader = Internal.Metadata.NativeFormat;
using Writer = writer.Internal.Metadata.NativeFormat.Writer;

using TypeAttributes = System.Reflection.TypeAttributes;
using MethodAttributes = System.Reflection.MethodAttributes;
using CallingConventions = System.Reflection.CallingConventions;

namespace System.Private.Reflection.Metadata.Tests
{
    /// <summary>
    /// Tests for metadata roundtripping. We emit a metadata blob, and read it back
    /// to check if it has expected values.
    /// </summary>
    public class RoundTripTests
    {
        /// <summary>
        /// Builds a graph of simple metadata test data.
        /// </summary>
        private static Writer.ScopeDefinition BuildSimpleTestData()
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

            // System.ValueType type
            var valueTypeType = new Writer.TypeDefinition
            {
                BaseType = objectType,
                Flags = TypeAttributes.Public,
                Name = (Writer.ConstantStringValue)"ValueType",
                NamespaceDefinition = systemNamespaceDefinition
            };
            systemNamespaceDefinition.TypeDefinitions.Add(valueTypeType);

            // System.Void type
            var voidType = new Writer.TypeDefinition
            {
                BaseType = valueTypeType,
                Flags = TypeAttributes.Public,
                Name = (Writer.ConstantStringValue)"Void",
                NamespaceDefinition = systemNamespaceDefinition
            };
            systemNamespaceDefinition.TypeDefinitions.Add(voidType);

            // System.String type
            var stringType = new Writer.TypeDefinition
            {
                BaseType = objectType,
                Flags = TypeAttributes.Public,
                Name = (Writer.ConstantStringValue)"String",
                NamespaceDefinition = systemNamespaceDefinition
            };
            systemNamespaceDefinition.TypeDefinitions.Add(stringType);

            // System.Object..ctor() method
            var objectCtorMethod = new Writer.Method
            {
                Flags = MethodAttributes.Public
                    | MethodAttributes.RTSpecialName
                    | MethodAttributes.SpecialName,
                Name = (Writer.ConstantStringValue)".ctor",
                Signature = new Writer.MethodSignature
                {
                    CallingConvention = CallingConventions.HasThis,
                    ReturnType =  voidType,
                },
            };
            objectType.Methods.Add(objectCtorMethod);

            // System.String..ctor() method
            var stringCtorMethod = new Writer.Method
            {
                Flags = MethodAttributes.Public
                    | MethodAttributes.RTSpecialName
                    | MethodAttributes.SpecialName,
                Name = (Writer.ConstantStringValue)".ctor",
                Signature = new Writer.MethodSignature
                {
                    CallingConvention = CallingConventions.HasThis,
                    ReturnType =  voidType,
                },
            };
            stringType.Methods.Add(stringCtorMethod);

            return systemRuntimeScope;
        }

        [Fact]
        public static unsafe void TestSimpleRoundTripping()
        {
            var wr = new Writer.MetadataWriter();
            wr.ScopeDefinitions.Add(BuildSimpleTestData());
            var ms = new MemoryStream();
            wr.Write(ms);
            
            fixed (byte* pBuffer = ms.ToArray())
            {
                var rd = new Reader.MetadataReader((IntPtr)pBuffer, (int)ms.Length);

                // Validate the System.Runtime scope
                Reader.ScopeDefinitionHandle scopeHandle = rd.ScopeDefinitions.Single();
                Reader.ScopeDefinition systemRuntimeScope = scopeHandle.GetScopeDefinition(rd);
                Assert.Equal(4, systemRuntimeScope.MajorVersion);
                Assert.Equal("System.Runtime", systemRuntimeScope.Name.GetConstantStringValue(rd).Value);

                // Validate the root namespace and <Module> type
                Reader.NamespaceDefinition rootNamespace = systemRuntimeScope.RootNamespaceDefinition.GetNamespaceDefinition(rd);
                Assert.Equal(1, rootNamespace.TypeDefinitions.Count);
                Reader.TypeDefinition moduleType = rootNamespace.TypeDefinitions.Single().GetTypeDefinition(rd);
                Assert.Equal("<Module>", moduleType.Name.GetConstantStringValue(rd).Value);
                Assert.Equal(1, rootNamespace.NamespaceDefinitions.Count);

                // Validate the System namespace
                Reader.NamespaceDefinition systemNamespace = rootNamespace.NamespaceDefinitions.Single().GetNamespaceDefinition(rd);
                Assert.Equal(4, systemNamespace.TypeDefinitions.Count);
                foreach (var typeHandle in systemNamespace.TypeDefinitions)
                {
                    Reader.TypeDefinition type = typeHandle.GetTypeDefinition(rd);
                    string typeName = type.Name.GetConstantStringValue(rd).Value;

                    string baseTypeName = null;
                    if (!type.BaseType.IsNull(rd))
                    {
                        baseTypeName = type.BaseType.ToTypeDefinitionHandle(rd).GetTypeDefinition(rd).Name.GetConstantStringValue(rd).Value;
                    }

                    switch (typeName)
                    {
                        case "Object":
                            Assert.Null(baseTypeName);
                            Assert.Equal(1, type.Methods.Count);
                            break;
                        case "Void":
                            Assert.Equal("ValueType", baseTypeName);
                            Assert.Equal(0, type.Methods.Count);
                            break;
                        case "String":
                            Assert.Equal("Object", baseTypeName);
                            Assert.Equal(1, type.Methods.Count);
                            break;
                        case "ValueType":
                            Assert.Equal("Object", baseTypeName);
                            Assert.Equal(0, type.Methods.Count);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }

        [Fact]
        public static unsafe void TestCommonTailOptimization()
        {
            var wr = new Writer.MetadataWriter();
            wr.ScopeDefinitions.Add(BuildSimpleTestData());
            var ms = new MemoryStream();
            wr.Write(ms);

            fixed (byte* pBuffer = ms.ToArray())
            {
                var rd = new Reader.MetadataReader((IntPtr)pBuffer, (int)ms.Length);

                Reader.ScopeDefinitionHandle scopeHandle = rd.ScopeDefinitions.Single();
                Reader.ScopeDefinition systemRuntimeScope = scopeHandle.GetScopeDefinition(rd);
                Reader.NamespaceDefinition rootNamespace = systemRuntimeScope.RootNamespaceDefinition.GetNamespaceDefinition(rd);
                Reader.NamespaceDefinition systemNamespace =
                    rootNamespace.NamespaceDefinitions.AsEnumerable().Single(
                        ns => ns.GetNamespaceDefinition(rd).Name.StringEquals("System", rd)
                        ).GetNamespaceDefinition(rd);

                // This validates the common tail optimization.
                // Since both System.Object and System.String define a default constructor and the
                // records are structurally equivalent, there should only be one metadata record
                // representing a default .ctor in the blob.
                Reader.TypeDefinition objectType = systemNamespace.TypeDefinitions.AsEnumerable().Single(
                    t => t.GetTypeDefinition(rd).Name.StringEquals("Object", rd)
                    ).GetTypeDefinition(rd);
                Reader.TypeDefinition stringType = systemNamespace.TypeDefinitions.AsEnumerable().Single(
                    t => t.GetTypeDefinition(rd).Name.StringEquals("String", rd)
                    ).GetTypeDefinition(rd);

                Reader.MethodHandle objectCtor = objectType.Methods.Single();
                Reader.MethodHandle stringCtor = stringType.Methods.Single();

                Assert.True(objectCtor.Equals(stringCtor));
            }
        }
    }
}
