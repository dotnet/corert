// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public class StaticFieldLayoutTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;

        public StaticFieldLayoutTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
        }

        [Fact]
        public void TestNoPointers()
        {
            MetadataType t = _testModule.GetType("StaticFieldLayout", "NoPointers");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "int1":
                        Assert.Equal(0, field.Offset);
                        break;
                    case "byte1":
                        Assert.Equal(4, field.Offset);
                        break;
                    case "char1":
                        Assert.Equal(6, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestStillNoPointers()
        {
            //
            // Test that static offsets ignore instance fields preceeding them
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "StillNoPointers");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "bool1":
                        Assert.Equal(0, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestClassNoPointers()
        {
            //
            // Ensure classes behave the same as structs when containing statics
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "ClassNoPointers");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "int1":
                        Assert.Equal(0, field.Offset);
                        break;
                    case "byte1":
                        Assert.Equal(4, field.Offset);
                        break;
                    case "char1":
                        Assert.Equal(6, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestHasPointers()
        {
            //
            // Test a struct containing static types with pointers
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "HasPointers");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "string1":
                        Assert.Equal(8, field.Offset);
                        break;
                    case "class1":
                        Assert.Equal(16, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestMixPointersAndNonPointers()
        {
            //
            // Test that static fields with GC pointers get separate offsets from non-GC fields
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "MixPointersAndNonPointers");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "string1":
                        Assert.Equal(8, field.Offset);
                        break;
                    case "int1":
                        Assert.Equal(0, field.Offset);
                        break;
                    case "class1":
                        Assert.Equal(16, field.Offset);
                        break;
                    case "int2":
                        Assert.Equal(4, field.Offset);
                        break;
                    case "string2":
                        Assert.Equal(24, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestEnsureInheritanceResetsStaticOffsets()
        {
            //
            // Test that when inheriting a class with static fields, the derived slice's static fields
            // are again offset from 0
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "EnsureInheritanceResetsStaticOffsets");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "int3":
                        Assert.Equal(0, field.Offset);
                        break;
                    case "string3":
                        Assert.Equal(8, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }

        [Fact]
        public void TestStaticSelfRef()
        {
            //
            // Test that we can load a struct which has a static field referencing itself without
            // going into an infinite loop
            //

            MetadataType t = _testModule.GetType("StaticFieldLayout", "StaticSelfRef");

            foreach (var field in t.GetFields())
            {
                if (!field.IsStatic)
                    continue;

                switch (field.Name)
                {
                    case "selfRef1":
                        Assert.Equal(0, field.Offset);
                        break;
                    default:
                        throw new Exception(field.Name);
                }
            }
        }
    }
}
