// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;
using System.Threading;
using System.Collections;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using Internal.Reflection.Core.NonPortable;
using Internal.IntrinsicSupport;
using EEType = Internal.Runtime.EEType;
using EETypeElementType = Internal.Runtime.EETypeElementType;

#if TARGET_64BIT
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    // Note that we make a T[] (single-dimensional w/ zero as the lower bound) implement both 
    // IList<U> and IReadOnlyList<U>, where T : U dynamically.  See the SZArrayHelper class for details.
    public abstract partial class Array : ICollection, IEnumerable, IList, IStructuralComparable, IStructuralEquatable, ICloneable
    {
        // CS0649: Field '{blah}' is never assigned to, and will always have its default value
#pragma warning disable 649
        // This field should be the first field in Array as the runtime/compilers depend on it
        [NonSerialized]
        private int _numComponents;
#pragma warning restore

#if TARGET_64BIT
        private const int POINTER_SIZE = 8;
#else
        private const int POINTER_SIZE = 4;
#endif
        //                                    Header       + m_pEEType    + _numComponents (with an optional padding)
        private const int SZARRAY_BASE_SIZE = POINTER_SIZE + POINTER_SIZE + POINTER_SIZE;

        public int Length
        {
            get
            {
                // NOTE: The compiler has assumptions about the implementation of this method.
                // Changing the implementation here (or even deleting this) will NOT have the desired impact
                return _numComponents;
            }
        }

        public long LongLength
        {
            get
            {
                return Length;
            }
        }

        internal bool IsSzArray
        {
            get
            {
                return this.EETypePtr.BaseSize == SZARRAY_BASE_SIZE;
            }
        }

        // This is the classlib-provided "get array eetype" function that will be invoked whenever the runtime
        // needs to know the base type of an array.
        [RuntimeExport("GetSystemArrayEEType")]
        private static unsafe EEType* GetSystemArrayEEType()
        {
            return EETypePtr.EETypePtrOf<Array>().ToPointer();
        }

        public static Array CreateInstance(Type elementType, int length)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException(nameof(elementType));

            return CreateSzArray(elementType, length);
        }

        public static unsafe Array CreateInstance(Type elementType, int length1, int length2)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException(nameof(elementType));
            if (length1 < 0)
                throw new ArgumentOutOfRangeException(nameof(length1));
            if (length2 < 0)
                throw new ArgumentOutOfRangeException(nameof(length2));

            Type arrayType = GetArrayTypeFromElementType(elementType, true, 2);
            int* pLengths = stackalloc int[2];
            pLengths[0] = length1;
            pLengths[1] = length2;
            return NewMultiDimArray(arrayType.TypeHandle.ToEETypePtr(), pLengths, 2);
        }

        public static unsafe Array CreateInstance(Type elementType, int length1, int length2, int length3)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException(nameof(elementType));
            if (length1 < 0)
                throw new ArgumentOutOfRangeException(nameof(length1));
            if (length2 < 0)
                throw new ArgumentOutOfRangeException(nameof(length2));
            if (length3 < 0)
                throw new ArgumentOutOfRangeException(nameof(length3));

            Type arrayType = GetArrayTypeFromElementType(elementType, true, 3);
            int* pLengths = stackalloc int[3];
            pLengths[0] = length1;
            pLengths[1] = length2;
            pLengths[2] = length3;
            return NewMultiDimArray(arrayType.TypeHandle.ToEETypePtr(), pLengths, 3);
        }

        public static Array CreateInstance(Type elementType, params int[] lengths)
        {
            if ((object)elementType == null)
                throw new ArgumentNullException(nameof(elementType));
            if (lengths == null)
                throw new ArgumentNullException(nameof(lengths));
            if (lengths.Length == 0)
                throw new ArgumentException(SR.Arg_NeedAtLeast1Rank);

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
                throw new ArgumentNullException(nameof(elementType));
            if (lengths == null)
                throw new ArgumentNullException(nameof(lengths));
            if (lowerBounds == null)
                throw new ArgumentNullException(nameof(lowerBounds));
            if (lengths.Length != lowerBounds.Length)
                throw new ArgumentException(SR.Arg_RanksAndBounds);
            if (lengths.Length == 0)
                throw new ArgumentException(SR.Arg_NeedAtLeast1Rank);

            return CreateMultiDimArray(elementType, lengths, lowerBounds);
        }

        private static Array CreateSzArray(Type elementType, int length)
        {
            // Though our callers already validated length once, this parameter is passed via arrays, so we must check it again
            // in case a malicious caller modified the array after the check.
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

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
            elementType = elementType.UnderlyingSystemType;
            ValidateElementType(elementType);

            if (multiDim)
                return elementType.MakeArrayType(rank);
            else
                return elementType.MakeArrayType();
        }

        private static void ValidateElementType(Type elementType)
        {
            if (!elementType.IsRuntimeImplemented())
                throw new ArgumentException(SR.Arg_MustBeType, nameof(elementType));
            while (elementType.IsArray)
            {
                elementType = elementType.GetElementType();
            }
            if (elementType.IsByRef || elementType.IsByRefLike)
                throw new NotSupportedException(SR.NotSupported_ByRefLikeArray);
            if (elementType.Equals(CommonRuntimeTypes.Void))
                throw new NotSupportedException(SR.NotSupported_VoidArray);
            if (elementType.ContainsGenericParameters)
                throw new NotSupportedException(SR.NotSupported_OpenType);
        }

        public void Initialize()
        {
            // Project N port note: On the desktop, this api is a nop unless the array element type is a value type with
            // an explicit nullary constructor. Such a type cannot be expressed in C# so Project N does not support this.
            // The ILC toolchain fails the build if it encounters such a type.
            return;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref byte GetRawArrayData()
        {
            return ref Unsafe.Add(ref Unsafe.As<RawArrayData>(this).Data, (int)(EETypePtr.BaseSize - SZARRAY_BASE_SIZE));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref int GetRawMultiDimArrayBounds()
        {
            Debug.Assert(!IsSzArray);
            return ref Unsafe.AddByteOffset(ref _numComponents, POINTER_SIZE);
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
                throw new ArgumentNullException(nameof(sourceArray));
            if (destinationArray == null)
                throw new ArgumentNullException(nameof(destinationArray));

            int sourceRank = sourceArray.Rank;
            int destinationRank = destinationArray.Rank;
            if (sourceRank != destinationRank)
                throw new RankException(SR.Rank_MultiDimNotSupported);

            if ((sourceIndex < 0) || (destinationIndex < 0) || (length < 0))
                throw new ArgumentOutOfRangeException();
            if ((length > sourceArray.Length) || length > destinationArray.Length)
                throw new ArgumentException();
            if ((length > sourceArray.Length - sourceIndex) || (length > destinationArray.Length - destinationIndex))
                throw new ArgumentException();

            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            EETypePtr destinationElementEEType = destinationArray.ElementEEType;

            if (!destinationElementEEType.IsValueType && !destinationElementEEType.IsPointer)
            {
                if (!sourceElementEEType.IsValueType && !sourceElementEEType.IsPointer)
                {
                    CopyImplGcRefArray(sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
                }
                else if (RuntimeImports.AreTypesAssignable(sourceElementEEType, destinationElementEEType))
                {
                    CopyImplValueTypeArrayToReferenceArray(sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
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
                else if (sourceElementEEType.IsPointer && destinationElementEEType.IsPointer)
                {
                    // CLR compat note: CLR only allows Array.Copy between pointee types that would be assignable
                    // to using array covariance rules (so int*[] can be copied to uint*[], but not to float*[]).
                    // This is rather weird since e.g. we don't allow casting int*[] to uint*[] otherwise.
                    // Instead of trying to replicate the behavior, we're choosing to be simply more permissive here.
                    CopyImplValueTypeArrayNoInnerGcRefs(sourceArray, sourceIndex, destinationArray, destinationIndex, length);
                }
                else if (IsSourceElementABaseClassOrInterfaceOfDestinationValueType(sourceElementEEType, destinationElementEEType))
                {
                    CopyImplReferenceArrayToValueTypeArray(sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
                }
                else if (sourceElementEEType.IsPrimitive && destinationElementEEType.IsPrimitive)
                {
                    // The only case remaining is that primitive types could have a widening conversion between the source element type and the destination
                    // If a widening conversion does not exist we are going to throw an ArrayTypeMismatchException from it.
                    CopyImplPrimitiveTypeWithWidening(sourceArray, sourceIndex, destinationArray, destinationIndex, length, reliable);
                }
                else
                {
                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                }
            }
        }

        private static bool IsSourceElementABaseClassOrInterfaceOfDestinationValueType(EETypePtr sourceElementEEType, EETypePtr destinationElementEEType)
        {
            if (sourceElementEEType.IsValueType || sourceElementEEType.IsPointer)
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
        private static unsafe void CopyImplGcRefArray(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            // For mismatched array types, the desktop Array.Copy has a policy that determines whether to throw an ArrayTypeMismatch without any attempt to copy
            // or to throw an InvalidCastException in the middle of a copy. This code replicates that policy.
            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            EETypePtr destinationElementEEType = destinationArray.ElementEEType;

            Debug.Assert(!sourceElementEEType.IsValueType && !sourceElementEEType.IsPointer);
            Debug.Assert(!destinationElementEEType.IsValueType && !destinationElementEEType.IsPointer);

            bool attemptCopy = RuntimeImports.AreTypesAssignable(sourceElementEEType, destinationElementEEType);
            bool mustCastCheckEachElement = !attemptCopy;
            if (reliable)
            {
                if (mustCastCheckEachElement)
                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_ConstrainedCopy);
            }
            else
            {
                attemptCopy = attemptCopy || RuntimeImports.AreTypesAssignable(destinationElementEEType, sourceElementEEType);

                // If either array is an interface array, we allow the attempt to copy even if the other element type does not statically implement the interface.
                // We don't have an "IsInterface" property in EETypePtr so we instead check for a null BaseType. The only the other EEType with a null BaseType is
                // System.Object but if that were the case, we would already have passed one of the AreTypesAssignable checks above.
                attemptCopy = attemptCopy || sourceElementEEType.BaseType.IsNull;
                attemptCopy = attemptCopy || destinationElementEEType.BaseType.IsNull;

                if (!attemptCopy)
                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
            }

            bool reverseCopy = ((object)sourceArray == (object)destinationArray) && (sourceIndex < destinationIndex);
            ref object refDestinationArray = ref Unsafe.As<byte, object>(ref destinationArray.GetRawArrayData());
            ref object refSourceArray = ref Unsafe.As<byte, object>(ref sourceArray.GetRawArrayData());
            if (reverseCopy)
            {
                sourceIndex += length - 1;
                destinationIndex += length - 1;
                for (int i = 0; i < length; i++)
                {
                    object value = Unsafe.Add(ref refSourceArray, sourceIndex - i);
                    if (mustCastCheckEachElement && value != null && RuntimeImports.IsInstanceOf(destinationElementEEType, value) == null)
                        throw new InvalidCastException(SR.InvalidCast_DownCastArrayElement);
                    Unsafe.Add(ref refDestinationArray, destinationIndex - i) = value;
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    object value = Unsafe.Add(ref refSourceArray, sourceIndex + i);
                    if (mustCastCheckEachElement && value != null && RuntimeImports.IsInstanceOf(destinationElementEEType, value) == null)
                        throw new InvalidCastException(SR.InvalidCast_DownCastArrayElement);
                    Unsafe.Add(ref refDestinationArray, destinationIndex + i) = value;
                }
            }
        }

        //
        // Array.CopyImpl case: Value-type array to Object[] or interface array copy.
        //
        private static unsafe void CopyImplValueTypeArrayToReferenceArray(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            Debug.Assert(sourceArray.ElementEEType.IsValueType || sourceArray.ElementEEType.IsPointer);
            Debug.Assert(!destinationArray.ElementEEType.IsValueType && !destinationArray.ElementEEType.IsPointer);

            // Caller has already validated this.
            Debug.Assert(RuntimeImports.AreTypesAssignable(sourceArray.ElementEEType, destinationArray.ElementEEType));

            if (reliable)
                throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_ConstrainedCopy);

            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            nuint sourceElementSize = sourceArray.ElementSize;

            fixed (byte* pSourceArray = &sourceArray.GetRawArrayData())
            {
                byte* pElement = pSourceArray + (nuint)sourceIndex * sourceElementSize;
                ref object refDestinationArray = ref Unsafe.As<byte, object>(ref destinationArray.GetRawArrayData());
                for (int i = 0; i < length; i++)
                {
                    object boxedValue = RuntimeImports.RhBox(sourceElementEEType, ref *pElement);
                    Unsafe.Add(ref refDestinationArray, destinationIndex + i) = boxedValue;
                    pElement += sourceElementSize;
                }
            }
        }

        //
        // Array.CopyImpl case: Object[] or interface array to value-type array copy.
        //
        private static unsafe void CopyImplReferenceArrayToValueTypeArray(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            Debug.Assert(!sourceArray.ElementEEType.IsValueType && !sourceArray.ElementEEType.IsPointer);
            Debug.Assert(destinationArray.ElementEEType.IsValueType || destinationArray.ElementEEType.IsPointer);

            if (reliable)
                throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);

            EETypePtr destinationElementEEType = destinationArray.ElementEEType;
            nuint destinationElementSize = destinationArray.ElementSize;
            bool isNullable = destinationElementEEType.IsNullable;

            fixed (byte* pDestinationArray = &destinationArray.GetRawArrayData())
            {
                ref object refSourceArray = ref Unsafe.As<byte, object>(ref sourceArray.GetRawArrayData());
                byte* pElement = pDestinationArray + (nuint)destinationIndex * destinationElementSize;

                for (int i = 0; i < length; i++)
                {
                    object boxedValue = Unsafe.Add(ref refSourceArray, sourceIndex + i);
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

                    RuntimeImports.RhUnbox(boxedValue, ref *pElement, destinationElementEEType);
                    pElement += destinationElementSize;
                }
            }
        }


        //
        // Array.CopyImpl case: Value-type array with embedded gc-references. 
        //
        private static unsafe void CopyImplValueTypeArrayWithInnerGcRefs(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            Debug.Assert(RuntimeImports.AreTypesEquivalent(sourceArray.EETypePtr, destinationArray.EETypePtr));
            Debug.Assert(sourceArray.ElementEEType.IsValueType);

            EETypePtr sourceElementEEType = sourceArray.EETypePtr.ArrayElementType;
            bool reverseCopy = ((object)sourceArray == (object)destinationArray) && (sourceIndex < destinationIndex);

            // Copy scenario: ValueType-array to value-type array with embedded gc-refs.
            object[] boxedElements = null;
            if (reliable)
            {
                boxedElements = new object[length];
                reverseCopy = false;
            }

            fixed (byte* pDstArray = &destinationArray.GetRawArrayData(), pSrcArray = &sourceArray.GetRawArrayData())
            {
                nuint cbElementSize = sourceArray.ElementSize;
                byte* pSourceElement = pSrcArray + (nuint)sourceIndex * cbElementSize;
                byte* pDestinationElement = pDstArray + (nuint)destinationIndex * cbElementSize;
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

                    object boxedValue = RuntimeImports.RhBox(sourceElementEEType, ref *pSourceElement);
                    if (reliable)
                        boxedElements[i] = boxedValue;
                    else
                        RuntimeImports.RhUnbox(boxedValue, ref *pDestinationElement, sourceElementEEType);

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
            Debug.Assert((sourceArray.ElementEEType.IsValueType && !sourceArray.ElementEEType.HasPointers) ||
                sourceArray.ElementEEType.IsPointer);
            Debug.Assert((destinationArray.ElementEEType.IsValueType && !destinationArray.ElementEEType.HasPointers) ||
                destinationArray.ElementEEType.IsPointer);

            // Copy scenario: ValueType-array to value-type array with no embedded gc-refs.
            nuint elementSize = sourceArray.ElementSize;
            fixed (byte* pSrcArray = &sourceArray.GetRawArrayData(), pDstArray = &destinationArray.GetRawArrayData())
            {
                byte* pSrcElements = pSrcArray + (nuint)sourceIndex * elementSize;
                byte* pDstElements = pDstArray + (nuint)destinationIndex * elementSize;
                nuint cbCopy = elementSize * (nuint)length;
                Buffer.Memmove(pDstElements, pSrcElements, cbCopy);
            }
        }

        //
        // Array.CopyImpl case: Primitive types that have a widening conversion
        //
        private static unsafe void CopyImplPrimitiveTypeWithWidening(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length, bool reliable)
        {
            EETypePtr sourceElementEEType = sourceArray.ElementEEType;
            EETypePtr destinationElementEEType = destinationArray.ElementEEType;

            Debug.Assert(sourceElementEEType.IsPrimitive && destinationElementEEType.IsPrimitive); // Caller has already validated this.

            EETypeElementType sourceElementType = sourceElementEEType.ElementType;
            EETypeElementType destElementType = destinationElementEEType.ElementType;

            nuint srcElementSize = sourceArray.ElementSize;
            nuint destElementSize = destinationArray.ElementSize;

            if ((sourceElementEEType.IsEnum || destinationElementEEType.IsEnum) && sourceElementType != destElementType)
                throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);

            if (reliable)
            {
                // ContrainedCopy() cannot even widen - it can only copy same type or enum to its exact integral subtype.
                if (sourceElementType != destElementType)
                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_ConstrainedCopy);
            }

            fixed (byte* pSrcArray = &sourceArray.GetRawArrayData(), pDstArray = &destinationArray.GetRawArrayData())
            {
                byte* srcData = pSrcArray + (nuint)sourceIndex * srcElementSize;
                byte* data = pDstArray + (nuint)destinationIndex * destElementSize;

                if (sourceElementType == destElementType)
                {
                    // Multidim arrays and enum->int copies can still reach this path.
                    Buffer.Memmove(dest: data, src: srcData, len: (nuint)length * srcElementSize);
                    return;
                }

                ulong dummyElementForZeroLengthCopies = 0;
                // If the element types aren't identical and the length is zero, we're still obliged to check the types for widening compatibility.
                // We do this by forcing the loop below to copy one dummy element.
                if (length == 0)
                {
                    srcData = (byte*)&dummyElementForZeroLengthCopies;
                    data = (byte*)&dummyElementForZeroLengthCopies;
                    length = 1;
                }

                for (int i = 0; i < length; i++, srcData += srcElementSize, data += destElementSize)
                {
                    // We pretty much have to do some fancy datatype mangling every time here, for
                    // converting w/ sign extension and floating point conversions.
                    switch (sourceElementType)
                    {
                        case EETypeElementType.Byte:
                            {
                                switch (destElementType)
                                {
                                    case EETypeElementType.Single:
                                        *(float*)data = *(byte*)srcData;
                                        break;

                                    case EETypeElementType.Double:
                                        *(double*)data = *(byte*)srcData;
                                        break;

                                    case EETypeElementType.Char:
                                    case EETypeElementType.Int16:
                                    case EETypeElementType.UInt16:
                                        *(short*)data = *(byte*)srcData;
                                        break;

                                    case EETypeElementType.Int32:
                                    case EETypeElementType.UInt32:
                                        *(int*)data = *(byte*)srcData;
                                        break;

                                    case EETypeElementType.Int64:
                                    case EETypeElementType.UInt64:
                                        *(long*)data = *(byte*)srcData;
                                        break;

                                    default:
                                        throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                                }
                                break;
                            }

                        case EETypeElementType.SByte:
                            switch (destElementType)
                            {
                                case EETypeElementType.Int16:
                                    *(short*)data = *(sbyte*)srcData;
                                    break;

                                case EETypeElementType.Int32:
                                    *(int*)data = *(sbyte*)srcData;
                                    break;

                                case EETypeElementType.Int64:
                                    *(long*)data = *(sbyte*)srcData;
                                    break;

                                case EETypeElementType.Single:
                                    *(float*)data = *(sbyte*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = *(sbyte*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case EETypeElementType.UInt16:
                        case EETypeElementType.Char:
                            switch (destElementType)
                            {
                                case EETypeElementType.Single:
                                    *(float*)data = *(ushort*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = *(ushort*)srcData;
                                    break;

                                case EETypeElementType.UInt16:
                                case EETypeElementType.Char:
                                    *(ushort*)data = *(ushort*)srcData;
                                    break;

                                case EETypeElementType.Int32:
                                case EETypeElementType.UInt32:
                                    *(uint*)data = *(ushort*)srcData;
                                    break;

                                case EETypeElementType.Int64:
                                case EETypeElementType.UInt64:
                                    *(ulong*)data = *(ushort*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case EETypeElementType.Int16:
                            switch (destElementType)
                            {
                                case EETypeElementType.Int32:
                                    *(int*)data = *(short*)srcData;
                                    break;

                                case EETypeElementType.Int64:
                                    *(long*)data = *(short*)srcData;
                                    break;

                                case EETypeElementType.Single:
                                    *(float*)data = *(short*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = *(short*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case EETypeElementType.Int32:
                            switch (destElementType)
                            {
                                case EETypeElementType.Int64:
                                    *(long*)data = *(int*)srcData;
                                    break;

                                case EETypeElementType.Single:
                                    *(float*)data = (float)*(int*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = *(int*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case EETypeElementType.UInt32:
                            switch (destElementType)
                            {
                                case EETypeElementType.Int64:
                                case EETypeElementType.UInt64:
                                    *(long*)data = *(uint*)srcData;
                                    break;

                                case EETypeElementType.Single:
                                    *(float*)data = (float)*(uint*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = *(uint*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;


                        case EETypeElementType.Int64:
                            switch (destElementType)
                            {
                                case EETypeElementType.Single:
                                    *(float*)data = (float)*(long*)srcData;
                                    break;

                                case EETypeElementType.Double:
                                    *(double*)data = (double)*(long*)srcData;
                                    break;

                                default:
                                    throw new ArrayTypeMismatchException(SR.ArrayTypeMismatch_CantAssignType);
                            }
                            break;

                        case EETypeElementType.UInt64:
                            switch (destElementType)
                            {
                                case EETypeElementType.Single:

                                    //*(float*) data = (float) *(Ulong*)srcData;
                                    long srcValToFloat = *(long*)srcData;
                                    float f = (float)srcValToFloat;
                                    if (srcValToFloat < 0)
                                        f += 4294967296.0f * 4294967296.0f; // This is 2^64

                                    *(float*)data = f;
                                    break;

                                case EETypeElementType.Double:
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

                        case EETypeElementType.Single:
                            switch (destElementType)
                            {
                                case EETypeElementType.Double:
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

        public static void Clear(Array array, int index, int length)
        {
            if (!RuntimeImports.TryArrayClear(array, index, length))
                ReportClearErrors(array, index, length);
        }

        private static unsafe void ReportClearErrors(Array array, int index, int length)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0 || index > array.Length || length < 0 || length > array.Length)
                throw new IndexOutOfRangeException();
            if (length > (array.Length - index))
                throw new IndexOutOfRangeException();

            // The above checks should have covered all the reasons why Clear would fail.
            Debug.Assert(false);
        }

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
                return this.EETypePtr.ArrayRank;
            }
        }

        // Allocate new multidimensional array of given dimensions. Assumes that that pLengths is immutable.
        internal static unsafe Array NewMultiDimArray(EETypePtr eeType, int* pLengths, int rank)
        {
            Debug.Assert(eeType.IsArray && !eeType.IsSzArray);
            Debug.Assert(rank == eeType.ArrayRank);

            // Code below assumes 0 lower bounds. MdArray of rank 1 with zero lower bounds should never be allocated.
            // The runtime always allocates an SzArray for those:
            // * newobj instance void int32[0...]::.ctor(int32)" actually gives you int[]
            // * int[] is castable to int[*] to make it mostly transparent
            // The callers need to check for this.
            Debug.Assert(rank != 1);

            ulong totalLength = 1;
            bool maxArrayDimensionLengthOverflow = false;

            for (int i = 0; i < rank; i++)
            {
                int length = pLengths[i];
                if (length < 0)
                    throw new OverflowException();
                if (length > MaxArrayLength)
                    maxArrayDimensionLengthOverflow = true;
                totalLength = totalLength * (ulong)length;
                if (totalLength > int.MaxValue)
                    throw new OutOfMemoryException(); // "Array dimensions exceeded supported range."
            }

            // Throw this exception only after everything else was validated for backward compatibility.
            if (maxArrayDimensionLengthOverflow)
                throw new OutOfMemoryException(); // "Array dimensions exceeded supported range."

            Array ret = RuntimeImports.RhNewArray(eeType, (int)totalLength);

            ref int bounds = ref ret.GetRawMultiDimArrayBounds();
            for (int i = 0; i < rank; i++)
            {
                Unsafe.Add(ref bounds, i) = pLengths[i];
            }

            return ret;
        }

        public int GetLowerBound(int dimension)
        {
            if (!IsSzArray)
            {
                int rank = Rank;
                if ((uint)dimension >= rank)
                    throw new IndexOutOfRangeException();

                return Unsafe.Add(ref GetRawMultiDimArrayBounds(), rank + dimension);
            }

            if (dimension != 0)
                throw new IndexOutOfRangeException();
            return 0;
        }

        public int GetUpperBound(int dimension)
        {
            if (!IsSzArray)
            {
                int rank = Rank;
                if ((uint)dimension >= rank)
                    throw new IndexOutOfRangeException();

                ref int bounds = ref GetRawMultiDimArrayBounds();

                int length = Unsafe.Add(ref bounds, dimension);
                int lowerBound = Unsafe.Add(ref bounds, rank + dimension);
                return length + lowerBound - 1;
            }

            if (dimension != 0)
                throw new IndexOutOfRangeException();
            return Length - 1;
        }

        public unsafe object GetValue(int index)
        {
            if (!IsSzArray)
            {
                if (Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

                return GetValue(&index, 1);
            }

            if ((uint)index >= (uint)Length)
                throw new IndexOutOfRangeException();

            if (ElementEEType.IsPointer)
                throw new NotSupportedException(SR.NotSupported_Type);

            return GetValueWithFlattenedIndex_NoErrorCheck(index);
        }

        public unsafe object GetValue(int index1, int index2)
        {
            if (Rank != 2)
                throw new ArgumentException(SR.Arg_Need2DArray);

            int* pIndices = stackalloc int[2];
            pIndices[0] = index1;
            pIndices[1] = index2;
            return GetValue(pIndices, 2);
        }

        public unsafe object GetValue(int index1, int index2, int index3)
        {
            if (Rank != 3)
                throw new ArgumentException(SR.Arg_Need3DArray);

            int* pIndices = stackalloc int[3];
            pIndices[0] = index1;
            pIndices[1] = index2;
            pIndices[2] = index3;
            return GetValue(pIndices, 3);
        }

        public unsafe object GetValue(params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));

            int length = indices.Length;

            if (IsSzArray && length == 1)
                return GetValue(indices[0]);

            if (Rank != length)
                throw new ArgumentException(SR.Arg_RankIndices);

            Debug.Assert(length > 0);
            fixed (int* pIndices = &indices[0])
                return GetValue(pIndices, length);
        }

        private unsafe object GetValue(int* pIndices, int rank)
        {
            Debug.Assert(Rank == rank);
            Debug.Assert(!IsSzArray);

            ref int bounds = ref GetRawMultiDimArrayBounds();

            int flattenedIndex = 0;
            for (int i = 0; i < rank; i++)
            {
                int index = pIndices[i] - Unsafe.Add(ref bounds, rank + i);
                int length = Unsafe.Add(ref bounds, i);
                if ((uint)index >= (uint)length)
                    throw new IndexOutOfRangeException();
                flattenedIndex = (length * flattenedIndex) + index;
            }

            if ((uint)flattenedIndex >= (uint)Length)
                throw new IndexOutOfRangeException();

            if (ElementEEType.IsPointer)
                throw new NotSupportedException(SR.NotSupported_Type);

            return GetValueWithFlattenedIndex_NoErrorCheck(flattenedIndex);
        }

        private object GetValueWithFlattenedIndex_NoErrorCheck(int flattenedIndex)
        {
            ref byte element = ref Unsafe.AddByteOffset(ref GetRawArrayData(), (nuint)flattenedIndex * ElementSize);

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                return RuntimeImports.RhBox(pElementEEType, ref element);
            }
            else
            {
                Debug.Assert(!pElementEEType.IsPointer);
                return Unsafe.As<byte, object>(ref element);
            }
        }

        public unsafe void SetValue(object value, int index)
        {
            if (!IsSzArray)
            {
                if (Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

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

                value = InvokeUtils.CheckArgument(value, pElementEEType, InvokeUtils.CheckArgumentSemantics.ArraySet, binderBundle: null);
                Debug.Assert(value == null || RuntimeImports.AreTypesAssignable(value.EETypePtr, pElementEEType));

                ref byte element = ref Unsafe.AddByteOffset(ref GetRawArrayData(), (nuint)index * ElementSize);
                RuntimeImports.RhUnbox(value, ref element, pElementEEType);
            }
            else if (pElementEEType.IsPointer)
            {
                throw new NotSupportedException(SR.NotSupported_Type);
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

        public unsafe void SetValue(object value, int index1, int index2)
        {
            if (Rank != 2)
                throw new ArgumentException(SR.Arg_Need2DArray);

            int* pIndices = stackalloc int[2];
            pIndices[0] = index1;
            pIndices[1] = index2;
            SetValue(value, pIndices, 2);
        }

        public unsafe void SetValue(object value, int index1, int index2, int index3)
        {
            if (Rank != 3)
                throw new ArgumentException(SR.Arg_Need3DArray);

            int* pIndices = stackalloc int[3];
            pIndices[0] = index1;
            pIndices[1] = index2;
            pIndices[2] = index3;
            SetValue(value, pIndices, 3);
        }

        public unsafe void SetValue(object value, params int[] indices)
        {
            if (indices == null)
                throw new ArgumentNullException(nameof(indices));

            int length = indices.Length;

            if (IsSzArray && length == 1)
            {
                SetValue(value, indices[0]);
                return;
            }

            if (Rank != length)
                throw new ArgumentException(SR.Arg_RankIndices);

            Debug.Assert(length > 0);
            fixed (int* pIndices = &indices[0])
            {
                SetValue(value, pIndices, length);
                return;
            }
        }

        private unsafe void SetValue(object value, int* pIndices, int rank)
        {
            Debug.Assert(Rank == rank);
            Debug.Assert(!IsSzArray);

            ref int bounds = ref GetRawMultiDimArrayBounds();

            int flattenedIndex = 0;
            for (int i = 0; i < rank; i++)
            {
                int index = pIndices[i] - Unsafe.Add(ref bounds, rank + i);
                int length = Unsafe.Add(ref bounds, i);
                if ((uint)index >= (uint)length)
                    throw new IndexOutOfRangeException();
                flattenedIndex = (length * flattenedIndex) + index;
            }

            if ((uint)flattenedIndex >= (uint)Length)
                throw new IndexOutOfRangeException();

            ref byte element = ref Unsafe.AddByteOffset(ref GetRawArrayData(), (nuint)flattenedIndex * ElementSize);

            EETypePtr pElementEEType = ElementEEType;
            if (pElementEEType.IsValueType)
            {
                // Unlike most callers of InvokeUtils.ChangeType(), Array.SetValue() does *not* permit conversion from a primitive to an Enum.
                if (value != null && !(value.EETypePtr == pElementEEType) && pElementEEType.IsEnum)
                    throw new InvalidCastException(SR.Format(SR.Arg_ObjObjEx, value.GetType(), Type.GetTypeFromHandle(new RuntimeTypeHandle(pElementEEType))));

                value = InvokeUtils.CheckArgument(value, pElementEEType, InvokeUtils.CheckArgumentSemantics.ArraySet, binderBundle: null);
                Debug.Assert(value == null || RuntimeImports.AreTypesAssignable(value.EETypePtr, pElementEEType));

                RuntimeImports.RhUnbox(value, ref element, pElementEEType);
            }
            else if (pElementEEType.IsPointer)
            {
                throw new NotSupportedException(SR.NotSupported_Type);
            }
            else
            {
                try
                {
                    RuntimeImports.RhCheckArrayStore(this, value);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new InvalidCastException(SR.InvalidCast_StoreArrayElement);
                }
                Unsafe.As<byte, object>(ref element) = value;
            }
        }

        internal EETypePtr ElementEEType
        {
            get
            {
                return this.EETypePtr.ArrayElementType;
            }
        }

        internal CorElementType GetCorElementTypeOfElementType()
        {
            return ElementEEType.CorElementType;
        }

        internal bool IsValueOfElementType(object o)
        {
            return ElementEEType.Equals(o.EETypePtr);
        }

        public IEnumerator GetEnumerator()
        {
            return new ArrayEnumerator(this);
        }

        private sealed class ArrayEnumerator : IEnumerator, ICloneable
        {
            private Array _array;
            private int _index;
            private int _endIndex; // cache array length, since it's a little slow.

            internal ArrayEnumerator(Array array)
            {
                _array = array;
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

            public void Reset()
            {
                _index = -1;
            }

            public object Clone()
            {
                return MemberwiseClone();
            }

            public object Current
            {
                get
                {
                    if (_index < 0) throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
                    if (_index >= _endIndex) throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
                    if (_array.ElementEEType.IsPointer) throw new NotSupportedException(SR.NotSupported_Type);
                    return _array.GetValueWithFlattenedIndex_NoErrorCheck(_index);
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

        private static int IndexOfImpl<T>(T[] array, T value, int startIndex, int count)
        {
            // See comment in EqualityComparerHelpers.GetComparerForReferenceTypesOnly for details
            EqualityComparer<T> comparer = EqualityComparerHelpers.GetComparerForReferenceTypesOnly<T>();

            int endIndex = startIndex + count;
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
                    if (EqualityComparerHelpers.StructOnlyEquals<T>(array[i], value))
                        return i;
                }
            }

            return -1;
        }

        private static int LastIndexOfImpl<T>(T[] array, T value, int startIndex, int count)
        {
            // See comment in EqualityComparerHelpers.GetComparerForReferenceTypesOnly for details
            EqualityComparer<T> comparer = EqualityComparerHelpers.GetComparerForReferenceTypesOnly<T>();

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
                    if (EqualityComparerHelpers.StructOnlyEquals<T>(array[i], value))
                        return i;
                }
            }

            return -1;
        }
    }

    internal class ArrayEnumeratorBase : ICloneable
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

        public object Clone()
        {
            return MemberwiseClone();
        }
    }

    //
    // Note: the declared base type and interface list also determines what Reflection returns from TypeInfo.BaseType and TypeInfo.ImplementedInterfaces for array types.
    // This also means the class must be declared "public" so that the framework can reflect on it.
    //
    public class Array<T> : Array, IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyList<T>
    {
        // Prevent the C# compiler from generating a public default constructor
        private Array() { }

        public new IEnumerator<T> GetEnumerator()
        {
            // get length so we don't have to call the Length property again in ArrayEnumerator constructor
            // and avoid more checking there too.
            int length = this.Length;
            return length == 0 ? ArrayEnumerator.Empty : new ArrayEnumerator(Unsafe.As<T[]>(this), length);
        }

        public int Count
        {
            get
            {
                return this.Length;
            }
        }

        //
        // Fun fact:
        //
        //  ((int[])a).IsReadOnly returns false.
        //  ((IList<int>)a).IsReadOnly returns true.
        //
        public new bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        public void Add(T item)
        {
            ThrowHelper.ThrowNotSupportedException();
        }

        public void Clear()
        {
            ThrowHelper.ThrowNotSupportedException();
        }

        public bool Contains(T item)
        {
            T[] array = Unsafe.As<T[]>(this);
            return Array.IndexOf(array, item, 0, array.Length) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(Unsafe.As<T[]>(this), 0, array, arrayIndex, this.Length);
        }

        public bool Remove(T item)
        {
            ThrowHelper.ThrowNotSupportedException();
            return false; // unreachable
        }

        public T this[int index]
        {
            get
            {
                try
                {
                    return Unsafe.As<T[]>(this)[index];
                }
                catch (IndexOutOfRangeException)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                    return default; // unreachable
                }
            }
            set
            {
                try
                {
                    Unsafe.As<T[]>(this)[index] = value;
                }
                catch (IndexOutOfRangeException)
                {
                    ThrowHelper.ThrowArgumentOutOfRange_IndexException();
                }
            }
        }

        public int IndexOf(T item)
        {
            T[] array = Unsafe.As<T[]>(this);
            return Array.IndexOf(array, item, 0, array.Length);
        }

        public void Insert(int index, T item)
        {
            ThrowHelper.ThrowNotSupportedException();
        }

        public void RemoveAt(int index)
        {
            ThrowHelper.ThrowNotSupportedException();
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
                    if ((uint)_index >= (uint)_endIndex)
                        ThrowHelper.ThrowInvalidOperationException();
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

    public class MDArray
    {
        public const int MinRank = 1;
        public const int MaxRank = 32;
    }
}
