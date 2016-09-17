// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
    // This file contains wrapper classes that allow ProjectN to support
    // arrays of more than one dimension. This is accomplished using the 
    // wrapper classes here and a set of IL transforms to map "normal"
    // multidimensional arrays to these classes.
    //
    // Special support for this is baked into at least the following places:
    //      * The framework libraries (these classes)
    //      * UTC
    //      * IL Transforms
    //      * Dependency reducer engine
    //      * Debugger
    //      * Reflection APIs
    //
    // When building this solution (Aug 2016) we only built support for MDArrays
    // from rank 2 to 4 to match previously shipped behavior. 
    //
    // The desktop CLR supports arrays of up to 32 dimensions so that provides
    // an upper limit on how much this needs to be built out.
    
    public class MDArray
    {
        public const int MinRank = 2;
        public const int MaxRank = 4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank2<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _lowerBound1;
        private int _lowerBound2;
        private T _data;

        public static T[,] Ctor(int length1, int length2)
        {
            if ((length1 < 0) || (length2 < 0))
                throw new OverflowException();

            MDArrayRank2<T> newArray = Unsafe.As<MDArrayRank2<T>>(RuntimeImports.RhNewArray(typeof(T[,]).TypeHandle.ToEETypePtr(), checked(length1 * length2)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            return Unsafe.As<T[,]>(newArray);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,] array, int index1, int index2)
        {
            MDArrayRank2<T> mdArrayObj = Unsafe.As<MDArrayRank2<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();

            int index = (index1 * mdArrayObj._upperBound2) + index2;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,] array, int index1, int index2)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2);
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static T Get(T[,] array, int index1, int index2)
        {
            return InternalAddress(array, index1, index2);
        }

        public static void Set(T[,] array, int index1, int index2, T value)
        {
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank3<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private T _data;

        public static T[,,] Ctor(int length1, int length2, int length3)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0))
                throw new OverflowException();

            MDArrayRank3<T> newArray = Unsafe.As<MDArrayRank3<T>>(RuntimeImports.RhNewArray(typeof(T[,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            return Unsafe.As<T[,,]>(newArray);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,] array, int index1, int index2, int index3)
        {
            MDArrayRank3<T> mdArrayObj = Unsafe.As<MDArrayRank3<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();

            int index = (((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,] array, int index1, int index2, int index3)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3);
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static T Get(T[,,] array, int index1, int index2, int index3)
        {
            return InternalAddress(array, index1, index2, index3);
        }

        public static void Set(T[,,] array, int index1, int index2, int index3, T value)
        {
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank4<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private T _data;

        public static T[,,,] Ctor(int length1, int length2, int length3, int length4)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0))
                throw new OverflowException();

            MDArrayRank4<T> newArray = Unsafe.As<MDArrayRank4<T>>(RuntimeImports.RhNewArray(typeof(T[,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            return Unsafe.As<T[,,,]>(newArray);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,] array, int index1, int index2, int index3, int index4)
        {
            MDArrayRank4<T> mdArrayObj = Unsafe.As<MDArrayRank4<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();

            int index = (((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,] array, int index1, int index2, int index3, int index4)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4);
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static T Get(T[,,,] array, int index1, int index2, int index3, int index4)
        {
            return InternalAddress(array, index1, index2, index3, index4);
        }

        public static void Set(T[,,,] array, int index1, int index2, int index3, int index4, T value)
        {
            if (!EETypePtr.EETypePtrOf<T>().IsValueType)
            {
                RuntimeImports.RhCheckArrayStore(array, (object)value);
            }

            InternalAddress(array, index1, index2, index3, index4) = value;
        }
    }
}
