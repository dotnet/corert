// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Internal.Runtime.Augments;

namespace Internal.Runtime.CompilerServices
{
    public static class FunctionPointerOps
    {
        private struct GenericMethodDescriptorInfo
        {
            public IntPtr MethodFunctionPointer;
            public IntPtr InstantiationArgument;
        }

        private unsafe struct RuntimeGeneratedGenericMethodDescriptor
        {
            public GenericMethodDescriptor Descriptor;
            private IntPtr _MethodDictionaryPointer;

            public void Set(IntPtr methodFunctionPointer, IntPtr methodDictionaryPointer)
            {
                _MethodDictionaryPointer = methodDictionaryPointer;
                Descriptor._MethodFunctionPointer = methodFunctionPointer;

                // This is ONLY safe if this structure is never moved. Do not box this struct.
                fixed (IntPtr* pointerPointer = &_MethodDictionaryPointer)
                {
                    Descriptor._MethodDictionaryPointerPointer = pointerPointer;
                }
            }
        }


        private static uint s_genericFunctionPointerNextIndex = 0;
        private const uint c_genericDictionaryChunkSize = 1024;
        private const int c_genericFunctionPointerOffset = 2;
        private static LowLevelList<IntPtr> s_genericFunctionPointerCollection = new LowLevelList<IntPtr>();
        private static LowLevelDictionary<GenericMethodDescriptorInfo, uint> s_genericFunctionPointerDictionary = new LowLevelDictionary<GenericMethodDescriptorInfo, uint>(new GenericMethodFunctionPointerDescriptorHashingType());

        private class GenericMethodFunctionPointerDescriptorHashingType : IEqualityComparer<GenericMethodDescriptorInfo>
        {
            bool IEqualityComparer<GenericMethodDescriptorInfo>.Equals(GenericMethodDescriptorInfo x, GenericMethodDescriptorInfo y)
            {
                if (x.MethodFunctionPointer != y.MethodFunctionPointer)
                    return false;

                if (x.InstantiationArgument != y.InstantiationArgument)
                    return false;

                return true;
            }

            int IEqualityComparer<GenericMethodDescriptorInfo>.GetHashCode(GenericMethodDescriptorInfo obj)
            {
                int a = obj.InstantiationArgument.GetHashCode();
                int b = obj.MethodFunctionPointer.GetHashCode();
                return (a ^ b) + (a << 11) - (b >> 13);
            }
        }

        public static unsafe IntPtr GetGenericMethodFunctionPointer(IntPtr canonFunctionPointer, IntPtr instantiationArgument)
        {
            if (instantiationArgument == IntPtr.Zero)
                return canonFunctionPointer;

            lock (s_genericFunctionPointerDictionary)
            {
                GenericMethodDescriptorInfo key;
                key.MethodFunctionPointer = canonFunctionPointer;
                key.InstantiationArgument = instantiationArgument;

                uint index = 0;
                if (!s_genericFunctionPointerDictionary.TryGetValue(key, out index))
                {
                    // Capture new index value
                    index = s_genericFunctionPointerNextIndex;

                    int newChunkIndex = (int)(index / c_genericDictionaryChunkSize);
                    uint newSubChunkIndex = index % c_genericDictionaryChunkSize;

                    // Generate new chunk if existing chunks are insufficient
                    if (s_genericFunctionPointerCollection.Count <= newChunkIndex)
                    {
                        System.Diagnostics.Debug.Assert(newSubChunkIndex == 0);

                        // New generic descriptors are allocated on the native heap and not tracked in the GC.
                        UIntPtr allocationSize = new UIntPtr((uint)(c_genericDictionaryChunkSize * sizeof(RuntimeGeneratedGenericMethodDescriptor)));
                        IntPtr pNewMem = Interop.MemAlloc(allocationSize);
                        s_genericFunctionPointerCollection.Add(pNewMem);
                    }

                    RuntimeGeneratedGenericMethodDescriptor* newDescriptor = &((RuntimeGeneratedGenericMethodDescriptor*)s_genericFunctionPointerCollection[newChunkIndex])[newSubChunkIndex];

                    newDescriptor->Set(canonFunctionPointer, instantiationArgument);

                    s_genericFunctionPointerDictionary.LookupOrAdd(key, index);

                    // Now that we can no longer have failed, update the next index.
                    s_genericFunctionPointerNextIndex++;
                }

                // Lookup within list
                int chunkIndex = (int)(index / c_genericDictionaryChunkSize);
                uint subChunkIndex = index % c_genericDictionaryChunkSize;
                RuntimeGeneratedGenericMethodDescriptor* genericRuntimeFunctionPointer = &((RuntimeGeneratedGenericMethodDescriptor*)s_genericFunctionPointerCollection[chunkIndex])[subChunkIndex];

                GenericMethodDescriptor* genericFunctionPointer = &genericRuntimeFunctionPointer->Descriptor;
                System.Diagnostics.Debug.Assert(canonFunctionPointer == genericFunctionPointer->MethodFunctionPointer);
                System.Diagnostics.Debug.Assert(instantiationArgument == genericFunctionPointer->InstantiationArgument);

                return (IntPtr)((byte*)genericFunctionPointer + c_genericFunctionPointerOffset);
            }
        }

        public static unsafe bool IsGenericMethodPointer(IntPtr functionPointer)
        {
            // Check the low bit to find out what kind of function pointer we have here.
#if BIT64
            if ((functionPointer.ToInt64() & c_genericFunctionPointerOffset) == c_genericFunctionPointerOffset)
#else
            if ((functionPointer.ToInt32() & c_genericFunctionPointerOffset) == c_genericFunctionPointerOffset)
#endif
            {
                return true;
            }
            return false;
        }

        [CLSCompliant(false)]
        public static unsafe GenericMethodDescriptor* ConvertToGenericDescriptor(IntPtr functionPointer)
        {
            return (GenericMethodDescriptor*)((byte*)functionPointer - c_genericFunctionPointerOffset);
        }

        public static unsafe bool Compare(IntPtr functionPointerA, IntPtr functionPointerB)
        {
            if (!IsGenericMethodPointer(functionPointerA))
            {
                IntPtr codeTargetA = RuntimeAugments.GetCodeTarget(functionPointerA);
                IntPtr codeTargetB = RuntimeAugments.GetCodeTarget(functionPointerB);
                return codeTargetA == codeTargetB;
            }
            else
            {
                if (!IsGenericMethodPointer(functionPointerB))
                    return false;

                GenericMethodDescriptor* pointerDefA = ConvertToGenericDescriptor(functionPointerA);
                GenericMethodDescriptor* pointerDefB = ConvertToGenericDescriptor(functionPointerB);

                if (pointerDefA->InstantiationArgument != pointerDefB->InstantiationArgument)
                    return false;

                IntPtr codeTargetA = RuntimeAugments.GetCodeTarget(pointerDefA->MethodFunctionPointer);
                IntPtr codeTargetB = RuntimeAugments.GetCodeTarget(pointerDefB->MethodFunctionPointer);
                return codeTargetA == codeTargetB;
            }
        }
    }
}
