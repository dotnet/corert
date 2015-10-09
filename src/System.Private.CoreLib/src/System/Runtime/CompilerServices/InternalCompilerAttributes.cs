// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    // This attribute is only for use in a Class Library 
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class IntrinsicAttribute : Attribute
    {
        public bool IgnoreBody;
    }

    // At the moment, we don't inline anything across modules other than Object..ctor,
    // so this attribute is only for use in a Class Library.  If we ever broaden this,
    // we will want to make this a public attribute.
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Method)]
    internal sealed class NonVersionableAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BoundAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class BoundsCheckingAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class StackOnlyAttribute : Attribute { }

    // This is a dummy class to be replaced by the compiler with a in T
    // It has to be a dummy class to avoid complicated type substitution
    // and other complications in the compiler.
    public sealed class ByReference<T>
    {
        //
        // Managed pointer creation
        //
        [Intrinsic]
        public static extern ByReference<T> FromRef(ref T pointer);

        [Intrinsic]
        [CLSCompliant(false)]
        public static extern UIntPtr ToPointer(ByReference<T> pointer);

        [Intrinsic]
        public static extern ByReference<T1> Cast<T1>(ByReference<T> pointer);

        //
        // Value access
        //
        [Intrinsic]
        public static extern T Load(ByReference<T> pointer);

        [Intrinsic]
        private static extern void Store(ByReference<T> pointer, T value);

        public static T LoadAtIndex(ByReference<T> pointer, int index)
        {
            ByReference<T> temp = Add(pointer, index);
            return Load(temp);
        }

        internal static void StoreAtIndex(ByReference<T> pointer, int index, T value)
        {
            ByReference<T> temp = Add(pointer, index);
            Store(temp, value);
        }

        //
        // Pointer arithmetic
        //
        [Intrinsic]
        private static extern ByReference<T> AddRaw(ByReference<T> pointer, int rawOffset);

        [Intrinsic]
        private static extern ByReference<T> SubRaw(ByReference<T> pointer, int rawOffset);

        [Intrinsic]
        private static extern int UncheckedMul(int a, int b);

        [Intrinsic]
        private static extern int SizeOfTUnsigned();

        private static int SizeOfT()
        {
            unchecked
            {
                // The IL sizeof(T) is unsigned but we need signed integer for all our uses.
                return (int)SizeOfTUnsigned();
            }
        }

        public static ByReference<T> Add(ByReference<T> pointer, int offset)
        {
            return AddRaw(pointer, UncheckedMul(offset, SizeOfT()));
        }

        [Intrinsic]
        private static extern bool PointerEquals(ByReference<T> value1, ByReference<T> value2);
    }
}