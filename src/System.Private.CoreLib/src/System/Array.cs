// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;

using Internal.Runtime.Augments;
using Internal.Reflection.Core.NonPortable;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    // Note that we make a T[] (single-dimensional w/ zero as the lower bound) implement both 
    // IList<U> and IReadOnlyList<U>, where T : U dynamically.  See the SZArrayHelper class for details.
    public abstract class Array : ICollection, IEnumerable, IList, IStructuralComparable, IStructuralEquatable
    {
        // This ctor exists solely to prevent C# from generating a protected .ctor that violates the surface area. I really want this to be a
        // "protected-and-internal" rather than "internal" but C# has no keyword for the former.
        internal Array() { }


        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // This field should be the first field in Array as the runtime/compilers depend on it
        private int _numComponents;
#pragma warning restore

#if BIT64
        private const int POINTER_SIZE = 8;
        private const int PADDING = 1; // _numComponents is padded by one Int32 to make the first element pointer-aligned
#else
        private const int POINTER_SIZE = 4;
        private const int PADDING = 0;
#endif
        //                                     Header       + m_pEEType    + _numComponents (with an optional padding)
        internal const int SZARRAY_BASE_SIZE = POINTER_SIZE + POINTER_SIZE + (1 + PADDING) * 4;

        public int Length
        {
            get
            {
                // NOTE: The compiler has assumptions about the implementation of this method.
                // Changing the implementation here (or even deleting this) will NOT have the desired impact
                return _numComponents;
            }
        }

        internal bool IsSzArray
        {
            get
            {
#if REAL_MULTIDIM_ARRAYS
                return this.EETypePtr.BaseSize == SZARRAY_BASE_SIZE;
#else
                return !(this is MDArray);
#endif
            }
        }

        internal void SetLength(int length)
        {
            _numComponents = length;
        }

        public static Array CreateInstance(Type elementType, int length)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException("elementType");

            Contract.Ensures(Contract.Result<Array>() != null);
            Contract.Ensures(Contract.Result<Array>().Rank == 1);
            Contract.EndContractBlock();

            return CreateSzArray(elementType, length);
        }

        public static Array CreateInstance(Type elementType, params int[] lengths)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException("elementType");
            if (lengths == null)
                throw new ArgumentNullException("lengths");
            if (lengths.Length == 0)
                throw new ArgumentException(SR.Arg_NeedAtLeast1Rank);

            Contract.Ensures(Contract.Result<Array>() != null);
            Contract.Ensures(Contract.Result<Array>().Rank == lengths.Length);
            Contract.EndContractBlock();

            if (lengths.Length == 1)
            {
                int length = lengths[0];
                return CreateSzArray(elementType, length);
            }
            else
            {
                return CreateMultiDimArray(elementType, lengths, null);
            }
        }

        public static Array CreateInstance(Type elementType, int[] lengths, int[] lowerBounds)
        {
            if (elementType == null)
                throw new ArgumentNullException("elementType");
            if (lengths == null)
                throw new ArgumentNullException("lengths");
            if (lowerBounds == null)
                throw new ArgumentNullException("lowerBounds");
            if (lengths.Length != lowerBounds.Length)
                throw new ArgumentException(SR.Arg_RanksAndBounds);
            if (lengths.Length == 0)
                throw new ArgumentException(SR.Arg_NeedAtLeast1Rank);
            Contract.Ensures(Contract.Result<Array>() != null);
            Contract.Ensures(Contract.Result<Array>().Rank == lengths.Length);
            Contract.EndContractBlock();

            if (lengths.Length == 1 && lowerBounds[0] == 0)
            {
                int length = lengths[0];
                return CreateSzArray(elementType, length);
            }
            else
            {
                return CreateMultiDimArray(elementType, lengths, lowerBounds);
            }
        }

        private static Array CreateSzArray(Type elementType, int length)
        {
            // Though our callers already validated length once, this parameter is passed via arrays, so we must check it again
            // in case a malicious caller modified the array after the check.
            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            Type arrayType = GetArrayTypeFromElementType(elementType, false, 1);
            return RuntimeImports.RhNewArray(arrayType.TypeHandle.ToEETypePtr(), length);
        }

        private static Array CreateMultiDimArray(Type elementType, int[] lengths, int[] lowerBounds)
        {
            Debug.Assert(lengths != null);
            Debug.Assert(lowerBounds == null || lowerBounds.Length == lengths.Length);

            for (int i = 0; i < lengths.Length; i++)
            {
                if (lengths[i] < 0)
                    throw new ArgumentOutOfRangeException("lengths[" + i + "]", SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            int rank = lengths.Length;
            Type arrayType = GetArrayTypeFromElementType(elementType, true, rank);
            return RuntimeAugments.NewMultiDimArray(arrayType.TypeHandle, lengths, lowerBounds);
        }

        private static Type GetArrayTypeFromElementType(Type elementType, bool multiDim, int rank)
        {
            RuntimeType runtimeElementType = elementType as RuntimeType;
            if (runtimeElementType == null)
                throw new InvalidOperationException(SR.InvalidOperation_ArrayCreateInstance_NotARuntimeType);
            if (runtimeElementType.Equals(typeof(void)))
                throw new NotSupportedException(SR.NotSupported_VoidArray);

            try
            {
                if (multiDim)
                    return ReflectionCoreNonPortable.GetMultiDimArrayType(runtimeElementType, rank);
                else
                    return ReflectionCoreNonPortable.GetArrayType(runtimeElementType);
            }
            catch
            {
                if (runtimeElementType.InternalIsOpen)
                    throw new NotSupportedException(SR.NotSupported_OpenType);
                throw;
            }
        }


        public void Initialize()
        {
            // Project N port note: On the desktop, this api is a nop unless the array element type is a value type with
            // an explicit nullary constructor. Such a type cannot be expressed in C# so Project N does not support this.
            // The ILC toolchain fails the build if it encounters such a type.
            return;
        }

        // If you use C#'s 'fixed' statement to get the address of m_pEEType, you want to pass it into this
        // function to get the address of the first field.  NOTE: If you use GetAddrOfPinnedObject instead,
        // C# may optimize away the pinned local, producing incorrect results.
        static internal unsafe byte* GetAddrOfPinnedArrayFromEETypeField(IntPtr* ppEEType)
        {
#if REAL_MULTIDIM_ARRAYS
            // -POINTER_SIZE to account for the sync block
            return (byte*)ppEEType + new EETypePtr(*ppEEType).BaseSize - POINTER_SIZE;
#else
            return (byte*)ppEEType + sizeof(EETypePtr) + ((1 + PADDING) * sizeof(int));
#endif
        }


        //public static ReadOnlyCollection<T> AsReadOnly<T>(T[] array) {
        //    if (array == null) {
        //        throw new ArgumentNullException("array");                
        //    }

        //    // T[] implements IList<T>.
        //    return new ReadOnlyCollection<T>(array);
        //}

        public static void Resize<T>(ref T[] array, int newSize)
        {
            if (newSize < 0)
                throw new ArgumentOutOfRangeException("newSize", SR.ArgumentOutOfRange_NeedNonNegNum);

            T[] larray = array;
            if (larray == null)
            {
                array = new T[newSize];
                return;
            }

            if (larray.Length != newSize)
            {
                T[] newArray = new T[newSize];
                Copy(larray, 0, newArray, 0, larray.Length > newSize ? newSize : larray.Length);
                array = newArray;
            }
        }

        // Copies length elements from sourceArray, starting at sourceIndex, to
        // destinationArray, starting at destinationIndex.
        //
        public static void Copy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
        {
            if (!RuntimeImports.TryArrayCopy(sourceArray, sourceIndex, destinationArray, destinationIndex, length))
                CopyImpl(sourceArray, sourceIndex, destinationArray, destinationIndex, length, false);
        }

        // Provides a strong exception guarantee - either it succeeds, or
        // it throws an exception with no side effects.  The arrays must be
        // compatible array types based on the array element type - this 
        // method does not support casting, boxing, or primitive widening.
        // It will up-cast, assuming the array types are correct.
        public static void ConstrainedCopy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
        {
            if (!RuntimeImports.TryArrayCopy(sourceArray, sourceIndex, destinationArray, destinationIndex, length))
                CopyImpl(sourceArray, sourceIndex, destinationArray, destinationIndex, length, true);
        }

        public static void Copy(Array sourceArray, Array destinationArray, int length)
        {
            if (!RuntimeImports.TryArrayCopy(sourceArray, 0, destinationArray, 0, length))
                CopyImpl(sourceArray, 0, destinationArray, 0, length, false);
        }

        //
        // Funnel for all the Array.Copy() overloads. The "reliable" parameter indicates whether the caller for ConstrainedCopy()
        // (must leave destination array unchanged on any exception.)
        //
        private static unsafe void CopyImpl(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            if (sourceArray == null)
                throw new ArgumentNullException("sourceArray");
            if (destinationArray == null)
                throw new ArgumentNullException("destinationArray");

            int sourceRank = sourceArray.Rank;
            int destinationRank = destinationArray.Rank;
            if (sourceRank != destinationRank)
                throw new RankException(SR.Rank_MultiDimNotSupported);

            sourceArray = sourceArray.FlattenedArray;
            destinationArray = destinationArray.FlattenedArray;

            if ((sourceIndex < 0) || (destinationIndex < 0) || (length < 0))
                throw new ArgumentOutOfRangeException();
            if ((length > sourceArray.Length) || length > destinationArray.Length)
                throw new ArgumentException();
            if ((length > sourceArray.Length - sourceIndex) || (length > destinationArray.Length - destinationIndex))
                throw new ArgumentException();

            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            EETypePtr destinationElementEEType = destinationArray.ElementEEType;

            if (!(destinationElementEEType.IsValueType))
            {
                if (!(sourceElementEEType.IsValueType))
                {
                    CopyImplGcRefArray((Object[])sourceArray, sourceIndex, (Object[])destinationArray, destinationIndex, length, reliable);
                }
                else if (RuntimeImports.AreTypesAssignable(sourceElementEEType, destinationElementEEType))
                {
                    CopyImplValueTypeArrayToReferenceArray(sourceArray, sourceIndex, (Object[])destinationArray, destinationIndex, length, reliable);
                }
                else
                {
                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                }
            }
            else
            {
                if (RuntimeImports.AreTypesEquivalent(sourceElementEEType, destinationElementEEType))
                {
                    if (sourceElementEEType.HasPointers)
                    {
                        CopyImplValueTypeArrayWithInnerGcRefs(sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
                    }
                    else
                    {
                        CopyImplValueTypeArrayNoInnerGcRefs(sourceArray, sourceIndex, destinationArray, destinationIndex, length);
                    }
                }
                else if (IsSourceElementABaseClassOrInterfaceOfDestinationValueType(sourceElementEEType, destinationElementEEType))
                {
                    CopyImplReferenceArrayToValueTypeArray((Object[])sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
                }
                else
                {
                    // The only case remaining is that primitive types could have a widening conversion between the source element type and the destination
                    // If a widening conversion does not exist we are going to throw an ArrayTypeMismatchException from it.
                    CopyImplPrimitiveTypeWithWidening(sourceArray, sourceIndex, destinationArray, destinationIndex, length);
                }
            }
        }

        private static bool IsSourceElementABaseClassOrInterfaceOfDestinationValueType(EETypePtr sourceElementEEType, EETypePtr destinationElementEEType)
        {
            if (sourceElementEEType.IsValueType)
                return false;

            // It may look like we're passing the arguments to AreTypesAssignable in the wrong order but we're not. The source array is an interface or Object array, the destination
            // array is a value type array. Our job is to check if the destination value type implements the interface - which is what this call to AreTypesAssignable does.
            // The copy loop still checks each element to make sure it actually is the correct valuetype.
            if (!RuntimeImports.AreTypesAssignable(destinationElementEEType, sourceElementEEType))
                return false;
            return true;
        }

        //
        // Array.CopyImpl case: Gc-ref array to gc-ref array copy.
        //
        private static unsafe void CopyImplGcRefArray(Object[] sourceArray, int sourceIndex, Object[] destinationArray, int destinationIndex, int length, bool reliable)
        {
            // For mismatched array types, the desktop Array.Copy has a policy that determines whether to throw an ArrayTypeMismatch without any attempt to copy
            // or to throw an InvalidCastException in the middle of a copy. This code replicates that policy.
            bool attemptCopy = false;
            if (reliable)
            {
                attemptCopy = (sourceArray.EETypePtr == destinationArray.EETypePtr);
            }
            else
            {
                EETypePtr sourceElementEEType = sourceArray.ElementEEType;
                EETypePtr destinationElementEEType = destinationArray.ElementEEType;
                attemptCopy = attemptCopy || RuntimeImports.AreTypesAssignable(sourceElementEEType, destinationElementEEType);
                attemptCopy = attemptCopy || RuntimeImports.AreTypesAssignable(destinationElementEEType, sourceElementEEType);

                // If either array is an interface array, we allow the attempt to copy even if the other element type does not statically implement the interface.
                // We don't have an "IsInterface" property in EETypePtr so we instead check for a null BaseType. The only the other EEType with a null BaseType is
                // System.Object but if that were the case, we would already have passed one of the AreTypesAssignable checks above.
                attemptCopy = attemptCopy || sourceElementEEType.BaseType.IsNull;
                attemptCopy = attemptCopy || destinationElementEEType.BaseType.IsNull;
            }
            if (!attemptCopy)
                throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);

            bool reverseCopy = ((object)sourceArray == (object)destinationArray) && (sourceIndex < destinationIndex);
            try
            {
                if (reverseCopy)
                {
                    sourceIndex += length - 1;
                    destinationIndex += length - 1;
                    for (int i = 0; i < length; i++)
                        destinationArray[destinationIndex - i] = sourceArray[sourceIndex - i];
                }
                else
                {
                    for (int i = 0; i < length; i++)
                        destinationArray[destinationIndex + i] = sourceArray[sourceIndex + i];
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new InvalidCastException(SR.InvalidCast_DownCastArrayElement);
            }
        }

        //
        // Array.CopyImpl case: Value-type array to Object[] or interface array copy.
        //
        private static unsafe void CopyImplValueTypeArrayToReferenceArray(Array sourceArray, int sourceIndex, Object[] destinationArray, int destinationIndex, int length, bool reliable)
        {
            if (reliable)
                throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);

            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            nuint sourceElementSize = sourceArray.ElementSize;

            fixed (IntPtr* pSourceArray = &sourceArray.m_pEEType)
            {
                byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pSourceArray)
                                            + (nuint)sourceIndex * sourceElementSize;

                for (int i = 0; i < length; i++)
                {
                    Object boxedValue = RuntimeImports.RhBox(sourceElementEEType, pElement);
                    destinationArray[destinationIndex + i] = boxedValue;
                    pElement += sourceElementSize;
                }
            }
        }

        //
        // Array.CopyImpl case: Object[] or interface array to value-type array copy.
        //
        private static unsafe void CopyImplReferenceArrayToValueTypeArray(Object[] sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            if (reliable)
                throw new ArrayTypeMismatchException();

            EETypePtr destinationElementEEType = destinationArray.ElementEEType;
            nuint destinationElementSize = destinationArray.ElementSize;
            bool isNullable = destinationElementEEType.IsNullable;

            fixed (IntPtr* pDestinationArray = &destinationArray.m_pEEType)
            {
                byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pDestinationArray)
                                            + (nuint)destinationIndex * destinationElementSize;

                for (int i = 0; i < length; i++)
                {
                    Object boxedValue = sourceArray[sourceIndex + i];

                    if (boxedValue == null)
                    {
                        if (!isNullable)
                            throw new InvalidCastException(SR.InvalidCast_DownCastArrayElement);
                    }
                    else
                    {
                        EETypePtr eeType = boxedValue.EETypePtr;
                        if (!(RuntimeImports.AreTypesAssignable(eeType, destinationElementEEType)))
                            throw new InvalidCastException(SR.InvalidCast_DownCastArrayElement);
                    }

                    RuntimeImports.RhUnbox(boxedValue, pElement, destinationElementEEType);
                    pElement += destinationElementSize;
                }
            }
        }





        //
        // Array.CopyImpl case: Value-type array with embedded gc-references. 
        //
        private static unsafe void CopyImplValueTypeArrayWithInnerGcRefs(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            EETypePtr sourceElementEEType = sourceArray.EETypePtr.ArrayElementType;
            bool reverseCopy = ((object)sourceArray == (object)destinationArray) && (sourceIndex < destinationIndex);

            // Copy scenario: ValueType-array to value-type array with embedded gc-refs.
            object[] boxedElements = null;
            if (reliable)
            {
                boxedElements = new object[length];
                reverseCopy = false;
            }

            fixed (IntPtr* pDstArray = &destinationArray.m_pEEType, pSrcArray = &sourceArray.m_pEEType)
            {
                nuint cbElementSize = sourceArray.ElementSize;
                byte* pSourceElement = Array.GetAddrOfPinnedArrayFromEETypeField(pSrcArray) + (nuint)sourceIndex * cbElementSize;
                byte* pDestinationElement = Array.GetAddrOfPinnedArrayFromEETypeField(pDstArray) + (nuint)destinationIndex * cbElementSize;
                if (reverseCopy)
                {
                    pSourceElement += (nuint)length * cbElementSize;
                    pDestinationElement += (nuint)length * cbElementSize;
                }

                for (int i = 0; i < length; i++)
                {
                    if (reverseCopy)
                    {
                        pSourceElement -= cbElementSize;
                        pDestinationElement -= cbElementSize;
                    }

                    object boxedValue = RuntimeImports.RhBox(sourceElementEEType, pSourceElement);
                    if (reliable)
                        boxedElements[i] = boxedValue;
                    else
                        RuntimeImports.RhUnbox(boxedValue, pDestinationElement, sourceElementEEType);

                    if (!reverseCopy)
                    {
                        pSourceElement += cbElementSize;
                        pDestinationElement += cbElementSize;
                    }
                }
            }

            if (reliable)
            {
                for (int i = 0; i < length; i++)
                    destinationArray.SetValue(boxedElements[i], destinationIndex + i);
            }
        }

        //
        // Array.CopyImpl case: Value-type array without embedded gc-references. 
        //
        internal static unsafe void CopyImplValueTypeArrayNoInnerGcRefs(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
        {
            Debug.Assert(sourceArray.ElementEEType.IsValueType && !sourceArray.ElementEEType.HasPointers);
            Debug.Assert(destinationArray.ElementEEType.IsValueType && !destinationArray.ElementEEType.HasPointers);

            // Copy scenario: ValueType-array to value-type array with no embedded gc-refs.
            nuint elementSize = sourceArray.ElementSize;
            fixed (IntPtr* pSrcArray = &sourceArray.m_pEEType, pDstArray = &destinationArray.m_pEEType)
            {
                byte* pSrcElements = Array.GetAddrOfPinnedArrayFromEETypeField(pSrcArray) + (nuint)sourceIndex * elementSize;
                byte* pDstElements = Array.GetAddrOfPinnedArrayFromEETypeField(pDstArray) + (nuint)destinationIndex * elementSize;
                nuint cbCopy = elementSize * (nuint)length;
                Buffer.Memmove(pDstElements, pSrcElements, cbCopy);
            }
        }

        //
        // Array.CopyImpl case: Primitive types that have a widening conversion
        //
        private static unsafe void CopyImplPrimitiveTypeWithWidening(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length)
        {
            RuntimeImports.RhCorElementType sourceElementType = sourceArray.EETypePtr.ArrayElementType.CorElementType;
            RuntimeImports.RhCorElementType destElementType = destinationArray.EETypePtr.ArrayElementType.CorElementType;

            nuint srcElementSize = sourceArray.ElementSize;
            nuint destElementSize = destinationArray.ElementSize;

            fixed (IntPtr* pSrcArray = &sourceArray.m_pEEType, pDstArray = &destinationArray.m_pEEType)
            {
                byte* srcData = Array.GetAddrOfPinnedArrayFromEETypeField(pSrcArray) + (nuint)sourceIndex * srcElementSize;
                byte* data = Array.GetAddrOfPinnedArrayFromEETypeField(pDstArray) + (nuint)destinationIndex * destElementSize;

                for (int i = 0; i < length; i++, srcData += srcElementSize, data += destElementSize)
                {
                    // We pretty much have to do some fancy datatype mangling every time here, for
                    // converting w/ sign extension and floating point conversions.
                    switch (sourceElementType)
                    {
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U1:
                            {
                                switch (destElementType)
                                {
                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                        *(float*)data = *(byte*)srcData;
                                        break;

                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                        *(double*)data = *(byte*)srcData;
                                        break;

                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                                        *(short*)data = *(byte*)srcData;
                                        break;

                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                                        *(int*)data = *(byte*)srcData;
                                        break;

                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                    case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                                        *(long*)data = *(byte*)srcData;
                                        break;

                                    default:
                                        throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                                }
                                break;
                            }

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I1:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                                    *(short*)data = *(sbyte*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                                    *(int*)data = *(sbyte*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                    *(long*)data = *(sbyte*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = *(sbyte*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(sbyte*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = *(ushort*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(ushort*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U2:
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_CHAR:
                                    *(ushort*)data = *(ushort*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                                    *(uint*)data = *(ushort*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                                    *(ulong*)data = *(ushort*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I2:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                                    *(int*)data = *(short*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                    *(long*)data = *(short*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = *(short*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(short*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I4:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                    *(long*)data = *(int*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = (float)*(int*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(int*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U4:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                                    *(long*)data = *(uint*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = (float)*(uint*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(uint*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;


                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_I8:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                                    *(float*)data = (float)*(long*)srcData;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = (double)*(long*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_U8:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:

                                    //*(float*) data = (float) *(Ulong*)srcData;
                                    long srcValToFloat = *(long*)srcData;
                                    float f = (float)srcValToFloat;
                                    if (srcValToFloat < 0)
                                        f += 4294967296.0f * 4294967296.0f; // This is 2^64

                                    *(float*)data = f;
                                    break;

                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    //*(double*) data = (double) *(Ulong*)srcData;
                                    long srcValToDouble = *(long*)srcData;
                                    double d = (double)srcValToDouble;
                                    if (srcValToDouble < 0)
                                        d += 4294967296.0 * 4294967296.0;   // This is 2^64

                                    *(double*)data = d;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R4:
                            switch (destElementType)
                            {
                                case RuntimeImports.RhCorElementType.ELEMENT_TYPE_R8:
                                    *(double*)data = *(float*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        default:
                            throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                    }
                }
            }
        }

        /// <summary>
        /// Copy the contents of a native buffer into a managed array.  This requires that the type of the
        /// destination array be blittable.
        /// </summary>
        /// <param name="source">Unmanaged memory to copy from.</param>
        /// <param name="destination">Array to copy into.  The type of the elements of the array must be blittable</param>
        /// <param name="startIndex">First index in the destination array to begin copying into</param>
        /// <param name="length">Number of elements to copy</param>
        internal static unsafe void CopyToManaged(IntPtr source, Array destination, int startIndex, int length)
        {
            if (source == IntPtr.Zero)
                throw new ArgumentNullException("source");
            if (destination == null)
                throw new ArgumentNullException("destination");
            if (!destination.IsElementTypeBlittable)
                throw new ArgumentException("destination", SR.Arg_CopyNonBlittableArray);
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.Arg_CopyOutOfRange);
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.Arg_CopyOutOfRange);
            if ((uint)startIndex + (uint)length > (uint)destination.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.Arg_CopyOutOfRange);

            nuint bytesToCopy = (nuint)length * destination.ElementSize;
            nuint startOffset = (nuint)startIndex * destination.ElementSize;

            fixed (IntPtr* destinationEEType = &destination.m_pEEType)
            {
                byte* destinationData = Array.GetAddrOfPinnedArrayFromEETypeField(destinationEEType) + startOffset;
                Buffer.Memmove(destinationData, (byte*)source, bytesToCopy);
            }
        }

        /// <summary>
        /// Copy the contents of the source array into unmanaged memory.  This requires that the type of
        /// the source array be blittable.
        /// </summary>
        /// <param name="source">Array to copy from.  This must be non-null and have blittable elements</param>
        /// <param name="startIndex">First index in the source array to begin copying</param>
        /// <param name="destination">Pointer to the unmanaged memory to blit the memory into</param>
        /// <param name="length">Number of elements to copy</param>
        internal static unsafe void CopyToNative(Array source, int startIndex, IntPtr destination, int length)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (!source.IsElementTypeBlittable)
                throw new ArgumentException("source", SR.Arg_CopyNonBlittableArray);
            if (destination == IntPtr.Zero)
                throw new ArgumentNullException("destination");
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", SR.Arg_CopyOutOfRange);
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", SR.Arg_CopyOutOfRange);
            if ((uint)startIndex + (uint)length > (uint)source.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.Arg_CopyOutOfRange);
            Contract.EndContractBlock();

            nuint bytesToCopy = (nuint)length * source.ElementSize;
            nuint startOffset = (nuint)startIndex * source.ElementSize;

            fixed (IntPtr* sourceEEType = &source.m_pEEType)
            {
                byte* sourceData = Array.GetAddrOfPinnedArrayFromEETypeField(sourceEEType) + startOffset;
                Buffer.Memmove((byte*)destination, sourceData, bytesToCopy);
            }
        }

        public static void Clear(Array array, int index, int length)
        {
            if (!RuntimeImports.TryArrayClear(array, index, length))
                ClearImpl(array, index, length);
        }

        private static unsafe void ClearImpl(Array array, int index, int length)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            array = array.FlattenedArray;

            if (index < 0 || index > array.Length || length < 0 || length > array.Length)
                throw new IndexOutOfRangeException();
            if (length > (array.Length - index))
                throw new IndexOutOfRangeException();

#if REAL_MULTIDIM_ARRAYS
            // The above checks should have covered all the reasons why Clear would fail.
            // NOTE: ONCE WE GET RID OF THE IFDEFS, WE SHOULD RENAME THIS METHOD.
            Debug.Assert(false);
#else
            bool success = RuntimeImports.TryArrayClear(array, index, length);
            Debug.Assert(success);
#endif
        }

        // We impose limits on maximum array length in each dimension to allow efficient 
        // implementation of advanced range check elimination in future.
        // Keep in sync with vm\gcscan.cpp and HashHelpers.MaxPrimeArrayLength.
        internal const int MaxArrayLength = 0X7FEFFFFF;

        public int GetLength(int dimension)
        {
            int length = GetUpperBound(dimension) + 1;
            // We don't support non-zero lower bounds so don't incur the cost of obtaining it.
            Debug.Assert(GetLowerBound(dimension) == 0);
            return length;
        }

        public int Rank
        {
            get
            {
#if REAL_MULTIDIM_ARRAYS
                return this.EETypePtr.ArrayRank;
#else
                MDArray mdArray = this as MDArray;
                if (mdArray != null)
                {
                    return mdArray.MDRank;
                }
                return 1;
#endif
            }
        }

#if REAL_MULTIDIM_ARRAYS
        // Allocate new multidimensional array of given dimensions. Assumes that that pLengths is immutable.
        internal unsafe static Array NewMultiDimArray(EETypePtr eeType, int * pLengths, int rank)
        {
            Debug.Assert(eeType.IsArray && !eeType.IsSzArray);
            Debug.Assert(rank == eeType.ArrayRank);

            for (int i = 0; i < rank; i++)
            {
                if (pLengths[i] < 0)
                    throw new OverflowException();
            }

            int totalLength = 1;

            for (int i = 0; i < rank; i++)
            {
                totalLength = checked(totalLength * pLengths[i]);
            }

            Array ret = RuntimeImports.RhNewArray(eeType, totalLength);

            fixed (int* pNumComponents = &ret._numComponents)
            {
                for (int i = 0; i < rank; i++)
                {
                    // Lengths follow after _numComponents.
                    *(pNumComponents + 1 + PADDING + i) = pLengths[i];
                }
            }

            return ret;
        }
#endif // REAL_MULTIDIM_ARRAYS

        // Number of elements in the Array.
        int ICollection.Count
        { get { return Length; } }

        // Is this Array read-only?
        bool IList.IsReadOnly
        { get { return false; } }

        bool IList.IsFixedSize
        {
            get { return true; }
        }

        // Is this Array synchronized (i.e., thread-safe)?  If you want a synchronized
        // collection, you can use SyncRoot as an object to synchronize your 
        // collection with.  You could also call GetSynchronized() 
        // to get a synchronized wrapper around the Array.
        bool ICollection.IsSynchronized
        { get { return false; } }

        Object IList.this[int index]
        {
            get
            {
                return GetValue(index);
            }

            set
            {
                SetValue(value, index);
            }
        }

        int IList.Add(Object value)
        {
            throw new NotSupportedException(SR.NotSupported_FixedSizeCollection);
        }

        bool IList.Contains(Object value)
        {
            return Array.IndexOf(this, value) >= 0;
        }

        void IList.Clear()
        {
            Array.Clear(this, 0, this.Length);
        }

        int IList.IndexOf(Object value)
        {
            return Array.IndexOf(this, value);
        }

        void IList.Insert(int index, Object value)
        {
            throw new NotSupportedException(SR.NotSupported_FixedSizeCollection);
        }

        void IList.Remove(Object value)
        {
            throw new NotSupportedException(SR.NotSupported_FixedSizeCollection);
        }

        void IList.RemoveAt(int index)
        {
            throw new NotSupportedException(SR.NotSupported_FixedSizeCollection);
        }

        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        // 
        // This method is to support the ICollection interface, and calls
        // Array.Copy internally.  If you aren't using ICollection explicitly,
        // call Array.Copy to avoid an extra indirection.
        // 
        public void CopyTo(Array array, int index)
        {
            // Note: Array.Copy throws a RankException and we want a consistent ArgumentException for all the IList CopyTo methods.
            if (array != null && array.Rank != 1)
                throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

            Array.Copy(this, 0, array, index, Length);
        }

        // Returns an object appropriate for synchronizing access to this 
        // Array.
        Object ICollection.SyncRoot
        {
            get { return this; }
        }

        // Make a new array which is a deep copy of the original array.
        // 
        public Object Clone()
        {
            return MemberwiseClone();
        }

        Int32 IStructuralComparable.CompareTo(Object other, IComparer comparer)
        {
            if (other == null)
            {
                return 1;
            }

            Array o = other as Array;

            if (o == null || this.Length != o.Length)
            {
                throw new ArgumentException(SR.ArgumentException_OtherNotArrayOfCorrectLength, "other");
            }

            int i = 0;
            int c = 0;

            while (i < o.Length && c == 0)
            {
                object left = GetValue(i);
                object right = o.GetValue(i);

                c = comparer.Compare(left, right);
                i++;
            }

            return c;
        }

        Boolean IStructuralEquatable.Equals(Object other, IEqualityComparer comparer)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            Array o = other as Array;

            if (o == null || o.Length != this.Length)
            {
                return false;
            }

            int i = 0;
            while (i < o.Length)
            {
                object left = GetValue(i);
                object right = o.GetValue(i);

                if (!comparer.Equals(left, right))
                {
                    return false;
                }
                i++;
            }

            return true;
        }

        // From System.Web.Util.HashCodeCombiner
        internal static int CombineHashCodes(int h1, int h2)
        {
            return (((h1 << 5) + h1) ^ h2);
        }

        int IStructuralEquatable.GetHashCode(IEqualityComparer comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException("comparer");

            int ret = 0;

            for (int i = (this.Length >= 8 ? this.Length - 8 : 0); i < this.Length; i++)
            {
                ret = CombineHashCodes(ret, comparer.GetHashCode(GetValue(i)));
            }

            return ret;
        }

        // Searches an array for a given element using a binary search algorithm.
        // Elements of the array are compared to the search value using the
        // IComparable interface, which must be implemented by all elements
        // of the array and the given search value. This method assumes that the
        // array is already sorted according to the IComparable interface;
        // if this is not the case, the result will be incorrect.
        //
        // The method returns the index of the given value in the array. If the
        // array does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value.
        // 
        public static int BinarySearch(Array array, Object value)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            return BinarySearch(array, 0, array.Length, value, null);
        }

        // Searches a section of an array for a given element using a binary search
        // algorithm. Elements of the array are compared to the search value using
        // the IComparable interface, which must be implemented by all
        // elements of the array and the given search value. This method assumes
        // that the array is already sorted according to the IComparable
        // interface; if this is not the case, the result will be incorrect.
        //
        // The method returns the index of the given value in the array. If the
        // array does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value.
        // 
        public static int BinarySearch(Array array, int index, int length, Object value)
        {
            return BinarySearch(array, index, length, value, null);
        }

        // Searches an array for a given element using a binary search algorithm.
        // Elements of the array are compared to the search value using the given
        // IComparer interface. If comparer is null, elements of the
        // array are compared to the search value using the IComparable
        // interface, which in that case must be implemented by all elements of the
        // array and the given search value. This method assumes that the array is
        // already sorted; if this is not the case, the result will be incorrect.
        // 
        // The method returns the index of the given value in the array. If the
        // array does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value.
        // 
        public static int BinarySearch(Array array, Object value, IComparer comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            return BinarySearch(array, 0, array.Length, value, comparer);
        }

        // Searches a section of an array for a given element using a binary search
        // algorithm. Elements of the array are compared to the search value using
        // the given IComparer interface. If comparer is null,
        // elements of the array are compared to the search value using the
        // IComparable interface, which in that case must be implemented by
        // all elements of the array and the given search value. This method
        // assumes that the array is already sorted; if this is not the case, the
        // result will be incorrect.
        // 
        // The method returns the index of the given value in the array. If the
        // array does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value.
        // 
        public static int BinarySearch(Array array, int index, int length, Object value, IComparer comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (index < 0 || length < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "length"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (array.Length - index < length)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            if (array.Rank != 1)
                throw new RankException(SR.Rank_MultiDimNotSupported);

            if (comparer == null) comparer = LowLevelComparer.Default;

            int lo = index;
            int hi = index + length - 1;
            Object[] objArray = array as Object[];
            if (objArray != null)
            {
                while (lo <= hi)
                {
                    // i might overflow if lo and hi are both large positive numbers. 
                    int i = GetMedian(lo, hi);

                    int c;
                    try
                    {
                        c = comparer.Compare(objArray[i], value);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
                    }
                    if (c == 0) return i;
                    if (c < 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }
            }
            else
            {
                while (lo <= hi)
                {
                    int i = GetMedian(lo, hi);

                    int c;
                    try
                    {
                        c = comparer.Compare(array.GetValue(i), value);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_IComparerFailed, e);
                    }
                    if (c == 0) return i;
                    if (c < 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }
            }
            return ~lo;
        }

        private static int GetMedian(int low, int hi)
        {
            // Note both may be negative, if we are dealing with arrays w/ negative lower bounds.
            return low + ((hi - low) >> 1);
        }

        public static int BinarySearch<T>(T[] array, T value)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            return BinarySearch<T>(array, 0, array.Length, value, null);
        }

        public static int BinarySearch<T>(T[] array, T value, System.Collections.Generic.IComparer<T> comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            return BinarySearch<T>(array, 0, array.Length, value, comparer);
        }

        public static int BinarySearch<T>(T[] array, int index, int length, T value)
        {
            return BinarySearch<T>(array, index, length, value, null);
        }

        public static int BinarySearch<T>(T[] array, int index, int length, T value, System.Collections.Generic.IComparer<T> comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0 || length < 0)
                throw new ArgumentOutOfRangeException((index < 0 ? "index" : "length"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (array.Length - index < length)
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            return ArraySortHelper<T>.BinarySearch(array, index, length, value, comparer);
        }

        // Returns the index of the first occurrence of a given value in an array.
        // The array is searched forwards, and the elements of the array are
        // compared to the given value using the Object.Equals method.
        // 
        public static int IndexOf(Array array, Object value)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return IndexOf(array, value, 0, array.Length);
        }

        // Returns the index of the first occurrence of a given value in a range of
        // an array. The array is searched forwards, starting at index
        // startIndex and ending at the last element of the array. The
        // elements of the array are compared to the given value using the
        // Object.Equals method.
        // 
        public static int IndexOf(Array array, Object value, int startIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return IndexOf(array, value, startIndex, array.Length - startIndex);
        }

        // Returns the index of the first occurrence of a given value in a range of
        // an array. The array is searched forwards, starting at index
        // startIndex and upto count elements. The
        // elements of the array are compared to the given value using the
        // Object.Equals method.
        // 
        public static int IndexOf(Array array, Object value, int startIndex, int count)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (array.Rank != 1)
                throw new RankException(SR.Rank_MultiDimNotSupported);
            if (startIndex < 0 || startIndex > array.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            if (count < 0 || count > array.Length - startIndex)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);

            Object[] objArray = array as Object[];
            int endIndex = startIndex + count;
            if (objArray != null)
            {
                if (value == null)
                {
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        if (objArray[i] == null) return i;
                    }
                }
                else
                {
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        Object obj = objArray[i];
                        if (obj != null && obj.Equals(value)) return i;
                    }
                }
            }
            else
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    Object obj = array.GetValue(i);
                    if (obj == null)
                    {
                        if (value == null) return i;
                    }
                    else
                    {
                        if (obj.Equals(value)) return i;
                    }
                }
            }
            return -1;
        }

#if !CORERT
       // These functions look odd, as they are part of a complex series of compiler intrinsics
       // designed to produce very high quality code for equality comparison cases without utilizing
       // reflection like other platforms. The major complication is that the specification of
       // IndexOf is that it is supposed to use IEquatable<T> if possible, but that requirement
       // cannot be expressed in IL directly due to the lack of constraints.
       // Instead, specialization at call time is used within the compiler. 
       // 
       // General Approach
       // - Redirect calls to LowLevelEqualityComparer<T>.Equals to EqualityComparer<T>.Equals, and also 
       //   do the same for get_Default in case anyone ever calls that. This allows the use of 
       //   LowLevelEqualityComparer<T> to result in usage of EqualityComparer<T>
       // - Perform fancy redirection for Array.GetComparerForReferenceTypesOnly<T>(). If T is a reference 
       //   type or UniversalCanon, have this redirect to EqualityComparer<T>.get_Default, Otherwise, use 
       //   the function as is. (will return null in that case)
       // - Change the contents of the IndexOf functions to have a pair of loops. One for if 
       //   GetComparerForReferenceTypesOnly returns null, and one for when it does not. 
       //   - If it does not return null, call the EqualityComparer<T> code.
       //   - If it does return null, use a special function StructOnlyEquals<T>(). 
       //     - Calls to that function result in calls to a pair of helper function in 
       //       EqualityComparerHelpers (StructOnlyEqualsIEquatable, or StructOnlyEqualsNullable) 
       //       depending on whether or not they are the right function to call.
       // - The end result is that in optimized builds, we have the same single function compiled size 
       //   characteristics that the old EqualsOnlyComparer<T>.Equals function had, but we maintain 
       //   correctness as well.
        private static LowLevelEqualityComparer<T> GetComparerForReferenceTypesOnly<T>()
        {
            // When T is a reference type or a universal canon type, then this will redirect to EqualityComparer<T>.Default.
            return null;
        }

        private static bool StructOnlyEquals<T>(T left, T right)
        {
           return left.Equals(right);
        }
#endif
        /// <summary>
        /// This version is called from Array<T>.IndexOf and Contains<T>, so it's in every unique array instance due to array interface implementation.
        /// Do not call into IndexOf<T>(Array array, Object value, int startIndex, int count) for size and space reasons.
        /// Otherwise there will be two IndexOf methods for each unique array instance, and extra parameter checking which are not needed for the common case.
        /// </summary>
        public static int IndexOf<T>(T[] array, T value)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

#if CORERT
            for (int i = 0; i < array.Length; i++)
            {
                if (EqualOnlyComparer<T>.Equals(array[i], value))
                    return i;
            }

#else
            // See comment above Array.GetComparerForReferenceTypesOnly for details
            LowLevelEqualityComparer<T> comparer = GetComparerForReferenceTypesOnly<T>();

            if (comparer != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (comparer.Equals(array[i], value))
                        return i;
                }
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (StructOnlyEquals<T>(array[i], value))
                        return i;
                }
            }
#endif
            return -1;
        }

        public static int IndexOf<T>(T[] array, T value, int startIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return IndexOf(array, value, startIndex, array.Length - startIndex);
        }

        public static int IndexOf<T>(T[] array, T value, int startIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (startIndex < 0 || startIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            }

            if (count < 0 || count > array.Length - startIndex)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            int endIndex = startIndex + count;
#if CORERT
            for (int i = startIndex; i < endIndex; i++)
            {
                if (EqualOnlyComparer<T>.Equals(array[i], value))
                    return i;
            }
#else
            // See comment above Array.GetComparerForReferenceTypesOnly for details
            LowLevelEqualityComparer<T> comparer = GetComparerForReferenceTypesOnly<T>();

            if (comparer != null)
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    if (comparer.Equals(array[i], value))
                        return i;
                }
            }
            else
            {
                for (int i = startIndex; i < endIndex; i++)
                {
                    if (StructOnlyEquals<T>(array[i], value))
                        return i;
                }
            }
#endif
            return -1;
        }

        public static int LastIndexOf(Array array, Object value)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            return LastIndexOf(array, value, array.Length - 1, array.Length);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // an array. The array is searched backwards, starting at index
        // startIndex and ending at index 0. The elements of the array are
        // compared to the given value using the Object.Equals method.
        // 
        public static int LastIndexOf(Array array, Object value, int startIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            return LastIndexOf(array, value, startIndex, startIndex + 1);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // an array. The array is searched backwards, starting at index
        // startIndex and counting uptocount elements. The elements of
        // the array are compared to the given value using the Object.Equals
        // method.
        // 
        public static int LastIndexOf(Array array, Object value, int startIndex, int count)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (array.Length == 0)
            {
                return -1;
            }

            if (startIndex < 0 || startIndex >= array.Length)
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            if (count > startIndex + 1)
                throw new ArgumentOutOfRangeException("endIndex", SR.ArgumentOutOfRange_EndIndexStartIndex);
            if (array.Rank != 1)
                throw new RankException(SR.Rank_MultiDimNotSupported);

            Object[] objArray = array as Object[];
            int endIndex = startIndex - count + 1;
            if (objArray != null)
            {
                if (value == null)
                {
                    for (int i = startIndex; i >= endIndex; i--)
                    {
                        if (objArray[i] == null) return i;
                    }
                }
                else
                {
                    for (int i = startIndex; i >= endIndex; i--)
                    {
                        Object obj = objArray[i];
                        if (obj != null && obj.Equals(value)) return i;
                    }
                }
            }
            else
            {
                for (int i = startIndex; i >= endIndex; i--)
                {
                    Object obj = array.GetValue(i);
                    if (obj == null)
                    {
                        if (value == null) return i;
                    }
                    else
                    {
                        if (obj.Equals(value)) return i;
                    }
                }
            }
            return -1;  // Return lb-1 for arrays with negative lower bounds.
        }

        public static int LastIndexOf<T>(T[] array, T value)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return LastIndexOf(array, value, array.Length - 1, array.Length);
        }

        public static int LastIndexOf<T>(T[] array, T value, int startIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            // if array is empty and startIndex is 0, we need to pass 0 as count
            return LastIndexOf(array, value, startIndex, (array.Length == 0) ? 0 : (startIndex + 1));
        }

        public static int LastIndexOf<T>(T[] array, T value, int startIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (array.Length == 0)
            {
                //
                // Special case for 0 length List
                // accept -1 and 0 as valid startIndex for compablility reason.
                //
                if (startIndex != -1 && startIndex != 0)
                {
                    throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
                }

                // only 0 is a valid value for count if array is empty
                if (count != 0)
                {
                    throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
                }
                return -1;
            }

            // Make sure we're not out of range            
            if (startIndex < 0 || startIndex >= array.Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            }

            // 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            if (count < 0 || startIndex - count + 1 < 0)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

#if CORERT
            int endIndex = startIndex - count + 1;
            for (int i = startIndex; i >= endIndex; i--)
            {
                if (EqualOnlyComparer<T>.Equals(array[i], value)) return i;
            }
#else
            // See comment above Array.GetComparerForReferenceTypesOnly for details
            LowLevelEqualityComparer<T> comparer = GetComparerForReferenceTypesOnly<T>();

            int endIndex = startIndex - count + 1;
            if (comparer != null)
            {
                for (int i = startIndex; i >= endIndex; i--)
                {
                    if (comparer.Equals(array[i], value))
                        return i;
                }
            }
            else
            {
                for (int i = startIndex; i >= endIndex; i--)
                {
                    if (StructOnlyEquals<T>(array[i], value))
                        return i;
                }
            }
#endif
            return -1;
        }

        // Reverses all elements of the given array. Following a call to this
        // method, an element previously located at index i will now be
        // located at index length - i - 1, where length is the
        // length of the array.
        // 
        public static void Reverse(Array array)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            Reverse(array, 0, array.Length);
        }

        // Reverses the elements in a range of an array. Following a call to this
        // method, an element in the range given by index and count
        // which was previously located at index i will now be located at
        // index index + (index + count - i - 1).
        // Reliability note: This may fail because it may have to box objects.
        // 
        public static void Reverse(Array array, int index, int length)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            int lowerBound = array.GetLowerBound(0);
            if (index < lowerBound || length < 0)
                throw new ArgumentOutOfRangeException((index < lowerBound ? "index" : "length"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (array.Length - (index - lowerBound) < length)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            if (array.Rank != 1)
                throw new RankException(SR.Rank_MultiDimNotSupported);

            int i = index;
            int j = index + length - 1;
            Object[] objArray = array as Object[];
            if (objArray != null)
            {
                while (i < j)
                {
                    Object temp = objArray[i];
                    objArray[i] = objArray[j];
                    objArray[j] = temp;
                    i++;
                    j--;
                }
            }
            else
            {
                while (i < j)
                {
                    Object temp = array.GetValue(i);
                    array.SetValue(array.GetValue(j), i);
                    array.SetValue(temp, j);
                    i++;
                    j--;
                }
            }
        }

        // Sorts the elements of an array. The sort compares the elements to each
        // other using the IComparable interface, which must be implemented
        // by all elements of the array.
        // 
        public static void Sort(Array array)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            Sort(array, null, 0, array.Length, null);
        }

        // Sorts the elements in a section of an array. The sort compares the
        // elements to each other using the IComparable interface, which
        // must be implemented by all elements in the given section of the array.
        // 
        public static void Sort(Array array, int index, int length)
        {
            Sort(array, null, index, length, null);
        }

        // Sorts the elements of an array. The sort compares the elements to each
        // other using the given IComparer interface. If comparer is
        // null, the elements are compared to each other using the
        // IComparable interface, which in that case must be implemented by
        // all elements of the array.
        // 
        public static void Sort(Array array, IComparer comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            Sort(array, null, 0, array.Length, comparer);
        }

        // Sorts the elements in a section of an array. The sort compares the
        // elements to each other using the given IComparer interface. If
        // comparer is null, the elements are compared to each other using
        // the IComparable interface, which in that case must be implemented
        // by all elements in the given section of the array.
        // 
        public static void Sort(Array array, int index, int length, IComparer comparer)
        {
            Sort(array, null, index, length, comparer);
        }

        public static void Sort(Array keys, Array items)
        {
            if (keys == null)
            {
                throw new ArgumentNullException("keys");
            }

            Sort(keys, items, keys.GetLowerBound(0), keys.Length, null);
        }

        public static void Sort(Array keys, Array items, IComparer comparer)
        {
            if (keys == null)
            {
                throw new ArgumentNullException("keys");
            }

            Sort(keys, items, keys.GetLowerBound(0), keys.Length, comparer);
        }

        public static void Sort(Array keys, Array items, int index, int length)
        {
            Sort(keys, items, index, length, null);
        }

        public static void Sort(Array keys, Array items, int index, int length, IComparer comparer)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (keys.Rank != 1 || (items != null && items.Rank != 1))
                throw new RankException(SR.Rank_MultiDimNotSupported);
            int keysLowerBound = keys.GetLowerBound(0);
            if (items != null && keysLowerBound != items.GetLowerBound(0))
                throw new ArgumentException(SR.Arg_LowerBoundsMustMatch);
            if (index < keysLowerBound || length < 0)
                throw new ArgumentOutOfRangeException((length < 0 ? "length" : "index"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (keys.Length - (index - keysLowerBound) < length || (items != null && (index - keysLowerBound) > items.Length - length))
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            if (length > 1)
            {
                IComparer<Object> comparerT = new ComparerAsComparerT(comparer);
                Object[] objKeys = keys as Object[];
                Object[] objItems = items as Object[];

                // Unfortunately, on Project N, we don't have the ability to specialize ArraySortHelper<> on demand
                // for value types. Rather than incur a boxing cost on every compare and every swap (and maintain a separate introsort algorithm
                // just for this), box them all, sort them as an Object[] array and unbox them back.

                // Check if either of the arrays need to be copied.
                if (objKeys == null)
                {
                    objKeys = new Object[index + length];
                    Array.CopyImplValueTypeArrayToReferenceArray(keys, index, objKeys, index, length, reliable: false);
                }
                if (objItems == null && items != null)
                {
                    objItems = new Object[index + length];
                    Array.CopyImplValueTypeArrayToReferenceArray(items, index, objItems, index, length, reliable: false);
                }

                Sort<Object, Object>(objKeys, objItems, index, length, comparerT);

                // If either array was copied, copy it back into the original
                if (objKeys != keys)
                {
                    Array.CopyImplReferenceArrayToValueTypeArray(objKeys, index, keys, index, length, reliable: false);
                }
                if (objItems != items)
                {
                    Array.CopyImplReferenceArrayToValueTypeArray(objItems, index, items, index, length, reliable: false);
                }
            }
        }

        // Wraps an IComparer inside an IComparer<Object>.
        private sealed class ComparerAsComparerT : IComparer<Object>
        {
            public ComparerAsComparerT(IComparer comparer)
            {
                _comparer = (comparer == null) ? LowLevelComparer.Default : comparer;
            }

            public int Compare(Object x, Object y)
            {
                return _comparer.Compare(x, y);
            }

            private IComparer _comparer;
        }

        public static void Sort<T>(T[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            Sort<T>(array, 0, array.Length, null);
        }

        public static void Sort<T>(T[] array, int index, int length)
        {
            Sort<T>(array, index, length, null);
        }

        public static void Sort<T>(T[] array, System.Collections.Generic.IComparer<T> comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            Sort<T>(array, 0, array.Length, comparer);
        }

        public static void Sort<T>(T[] array, int index, int length, System.Collections.Generic.IComparer<T> comparer)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (index < 0 || length < 0)
                throw new ArgumentOutOfRangeException((length < 0 ? "length" : "index"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (array.Length - index < length)
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            if (length > 1)
                ArraySortHelper<T>.Sort(array, index, length, comparer);
        }

        public static void Sort<T>(T[] array, Comparison<T> comparison)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (comparison == null)
            {
                throw new ArgumentNullException("comparison");
            }

            IComparer<T> comparer = new FunctorComparer<T>(comparison);
            Array.Sort(array, comparer);
        }

        internal sealed class FunctorComparer<T> : IComparer<T>
        {
            private Comparison<T> _comparison;

            public FunctorComparer(Comparison<T> comparison)
            {
                _comparison = comparison;
            }

            public int Compare(T x, T y)
            {
                return _comparison(x, y);
            }
        }

        public static void Sort<TKey, TValue>(TKey[] keys, TValue[] items)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            Contract.EndContractBlock();
            Sort<TKey, TValue>(keys, items, 0, keys.Length, null);
        }

        public static void Sort<TKey, TValue>(TKey[] keys, TValue[] items, int index, int length)
        {
            Sort<TKey, TValue>(keys, items, index, length, null);
        }

        public static void Sort<TKey, TValue>(TKey[] keys, TValue[] items, IComparer<TKey> comparer)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            Contract.EndContractBlock();
            Sort<TKey, TValue>(keys, items, 0, keys.Length, comparer);
        }

        public static void Sort<TKey, TValue>(TKey[] keys, TValue[] items, int index, int length, IComparer<TKey> comparer)
        {
            if (keys == null)
                throw new ArgumentNullException("keys");
            if (index < 0 || length < 0)
                throw new ArgumentOutOfRangeException((length < 0 ? "length" : "index"), SR.ArgumentOutOfRange_NeedNonNegNum);
            if (keys.Length - index < length || (items != null && index > items.Length - length))
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            Contract.EndContractBlock();

            if (length > 1)
            {
                if (items == null)
                {
                    Sort<TKey>(keys, index, length, comparer);
                    return;
                }

                ArraySortHelper<TKey, TValue>.Default.Sort(keys, items, index, length, comparer);
            }
        }

        // CopyTo copies a collection into an Array, starting at a particular
        // index into the array.
        // 
        // This method is to support the ICollection interface, and calls
        // Array.Copy internally.  If you aren't using ICollection explicitly,
        // call Array.Copy to avoid an extra indirection.
        internal void CopyTo<T>(T[] thatArray, int index)
        {
            T[] thisArray = (T[])this;
            Array.Copy(thisArray, 0, thatArray, index, Length);
        }

        public static T[] Empty<T>()
        {
            return EmptyArray<T>.Value;
        }

        private static class EmptyArray<T>
        {
            internal static readonly T[] Value = new T[0];
        }

        public static bool Exists<T>(T[] array, Predicate<T> match)
        {
            return Array.FindIndex(array, match) != -1;
        }

        public static T Find<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (match(array[i]))
                {
                    return array[i];
                }
            }
            return default(T);
        }

        public static T[] FindAll<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            LowLevelList<T> list = new LowLevelList<T>();
            for (int i = 0; i < array.Length; i++)
            {
                if (match(array[i]))
                {
                    list.Add(array[i]);
                }
            }

            return list.ToArray();
        }

        public static int FindIndex<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return FindIndex(array, 0, array.Length, match);
        }

        public static int FindIndex<T>(T[] array, int startIndex, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return FindIndex(array, startIndex, array.Length - startIndex, match);
        }

        public static int FindIndex<T>(T[] array, int startIndex, int count, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (startIndex < 0 || startIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
            }

            if (count < 0 || startIndex > array.Length - count)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (match(array[i])) return i;
            }
            return -1;
        }

        public static T FindLast<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            for (int i = array.Length - 1; i >= 0; i--)
            {
                if (match(array[i]))
                {
                    return array[i];
                }
            }
            return default(T);
        }

        public static int FindLastIndex<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return FindLastIndex(array, array.Length - 1, array.Length, match);
        }

        public static int FindLastIndex<T>(T[] array, int startIndex, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            return FindLastIndex(array, startIndex, startIndex + 1, match);
        }

        public static int FindLastIndex<T>(T[] array, int startIndex, int count, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            if (array.Length == 0)
            {
                // Special case for 0 length List
                if (startIndex != -1)
                {
                    throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
                }
            }
            else
            {
                // Make sure we're not out of range            
                if (startIndex < 0 || startIndex >= array.Length)
                {
                    throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
                }
            }

            // 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            if (count < 0 || startIndex - count + 1 < 0)
            {
                throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
            }

            int endIndex = startIndex - count;
            for (int i = startIndex; i > endIndex; i--)
            {
                if (match(array[i]))
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetLowerBound(int dimension)
        {
#if REAL_MULTIDIM_ARRAYS
            if (!IsSzArray)
            {
                int rank = Rank;
                if ((uint)dimension >= rank)
                    throw new IndexOutOfRangeException();

                unsafe
                {
                    fixed (int* pNumComponents = &_numComponents)
                    {
                        // Lower bounds follow after upper bounds.
                        return *(pNumComponents + 1 + PADDING + rank + dimension);
                    }
                }
            }
#else
            MDArray mdArray = this as MDArray;
            if (mdArray != null)
            {
                if ((dimension >= mdArray.MDRank) || (dimension < 0))
                    throw new IndexOutOfRangeException();

                return 0;
            }

            if (this.Rank != 1)
                throw new PlatformNotSupportedException(SR.Rank_MultiDimNotSupported);
#endif
            if (dimension != 0)
                throw new IndexOutOfRangeException();
            return 0;
        }

        public int GetUpperBound(int dimension)
        {
#if REAL_MULTIDIM_ARRAYS
            if (!IsSzArray)
            {
                int rank = Rank;
                if ((uint)dimension >= rank)
                    throw new IndexOutOfRangeException();

                unsafe
                {
                    fixed (int* pNumComponents = &_numComponents)
                    {
                        // Lenghts follow after _numComponents.
                        int length = *(pNumComponents + 1 + PADDING + dimension);
                        int lowerBound = *(pNumComponents + 1 + PADDING + rank + dimension);
                        return length + lowerBound - 1;
                    }
                }
            }
#else
            MDArray mdArray = this as MDArray;
            if (mdArray != null)
            {
                return mdArray.MDGetUpperBound(dimension);
            }

            if (this.Rank != 1)
                throw new PlatformNotSupportedException(SR.Rank_MultiDimNotSupported);
#endif
            if (dimension != 0)
                throw new IndexOutOfRangeException();
            return Length - 1;
        }

        public static bool TrueForAll<T>(T[] array, Predicate<T> match)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            if (match == null)
            {
                throw new ArgumentNullException("match");
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (!match(array[i]))
                {
                    return false;
                }
            }
            return true;
        }

#if REAL_MULTIDIM_ARRAYS
        public unsafe Object GetValue(int index)
        {
            if (!IsSzArray)
                return GetValue(&index, 1);

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                if ((uint)index >= (uint)Length)
                    throw new IndexOutOfRangeException();

                nuint elementSize = ElementSize;
                fixed (IntPtr* pThisArray = &m_pEEType)
                {
                    byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)index * elementSize;
                    return RuntimeImports.RhBox(pElementEEType, pElement);
                }
            }
            else
            {
                object[] objArray = this as object[];
                return objArray[index];
            }
        }

        public unsafe Object GetValue(params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException("indices");

            if (IsSzArray && indices.Length == 1)
                return GetValue(indices[0]);

            fixed (int* pIndices = indices)
                return GetValue(pIndices, indices.Length);
        }

        private unsafe Object GetValue(int* pIndices, int rank)
        {
            if (this.Rank != rank)
                throw new ArgumentException(SR.Arg_RankIndices);

            Debug.Assert(!IsSzArray);

            fixed (IntPtr* pThisArray = &m_pEEType)
            {
                // Lengths follow after _numComponents.
                int* pLengths = (int*)(pThisArray + 1) + 1 + PADDING;
                int* pLowerBounds = (int*)(pThisArray + 1) + 1 + PADDING + rank;

                int flattenedIndex = 0;
                int factor = 1;
                for (int i = 0; i < rank; i++)
                {
                    int index = pIndices[i] - pLowerBounds[i];
                    int length = pLengths[i];
                    if ((uint)index >= (uint)length)
                        throw new IndexOutOfRangeException();
                    flattenedIndex = flattenedIndex * factor + index;
                    factor = factor * length;
                }

                if ((uint)flattenedIndex >= (uint)Length)
                    throw new IndexOutOfRangeException();

                byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)flattenedIndex * ElementSize;

                EETypePtr pElementEEType = ElementEEType;
                if (pElementEEType.IsValueType)
                {
                    return RuntimeImports.RhBox(pElementEEType, pElement);
                }
                else
                {
                    return RuntimeAugments.LoadReferenceTypeField((IntPtr)pElement);
                }
            }
        }

        public unsafe void SetValue(Object value, int index)
        {
            if (!IsSzArray)
            {
                SetValue(value, &index, 1);
                return;
            }

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                if ((uint)index >= (uint)Length)
                    throw new IndexOutOfRangeException();

                // Unlike most callers of InvokeUtils.ChangeType(), Array.SetValue() does *not* permit conversion from a primitive to an Enum.
                if (value != null && !(value.EETypePtr == pElementEEType) && pElementEEType.IsEnum)
                    throw new InvalidCastException(SR.Format(SR.Arg_ObjObjEx, value.GetType(), Type.GetTypeFromHandle(new RuntimeTypeHandle(pElementEEType))));

                value = InvokeUtils.CheckArgument(value, pElementEEType, InvokeUtils.CheckArgumentSemantics.ArraySet);
                Debug.Assert(value == null || RuntimeImports.AreTypesAssignable(value.EETypePtr, pElementEEType));

                nuint elementSize = ElementSize;
                fixed (IntPtr* pThisArray = &m_pEEType)
                {
                    byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)index * elementSize;
                    RuntimeImports.RhUnbox(value, pElement, pElementEEType);
                }
            }
            else
            {
                object[] objArray = this as object[];
                try
                {
                    objArray[index] = value;
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new InvalidCastException(SR.InvalidCast_StoreArrayElement);
                }
            }
        }

        public unsafe void SetValue(Object value, params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException("indices");

            if (IsSzArray && indices.Length == 1)
                SetValue(value, indices[0]);

            fixed (int* pIndices = indices)
                SetValue(value, pIndices, indices.Length);
        }

        private unsafe void SetValue(Object value, int* pIndices, int rank)
        {
            if (this.Rank != rank)
                throw new ArgumentException(SR.Arg_RankIndices);

            Debug.Assert(!IsSzArray);

            fixed (IntPtr* pThisArray = &m_pEEType)
            {
                // Lengths follow after _numComponents.
                int* pLengths = (int*)(pThisArray + 1) + 1 + PADDING;
                int* pLowerBounds = (int*)(pThisArray + 1) + 1 + PADDING + rank;

                int flattenedIndex = 0;
                int factor = 1;
                for (int i = 0; i < rank; i++)
                {
                    int index = pIndices[i] - pLowerBounds[i];
                    int length = pLengths[i];
                    if ((uint)index >= (uint)length)
                        throw new IndexOutOfRangeException();
                    flattenedIndex = flattenedIndex * factor + index;
                    factor = factor * length;
                }

                if ((uint)flattenedIndex >= (uint)Length)
                    throw new IndexOutOfRangeException();

                byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)flattenedIndex * ElementSize;

                EETypePtr pElementEEType = ElementEEType;
                if (pElementEEType.IsValueType)
                {
                    // Unlike most callers of InvokeUtils.ChangeType(), Array.SetValue() does *not* permit conversion from a primitive to an Enum.
                    if (value != null && !(value.EETypePtr == pElementEEType) && pElementEEType.IsEnum)
                        throw new InvalidCastException(SR.Format(SR.Arg_ObjObjEx, value.GetType(), Type.GetTypeFromHandle(new RuntimeTypeHandle(pElementEEType))));

                    value = InvokeUtils.CheckArgument(value, pElementEEType, InvokeUtils.CheckArgumentSemantics.ArraySet);
                    Debug.Assert(value == null || RuntimeImports.AreTypesAssignable(value.EETypePtr, pElementEEType));

                    RuntimeImports.RhUnbox(value, pElement, pElementEEType);
                }
                else
                {
                    try
                    {
                        RuntimeImports.RhCheckArrayStore(this, value);
                        RuntimeAugments.StoreReferenceTypeField((IntPtr)pElement, value);
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new InvalidCastException(SR.InvalidCast_StoreArrayElement);
                    }
                }
            }
        }
#else // REAL_MULTIDIM_ARRAYS
        public unsafe Object GetValue(int index)
        {
            if (!IsSzArray)
                throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                if (index < 0 || index >= Length)
                    throw new IndexOutOfRangeException();

                nuint elementSize = ElementSize;
                fixed (IntPtr* pThisArray = &m_pEEType)
                {
                    byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)index * elementSize;
                    return RuntimeImports.RhBox(pElementEEType, pElement);
                }
            }
            else
            {
                object[] objArray = this as object[];
                return objArray[index];
            }
        }

        public Object GetValue(params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException("indices");

            MDArray mdArray = this as MDArray;
            if (mdArray != null)
            {
                return mdArray.MDGetValue(indices);
            }

            if (indices.Length != 1)
                throw new ArgumentException(SR.Arg_RankIndices);

            return GetValue(indices[0]);
        }

        public unsafe void SetValue(Object value, int index)
        {
            if (!IsSzArray)
                throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

            if (index < 0 || index >= Length)
                throw new IndexOutOfRangeException();

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                // Unlike most callers of InvokeUtils.ChangeType(), Array.SetValue() does *not* permit conversion from a primitive to an Enum.
                if (value != null && !(value.EETypePtr == pElementEEType) && pElementEEType.IsEnum)
                    throw new InvalidCastException(SR.Format(SR.Arg_ObjObjEx, value.GetType(), Type.GetTypeFromHandle(new RuntimeTypeHandle(pElementEEType))));

                value = InvokeUtils.CheckArgument(value, pElementEEType, InvokeUtils.CheckArgumentSemantics.ArraySet);
                Debug.Assert(value == null || RuntimeImports.AreTypesAssignable(value.EETypePtr, pElementEEType));

                nuint elementSize = ElementSize;
                fixed (IntPtr* pThisArray = &m_pEEType)
                {
                    byte* pElement = Array.GetAddrOfPinnedArrayFromEETypeField(pThisArray) + (nuint)index * elementSize;
                    RuntimeImports.RhUnbox(value, pElement, pElementEEType);
                }
            }
            else
            {
                object[] objArray = this as object[];
                try
                {
                    objArray[index] = value;
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new InvalidCastException(SR.InvalidCast_StoreArrayElement);
                }
            }
        }

        public void SetValue(Object value, params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException("indices");

            MDArray mdArray = this as MDArray;
            if (mdArray != null)
            {
                mdArray.MDSetValue(value, indices);
                return;
            }

            if (indices.Length != 1)
                throw new ArgumentException(SR.Arg_RankIndices);

            SetValue(value, indices[0]);
        }
#endif // REAL_MULTIDIM_ARRAYS

        public IEnumerator GetEnumerator()
        {
            return new SZArrayEnumerator(this);
        }

        internal EETypePtr ElementEEType
        {
            get
            {
                unsafe
                {
                    return this.EETypePtr.ArrayElementType;
                }
            }
        }


        //
        // Return storage size of an individual element in bytes.
        //
        internal nuint ElementSize
        {
            get
            {
                return EETypePtr.ComponentSize;
            }
        }

        internal bool IsElementTypeBlittable
        {
            get
            {
                if (ElementEEType.IsPrimitive)
                    return true;

                if (ElementEEType.IsValueType && !ElementEEType.HasPointers)
                    return true;

                return false;
            }
        }

        // Exposes the "flattened view" of a multidim array if necessary.
        // This is used to implement api's that accept multidim arrays and operate on them
        // as if they were actually SZArrays that concatenate the rows of the multidim array.
        //
        // The return value of this helper is guaranteed to be an SZArray.
        private Array FlattenedArray
        {
            get
            {
#if !REAL_MULTIDIM_ARRAYS
                // NOTE: ONCE WE GET RID OF THE IFDEFS, WE SHOULD DELETE THIS METHOD.
                MDArray mdArray = this as MDArray;
                if (mdArray != null)
                    return mdArray.MDFlattenedArray;
#endif
                return this;
            }
        }

        private sealed class SZArrayEnumerator : IEnumerator
        {
            private Array _array;
            private int _index;
            private int _endIndex; // cache array length, since it's a little slow.

            internal SZArrayEnumerator(Array array)
            {
                _array = array.FlattenedArray;
                _index = -1;
                _endIndex = array.Length;
            }

            public bool MoveNext()
            {
                if (_index < _endIndex)
                {
                    _index++;
                    return (_index < _endIndex);
                }
                return false;
            }

            public Object Current
            {
                get
                {
                    if (_index < 0) throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
                    if (_index >= _endIndex) throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
                    return _array.GetValue(_index);
                }
            }

            public void Reset()
            {
                _index = -1;
            }
        }
    }


    internal class ArrayEnumeratorBase
    {
        protected int _index;
        protected int _endIndex;

        internal ArrayEnumeratorBase()
        {
            _index = -1;
        }

        public bool MoveNext()
        {
            if (_index < _endIndex)
            {
                _index++;
                return (_index < _endIndex);
            }
            return false;
        }

        public void Dispose()
        {
        }
    }

    //
    // Note: the declared base type and interface list also determines what Reflection returns from TypeInfo.BaseType and TypeInfo.ImplementedInterfaces for array types.
    // This also means the class must be declared "public" so that the framework can reflect on it.
    //
    public class Array<T> : Array, IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyList<T>
    {
        private static T[] UnsafeCast(Array<T> array)
        {
            return RuntimeHelpers.UncheckedCast<T[]>(array);
        }

        private static object Id(object array)
        {
            return array;
        }

        public new IEnumerator<T> GetEnumerator()
        {
            // get length so we don't have to call the Length property again in ArrayEnumerator constructor
            // and avoid more checking there too.
            int length = this.Length;
            return length == 0 ? ArrayEnumerator.Empty : new ArrayEnumerator(UnsafeCast(this), length);
        }

        public int Count
        {
            get
            {
                T[] _this = UnsafeCast(this);
                return _this.Length;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return Array.IndexOf(UnsafeCast(this), item) != -1;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            T[] _this = UnsafeCast(this);
            if (array == null)
                throw new ArgumentNullException("array");
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException();
            int thisLength = _this.Length;
            int otherLength = array.Length;
            if ((otherLength - arrayIndex) < thisLength)
                throw new ArgumentException();

            if (!array.EETypePtr.HasPointers)
            {
                Array.CopyImplValueTypeArrayNoInnerGcRefs(_this, 0, array, arrayIndex, thisLength);
            }
            else
            {
                for (int idx = 0; idx < thisLength; idx++)
                    array[arrayIndex + idx] = _this[idx];
            }
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public T this[int index]
        {
            get
            {
                T[] _this = UnsafeCast(this);
                try
                {
                    return _this[index];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_Index);
                }
            }
            set
            {
                T[] _this = UnsafeCast(this);
                try
                {
                    _this[index] = value;
                }
                catch (IndexOutOfRangeException)
                {
                    throw new ArgumentOutOfRangeException("index", SR.ArgumentOutOfRange_Index);
                }
            }
        }

        public int IndexOf(T item)
        {
            return Array.IndexOf(UnsafeCast(this), item);
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        private sealed class ArrayEnumerator : ArrayEnumeratorBase, IEnumerator<T>
        {
            private T[] _array;

            // Passing -1 for endIndex so that MoveNext always returns false without mutating _index
            internal static readonly ArrayEnumerator Empty = new ArrayEnumerator(null, -1);

            internal ArrayEnumerator(T[] array, int endIndex)
            {
                _array = array;
                _endIndex = endIndex;
            }

            public T Current
            {
                get
                {
                    if (_index < 0 || _index >= _endIndex)
                        throw new InvalidOperationException();
                    return _array[_index];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                _index = -1;
            }
        }
    }
}
