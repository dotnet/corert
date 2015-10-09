// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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
    // When building this solution (Nov 2014) there were applications in the
    // Windows app store that contained references to Arrays up to seven
    // dimensions. Because of this there will be simpler but slower paths for
    // the eight+ dimension arrays.
    //
    // The desktop CLR supports arrays of up to 32 dimensions so that provides
    // an upper limit on how much this needs to be built out.

    // MDArray is a middle man class to help reflection.
    public abstract class MDArray : Array
    {
        public abstract int MDGetUpperBound(int dimension);
        public abstract void MDSetValue(Object value, params int[] indices);
        public abstract Object MDGetValue(params int[] indices);
        public abstract int MDRank { get; }
        public abstract void MDInitialize(int[] indices);

        // Exposes access to the SZArray that represents the "flattened view" of the array.
        // This is used to implement api's that accept multidim arrays and operate on them
        // as if they were actually SZArrays that concatenate the rows of the multidim array.
        internal abstract Array MDFlattenedArray { get; }
    }

    public abstract class MDArrayRank2 : MDArray
    {
        public sealed override int MDRank
        {
            get
            {
                return 2;
            }
        }
    }

    public class MDArrayRank2<T> : MDArrayRank2
    {
        // Do not use field initializers on these fields. The implementation of CreateInstance() bypasses the normal constructor
        // and calls MDInitialize() directly, so MDInitialize() must explicitly initialize all of the fields.

        // The compiler takes a dependency on field ordering of this type, m_array must be first.
        private T[] _array;

        // Debugger(DBI) takes dependency on the ordering and layout of these fields.
        // Please update DBI whenever these fields are updated or moved around.
        // see ICorDebugArrayValue methods in rh\src\debug\dbi\values.cpp
        private int _upperBound1;
        private int _upperBound2;

        // NoInlining is currently required because NUTC does not support inling of this constructor
        // during static initialization of multi-dimensional arrays.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public MDArrayRank2(int length1, int length2)
        {
            Initialize(length1, length2);
        }

        public override Object MDGetValue(params int[] indices)
        {
            if (indices.Length != 2)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1]);

            return _array[ComputeIndex(indices[0], indices[1])];
        }

        public T Get(int index1, int index2)
        {
            CheckBounds(index1, index2);

            return _array[ComputeIndex(index1, index2)];
        }

        public override void MDSetValue(object value, params int[] indices)
        {
            if (indices.Length != 2)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1]);

            _array.SetValue(value, ComputeIndex(indices[0], indices[1]));
        }

        public override int MDGetUpperBound(int dimension)
        {
            switch (dimension)
            {
                case 0:
                    return _upperBound1 - 1;
                case 1:
                    return _upperBound2 - 1;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public sealed override void MDInitialize(int[] indices)
        {
            Initialize(indices[0], indices[1]);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public void CheckBounds(int index1, int index2)
        {
            if ((index1 < 0) || (index1 >= _upperBound1))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index2 < 0) || (index2 >= _upperBound2))
            {
                throw new IndexOutOfRangeException();
            }
        }

        // This logic is duplicated in Redhawk DBI (implementation of ICorDebugArrayValue::GetElement)
        // We should update it too whenever this method changes.
        public int ComputeIndex(int index1, int index2)
        {
            // This arithmatic is unchecked because it is not possible to create
            // an array that would satisfy bounds checks AND overflow the math
            // here.
            return (_upperBound2 * index1) + index2;
        }

        public void Set(int index1, int index2, T value)
        {
            CheckBounds(index1, index2);

            _array[ComputeIndex(index1, index2)] = value;
        }

        internal sealed override Array MDFlattenedArray
        {
            get
            {
                return _array;
            }
        }

        private void Initialize(int length1, int length2)
        {
            if (length1 < 0 || length2 < 0)
                throw new OverflowException();

            _array = new T[checked(length1 * length2)];
            _upperBound1 = length1;
            _upperBound2 = length2;
            SetLength(_array.Length);
        }
    }

    public abstract class MDArrayRank3 : MDArray
    {
        public sealed override int MDRank
        {
            get
            {
                return 3;
            }
        }
    }

    public class MDArrayRank3<T> : MDArrayRank3
    {
        // Do not use field initializers on these fields. The implementation of CreateInstance() bypasses the normal constructor
        // and calls MDInitialize() directly, so MDInitialize() must explicitly initialize all of the fields.

        // The compiler takes a dependency on field ordering of this type, m_array must be first.
        private T[] _array;

        // Debugger(DBI) takes dependency on the ordering and layout of these fields.
        // Please update DBI whenever these fields are updated or moved around.
        // see ICorDebugArrayValue methods in rh\src\debug\dbi\values.cpp
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;

        // NoInlining is currently required because NUTC does not support inling of this constructor
        // during static initialization of multi-dimensional arrays.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public MDArrayRank3(int length1, int length2, int length3)
        {
            Initialize(length1, length2, length3);
        }

        public override Object MDGetValue(params int[] indices)
        {
            if (indices.Length != 3)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1], indices[2]);

            return _array[ComputeIndex(indices[0], indices[1], indices[2])];
        }

        public T Get(int index1, int index2, int index3)
        {
            CheckBounds(index1, index2, index3);

            return _array[ComputeIndex(index1, index2, index3)];
        }

        public override void MDSetValue(object value, params int[] indices)
        {
            if (indices.Length != 3)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1], indices[2]);

            _array.SetValue(value, ComputeIndex(indices[0], indices[1], indices[2]));
        }

        public override int MDGetUpperBound(int dimension)
        {
            switch (dimension)
            {
                case 0:
                    return _upperBound1 - 1;
                case 1:
                    return _upperBound2 - 1;
                case 2:
                    return _upperBound3 - 1;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public sealed override void MDInitialize(int[] indices)
        {
            Initialize(indices[0], indices[1], indices[2]);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public void CheckBounds(int index1, int index2, int index3)
        {
            if ((index1 < 0) || (index1 >= _upperBound1))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index2 < 0) || (index2 >= _upperBound2))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index3 < 0) || (index3 >= _upperBound3))
            {
                throw new IndexOutOfRangeException();
            }
        }

        // This logic is duplicated in Redhawk DBI (implementation of ICorDebugArrayValue::GetElement)
        // We should update it too whenever this method changes.
        public int ComputeIndex(int index1, int index2, int index3)
        {
            // This arithmatic is unchecked because it is not possible to create
            // an array that would satisfy bounds checks AND overflow the math
            // here.
            return (_upperBound3 * _upperBound2 * index1) +
                   (_upperBound3 * index2) +
                    index3;
        }

        public void Set(int index1, int index2, int index3, T value)
        {
            CheckBounds(index1, index2, index3);

            _array[ComputeIndex(index1, index2, index3)] = value;
        }

        internal sealed override Array MDFlattenedArray
        {
            get
            {
                return _array;
            }
        }

        private void Initialize(int length1, int length2, int length3)
        {
            if (length1 < 0 || length2 < 0 || length3 < 0)
                throw new OverflowException();

            _array = new T[checked(length1 * length2 * length3)];
            _upperBound1 = length1;
            _upperBound2 = length2;
            _upperBound3 = length3;
            SetLength(_array.Length);
        }
    }

    public abstract class MDArrayRank4 : MDArray
    {
        public sealed override int MDRank
        {
            get
            {
                return 4;
            }
        }
    }

    public class MDArrayRank4<T> : MDArrayRank4
    {
        // Do not use field initializers on these fields. The implementation of CreateInstance() bypasses the normal constructor
        // and calls MDInitialize() directly, so MDInitialize() must explicitly initialize all of the fields.

        // The compiler takes a dependency on field ordering of this type, m_array must be first.
        private T[] _array;

        // Debugger(DBI) takes dependency on the ordering and layout of these fields.
        // Please update DBI whenever these fields are updated or moved around.
        // see ICorDebugArrayValue methods in rh\src\debug\dbi\values.cpp
        private int _upperBound1;
        private int _upperBound2;
        private int _upperBound3;
        private int _upperBound4;

        // NoInlining is currently required because NUTC does not support inlining of this constructor
        // during static initialization of multi-dimensional arrays. See Dev12 Bug 743787.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public MDArrayRank4(int length1, int length2, int length3, int length4)
        {
            Initialize(length1, length2, length3, length4);
        }

        public override Object MDGetValue(params int[] indices)
        {
            if (indices.Length != 4)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1], indices[2], indices[3]);

            return _array[ComputeIndex(indices[0], indices[1], indices[2], indices[3])];
        }

        public T Get(int index1, int index2, int index3, int index4)
        {
            CheckBounds(index1, index2, index3, index4);

            return _array[ComputeIndex(index1, index2, index3, index4)];
        }

        public override void MDSetValue(object value, params int[] indices)
        {
            if (indices.Length != 4)
                throw new ArgumentException(SR.Arg_RankIndices);

            CheckBounds(indices[0], indices[1], indices[2], indices[3]);

            _array.SetValue(value, ComputeIndex(indices[0], indices[1], indices[2], indices[3]));
        }

        public override int MDGetUpperBound(int dimension)
        {
            switch (dimension)
            {
                case 0:
                    return _upperBound1 - 1;
                case 1:
                    return _upperBound2 - 1;
                case 2:
                    return _upperBound3 - 1;
                case 3:
                    return _upperBound4 - 1;
                default:
                    throw new IndexOutOfRangeException();
            }
        }

        public sealed override void MDInitialize(int[] indices)
        {
            Initialize(indices[0], indices[1], indices[2], indices[3]);
        }

        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        public void CheckBounds(int index1, int index2, int index3, int index4)
        {
            if ((index1 < 0) || (index1 >= _upperBound1))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index2 < 0) || (index2 >= _upperBound2))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index3 < 0) || (index3 >= _upperBound3))
            {
                throw new IndexOutOfRangeException();
            }
            if ((index4 < 0) || (index4 >= _upperBound4))
            {
                throw new IndexOutOfRangeException();
            }
        }

        // This logic is duplicated in Redhawk DBI (implementation of ICorDebugArrayValue::GetElement)
        // We should update it too whenever this method changes.
        public int ComputeIndex(int index1, int index2, int index3, int index4)
        {
            // This arithmatic is unchecked because it is not possible to create
            // an array that would satisfy bounds checks AND overflow the math
            // here.
            return (_upperBound4 * _upperBound3 * _upperBound2 * index1) +
                   (_upperBound4 * _upperBound3 * index2) +
                   (_upperBound4 * index3) +
                    index4;
        }

        public void Set(int index1, int index2, int index3, int index4, T value)
        {
            CheckBounds(index1, index2, index3, index4);

            _array[ComputeIndex(index1, index2, index3, index4)] = value;
        }

        internal sealed override Array MDFlattenedArray
        {
            get
            {
                return _array;
            }
        }

        private void Initialize(int length1, int length2, int length3, int length4)
        {
            if (length1 < 0 || length2 < 0 || length3 < 0 || length4 < 0)
                throw new OverflowException();

            _array = new T[checked(length1 * length2 * length3 * length4)];
            _upperBound1 = length1;
            _upperBound2 = length2;
            _upperBound3 = length3;
            _upperBound4 = length4;
            SetLength(_array.Length);
        }
    }
}
