// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using Internal.NativeFormat;
using Internal.Runtime.Augments;
using Internal.Runtime.CallInterceptor;
using Internal.Runtime.CompilerServices;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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

            set
            {
                this._offset = value;
            }
        }
    }

    public class TypeSystemHelper
    {
        public static unsafe IntPtr GetVirtualMethodFunctionPointer(IntPtr thisPointer, uint virtualMethodSlot)
        {
            // The first pointer in the object is a pointer to the EEType object
            EEType* eeType = *(EEType**)thisPointer;

            // The vtable of the object can be found at the end of EEType object
            IntPtr* vtable = eeType->GetVTableStartAddress();

            // Indexing the vtable to find out the actual function entry point
            IntPtr entryPoint = vtable[virtualMethodSlot];

            return entryPoint;
        }

        public static unsafe IntPtr GetInterfaceDispatchFunctionPointer(IntPtr thisPointer, RuntimeTypeHandle interfaceType, uint virtualMethodSlot)
        {
            object instance = Unsafe.As<IntPtr, object>(ref thisPointer);
            return RuntimeAugments.ResolveDispatch(instance, interfaceType, (int)virtualMethodSlot);
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
            objectTypeDesc = TypeLoaderEnvironment.Instance.GetConstructedTypeFromParserAndNativeLayoutContext(
                                ref parser,
                                nativeLayoutContext);
            TypeSystemContextFactory.Recycle(typeSystemContext);
            reader.Offset = parser.Offset;
            return objectTypeDesc;
        }
    }
}
