// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using Internal.NativeFormat;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;
using System.Diagnostics;

// The following definitions are required for interop with the VS Debugger
// Prior to making any changes to these, please reach out to the VS Debugger 
// team to make sure that your changes are not going to prevent the debugger
// from working.
namespace Internal.Runtime.DebuggerSupport
{
    public class LowLevelNativeFormatReader
    {
        private readonly NativeReader _nativeReader;
        private uint _offset;
        public unsafe LowLevelNativeFormatReader(byte* buffer, uint bufferSize)
        {
            _nativeReader = new NativeReader(buffer, bufferSize);
            _offset = 0;
        }

        public uint GetUnsigned()
        {
            uint value;
            _offset = _nativeReader.DecodeUnsigned(_offset, out value);
            return value;
        }

        public ulong GetUnsignedLong()
        {
            ulong value;
            _offset = _nativeReader.DecodeUnsignedLong(_offset, out value);
            return value;
        }

        public RuntimeSignature CreateRuntimeSignature()
        {
            return RuntimeSignature.CreateFromNativeLayoutSignatureForDebugger(_offset);
        }

        internal NativeReader InternalReader
        {
            get
            {
                return _nativeReader;
            }
        }

        internal uint Offset
        {
            get
            {
                return _offset;
            }
        }
    }

    public class TypeSystemHelper
    {
        public static Array NewArray(RuntimeTypeHandle arrElmType, int arrSize)
        {
            RuntimeTypeHandle arrayTypeHandle = default(RuntimeTypeHandle);
            bool succeed = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(
                    arrElmType,
                    false,
                    -1,
                    out arrayTypeHandle);
            Debug.Assert(succeed);
            return Internal.Runtime.Augments.RuntimeAugments.NewArray(arrayTypeHandle, arrSize);
        }

        public static Array NewMultiDimArray(RuntimeTypeHandle arrElmType, int rank, int[] dims, int[] lowerBounds)
        {
            RuntimeTypeHandle arrayTypeHandle = default(RuntimeTypeHandle);
            bool succeed = TypeLoaderEnvironment.Instance.TryGetArrayTypeForElementType(
                              arrElmType,
                              true,
                              rank,
                              out arrayTypeHandle
                              );
            Debug.Assert(succeed);
            return Internal.Runtime.Augments.RuntimeAugments.NewMultiDimArray(
                           arrayTypeHandle,
                           dims,
                           lowerBounds
                           );
        }

        public static bool CallingConverterDataFromMethodSignature(LowLevelNativeFormatReader reader,
                                                                   ulong[] externalReferences,
                                                                   out bool hasThis,
                                                                   out TypeDesc[] parameters,
                                                                   out bool[] paramsWithDependentLayout)
        {

            if (externalReferences == null)
            {
                throw new ArgumentNullException(nameof(externalReferences));
            }

            TypeSystemContext typeSystemContext = TypeSystemContextFactory.Create();
            bool result = TypeLoaderEnvironment.Instance.GetCallingConverterDataFromMethodSignature_NativeLayout_Common(
               typeSystemContext,
               reader.CreateRuntimeSignature(),
               Instantiation.Empty,
               Instantiation.Empty,
               out hasThis,
               out parameters,
               out paramsWithDependentLayout,
               reader.InternalReader,
               externalReferences);
            TypeSystemContextFactory.Recycle(typeSystemContext);
            return result;
        }

        public static RuntimeTypeHandle GetConstructedRuntimeTypeHandle(LowLevelNativeFormatReader reader, ulong[] externalReferences)
        {
            if (externalReferences == null)
            {
                throw new ArgumentNullException(nameof(externalReferences));
            }

            TypeDesc objectTypeDesc = GetConstructedType(reader, externalReferences);
            return objectTypeDesc.GetRuntimeTypeHandle();
        }

        public static unsafe object BoxAnyType(IntPtr pData, RuntimeTypeHandle typeHandle)
        {
            return RuntimeAugments.RhBoxAny(pData,typeHandle.Value);
        }

        private static TypeDesc GetConstructedType(LowLevelNativeFormatReader reader, ulong[] externalReferences)
        {
            NativeLayoutInfoLoadContext nativeLayoutContext = new NativeLayoutInfoLoadContext();
            TypeSystemContext typeSystemContext = TypeSystemContextFactory.Create();
            nativeLayoutContext._module = null;
            nativeLayoutContext._typeSystemContext = typeSystemContext;
            nativeLayoutContext._typeArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._methodArgumentHandles = Instantiation.Empty;
            nativeLayoutContext._debuggerPreparedExternalReferences = externalReferences;

            TypeDesc objectTypeDesc = null;
            NativeParser parser = new NativeParser(reader.InternalReader, reader.Offset);
            try
            {
                objectTypeDesc = TypeLoaderEnvironment.Instance.GetConstructedTypeFromParserAndNativeLayoutContext(
                                    ref parser, 
                                    nativeLayoutContext);
            }
            finally
            {
                TypeSystemContextFactory.Recycle(typeSystemContext);
            }
            return objectTypeDesc;
        }
    }
}
