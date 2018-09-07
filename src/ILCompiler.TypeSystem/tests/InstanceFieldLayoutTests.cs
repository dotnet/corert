// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Xunit;

namespace TypeSystemTests
{
    public class InstanceFieldLayoutTests
    {
        TestTypeSystemContext _context;
        ModuleDesc _testModule;
        ModuleDesc _ilTestModule;

        public InstanceFieldLayoutTests()
        {
            _context = new TestTypeSystemContext(TargetArchitecture.X64);
            var systemModule = _context.CreateModuleForSimpleName("CoreTestAssembly");
            _context.SetSystemModule(systemModule);

            _testModule = systemModule;
            _ilTestModule = _context.CreateModuleForSimpleName("ILTestAssembly");
        }

        [Fact]
        public void TestExplicitLayout()
        {
            MetadataType t = _testModule.GetType("Explicit", "Class1");

            // With 64bit, there should be 8 bytes for the System.Object EE data pointer +
            // 10 bytes up until the offset of the char field + the char size of 2 + we 
            // round up the whole instance size to the next pointer size (+4) = 24
            Assert.Equal(24, t.InstanceByteCount.AsInt);

            foreach (var field in t.GetFields())
            {
                if (field.IsStatic)
                    continue;

                if (field.Name == "Bar")
                {
                    // Bar has explicit offset 4 and is in a class (with S.O size overhead of <pointer size>)
                    // Therefore it should have offset 4 + 8 = 12
                  Assert.Equal(12, field.Offset.AsInt);
                }
                else if (field.Name == "Baz")
                {
                    // Baz has explicit offset 10. 10 + 8 = 18
                    Assert.Equal(18, field.Offset.AsInt);
                }
                else
                {
                    Assert.True(false);
                }
            }
        }

        [Fact]
        public void TestExplicitLayoutThatIsEmpty()
        {
            var explicitEmptyClassType = _testModule.GetType("Explicit", "ExplicitEmptyClass");

            // ExplicitEmpty class has 8 from System.Object overhead = 8
            Assert.Equal(8, explicitEmptyClassType.InstanceByteCount.AsInt);

            var explicitEmptyStructType = _testModule.GetType("Explicit", "ExplicitEmptyStruct");

            // ExplicitEmpty class has 0 bytes in it... so instance field size gets pushed up to 1.
            Assert.Equal(1, explicitEmptyStructType.InstanceFieldSize.AsInt);
        }

        [Fact]
        public void TestExplicitTypeLayoutWithSize()
        {
            var explicitSizeType = _testModule.GetType("Explicit", "ExplicitSize");
            Assert.Equal(48, explicitSizeType.InstanceByteCount.AsInt);
        }

        [Fact]
        public void TestExplicitTypeLayoutWithInheritance()
        {
            MetadataType class2Type = _testModule.GetType("Explicit", "Class2");

            // Class1 has size 24 which Class2 inherits from.  Class2 adds a byte at offset 20, so + 21
            // = 45, rounding up to the next pointer size = 48
            Assert.Equal(48, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                if (f.Name == "Lol")
                {
                    // First field after base class, with offset 0 so it should lie on the byte count of 
                    // the base class = 24
                    Assert.Equal(24, f.Offset.AsInt);
                }
                else if (f.Name == "Omg")
                {
                    // Offset 20 from base class byte count = 44
                    Assert.Equal(44, f.Offset.AsInt);
                }
                else
                {
                    Assert.True(false);
                }
            }
        }

        [Fact]
        public void TestSequentialTypeLayout()
        {
            MetadataType class1Type = _testModule.GetType("Sequential", "Class1");

            // Byte count
            // Base Class       8
            // MyInt            4
            // MyBool           1 + 1 padding
            // MyChar           2
            // MyString         8
            // MyByteArray      8
            // MyClass1SelfRef  8
            // -------------------
            //                  40 (0x28)
            Assert.Equal(0x28, class1Type.InstanceByteCount.AsInt);

            foreach (var f in class1Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyInt":
                        Assert.Equal(0x8, f.Offset.AsInt);
                        break;
                    case "MyBool":
                        Assert.Equal(0xC, f.Offset.AsInt);
                        break;
                    case "MyChar":
                        Assert.Equal(0xE, f.Offset.AsInt);
                        break;
                    case "MyString":
                        Assert.Equal(0x10, f.Offset.AsInt);
                        break;
                    case "MyByteArray":
                        Assert.Equal(0x18, f.Offset.AsInt);
                        break;
                    case "MyClass1SelfRef":
                        Assert.Equal(0x20, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestSequentialTypeLayoutInheritance()
        {
            MetadataType class2Type = _testModule.GetType("Sequential", "Class2");

            // Byte count
            // Base Class       40
            // MyInt2           4 + 4 byte padding to make class size % pointer size == 0
            // -------------------
            //                  48 (0x30)
            Assert.Equal(0x30, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyInt2":
                        Assert.Equal(0x28, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestSequentialTypeLayoutStruct()
        {
            MetadataType struct0Type = _testModule.GetType("Sequential", "Struct0");

            // Byte count
            // bool     b1      1
            // bool     b2      1
            // bool     b3      1 + 1 padding for int alignment
            // int      i1      4
            // string   s1      8
            // -------------------
            //                  16 (0x10)
            Assert.Equal(0x10, struct0Type.InstanceByteCount.AsInt);

            foreach (var f in struct0Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "b1":
                        Assert.Equal(0x0, f.Offset.AsInt);
                        break;
                    case "b2":
                        Assert.Equal(0x1, f.Offset.AsInt);
                        break;
                    case "b3":
                        Assert.Equal(0x2, f.Offset.AsInt);
                        break;
                    case "i1":
                        Assert.Equal(0x4, f.Offset.AsInt);
                        break;
                    case "s1":
                        Assert.Equal(0x8, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        // Test that when a struct is used as a field, we use its instance byte size as the size (ie, treat it
        // as a value type) and not a pointer size.
        public void TestSequentialTypeLayoutStructEmbedded()
        {
            MetadataType struct1Type = _testModule.GetType("Sequential", "Struct1");

            // Byte count
            // struct   MyStruct0   16
            // bool     MyBool      1
            // -----------------------
            //                      24 (0x18)
            Assert.Equal(0x18, struct1Type.InstanceByteCount.AsInt);

            foreach (var f in struct1Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyStruct0":
                        Assert.Equal(0x0, f.Offset.AsInt);
                        break;
                    case "MyBool":
                        Assert.Equal(0x10, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayout()
        {
            MetadataType class1Type = _testModule.GetType("Auto", "Class1_7BytesRemaining");

            // Byte count
            // Base Class       8
            // MyByteArray      8
            // MyString1        8
            // MyDouble         8
            // MyLong           8
            // MyInt            4
            // MyChar1          2
            // MyBool1          1
            // MyBool2          1
            // MyStruct0        1 + 7 byte padding to make class size % pointer size == 0
            // -------------------
            //                  56 (0x38)
            Assert.Equal(0x38, class1Type.InstanceByteCount.AsInt);

            foreach (var f in class1Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyByteArray":
                        Assert.Equal(0x08, f.Offset.AsInt);
                        break;
                    case "MyString1":
                        Assert.Equal(0x10, f.Offset.AsInt);
                        break;
                    case "MyDouble":
                        Assert.Equal(0x18, f.Offset.AsInt);
                        break;
                    case "MyLong":
                        Assert.Equal(0x20, f.Offset.AsInt);
                        break;
                    case "MyInt":
                        Assert.Equal(0x28, f.Offset.AsInt);
                        break;
                    case "MyChar1":
                        Assert.Equal(0x2C, f.Offset.AsInt);
                        break;
                    case "MyBool1":
                        Assert.Equal(0x2E, f.Offset.AsInt);
                        break;
                    case "MyBool2":
                        Assert.Equal(0x2F, f.Offset.AsInt);
                        break;
                    case "MyStruct0":
                        Assert.Equal(0x30, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayoutInheritanceClass2()
        {
            MetadataType class2Type = _testModule.GetType("Auto", "Class2_3BytesRemaining");

            // Byte count
            // Base Class       49 (unaligned) + 3 byte padding to align the int
            // MyInt2           4
            // MyString2        8
            // MyChar2          2
            // MyChar3          2
            // MyBool3          1 + 3 byte padding to align the start of type fields
            // -------------------
            //                  72 (0x48)
            Assert.Equal(0x48, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyInt2":
                        Assert.Equal(0x34, f.Offset.AsInt);
                        break;
                    case "MyString2":
                        Assert.Equal(0x38, f.Offset.AsInt);
                        break;
                    case "MyChar2":
                        Assert.Equal(0x40, f.Offset.AsInt);
                        break;
                    case "MyChar3":
                        Assert.Equal(0x42, f.Offset.AsInt);
                        break;
                    case "MyBool3":
                        Assert.Equal(0x44, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayoutInheritanceClass3()
        {
            MetadataType class2Type = _testModule.GetType("Auto", "Class3");

            // Byte count
            // Base Class       49 (unaligned) + 3 byte padding to align int
            // MyInt3           4
            // MyString3        8
            // -------------------
            //                  64 (0x40)
            Assert.Equal(0x40, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyInt3":
                        Assert.Equal(0x34, f.Offset.AsInt);
                        break;
                    case "MyString3":
                        Assert.Equal(0x38, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayoutInheritanceClass4()
        {
            MetadataType class2Type = _testModule.GetType("Auto", "Class4");

            // Byte count
            // Base Class       49 (unaligned) + 3 byte padding to align char
            // MyChar4          2
            // MyChar42         2
            // MyString4        8
            // MyChar43         2 + 6 byte padding to align the start of type fields
            // -------------------
            //                  72 (0x48)
            Assert.Equal(0x48, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyChar4":
                        Assert.Equal(0x34, f.Offset.AsInt);
                        break;
                    case "MyChar42":
                        Assert.Equal(0x36, f.Offset.AsInt);
                        break;
                    case "MyString4":
                        Assert.Equal(0x38, f.Offset.AsInt);
                        break;
                    case "MyChar43":
                        Assert.Equal(0x40, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayoutInheritanceClass5()
        {
            MetadataType class2Type = _testModule.GetType("Auto", "Class5");

            // Byte count
            // Base Class       49 (unaligned) + 3 byte padding to align char
            // MyChar5          2
            // MyBool5          1 + 1 byte padding to make class size % pointer size == 0
            // MyString5        8
            // MyLong5          8
            // -------------------
            //                  72 (0x48)
            Assert.Equal(0x48, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyChar5":
                        Assert.Equal(0x34, f.Offset.AsInt);
                        break;
                    case "MyBool4":
                        Assert.Equal(0x36, f.Offset.AsInt);
                        break;
                    case "MyString5":
                        Assert.Equal(0x38, f.Offset.AsInt);
                        break;
                    case "MyLong5":
                        Assert.Equal(0x40, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestAutoTypeLayoutInheritanceClass6()
        {
            MetadataType class2Type = _testModule.GetType("Auto", "Class6");

            // Byte count
            // Base Class       69 (unaligned) + 3 byte padding to align int
            // MyInt6           4
            // MyChar6          2 + 2 byte padding to make class size % pointer size == 0
            // -------------------
            //                  80 (0x50)
            Assert.Equal(0x50, class2Type.InstanceByteCount.AsInt);

            foreach (var f in class2Type.GetFields())
            {
                if (f.IsStatic)
                    continue;

                switch (f.Name)
                {
                    case "MyInt6":
                        Assert.Equal(0x48, f.Offset.AsInt);
                        break;
                    case "MyChar6":
                        Assert.Equal(0x4C, f.Offset.AsInt);
                        break;
                    default:
                        Assert.True(false);
                        break;
                }
            }
        }

        [Fact]
        public void TestTypeContainsGCPointers()
        {
            MetadataType type = _testModule.GetType("ContainsGCPointers", "NoPointers");
            Assert.False(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "StillNoPointers");
            Assert.False(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "ClassNoPointers");
            Assert.False(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "HasPointers");
            Assert.True(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "FieldHasPointers");
            Assert.True(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "ClassHasPointers");
            Assert.True(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "BaseClassHasPointers");
            Assert.True(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "ClassHasIntArray");
            Assert.True(type.ContainsGCPointers);

            type = _testModule.GetType("ContainsGCPointers", "ClassHasArrayOfClassType");
            Assert.True(type.ContainsGCPointers);
        }

        [Fact]
        public void TestByRefLikeTypes()
        {
            {
                DefType type = _context.GetWellKnownType(WellKnownType.TypedReference);
                Assert.True(type.IsByRefLike);
            }

            {
                DefType type = _context.GetWellKnownType(WellKnownType.ByReferenceOfT);
                Assert.True(type.IsByRefLike);
            }

            {
                DefType type = _testModule.GetType("IsByRefLike", "ByRefLikeStruct");
                Assert.True(type.IsByRefLike);
            }

            {
                DefType type = _testModule.GetType("IsByRefLike", "NotByRefLike");
                Assert.False(type.IsByRefLike);
            }
        }

        [Fact]
        public void TestInvalidByRefLikeTypes()
        {
            {
                DefType type = _ilTestModule.GetType("IsByRefLike", "InvalidClass1");
                Assert.Throws<TypeSystemException.TypeLoadException>(() => type.ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields));
            }

            {
                DefType type = _ilTestModule.GetType("IsByRefLike", "InvalidClass2");
                Assert.Throws<TypeSystemException.TypeLoadException>(() => type.ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields));
            }

            {
                DefType type = _ilTestModule.GetType("IsByRefLike", "InvalidStruct");
                Assert.Throws<TypeSystemException.TypeLoadException>(() => type.ComputeInstanceLayout(InstanceLayoutKind.TypeAndFields));
            }
        }
    }
}
