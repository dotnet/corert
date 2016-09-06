// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    // This attribute is only for use in a Class Library 
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class IntrinsicAttribute : Attribute { }

#if !CORERT
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BoundAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class BoundsCheckingAttribute : Attribute { }
#endif

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class StackOnlyAttribute : Attribute { }

#if !CORERT
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
        internal static extern void Store(ByReference<T> pointer, T value);

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
        internal static extern ByReference<T> AddRaw(ByReference<T> pointer, int rawOffset);

        [Intrinsic]
        private static extern ByReference<T> SubRaw(ByReference<T> pointer, int rawOffset);

        [Intrinsic]
        private static extern int UncheckedMul(int a, int b);

        [Intrinsic]
        private static extern int SizeOfTUnsigned();

        internal static int SizeOfT()
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
#endif
}
