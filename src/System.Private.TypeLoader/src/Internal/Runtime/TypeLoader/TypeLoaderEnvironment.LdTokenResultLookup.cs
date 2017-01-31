﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;

using Internal.NativeFormat;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    public sealed partial class TypeLoaderEnvironment
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DynamicFieldHandleInfo
        {
            public IntPtr DeclaringType;
            public IntPtr FieldName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DynamicMethodHandleInfo
        {
            public IntPtr DeclaringType;
            public IntPtr MethodName;
            public RuntimeSignature MethodSignature;
            public int NumGenericArgs;
            public IntPtr GenericArgsArray;
        }


        #region Field Ldtoken Functions
        public unsafe IntPtr TryGetRuntimeFieldHandleForComponents(RuntimeTypeHandle declaringTypeHandle, IntPtr fieldName)
        {
            IntPtr runtimeFieldHandleValue = MemoryHelpers.AllocateMemory(sizeof(DynamicFieldHandleInfo));

            DynamicFieldHandleInfo* fieldData = (DynamicFieldHandleInfo*)runtimeFieldHandleValue.ToPointer();
            fieldData->DeclaringType = *(IntPtr*)&declaringTypeHandle;
            fieldData->FieldName = fieldName;

            // Special flag (lowest bit set) in the handle value to indicate it was dynamically allocated
            return runtimeFieldHandleValue + 1;
        }
        public bool TryGetRuntimeFieldHandleComponents(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out string fieldName)
        {
            return runtimeFieldHandle.IsDynamic() ?
                TryGetDynamicRuntimeFieldHandleComponents(runtimeFieldHandle, out declaringTypeHandle, out fieldName) :
                TryGetStaticRuntimeFieldHandleComponents(runtimeFieldHandle, out declaringTypeHandle, out fieldName);
        }
        private unsafe bool TryGetDynamicRuntimeFieldHandleComponents(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out string fieldName)
        {
            IntPtr runtimeFieldHandleValue = *(IntPtr*)&runtimeFieldHandle;

            // Special flag in the handle value to indicate it was dynamically allocated
            Debug.Assert((runtimeFieldHandleValue.ToInt64() & 0x1) == 0x1);
            runtimeFieldHandleValue = runtimeFieldHandleValue - 1;

            DynamicFieldHandleInfo* fieldData = (DynamicFieldHandleInfo*)runtimeFieldHandleValue.ToPointer();
            declaringTypeHandle = *(RuntimeTypeHandle*)&(fieldData->DeclaringType);

            // FieldName points to the field name in NativeLayout format, so we parse it using a NativeParser
            IntPtr fieldNamePtr = fieldData->FieldName;
            fieldName = GetStringFromMemoryInNativeFormat(fieldNamePtr);

            return true;
        }

        private static unsafe string GetStringFromMemoryInNativeFormat(IntPtr pointerToDataStream)
        {
            byte* dataStream = (byte*)pointerToDataStream.ToPointer();
            uint stringLen = NativePrimitiveDecoder.DecodeUnsigned(ref dataStream);
            return Encoding.UTF8.GetString(dataStream, checked((int)stringLen));
        }

        private static LowLevelDictionary<string, IntPtr> s_nativeFormatStrings;

        /// <summary>
        /// From a string, get a pointer to an allocated memory location that holds a NativeFormat encoded string.
        /// This is used for the creation of RuntimeFieldHandles from metadata.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public IntPtr GetNativeFormatStringForString(string str)
        {
            using (LockHolder.Hold(_typeLoaderLock))
            {
                IntPtr result;
                if (s_nativeFormatStrings.TryGetValue(str, out result))
                    return result;

                NativePrimitiveEncoder stringEncoder = new NativePrimitiveEncoder();
                stringEncoder.Init();
                byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
                stringEncoder.WriteUnsigned(checked((uint)utf8Bytes.Length));
                foreach (byte b in utf8Bytes)
                    stringEncoder.WriteByte(b);

                IntPtr allocatedNativeFormatString = MemoryHelpers.AllocateMemory(stringEncoder.Size);
                unsafe
                {
                    stringEncoder.Save((byte*)allocatedNativeFormatString.ToPointer(), stringEncoder.Size);
                }
                s_nativeFormatStrings.Add(str, allocatedNativeFormatString);
                return allocatedNativeFormatString;
            }
        }

        private unsafe bool TryGetStaticRuntimeFieldHandleComponents(RuntimeFieldHandle runtimeFieldHandle, out RuntimeTypeHandle declaringTypeHandle, out string fieldName)
        {
            fieldName = null;
            declaringTypeHandle = default(RuntimeTypeHandle);

            // Make sure it's not a dynamically allocated RuntimeFieldHandle before we attempt to use it to parse native layout data
            Debug.Assert(((*(IntPtr*)&runtimeFieldHandle).ToInt64() & 0x1) == 0);

            RuntimeFieldHandleInfo* fieldData = *(RuntimeFieldHandleInfo**)&runtimeFieldHandle;

#if CORERT
            // The native layout info signature is a pair. 
            // The first is a pointer that points to the TypeManager indirection cell.
            // The second is the offset into the native layout info blob in that TypeManager, where the native signature is encoded.
            IntPtr* nativeLayoutInfoSignatureData = (IntPtr*)fieldData->NativeLayoutInfoSignature;

            RuntimeSignature signature = RuntimeSignature.CreateFromNativeLayoutSignature(
                *(IntPtr*)nativeLayoutInfoSignatureData[0],
                (uint)nativeLayoutInfoSignatureData[1].ToInt32());
#else
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(fieldData->NativeLayoutInfoSignature);

            RuntimeSignature signature = RuntimeSignature.CreateFromNativeLayoutSignature(
                moduleHandle,
                GetNativeLayoutInfoReader(moduleHandle).AddressToOffset(fieldData->NativeLayoutInfoSignature));
#endif

            RuntimeSignature remainingSignature;
            if (!GetTypeFromSignatureAndContext(signature, null, null, out declaringTypeHandle, out remainingSignature))
                return false;

            // GetTypeFromSignatureAndContext parses the type from the signature and returns a pointer to the next
            // part of the native layout signature to read which we get the field name from
            var reader = GetNativeLayoutInfoReader(remainingSignature.ModuleHandle);
            var parser = new NativeParser(reader, remainingSignature.NativeLayoutOffset);
            fieldName = parser.GetString();

            return true;
        }
        #endregion


        #region Method Ldtoken Functions
        /// <summary>
        /// Create a runtime method handle from name, signature and generic arguments. If the methodSignature 
        /// is constructed from a metadata token, the methodName should be IntPtr.Zero, as it already encodes the method
        /// name.
        /// </summary>
        internal unsafe IntPtr TryGetRuntimeMethodHandleForComponents(RuntimeTypeHandle declaringTypeHandle, IntPtr methodName, RuntimeSignature methodSignature, RuntimeTypeHandle[] genericMethodArgs)
        {
            // TODO! Consider interning these!, but if we do remember this function is called from code which isn't under the type builder lock 
            int sizeToAllocate = sizeof(DynamicMethodHandleInfo);
            // Use checked arithmetics to ensure there aren't any overflows/truncations
            sizeToAllocate = checked(sizeToAllocate + (genericMethodArgs.Length > 0 ? sizeof(IntPtr) * (genericMethodArgs.Length - 1) : 0));
            IntPtr runtimeMethodHandleValue = MemoryHelpers.AllocateMemory(sizeToAllocate);

            DynamicMethodHandleInfo* methodData = (DynamicMethodHandleInfo*)runtimeMethodHandleValue.ToPointer();
            methodData->DeclaringType = *(IntPtr*)&declaringTypeHandle;
            methodData->MethodName = methodName;
            methodData->MethodSignature = methodSignature;
            methodData->NumGenericArgs = genericMethodArgs.Length;
            IntPtr* genericArgPtr = &(methodData->GenericArgsArray);
            for (int i = 0; i < genericMethodArgs.Length; i++)
            {
                RuntimeTypeHandle currentArg = genericMethodArgs[i];
                genericArgPtr[i] = *(IntPtr*)&currentArg;
            }

            // Special flag in the handle value to indicate it was dynamically allocated, and doesn't point into the InvokeMap blob
            return runtimeMethodHandleValue + 1;
        }

        public unsafe bool TryGetRuntimeMethodHandleForComponents(RuntimeTypeHandle declaringTypeHandle, IntPtr methodName, RuntimeSignature methodSignature, RuntimeTypeHandle[] genericMethodArgs, out RuntimeMethodHandle handle)
        {
            handle = default(RuntimeMethodHandle);
            fixed (RuntimeMethodHandle* pRMH = &handle)
            {
                IntPtr rmhAsPointer = TryGetRuntimeMethodHandleForComponents(declaringTypeHandle, methodName, methodSignature, genericMethodArgs);
                *((IntPtr*)pRMH) = rmhAsPointer;
                return rmhAsPointer != IntPtr.Zero;
            }
        }

        public bool TryGetRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs)
        {
            return runtimeMethodHandle.IsDynamic() ?
                TryGetDynamicRuntimeMethodHandleComponents(runtimeMethodHandle, out declaringTypeHandle, out nameAndSignature, out genericMethodArgs) :
                TryGetStaticRuntimeMethodHandleComponents(runtimeMethodHandle, out declaringTypeHandle, out nameAndSignature, out genericMethodArgs);
        }
        private unsafe bool TryGetDynamicRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs)
        {
            IntPtr runtimeMethodHandleValue = *(IntPtr*)&runtimeMethodHandle;
            Debug.Assert((runtimeMethodHandleValue.ToInt64() & 0x1) == 0x1);

            // Special flag in the handle value to indicate it was dynamically allocated, and doesn't point into the InvokeMap blob
            runtimeMethodHandleValue = runtimeMethodHandleValue - 1;

            DynamicMethodHandleInfo* methodData = (DynamicMethodHandleInfo*)runtimeMethodHandleValue.ToPointer();
            declaringTypeHandle = *(RuntimeTypeHandle*)&(methodData->DeclaringType);
            genericMethodArgs = Empty<RuntimeTypeHandle>.Array;

            if (methodData->NumGenericArgs > 0)
            {
                IntPtr* genericArgPtr = &(methodData->GenericArgsArray);
                genericMethodArgs = new RuntimeTypeHandle[methodData->NumGenericArgs];
                for (int i = 0; i < methodData->NumGenericArgs; i++)
                {
                    genericMethodArgs[i] = *(RuntimeTypeHandle*)&(genericArgPtr[i]);
                }
            }

            if (methodData->MethodSignature.IsNativeLayoutSignature)
            {
                // MethodName points to the method name in NativeLayout format, so we parse it using a NativeParser
                IntPtr methodNamePtr = methodData->MethodName;
                string name = GetStringFromMemoryInNativeFormat(methodNamePtr);

                nameAndSignature = new MethodNameAndSignature(name, methodData->MethodSignature);
            }
            else
            {
                ModuleInfo moduleInfo = methodData->MethodSignature.GetModuleInfo();

                string name;
#if ECMA_METADATA_SUPPORT
                if (moduleInfo is NativeFormatModuleInfo)
#endif            
                {
                    var metadataReader = ((NativeFormatModuleInfo)moduleInfo).MetadataReader;
                    var methodHandle = methodData->MethodSignature.Token.AsHandle().ToMethodHandle(metadataReader);
                    var method = methodHandle.GetMethod(metadataReader);
                    name = metadataReader.GetConstantStringValue(method.Name).Value;
                }
#if ECMA_METADATA_SUPPORT
                else
                {
                    var ecmaReader = ((EcmaModuleInfo)moduleInfo).EcmaPEInfo.Reader;
                    var ecmaHandle = System.Reflection.Metadata.Ecma335.MetadataTokens.Handle(methodData->MethodSignature.Token);
                    var ecmaMethodHandle = (System.Reflection.Metadata.MethodDefinitionHandle)ecmaHandle;
                    var ecmaMethod = ecmaReader.GetMethodDefinition(ecmaMethodHandle);
                    name = ecmaReader.GetString(ecmaMethod.Name);
                }
#endif
                nameAndSignature = new MethodNameAndSignature(name, methodData->MethodSignature);
            }

            return true;
        }
        private unsafe bool TryGetStaticRuntimeMethodHandleComponents(RuntimeMethodHandle runtimeMethodHandle, out RuntimeTypeHandle declaringTypeHandle, out MethodNameAndSignature nameAndSignature, out RuntimeTypeHandle[] genericMethodArgs)
        {
            declaringTypeHandle = default(RuntimeTypeHandle);
            nameAndSignature = null;
            genericMethodArgs = null;

            // Make sure it's not a dynamically allocated RuntimeMethodHandle before we attempt to use it to parse native layout data
            Debug.Assert(((*(IntPtr*)&runtimeMethodHandle).ToInt64() & 0x1) == 0);

            RuntimeMethodHandleInfo* methodData = *(RuntimeMethodHandleInfo**)&runtimeMethodHandle;

#if CORERT
            // The native layout info signature is a pair. 
            // The first is a pointer that points to the TypeManager indirection cell.
            // The second is the offset into the native layout info blob in that TypeManager, where the native signature is encoded.
            IntPtr* nativeLayoutInfoSignatureData = (IntPtr*)methodData->NativeLayoutInfoSignature;

            RuntimeSignature signature = RuntimeSignature.CreateFromNativeLayoutSignature(
                *(IntPtr*)nativeLayoutInfoSignatureData[0],
                (uint)nativeLayoutInfoSignatureData[1].ToInt32());
#else
            IntPtr moduleHandle = RuntimeAugments.GetModuleFromPointer(methodData->NativeLayoutInfoSignature);

            RuntimeSignature signature = RuntimeSignature.CreateFromNativeLayoutSignature(
                moduleHandle,
                GetNativeLayoutInfoReader(moduleHandle).AddressToOffset(methodData->NativeLayoutInfoSignature));
#endif

            RuntimeSignature remainingSignature;
            return GetMethodFromSignatureAndContext(signature, null, null, out declaringTypeHandle, out nameAndSignature, out genericMethodArgs, out remainingSignature);
        }
        #endregion
    }
}
