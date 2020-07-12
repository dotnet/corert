// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#pragma warning disable 414
#pragma warning disable 67
#pragma warning disable 3009
#pragma warning disable 3016
#pragma warning disable 3001
#pragma warning disable 3015
#pragma warning disable 169
#pragma warning disable 649

[assembly: SampleMetadata.SimpleStringCa("assembly")]

public class SimpleTopLevelClass
{
    public void MethodWithDefaultParamValue(int a1 = 32)
    {
    }
}

namespace SampleMetadata
{
    public class Outer
    {
        public class Inner
        {
            public class ReallyInner
            {
            }
        }
    }

    public class Outer2
    {
        public class Inner2
        {
            protected class ProtectedInner
            {
            }

            internal class InternalInner
            {
            }

            private class PrivateInner
            {
            }
        }

        private class PrivateInner2
        {
            public class ReallyInner2
            {
            }
        }
    }

    internal class Hidden
    {
        public class PublicInsideHidden
        {
        }
    }

    public class SimpleGenericType<T>
    {
    }

    [SimpleStringCa("type")]
    public class GenericTypeWithThreeParameters<X, Y, Z>
    {
        public GenericTypeWithThreeParameters(int x)
        {
        }

        [SimpleStringCa("ctor")]
        private GenericTypeWithThreeParameters(String s)
        {
        }

        [return: SimpleStringCa("return")]
        public void SimpleMethod()
        {
        }

        [SimpleStringCa("method")]
        public M SimpleGenericMethod<M, N>(X arg1, N arg2)
        {
            throw null;
        }

        public X SimpleReadOnlyProperty { get { throw null; } }

        [SimpleStringCa("property")]
        public Y SimpleMutableProperty
        {
            get
            {
                throw null;
            }
            [SimpleStringCa("setter")]
            set
            {
                throw null;
            }
        }

        [IndexerName("SimpleIndexedProperty")]
        public X this[int i1, Y i2] { get { throw null; } }

        public int MyField1;

        [SimpleStringCa("field")]
        public static String MyField2;

        private double MyField3 = 0.0;

        [SimpleStringCa("event")]
        public event Action SimpleEvent
        {
            add
            {
                throw null;
            }

            remove
            {
                throw null;
            }
        }

        [SimpleStringCa("nestedtype")]
        public class InnerType
        {
        }

        public class InnerGenericType<W>
        {
        }

        static GenericTypeWithThreeParameters()
        {
        }
    }

    public class Derived : SampleMetadata.Extra.Deep.BaseContainer.SubmergedBaseClass
    {
    }

    public interface IFoo
    {
    }

    public interface IBar
    {
    }

    public interface IComplex : IFoo, IBar, IList, IEnumerable
    {
    }

    public class CFoo : IFoo
    {
    }

    public class GenericTypeWithConstraints<A, B, C, D, E, F, G, H, AA, BB, CC, DD, EE, FF, GG, HH>
        where B : CFoo, IList
        where C : IList
        where D : struct
        where E : class
        where F : new()
        where G : class, new()
        where H : IList, new()
        where AA : A
        where BB : B
        where CC : C
        // where DD : D // cannot use a "struct"-constrainted generic parameter as a constraint.
        where EE : E
        where FF : F
        where GG : G
        where HH : H
    {
    }

    public struct SimpleStruct
    {
    }

    public struct SimpleGenericStruct<T>
    {
    }

    public interface ISimpleGenericInterface<T>
    {
    }

    public class MethodContainer<T>
    {
        public String Moo(int i, T g, String[] s, IEnumerable<String> e, ref double d, out Object o)
        {
            throw null;
        }

        [IndexerName("SimpleIndexedSetterOnlyProperty")]
        public int this[String i1, T i2] { set { throw null; } }
    }

    public enum SimpleEnum
    {
        Red = 1,
        Blue = 2,
        Green = 3,
        Green_a = 3,
        Green_b = 3,
    }

    public enum ByteEnum : byte
    {
        Min = 0,
        One = 1,
        Two = 2,
        Max = 0xff,
    }

    public enum SByteEnum : sbyte
    {
        Min = -128,
        One = 1,
        Two = 2,
        Max = 127,
    }

    public enum UInt16Enum : ushort
    {
        Min = 0,
        One = 1,
        Two = 2,
        Max = 0xffff,
    }

    public enum Int16Enum : short
    {
        Min = unchecked((short)0x8000),
        One = 1,
        Two = 2,
        Max = 0x7fff,
    }

    public enum UInt32Enum : uint
    {
        Min = 0,
        One = 1,
        Two = 2,
        Max = 0xffffffff,
    }

    public enum Int32Enum : int
    {
        Min = unchecked((int)0x80000000),
        One = 1,
        Two = 2,
        Max = 0x7fffffff,
    }

    public enum UInt64Enum : ulong
    {
        Min = 0,
        One = 1,
        Two = 2,
        Max = 0xffffffffffffffff,
    }

    public enum Int64Enum : long
    {
        Min = unchecked((long)0x8000000000000000L),
        One = 1,
        Two = 2,
        Max = 0x7fffffffffffffff,
    }

    public class SimpleCaAttribute : Attribute
    {
        public SimpleCaAttribute()
        {
        }
    }

    public class SimpleStringCaAttribute : Attribute
    {
        public SimpleStringCaAttribute(String s)
        {
        }
    }

    public class SimpleCaWithBigCtorAttribute : Attribute
    {
        public SimpleCaWithBigCtorAttribute(bool b, char c, float f, double d, sbyte sb, short sh, int i, long l, byte by, ushort us, uint ui, ulong ul, String s, Type t)
        {
        }
    }

    public class SimpleArrayCaAttribute : Attribute
    {
        public SimpleArrayCaAttribute(bool[] b) { }
        public SimpleArrayCaAttribute(char[] b) { }
        public SimpleArrayCaAttribute(float[] b) { }
        public SimpleArrayCaAttribute(double[] b) { }
        public SimpleArrayCaAttribute(sbyte[] b) { }
        public SimpleArrayCaAttribute(short[] b) { }
        public SimpleArrayCaAttribute(int[] b) { }
        public SimpleArrayCaAttribute(long[] b) { }
        public SimpleArrayCaAttribute(byte[] b) { }
        public SimpleArrayCaAttribute(ushort[] b) { }
        public SimpleArrayCaAttribute(uint[] b) { }
        public SimpleArrayCaAttribute(ulong[] b) { }
        public SimpleArrayCaAttribute(String[] b) { }
        public SimpleArrayCaAttribute(Type[] b) { }
    }

    public class SimpleObjectArrayCaAttribute : Attribute
    {
        public SimpleObjectArrayCaAttribute(object[] o) { }
    }

    public class SimpleEnumCaAttribute : Attribute
    {
        public SimpleEnumCaAttribute(SimpleEnum e) { }
        public SimpleEnumCaAttribute(SimpleEnum[] e) { }
    }

    public class AnnoyingSpecialCaseAttribute : Attribute
    {
        public AnnoyingSpecialCaseAttribute(Object o)
        {
        }

        public Object Oops;
    }

    public class SimpleCaWithNamedParametersAttribute : Attribute
    {
        public SimpleCaWithNamedParametersAttribute(int i, String s)
        {
        }

        public double DParameter;

        public Type TParameter { get; set; }
    }

    [SimpleCaWithBigCtor(true, 'c', (float)1.5f, 1.5, (sbyte)(-2), (short)(-2), (int)(-2), (long)(-2), (byte)0xfe, (ushort)0xfedc, (uint)0xfedcba98, (ulong)0xfedcba9876543210, "Hello", typeof(IEnumerable<String>))]
    public class CaHolder
    {
        [AnnoyingSpecialCase(SimpleEnum.Blue, Oops = SimpleEnum.Red)]
        public class ObjectCa { }

        [SimpleObjectArrayCa(new object[] { SimpleEnum.Red, 123 })]
        public class ObjectArrayCa { }

        [SimpleEnumCa(SimpleEnum.Green)]
        public class EnumCa { }

        [SimpleEnumCa(new SimpleEnum[] { SimpleEnum.Green, SimpleEnum.Red })]
        public class EnumArray { }

        [SimpleCaWithNamedParameters(42, "Yo", DParameter = 2.3, TParameter = typeof(IList<String>))]
        public class Named { }

        [SimpleArrayCa(new bool[] { true, false, true, true, false, false, false, true })]
        public class BoolArray { }

        [SimpleArrayCa(new char[] { 'a', 'b', 'c', 'd', 'e' })]
        public class CharArray { }

        [SimpleArrayCa(new byte[] { 1, 2, 0xfe, 0xff })]
        public class ByteArray { }

        [SimpleArrayCa(new sbyte[] { 1, 2, -2, -1 })]
        public class SByteArray { }

        [SimpleArrayCa(new ushort[] { 1, 2, 0xfedc })]
        public class UShortArray { }

        [SimpleArrayCa(new short[] { 1, 2, -2, -1 })]
        public class ShortArray { }

        [SimpleArrayCa(new uint[] { 1, 2, 0xfedcba98 })]
        public class UIntArray { }

        [SimpleArrayCa(new int[] { 1, 2, -2, -1 })]
        public class IntArray { }

        [SimpleArrayCa(new ulong[] { 1, 2, 0xfedcba9876543210 })]
        public class ULongArray { }

        [SimpleArrayCa(new long[] { 1, 2, -2, -1 })]
        public class LongArray { }

        [SimpleArrayCa(new float[] { 1.2f, 3.5f })]
        public class FloatArray { }

        [SimpleArrayCa(new double[] { 1.2, 3.5 })]
        public class DoubleArray { }

        [SimpleArrayCa(new String[] { "Hello", "There" })]
        public class StringArray { }

        [SimpleArrayCa(new Type[] { typeof(Object), typeof(String), null })]
        public class TypeArray { }
    }

    public class DefaultValueHolder
    {
        public static void LotsaDefaults(
            bool b = true,
            char c = 'a',
            sbyte sb = -2,
            byte by = 0xfe,
            short sh = -2,
            ushort ush = 0xfedc,
            int i = -2,
            uint ui = 0xfedcba98,
            long l = -2,
            ulong ul = 0xfedcba9876543210,
            float f = 1.5f,
            double d = 1.5,
            String s = "Hello",
            String sn = null,
            Object o = null,
            SimpleEnum e = SimpleEnum.Blue
            )
        {
        }
    }

    public class SimpleIntCustomAttributeAttribute : Attribute
    {
        public SimpleIntCustomAttributeAttribute(int a1)
        {
        }
    }

    [SimpleIntCustomAttribute(32)]
    public class SimpleTypeWithCustomAttribute
    {
        [SimpleIntCustomAttribute(64)]
        public void SimpleMethodWithCustomAttribute()
        {
        }
    }

    public interface IMethodImplTest
    {
        void MethodImplFunc();
    }

    public class MethodImplTest : IMethodImplTest
    {
        void IMethodImplTest.MethodImplFunc()
        {
        }
    }

    public class GenericOutside<S>
    {
        public class Inside
        {
            public class ReallyInside<I>
            {
                public S TypeS_Field;
                public I TypeI_Field;

                public class Really2Inside
                {
                    public class Really3Inside<D>
                    {
                        public class Really4Inside<O>
                        {
                            public GenericTypeWithThreeParameters<S, I, D> TypeSID_Field;
                            public GenericTypeWithThreeParameters<S, I, O> TypeSIO_Field;
                        }
                    }
                }
            }
        }
    }

    public class NonGenericOutside
    {
        public class GenericInside<T>
        {
        }
    }

    public static class SimpleStaticClass
    {
        public class NestedInsideSimpleStatic
        {
        }

        public static void Foo(int x)
        {
        }
    }

    public static class SimpleGenericStaticClass<T>
    {
        public class NestedInsideSimpleStatic
        {
        }

        public static void Foo(T x)
        {
        }
    }
}

namespace SampleMetadata.Extra.Deep
{
    public class BaseContainer
    {
        [SimpleStringCa("base")]
        public class SubmergedBaseClass
        {
        }
    }
}

namespace SampleMetadata
{
    // Create two types with the exact same shape. Ensure that MdTranform and Reflection can
    // distinguish the members.

    public class DoppleGanger1
    {
        public DoppleGanger1()
        {
        }

        public void Moo()
        {
        }

        public int Field;

        public int Prop { get; set; }

        public event Action Event
        {
            add
            {
                throw null;
            }

            remove
            {
                throw null;
            }
        }

        public class NestedType
        {
        }
    }

    public class DoppleGanger2
    {
        public DoppleGanger2()
        {
        }

        public void Moo()
        {
        }

        public int Field;

        public int Prop { get; set; }

        public event Action Event
        {
            add
            {
                throw null;
            }

            remove
            {
                throw null;
            }
        }

        public class NestedType
        {
        }
    }
}

namespace SampleMetadata
{
    // Create two types with "identical" methods.

    public class ProtoType
    {
        [SimpleStringCa("1")]
        public void Meth()
        {
        }

        public void Foo([SimpleStringCa("p1")] int twin, Object differentTypes)
        {
        }
    }

    public class Similar
    {
        [SimpleStringCa("2")]
        public void Meth()
        {
        }

        public void Foo([SimpleStringCa("p2")] int twin, String differentTypes)
        {
        }
    }
}

namespace SampleMetadata
{
    public class MethodFamilyIsInstance
    {
        public void MethodFamily()
        {
        }
    }

    public class MethodFamilyIsStatic
    {
        public static void MethodFamily()
        {
        }
    }

    public class MethodFamilyIsPrivate
    {
        private void MethodFamily()
        {
        }
    }

    public class MethodFamilyIsGeneric
    {
        public void MethodFamily<M1>()
            where M1 : IEquatable<M1>
        {
        }
    }

    public class MethodFamilyIsAlsoGeneric
    {
        public void MethodFamily<M2>()
            where M2 : IEquatable<M2>
        {
        }
    }

    public class MethodFamilyIsUnary
    {
        public void MethodFamily(int m)
        {
        }
    }

    public class MethodFamilyReturnsSomething
    {
        public String MethodFamily()
        {
            return null;
        }
    }

    public class MethodFamilyHasCtor1
    {
        [Circular(1)]
        public void MethodFamily()
        {
        }
    }

    public class MethodFamilyHasCtor2
    {
        [Circular(2)]
        public void MethodFamily()
        {
        }
    }
}

namespace SampleMetadata
{
    [Circular(1)]
    public class CircularAttribute : Attribute
    {
        [Circular(2)]
        public CircularAttribute([Circular(3)] int x)
        {
        }
    }
}

namespace SampleMetadata.NS1
{
    public class Twin
    {
    }
}

namespace SampleMetadata.NS2
{
    public class Twin
    {
    }
}

namespace SampleMetadata
{
    public class FieldInvokeSampleBase
    {
        public String InheritedField;
    }

    public class FieldInvokeSample : FieldInvokeSampleBase
    {
        public String InstanceField;

        public static String StaticField;

        public const int LiteralInt32Field = 42;

        public const SimpleEnum LiteralEnumField = SimpleEnum.Green;

        public const String LiteralStringField = "Hello";

        public const Object LiteralNullField = null;
    }

    public class PropertyInvokeSample
    {
        public PropertyInvokeSample(String name)
        {
            this.Name = name;
        }

        public String InstanceProperty
        {
            get;
            set;
        }
        public static String StaticProperty
        {
            get;
            set;
        }

        public String ReadOnlyInstanceProperty
        {
            get { return "rip:"; }
        }
        public static String ReadOnlyStaticProperty
        {
            get { return "rsp"; }
        }

        [IndexerName("IndexedProperty")]
        public String this[int i1, int i2]
        {
            get
            {
                return _indexed;
            }

            set
            {
                _indexed = value;
            }
        }

        private String _indexed;

        public String Name;
    }

    public class MethodInvokeSample
    {
        public MethodInvokeSample(String name)
        {
            this.Name = name;
        }

        public void InstanceMethod(int x, String s)
        {
        }

        public void InstanceMethodWithSingleParameter(String s)
        {
        }

        public static void StaticMethod(int x, String s)
        {
        }

        public String InstanceFunction(int x, String s)
        {
            throw null;
        }

        public static String StaticFunction(int x, String s)
        {
            throw null;
        }

        public static String LastStaticCall { get; set; }
        public String LastCall { get; set; }
        public String Name;
    }
}

namespace SampleMetadataEx
{
    public enum Color
    {
        Red = 1,
        Green = 2,
        Blue = 3,
    }

    //=========================================================================================
    //=========================================================================================
    public class DataTypeTestAttribute : Attribute
    {
        public DataTypeTestAttribute(int i, String s, Type t, Color c, int[] iArray, String[] sArray, Type[] tArray, Color[] cArray)
        {
            this.I = i;
            this.S = s;
            this.T = t;
            this.C = c;
            this.IArray = iArray;
            this.SArray = sArray;
            this.TArray = tArray;
            this.CArray = cArray;
        }

        public int I { get; private set; }
        public String S { get; private set; }
        public Type T { get; private set; }
        public Color C { get; private set; }

        public int[] IArray { get; private set; }
        public String[] SArray { get; private set; }
        public Type[] TArray { get; private set; }
        public Color[] CArray { get; private set; }
    }

    //=========================================================================================
    // Named arguments.
    //=========================================================================================
    public class NamedArgumentsAttribute : Attribute
    {
        public NamedArgumentsAttribute()
        {
        }

        public int F1;
        public Color F2;
        public int P1 { get; set; }
        public Color P2 { get; set; }
    }

    //=========================================================================================
    // The annoying special case where the formal parameter type is Object.
    //=========================================================================================
    public class ObjectTypeTestAttribute : Attribute
    {
        public ObjectTypeTestAttribute(Object o)
        {
            this.O = o;
        }

        public Object O { get; private set; }

        public Object F;

        public Object P { get; set; }
    }

    public class BaseAttribute : Attribute
    {
        public BaseAttribute(String s)
        {
            S = s;
        }

        public BaseAttribute(String s, int i)
        {
        }

        public int F;

        public int P { get; set; }

        public String S { get; private set; }

        public sealed override String ToString()
        {
            throw null;
        }
    }

    public class MidAttribute : BaseAttribute
    {
        public MidAttribute(String s)
            : base(s)
        {
        }

        public MidAttribute(String s, int i)
            : base(s, i)
        {
        }

        public new int F;

        public new int P { get; set; }
    }

    public class DerivedAttribute : MidAttribute
    {
        public DerivedAttribute(String s)
            : base(s)
        {
        }

        public new int F;

        public new int P { get; set; }
    }

    public class NonInheritableAttribute : BaseAttribute
    {

        public NonInheritableAttribute(String s)
            : base(s)
        {
        }
    }

    public class BaseAmAttribute : Attribute
    {
        public BaseAmAttribute(String s)
        {
            S = s;
        }

        public BaseAmAttribute(String s, int i)
        {
        }

        public String S { get; private set; }

        public sealed override String ToString()
        {
            throw null;
        }
    }

    public class MidAmAttribute : BaseAmAttribute
    {
        public MidAmAttribute(String s)
            : base(s)
        {
        }
    }

    public class DerivedAmAttribute : MidAmAttribute
    {
        public DerivedAmAttribute(String s)
            : base(s)
        {
        }
    }
}

namespace SampleMetadataEx
{
    public class CaHolder
    {
        [DataTypeTest(
            42,
            "FortyTwo",
            typeof(IList<String>),
            Color.Green,
            new int[] { 1, 2, 3 },
            new String[] { "One", "Two", "Three" },
            new Type[] { typeof(int), typeof(String) },
            new Color[] { Color.Red, Color.Blue }
            )]
        public int DataTypeTest = 1;

        [NamedArguments(F1 = 42, F2 = Color.Blue, P1 = 77, P2 = Color.Green)]
        public int NamedArgumentsTest = 1;

        [ObjectTypeTest(Color.Red, F = Color.Blue, P = Color.Green)]
        public int ObjectTest = 1;

        [Base("B", F = 1, P = 2)]
        public int BaseTest = 1;

        [Mid("M", F = 5, P = 6)]
        public int MidTest = 1;

        [Derived("D", F = 8, P = 9)]
        public int DerivedTest = 1;
    }

    [BaseAttribute("[Base]SearchType.Field")]
    [MidAttribute("[Mid]SearchType.Field")]
    [DerivedAttribute("[Derived]SearchType.Field")]
    public class SearchType1
    {
        public int Field;
    }

    [BaseAttribute("[Base]B")]
    [MidAttribute("[Mid]B", 42)]   // This is hidden by down-level MidAttributes, even though the down-level MidAttributes are using a different .ctor
    [DerivedAttribute("[Derived]B")]
    [BaseAmAttribute("[BaseAm]B")]
    [MidAmAttribute("[MidAm]B")]
    [DerivedAmAttribute("[DerivedAm]B")]
    public abstract class B
    {
        [BaseAm("[BaseAm]B.M1()")]
        public virtual void M1([BaseAm("[BaseAm]B.M1.x")] int x) { }

        [BaseAm("[BaseAm]B.M2()")]
        public void M2([BaseAm("[BaseAm]B.M2.x")] int x) { }

        [BaseAm("[BaseAm]B.P1")]
        public virtual int P1 { get; set; }

        [BaseAm("[BaseAm]B.P2")]
        public int P2 { get; set; }

        [BaseAm("[BaseAm]B.E1")]
        public virtual event Action E1 { add { } remove { } }

        [BaseAm("[BaseAm]B.E2")]
        public event Action E2 { add { } remove { } }
    }

    [NonInheritable("[Noninheritable]M")]
    [MidAttribute("[Mid]M")]
    [DerivedAttribute("[Derived]M")]
    [MidAmAttribute("[MidAm]M")]
    [DerivedAmAttribute("[DerivedAm]M")]
    public abstract class M : B
    {
        public override void M1(int x) { }
        public override int P1 { get; set; }
        public override event Action E1 { add { } remove { } }
    }

    [DerivedAttribute("[Derived]D")]
    [DerivedAmAttribute("[DerivedAm]D")]
    public abstract class D : M, ID
    {
        public void Foo() { }

        public new virtual void M1(int x) { }
        public new void M2(int x) { }

        public new virtual int P1 { get; set; }
        public new int P2 { get; set; }

        public new virtual event Action E1 { add { } remove { } }
        public new event Action E2 { add { } remove { } }
    }

    // These attributes won't be inherited as the CA inheritance only walks base classes, not interfaces.
    [BaseAttribute("[Base]ID")]
    [MidAttribute("[Mid]ID")]
    [DerivedAttribute("[Derived]ID")]
    [BaseAmAttribute("[BaseAm]ID")]
    [MidAmAttribute("[MidAm]ID")]
    [DerivedAmAttribute("[DerivedAm]ID")]
    public interface ID
    {
        [BaseAmAttribute("[BaseAm]ID.Foo()")]
        void Foo();
    }
}

namespace SampleMetadataRex
{
    public class DelegateBinding
    {
        public static void M1()
        {
        }

        public static void M2()
        {
        }
    }

    public class MethodLookup
    {
        public void Moo(int x) { }
        public void Moo(String s) { }
        public void Moo(String s, int x) { }
    }

    public interface IFoo
    {
        void Foo();
        void Hidden();
        int FooProp { get; set; }
        event Action FooEvent;
    }

    public interface IBar : IFoo
    {
        void Bar();
        new void Hidden();
    }

    public abstract class CBar : IBar
    {
        public void Bar()
        {
        }

        public void Hidden()
        {
        }

        public void Foo()
        {
        }

        public int FooProp
        {
            get
            {
                throw null;
            }
            set
            {
                throw null;
            }
        }

        public event Action FooEvent
        {
            add { }
            remove { }
        }
    }

    public abstract class Base
    {
        // Instance fields

        public int B_InstFieldPublic;

        protected int B_InstFieldFamily;

        private int B_InstFieldPrivate;

        internal int B_InstFieldAssembly;

        protected internal int B_InstFieldFamOrAssembly;

        // Hidden fields

        public int B_HiddenFieldPublic;

        protected int B_HiddenFieldFamily;

        private int B_HiddenFieldPrivate;

        internal int B_HiddenFieldAssembly;

        protected internal int B_HiddenFieldFamOrAssembly;

        // Static fields

        public static int B_StaticFieldPublic;

        protected static int B_StaticFieldFamily;

        private static int B_StaticFieldPrivate;

        internal static int B_StaticFieldAssembly;

        protected internal static int B_StaticFieldFamOrAssembly;

        // Instance methods

        public void B_InstMethPublic() { }

        protected void B_InstMethFamily() { }

        private void B_InstMethPrivate() { }

        internal void B_InstMethAssembly() { }

        protected internal void B_InstMethFamOrAssembly() { }

        // Hidden methods

        public void B_HiddenMethPublic() { }

        protected void B_HiddenMethFamily() { }

        private void B_HiddenMethPrivate() { }

        internal void B_HiddenMethAssembly() { }

        protected internal void B_HiddenMethFamOrAssembly() { }

        // Static methods

        public static void B_StaticMethPublic() { }

        protected static void B_StaticMethFamily() { }

        private static void B_StaticMethPrivate() { }

        internal static void B_StaticMethAssembly() { }
        protected internal static void B_StaticMethFamOrAssembly() { }

        // Virtual methods

        public virtual void B_VirtualMethPublic() { }

        protected virtual void B_VirtualMethFamily() { }

        internal virtual void B_VirtualMethAssembly() { }

        protected virtual internal void B_VirtualMethFamOrAssembly() { }

        // Instance properties

        public int B_InstPropPublic { get { return 5; } }

        protected int B_InstPropFamily { get { return 5; } }

        private int B_InstPropPrivate { get { return 5; } }

        internal int B_InstPropAssembly { get { return 5; } }

        protected internal int B_InstPropFamOrAssembly { get { return 5; } }

        // Hidden properties

        public int B_HiddenPropPublic { get { return 5; } }

        protected int B_HiddenPropFamily { get { return 5; } }

        private int B_HiddenPropPrivate { get { return 5; } }

        internal int B_HiddenPropAssembly { get { return 5; } }

        protected internal int B_HiddenPropFamOrAssembly { get { return 5; } }

        // Static properties

        public static int B_StaticPropPublic { get { return 5; } }

        protected static int B_StaticPropFamily { get { return 5; } }

        private static int B_StaticPropPrivate { get { return 5; } }

        internal static int B_StaticPropAssembly { get { return 5; } }

        protected internal static int B_StaticPropFamOrAssembly { get { return 5; } }

        // Virtual properties

        public virtual int B_VirtualPropPublic { get { return 5; } }

        protected virtual int B_VirtualPropFamily { get { return 5; } }

        internal virtual int B_VirtualPropAssembly { get { return 5; } }

        protected virtual internal int B_VirtualPropFamOrAssembly { get { return 5; } }

        // Instance events

        public event Action B_InstEventPublic { add { } remove { } }

        protected event Action B_InstEventFamily { add { } remove { } }

        private event Action B_InstEventPrivate { add { } remove { } }

        internal event Action B_InstEventAssembly { add { } remove { } }

        protected internal event Action B_InstEventFamOrAssembly { add { } remove { } }

        // Hidden events

        public event Action B_HiddenEventPublic { add { } remove { } }

        protected event Action B_HiddenEventFamily { add { } remove { } }

        private event Action B_HiddenEventPrivate { add { } remove { } }

        internal event Action B_HiddenEventAssembly { add { } remove { } }

        protected internal event Action B_HiddenEventFamOrAssembly { add { } remove { } }

        // Static events

        public static event Action B_StaticEventPublic { add { } remove { } }

        protected static event Action B_StaticEventFamily { add { } remove { } }

        private static event Action B_StaticEventPrivate { add { } remove { } }

        internal static event Action B_StaticEventAssembly { add { } remove { } }

        protected internal static event Action B_StaticEventFamOrAssembly { add { } remove { } }

        // Virtual events

        public virtual event Action B_VirtualEventPublic { add { } remove { } }

        protected virtual event Action B_VirtualEventFamily { add { } remove { } }

        internal virtual event Action B_VirtualEventAssembly { add { } remove { } }

        protected virtual internal event Action B_VirtualEventFamOrAssembly { add { } remove { } }
    }

    public abstract class Mid : Base
    {
        // Instance fields

        public int M_InstFieldPublic;

        protected int M_InstFieldFamily;

        private int M_InstFieldPrivate;

        internal int M_InstFieldAssembly;

        protected internal int M_InstFieldFamOrAssembly;

        // Hidden fields

        public new int B_HiddenFieldPublic;

        protected new int B_HiddenFieldFamily;

        private /*new*/ int B_HiddenFieldPrivate;

        internal new int B_HiddenFieldAssembly;

        protected internal new int B_HiddenFieldFamOrAssembly;

        // Static fields

        public static int M_StaticFieldPublic;

        protected static int M_StaticFieldFamily;

        private static int M_StaticFieldPrivate;

        internal static int M_StaticFieldAssembly;

        protected internal static int M_StaticFieldFamOrAssembly;

        // Instance methods

        public void M_InstMethPublic() { }

        protected void M_InstMethFamily() { }

        private void M_InstMethPrivate() { }

        internal void M_InstMethAssembly() { }

        protected internal void M_InstMethFamOrAssembly() { }

        // Hidden methods

        public new void B_HiddenMethPublic() { }

        protected new void B_HiddenMethFamily() { }

        private void B_HiddenMethPrivate() { }

        internal new void B_HiddenMethAssembly() { }

        protected new internal void B_HiddenMethFamOrAssembly() { }

        // Static methods

        public static void M_StaticMethPublic() { }

        protected static void M_StaticMethFamily() { }

        private static void M_StaticMethPrivate() { }

        internal static void M_StaticMethAssembly() { }

        protected internal static void M_StaticMethFamOrAssembly() { }

        // Overriding Virtual methods

        public override void B_VirtualMethPublic() { }

        protected override void B_VirtualMethFamily() { }

        internal override void B_VirtualMethAssembly() { }

        protected override internal void B_VirtualMethFamOrAssembly() { }

        // Instance properties

        public int M_InstPropPublic { get { return 5; } }

        protected int M_InstPropFamily { get { return 5; } }

        private int M_InstPropPrivate { get { return 5; } }

        internal int M_InstPropAssembly { get { return 5; } }

        protected internal int M_InstPropFamOrAssembly { get { return 5; } }

        // Hidden properties

        public new int B_HiddenPropPublic { get { return 5; } }

        protected new int B_HiddenPropFamily { get { return 5; } }

        private int B_HiddenPropPrivate { get { return 5; } }

        internal new int B_HiddenPropAssembly { get { return 5; } }

        protected new internal int B_HiddenPropFamOrAssembly { get { return 5; } }

        // Static properties

        public static int M_StaticPropPublic { get { return 5; } }

        protected static int M_StaticPropFamily { get { return 5; } }

        private static int M_StaticPropPrivate { get { return 5; } }

        internal static int M_StaticPropAssembly { get { return 5; } }

        protected internal static int M_StaticPropFamOrAssembly { get { return 5; } }

        // Overriding Virtual properties

        public override int B_VirtualPropPublic { get { return 5; } }

        protected override int B_VirtualPropFamily { get { return 5; } }

        internal override int B_VirtualPropAssembly { get { return 5; } }

        protected override internal int B_VirtualPropFamOrAssembly { get { return 5; } }

        // Instance events

        public event Action M_InstEventPublic { add { } remove { } }

        protected event Action M_InstEventFamily { add { } remove { } }

        private event Action M_InstEventPrivate { add { } remove { } }

        internal event Action M_InstEventAssembly { add { } remove { } }

        protected internal event Action M_InstEventFamOrAssembly { add { } remove { } }

        // Hidden events

        public new event Action B_HiddenEventPublic { add { } remove { } }

        protected new event Action B_HiddenEventFamily { add { } remove { } }

        private event Action B_HiddenEventPrivate { add { } remove { } }

        internal new event Action B_HiddenEventAssembly { add { } remove { } }

        protected new internal event Action B_HiddenEventFamOrAssembly { add { } remove { } }

        // Static events

        public static event Action M_StaticEventPublic { add { } remove { } }

        protected static event Action M_StaticEventFamily { add { } remove { } }

        private static event Action M_StaticEventPrivate { add { } remove { } }

        internal static event Action M_StaticEventAssembly { add { } remove { } }

        protected internal static event Action M_StaticEventFamOrAssembly { add { } remove { } }

        // Overriding Virtual events

        public override event Action B_VirtualEventPublic { add { } remove { } }

        protected override event Action B_VirtualEventFamily { add { } remove { } }

        internal override event Action B_VirtualEventAssembly { add { } remove { } }

        protected override internal event Action B_VirtualEventFamOrAssembly { add { } remove { } }
    }

    public abstract class Derived : Mid
    {
        // New Virtual methods

        public new virtual void B_VirtualMethPublic() { }

        protected new virtual void B_VirtualMethFamily() { }

        internal new virtual void B_VirtualMethAssembly() { }

        protected new virtual internal void B_VirtualMethFamOrAssembly() { }

        // New Virtual properties

        public new virtual int B_VirtualPropPublic { get { return 5; } }

        protected new virtual int B_VirtualPropFamily { get { return 5; } }

        internal new virtual int B_VirtualPropAssembly { get { return 5; } }

        protected new virtual internal int B_VirtualPropFamOrAssembly { get { return 5; } }

        // New Virtual events

        public new virtual event Action B_VirtualEventPublic { add { } remove { } }

        protected new virtual event Action B_VirtualEventFamily { add { } remove { } }

        internal new virtual event Action B_VirtualEventAssembly { add { } remove { } }

        protected new virtual internal event Action B_VirtualEventFamOrAssembly { add { } remove { } }
    }

    public abstract class S1
    {

        public int Prop1 { get; set; }

        public int Prop2 { get; set; }

        public event Action Event1 { add { } remove { } }

        public event Action Event2 { add { } remove { } }

        protected abstract void M1();

        protected virtual void M3() { }

        protected virtual void M4() { }
    }

    public abstract class S2 : S1
    {

        private new int Prop1 { get; set; }

        private new event Action Event1 { add { } remove { } }

        protected virtual void M2() { }

        protected new virtual void M4() { }
    }

    public abstract class S3 : S2
    {
        public static new int Prop2 { get; set; }
        public static new event Action Event2 { add { } remove { } }

        protected override void M1() { }

        protected abstract override void M2();

        protected override void M4() { }
    }

    public abstract class S4 : S3
    {

        protected override void M2() { }

        protected override void M3() { }

        protected new virtual void M4() { }
    }

    public interface INov1
    {
        void Foo();
    }

    public interface INov2 : INov1
    {
    }

    public class Nov : INov2
    {
        public static void S() { }
        public void I() { }
        public void Foo() { }
    }

    public class GetRuntimeMethodBase
    {
        public void Hidden1(int x) { }

        public void DefinedInBaseOnly(int x) { }

        public void InExact(Object o) { }

        public void Close1(String s) { }

        public void VarArgs(int x, params Object[] varargs) { }

        public void Primitives(int i) { }
    }

    public class GetRuntimeMethodDerived : GetRuntimeMethodBase
    {
        public new void Hidden1(int x) { }

        public void Close1(Object s) { }
    }
}

namespace SampleMetadataMethodImpl
{
    interface ICloneable
    {
        void Clone();
        void GenericClone<T>();
    }

    class ImplementsICloneable : ICloneable
    {
        void ICloneable.Clone()
        {
        }
        void ICloneable.GenericClone<T>()
        {
        }
    }
}

