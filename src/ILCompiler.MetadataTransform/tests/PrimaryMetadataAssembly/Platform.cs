// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 649

namespace System
{
    // Dummy core types to allow us compiling this assembly as a core library so that the type
    // system tests don't have a dependency on a real core library.

    // We might need to bring in some extra things (Interface lists? Other virtual methods on Object?),
    // but let's postpone that until actually needed.
    
    public class Object
    {
        internal IntPtr m_pEEType;
        public virtual string ToString() { return null; }

        ~Object()
        {
        }
    }

    public struct Void { }
    public struct Boolean { }
    public struct Char { }
    public struct SByte { }
    public struct Byte { }
    public struct Int16 { }
    public struct UInt16 { }
    public struct Int32 { }
    public struct UInt32 { }
    public struct Int64 { }
    public struct UInt64 { }
    public struct IntPtr { }
    public struct UIntPtr { }
    public struct Single { }
    public struct Double { }
    public abstract class ValueType { }
    public abstract class Enum : ValueType { }
    public struct Nullable<T> where T : struct { }
    
    public sealed class String { }
    public abstract class Array : System.Collections.IList { }
    public abstract class Delegate { }
    public abstract class MulticastDelegate : Delegate { }

    public struct RuntimeTypeHandle { }
    public struct RuntimeMethodHandle { }
    public struct RuntimeFieldHandle { }

    public class Attribute { }

    public class Array<T> : Array, System.Collections.Generic.IList<T> { }
}

namespace System.Collections
{
    interface IList
    { }
}

namespace System.Collections.Generic
{
    interface IList<T>
    {

    }
}

namespace System.Runtime.InteropServices
{
    public enum LayoutKind
    {
        Sequential = 0, // 0x00000008,
        Explicit = 2, // 0x00000010,
        Auto = 3, // 0x00000000,
    }

    public sealed class StructLayoutAttribute : Attribute
    {
        internal LayoutKind _val;

        public StructLayoutAttribute(LayoutKind layoutKind)
        {
            _val = layoutKind;
        }

        public LayoutKind Value { get { return _val; } }
        public int Pack;
        public int Size;
    }

    public sealed class FieldOffsetAttribute : Attribute
    {
        private int _val;
        public FieldOffsetAttribute(int offset)
        {
            _val = offset;
        }
        public int Value { get { return _val; } }
    }
}

