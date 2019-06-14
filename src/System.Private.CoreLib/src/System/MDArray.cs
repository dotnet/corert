// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

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
    // The desktop CLR supports arrays of up to 32 dimensions so that provides
    // an upper limit on how much this needs to be built out.

    internal static class MDPointerArrayHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MDArrayShape
        {
            public IntPtr _count;
            public int _upperBound1;
            // Followed by rank-1 upperbounds
            // Then rank lobounds
        }
 
        public static object GetMDPointerArray(EETypePtr pointerDepthType, EETypePtr elementType, params int[] lengths)
        {
            // pointerDepthType is used to encode the pointer rank of the mdarray. This is exceedingly odd and inefficient
            // but it works, and will allow the parameters to the various helpers to be consistent. This is only 
            // acceptable as the feature is exceedingly rarely used in practice.
            int pointerRank = 1;
            while (pointerDepthType.IsGeneric)
            {
                pointerRank++;
                pointerDepthType = pointerDepthType.Instantiation[0];
            }
            
            int rank = lengths.Length;

            // thPointerType will be the element type of the array once the iPointerRank loop below
            // finishes.
            RuntimeTypeHandle thPointerType = new RuntimeTypeHandle(elementType);
            for (int iPointerRank = 0; iPointerRank < pointerRank; iPointerRank++)
            {
                bool pointerTypeConstructionSuccess = RuntimeAugments.TypeLoaderCallbacks.TryGetPointerTypeForTargetType(thPointerType, out thPointerType);
                if (!pointerTypeConstructionSuccess)
                    throw new TypeLoadException();
            }

            RuntimeTypeHandle mdArrayType;
            bool mdArrayTypeConstruction = RuntimeAugments.TypeLoaderCallbacks.TryGetArrayTypeForElementType(thPointerType, true, rank, out mdArrayType);
            if (!mdArrayTypeConstruction)
                throw new TypeLoadException();

            int totalLength = 1;
            foreach (int length in lengths)
            {
                if (length < 0)
                    throw new OverflowException();
                totalLength = checked(totalLength * length);
            }

            MDArrayShape newArray = Unsafe.As<MDArrayShape>(RuntimeImports.RhNewArray(mdArrayType.ToEETypePtr(), totalLength));

            // Assign upper bounds
            unsafe
            {
                fixed(int *pUpperBound = &newArray._upperBound1)
                {
                    for (int iRank = 0; iRank < lengths.Length; iRank++)
                    {
                        pUpperBound[iRank] = lengths[iRank];
                    }
                }
            }
            return newArray;
        }
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

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2);
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
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,] array, int index1, int index2)
        {
            return ref InternalAddress(array, index1, index2);
        }

        public static T Get(T[,] array, int index1, int index2)
        {
            return InternalAddress(array, index1, index2);
        }

        public static void Set(T[,] array, int index1, int index2, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
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

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3);
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
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,] array, int index1, int index2, int index3)
        {
            return ref InternalAddress(array, index1, index2, index3);
        }

        public static T Get(T[,,] array, int index1, int index2, int index3)
        {
            return InternalAddress(array, index1, index2, index3);
        }

        public static void Set(T[,,] array, int index1, int index2, int index3, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
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

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4);
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
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,] array, int index1, int index2, int index3, int index4)
        {
            return ref InternalAddress(array, index1, index2, index3, index4);
        }

        public static T Get(T[,,,] array, int index1, int index2, int index3, int index4)
        {
            return InternalAddress(array, index1, index2, index3, index4);
        }

        public static void Set(T[,,,] array, int index1, int index2, int index3, int index4, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank5<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private T _data;

        public static T[,,,,] Ctor(int length1, int length2, int length3, int length4, int length5)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0))
                throw new OverflowException();

            MDArrayRank5<T> newArray = Unsafe.As<MDArrayRank5<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            return Unsafe.As<T[,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,] array, int index1, int index2, int index3, int index4, int index5)
        {
            MDArrayRank5<T> mdArrayObj = Unsafe.As<MDArrayRank5<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();

            int index = (((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,] array, int index1, int index2, int index3, int index4, int index5)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,] array, int index1, int index2, int index3, int index4, int index5)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5);
        }

        public static T Get(T[,,,,] array, int index1, int index2, int index3, int index4, int index5)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5);
        }

        public static void Set(T[,,,,] array, int index1, int index2, int index3, int index4, int index5, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank6<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private T _data;

        public static T[,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0))
                throw new OverflowException();

            MDArrayRank6<T> newArray = Unsafe.As<MDArrayRank6<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            return Unsafe.As<T[,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6)
        {
            MDArrayRank6<T> mdArrayObj = Unsafe.As<MDArrayRank6<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();

            int index = (((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6);
        }

        public static T Get(T[,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6);
        }

        public static void Set(T[,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank7<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private T _data;

        public static T[,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0))
                throw new OverflowException();

            MDArrayRank7<T> newArray = Unsafe.As<MDArrayRank7<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            return Unsafe.As<T[,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7)
        {
            MDArrayRank7<T> mdArrayObj = Unsafe.As<MDArrayRank7<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();

            int index = (((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7);
        }

        public static T Get(T[,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7);
        }

        public static void Set(T[,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank8<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private T _data;

        public static T[,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0))
                throw new OverflowException();

            MDArrayRank8<T> newArray = Unsafe.As<MDArrayRank8<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            return Unsafe.As<T[,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8)
        {
            MDArrayRank8<T> mdArrayObj = Unsafe.As<MDArrayRank8<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8);
        }

        public static T Get(T[,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8);
        }

        public static void Set(T[,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank9<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private T _data;

        public static T[,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0))
                throw new OverflowException();

            MDArrayRank9<T> newArray = Unsafe.As<MDArrayRank9<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            return Unsafe.As<T[,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9)
        {
            MDArrayRank9<T> mdArrayObj = Unsafe.As<MDArrayRank9<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9);
        }

        public static T Get(T[,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9);
        }

        public static void Set(T[,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank10<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private T _data;

        public static T[,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0))
                throw new OverflowException();

            MDArrayRank10<T> newArray = Unsafe.As<MDArrayRank10<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            return Unsafe.As<T[,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10)
        {
            MDArrayRank10<T> mdArrayObj = Unsafe.As<MDArrayRank10<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10);
        }

        public static T Get(T[,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10);
        }

        public static void Set(T[,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank11<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private T _data;

        public static T[,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0))
                throw new OverflowException();

            MDArrayRank11<T> newArray = Unsafe.As<MDArrayRank11<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            return Unsafe.As<T[,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11)
        {
            MDArrayRank11<T> mdArrayObj = Unsafe.As<MDArrayRank11<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11);
        }

        public static T Get(T[,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11);
        }

        public static void Set(T[,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank12<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private T _data;

        public static T[,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0))
                throw new OverflowException();

            MDArrayRank12<T> newArray = Unsafe.As<MDArrayRank12<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            return Unsafe.As<T[,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12)
        {
            MDArrayRank12<T> mdArrayObj = Unsafe.As<MDArrayRank12<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12);
        }

        public static T Get(T[,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12);
        }

        public static void Set(T[,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank13<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private T _data;

        public static T[,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0))
                throw new OverflowException();

            MDArrayRank13<T> newArray = Unsafe.As<MDArrayRank13<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            return Unsafe.As<T[,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13)
        {
            MDArrayRank13<T> mdArrayObj = Unsafe.As<MDArrayRank13<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13);
        }

        public static T Get(T[,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13);
        }

        public static void Set(T[,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank14<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private T _data;

        public static T[,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0))
                throw new OverflowException();

            MDArrayRank14<T> newArray = Unsafe.As<MDArrayRank14<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            return Unsafe.As<T[,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14)
        {
            MDArrayRank14<T> mdArrayObj = Unsafe.As<MDArrayRank14<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14);
        }

        public static T Get(T[,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14);
        }

        public static void Set(T[,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank15<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private T _data;

        public static T[,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0))
                throw new OverflowException();

            MDArrayRank15<T> newArray = Unsafe.As<MDArrayRank15<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            return Unsafe.As<T[,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15)
        {
            MDArrayRank15<T> mdArrayObj = Unsafe.As<MDArrayRank15<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15);
        }

        public static T Get(T[,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15);
        }

        public static void Set(T[,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank16<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private T _data;

        public static T[,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0))
                throw new OverflowException();

            MDArrayRank16<T> newArray = Unsafe.As<MDArrayRank16<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            return Unsafe.As<T[,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16)
        {
            MDArrayRank16<T> mdArrayObj = Unsafe.As<MDArrayRank16<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16);
        }

        public static T Get(T[,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16);
        }

        public static void Set(T[,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank17<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0))
                throw new OverflowException();

            MDArrayRank17<T> newArray = Unsafe.As<MDArrayRank17<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17)
        {
            MDArrayRank17<T> mdArrayObj = Unsafe.As<MDArrayRank17<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank18<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0))
                throw new OverflowException();

            MDArrayRank18<T> newArray = Unsafe.As<MDArrayRank18<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18)
        {
            MDArrayRank18<T> mdArrayObj = Unsafe.As<MDArrayRank18<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank19<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0))
                throw new OverflowException();

            MDArrayRank19<T> newArray = Unsafe.As<MDArrayRank19<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19)
        {
            MDArrayRank19<T> mdArrayObj = Unsafe.As<MDArrayRank19<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank20<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0))
                throw new OverflowException();

            MDArrayRank20<T> newArray = Unsafe.As<MDArrayRank20<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20)
        {
            MDArrayRank20<T> mdArrayObj = Unsafe.As<MDArrayRank20<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank21<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0))
                throw new OverflowException();

            MDArrayRank21<T> newArray = Unsafe.As<MDArrayRank21<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21)
        {
            MDArrayRank21<T> mdArrayObj = Unsafe.As<MDArrayRank21<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank22<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0))
                throw new OverflowException();

            MDArrayRank22<T> newArray = Unsafe.As<MDArrayRank22<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22)
        {
            MDArrayRank22<T> mdArrayObj = Unsafe.As<MDArrayRank22<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank23<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0))
                throw new OverflowException();

            MDArrayRank23<T> newArray = Unsafe.As<MDArrayRank23<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23)
        {
            MDArrayRank23<T> mdArrayObj = Unsafe.As<MDArrayRank23<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank24<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0))
                throw new OverflowException();

            MDArrayRank24<T> newArray = Unsafe.As<MDArrayRank24<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24)
        {
            MDArrayRank24<T> mdArrayObj = Unsafe.As<MDArrayRank24<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank25<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0))
                throw new OverflowException();

            MDArrayRank25<T> newArray = Unsafe.As<MDArrayRank25<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25)
        {
            MDArrayRank25<T> mdArrayObj = Unsafe.As<MDArrayRank25<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank26<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0))
                throw new OverflowException();

            MDArrayRank26<T> newArray = Unsafe.As<MDArrayRank26<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26)
        {
            MDArrayRank26<T> mdArrayObj = Unsafe.As<MDArrayRank26<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank27<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0))
                throw new OverflowException();

            MDArrayRank27<T> newArray = Unsafe.As<MDArrayRank27<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27)
        {
            MDArrayRank27<T> mdArrayObj = Unsafe.As<MDArrayRank27<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank28<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _upperBound28;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private int _lowerBound28;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0) || (length28 < 0))
                throw new OverflowException();

            MDArrayRank28<T> newArray = Unsafe.As<MDArrayRank28<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27 * length28)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            newArray._upperBound28 = length28;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27, length28);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28)
        {
            MDArrayRank28<T> mdArrayObj = Unsafe.As<MDArrayRank28<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();
            if ((index28 < 0) || (index28 >= mdArrayObj._upperBound28))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27) * mdArrayObj._upperBound28) + index28;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank29<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _upperBound28;
        private int _upperBound29;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private int _lowerBound28;
        private int _lowerBound29;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0) || (length28 < 0) || (length29 < 0))
                throw new OverflowException();

            MDArrayRank29<T> newArray = Unsafe.As<MDArrayRank29<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27 * length28 * length29)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            newArray._upperBound28 = length28;
            newArray._upperBound29 = length29;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27, length28, length29);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29)
        {
            MDArrayRank29<T> mdArrayObj = Unsafe.As<MDArrayRank29<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();
            if ((index28 < 0) || (index28 >= mdArrayObj._upperBound28))
                throw new IndexOutOfRangeException();
            if ((index29 < 0) || (index29 >= mdArrayObj._upperBound29))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27) * mdArrayObj._upperBound28) + index28) * mdArrayObj._upperBound29) + index29;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank30<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _upperBound28;
        private int _upperBound29;
        private int _upperBound30;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private int _lowerBound28;
        private int _lowerBound29;
        private int _lowerBound30;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0) || (length28 < 0) || (length29 < 0) || (length30 < 0))
                throw new OverflowException();

            MDArrayRank30<T> newArray = Unsafe.As<MDArrayRank30<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27 * length28 * length29 * length30)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            newArray._upperBound28 = length28;
            newArray._upperBound29 = length29;
            newArray._upperBound30 = length30;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27, length28, length29, length30);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30)
        {
            MDArrayRank30<T> mdArrayObj = Unsafe.As<MDArrayRank30<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();
            if ((index28 < 0) || (index28 >= mdArrayObj._upperBound28))
                throw new IndexOutOfRangeException();
            if ((index29 < 0) || (index29 >= mdArrayObj._upperBound29))
                throw new IndexOutOfRangeException();
            if ((index30 < 0) || (index30 >= mdArrayObj._upperBound30))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27) * mdArrayObj._upperBound28) + index28) * mdArrayObj._upperBound29) + index29) * mdArrayObj._upperBound30) + index30;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank31<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _upperBound28;
        private int _upperBound29;
        private int _upperBound30;
        private int _upperBound31;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private int _lowerBound28;
        private int _lowerBound29;
        private int _lowerBound30;
        private int _lowerBound31;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30, int length31)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0) || (length28 < 0) || (length29 < 0) || (length30 < 0) || (length31 < 0))
                throw new OverflowException();

            MDArrayRank31<T> newArray = Unsafe.As<MDArrayRank31<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27 * length28 * length29 * length30 * length31)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            newArray._upperBound28 = length28;
            newArray._upperBound29 = length29;
            newArray._upperBound30 = length30;
            newArray._upperBound31 = length31;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30, int length31)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27, length28, length29, length30, length31);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31)
        {
            MDArrayRank31<T> mdArrayObj = Unsafe.As<MDArrayRank31<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();
            if ((index28 < 0) || (index28 >= mdArrayObj._upperBound28))
                throw new IndexOutOfRangeException();
            if ((index29 < 0) || (index29 >= mdArrayObj._upperBound29))
                throw new IndexOutOfRangeException();
            if ((index30 < 0) || (index30 >= mdArrayObj._upperBound30))
                throw new IndexOutOfRangeException();
            if ((index31 < 0) || (index31 >= mdArrayObj._upperBound31))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27) * mdArrayObj._upperBound28) + index28) * mdArrayObj._upperBound29) + index29) * mdArrayObj._upperBound30) + index30) * mdArrayObj._upperBound31) + index31;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31) = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class MDArrayRank32<T>
    {
        private IntPtr _count;
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;
        private int _upperBound5;
        private int _upperBound6;
        private int _upperBound7;
        private int _upperBound8;
        private int _upperBound9;
        private int _upperBound10;
        private int _upperBound11;
        private int _upperBound12;
        private int _upperBound13;
        private int _upperBound14;
        private int _upperBound15;
        private int _upperBound16;
        private int _upperBound17;
        private int _upperBound18;
        private int _upperBound19;
        private int _upperBound20;
        private int _upperBound21;
        private int _upperBound22;
        private int _upperBound23;
        private int _upperBound24;
        private int _upperBound25;
        private int _upperBound26;
        private int _upperBound27;
        private int _upperBound28;
        private int _upperBound29;
        private int _upperBound30;
        private int _upperBound31;
        private int _upperBound32;
        private int _lowerBound1;
        private int _lowerBound2;
        private int _lowerBound3;
        private int _lowerBound4;
        private int _lowerBound5;
        private int _lowerBound6;
        private int _lowerBound7;
        private int _lowerBound8;
        private int _lowerBound9;
        private int _lowerBound10;
        private int _lowerBound11;
        private int _lowerBound12;
        private int _lowerBound13;
        private int _lowerBound14;
        private int _lowerBound15;
        private int _lowerBound16;
        private int _lowerBound17;
        private int _lowerBound18;
        private int _lowerBound19;
        private int _lowerBound20;
        private int _lowerBound21;
        private int _lowerBound22;
        private int _lowerBound23;
        private int _lowerBound24;
        private int _lowerBound25;
        private int _lowerBound26;
        private int _lowerBound27;
        private int _lowerBound28;
        private int _lowerBound29;
        private int _lowerBound30;
        private int _lowerBound31;
        private int _lowerBound32;
        private T _data;

        public static T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] Ctor(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30, int length31, int length32)
        {
            if ((length1 < 0) || (length2 < 0) || (length3 < 0) || (length4 < 0) || (length5 < 0) || (length6 < 0) || (length7 < 0) || (length8 < 0) || (length9 < 0) || (length10 < 0) || (length11 < 0) || (length12 < 0) || (length13 < 0) || (length14 < 0) || (length15 < 0) || (length16 < 0) || (length17 < 0) || (length18 < 0) || (length19 < 0) || (length20 < 0) || (length21 < 0) || (length22 < 0) || (length23 < 0) || (length24 < 0) || (length25 < 0) || (length26 < 0) || (length27 < 0) || (length28 < 0) || (length29 < 0) || (length30 < 0) || (length31 < 0) || (length32 < 0))
                throw new OverflowException();

            MDArrayRank32<T> newArray = Unsafe.As<MDArrayRank32<T>>(RuntimeImports.RhNewArray(typeof(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]).TypeHandle.ToEETypePtr(), checked(length1 * length2 * length3 * length4 * length5 * length6 * length7 * length8 * length9 * length10 * length11 * length12 * length13 * length14 * length15 * length16 * length17 * length18 * length19 * length20 * length21 * length22 * length23 * length24 * length25 * length26 * length27 * length28 * length29 * length30 * length31 * length32)));
            newArray._upperBound1 = length1;
            newArray._upperBound2 = length2;
            newArray._upperBound3 = length3;
            newArray._upperBound4 = length4;
            newArray._upperBound5 = length5;
            newArray._upperBound6 = length6;
            newArray._upperBound7 = length7;
            newArray._upperBound8 = length8;
            newArray._upperBound9 = length9;
            newArray._upperBound10 = length10;
            newArray._upperBound11 = length11;
            newArray._upperBound12 = length12;
            newArray._upperBound13 = length13;
            newArray._upperBound14 = length14;
            newArray._upperBound15 = length15;
            newArray._upperBound16 = length16;
            newArray._upperBound17 = length17;
            newArray._upperBound18 = length18;
            newArray._upperBound19 = length19;
            newArray._upperBound20 = length20;
            newArray._upperBound21 = length21;
            newArray._upperBound22 = length22;
            newArray._upperBound23 = length23;
            newArray._upperBound24 = length24;
            newArray._upperBound25 = length25;
            newArray._upperBound26 = length26;
            newArray._upperBound27 = length27;
            newArray._upperBound28 = length28;
            newArray._upperBound29 = length29;
            newArray._upperBound30 = length30;
            newArray._upperBound31 = length31;
            newArray._upperBound32 = length32;
            return Unsafe.As<T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,]>(newArray);
        }

        // Since all multidimensional array handling is done via generics, and generics cannot be used with pointers
        // in C#, use TPointerDepthType to indicate the pointer rank of the array. This is *highly* inefficient, but
        // use of this feature is exceedingly rare.
        public static object PointerArrayCtor<TPointerDepthType>(int length1, int length2, int length3, int length4, int length5, int length6, int length7, int length8, int length9, int length10, int length11, int length12, int length13, int length14, int length15, int length16, int length17, int length18, int length19, int length20, int length21, int length22, int length23, int length24, int length25, int length26, int length27, int length28, int length29, int length30, int length31, int length32)
        {
            return MDPointerArrayHelper.GetMDPointerArray(EETypePtr.EETypePtrOf<TPointerDepthType>(), EETypePtr.EETypePtrOf<T>(), length1, length2, length3, length4, length5, length6, length7, length8, length9, length10, length11, length12, length13, length14, length15, length16, length17, length18, length19, length20, length21, length22, length23, length24, length25, length26, length27, length28, length29, length30, length31, length32);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ref T InternalAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, int index32)
        {
            MDArrayRank32<T> mdArrayObj = Unsafe.As<MDArrayRank32<T>>(array);
            if ((index1 < 0) || (index1 >= mdArrayObj._upperBound1))
                throw new IndexOutOfRangeException();
            if ((index2 < 0) || (index2 >= mdArrayObj._upperBound2))
                throw new IndexOutOfRangeException();
            if ((index3 < 0) || (index3 >= mdArrayObj._upperBound3))
                throw new IndexOutOfRangeException();
            if ((index4 < 0) || (index4 >= mdArrayObj._upperBound4))
                throw new IndexOutOfRangeException();
            if ((index5 < 0) || (index5 >= mdArrayObj._upperBound5))
                throw new IndexOutOfRangeException();
            if ((index6 < 0) || (index6 >= mdArrayObj._upperBound6))
                throw new IndexOutOfRangeException();
            if ((index7 < 0) || (index7 >= mdArrayObj._upperBound7))
                throw new IndexOutOfRangeException();
            if ((index8 < 0) || (index8 >= mdArrayObj._upperBound8))
                throw new IndexOutOfRangeException();
            if ((index9 < 0) || (index9 >= mdArrayObj._upperBound9))
                throw new IndexOutOfRangeException();
            if ((index10 < 0) || (index10 >= mdArrayObj._upperBound10))
                throw new IndexOutOfRangeException();
            if ((index11 < 0) || (index11 >= mdArrayObj._upperBound11))
                throw new IndexOutOfRangeException();
            if ((index12 < 0) || (index12 >= mdArrayObj._upperBound12))
                throw new IndexOutOfRangeException();
            if ((index13 < 0) || (index13 >= mdArrayObj._upperBound13))
                throw new IndexOutOfRangeException();
            if ((index14 < 0) || (index14 >= mdArrayObj._upperBound14))
                throw new IndexOutOfRangeException();
            if ((index15 < 0) || (index15 >= mdArrayObj._upperBound15))
                throw new IndexOutOfRangeException();
            if ((index16 < 0) || (index16 >= mdArrayObj._upperBound16))
                throw new IndexOutOfRangeException();
            if ((index17 < 0) || (index17 >= mdArrayObj._upperBound17))
                throw new IndexOutOfRangeException();
            if ((index18 < 0) || (index18 >= mdArrayObj._upperBound18))
                throw new IndexOutOfRangeException();
            if ((index19 < 0) || (index19 >= mdArrayObj._upperBound19))
                throw new IndexOutOfRangeException();
            if ((index20 < 0) || (index20 >= mdArrayObj._upperBound20))
                throw new IndexOutOfRangeException();
            if ((index21 < 0) || (index21 >= mdArrayObj._upperBound21))
                throw new IndexOutOfRangeException();
            if ((index22 < 0) || (index22 >= mdArrayObj._upperBound22))
                throw new IndexOutOfRangeException();
            if ((index23 < 0) || (index23 >= mdArrayObj._upperBound23))
                throw new IndexOutOfRangeException();
            if ((index24 < 0) || (index24 >= mdArrayObj._upperBound24))
                throw new IndexOutOfRangeException();
            if ((index25 < 0) || (index25 >= mdArrayObj._upperBound25))
                throw new IndexOutOfRangeException();
            if ((index26 < 0) || (index26 >= mdArrayObj._upperBound26))
                throw new IndexOutOfRangeException();
            if ((index27 < 0) || (index27 >= mdArrayObj._upperBound27))
                throw new IndexOutOfRangeException();
            if ((index28 < 0) || (index28 >= mdArrayObj._upperBound28))
                throw new IndexOutOfRangeException();
            if ((index29 < 0) || (index29 >= mdArrayObj._upperBound29))
                throw new IndexOutOfRangeException();
            if ((index30 < 0) || (index30 >= mdArrayObj._upperBound30))
                throw new IndexOutOfRangeException();
            if ((index31 < 0) || (index31 >= mdArrayObj._upperBound31))
                throw new IndexOutOfRangeException();
            if ((index32 < 0) || (index32 >= mdArrayObj._upperBound32))
                throw new IndexOutOfRangeException();

            int index = (((((((((((((((((((((((((((((((((((((((((((((((((((((((((((((index1 * mdArrayObj._upperBound2) + index2) * mdArrayObj._upperBound3) + index3) * mdArrayObj._upperBound4) + index4) * mdArrayObj._upperBound5) + index5) * mdArrayObj._upperBound6) + index6) * mdArrayObj._upperBound7) + index7) * mdArrayObj._upperBound8) + index8) * mdArrayObj._upperBound9) + index9) * mdArrayObj._upperBound10) + index10) * mdArrayObj._upperBound11) + index11) * mdArrayObj._upperBound12) + index12) * mdArrayObj._upperBound13) + index13) * mdArrayObj._upperBound14) + index14) * mdArrayObj._upperBound15) + index15) * mdArrayObj._upperBound16) + index16) * mdArrayObj._upperBound17) + index17) * mdArrayObj._upperBound18) + index18) * mdArrayObj._upperBound19) + index19) * mdArrayObj._upperBound20) + index20) * mdArrayObj._upperBound21) + index21) * mdArrayObj._upperBound22) + index22) * mdArrayObj._upperBound23) + index23) * mdArrayObj._upperBound24) + index24) * mdArrayObj._upperBound25) + index25) * mdArrayObj._upperBound26) + index26) * mdArrayObj._upperBound27) + index27) * mdArrayObj._upperBound28) + index28) * mdArrayObj._upperBound29) + index29) * mdArrayObj._upperBound30) + index30) * mdArrayObj._upperBound31) + index31) * mdArrayObj._upperBound32) + index32;
            return ref Unsafe.Add(ref mdArrayObj._data, index);
        }

        public static ref T Address(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, int index32)
        {
            ref T returnValue = ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31, index32);
            if (RuntimeHelpers.IsReference<T>())
            {
                if (!EETypePtr.EETypePtrOf<T>().FastEquals(array.EETypePtr.ArrayElementType))
                    throw new ArrayTypeMismatchException();
            }
            return ref returnValue;
        }

        public static ref T ReadonlyAddress(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, int index32)
        {
            return ref InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31, index32);
        }

        public static T Get(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, int index32)
        {
            return InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31, index32);
        }

        public static void Set(T[,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,] array, int index1, int index2, int index3, int index4, int index5, int index6, int index7, int index8, int index9, int index10, int index11, int index12, int index13, int index14, int index15, int index16, int index17, int index18, int index19, int index20, int index21, int index22, int index23, int index24, int index25, int index26, int index27, int index28, int index29, int index30, int index31, int index32, T value)
        {
            if (RuntimeHelpers.IsReference<T>())
            {
                RuntimeImports.RhCheckArrayStore(array, value);
            }

            InternalAddress(array, index1, index2, index3, index4, index5, index6, index7, index8, index9, index10, index11, index12, index13, index14, index15, index16, index17, index18, index19, index20, index21, index22, index23, index24, index25, index26, index27, index28, index29, index30, index31, index32) = value;
        }
    }

}
