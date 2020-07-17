// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Debug = System.Diagnostics.Debug;

using TableElement = System.UInt32;

namespace Internal.Runtime.TypeLoader
{
    public struct ExternalReferencesTable
    {
        private IntPtr _elements;
        private uint _elementsCount;
        private TypeManagerHandle _moduleHandle;
        private ulong[] debuggerPreparedExternalReferences;

        public bool IsInitialized() { return (debuggerPreparedExternalReferences != null) || !_moduleHandle.IsNull; }

        private unsafe bool Initialize(NativeFormatModuleInfo module, ReflectionMapBlob blobId)
        {
            _moduleHandle = module.Handle;

            byte* pBlob;
            uint cbBlob;
            if (!module.TryFindBlob(blobId, out pBlob, out cbBlob))
            {
                _elements = IntPtr.Zero;
                _elementsCount = 0;
                return false;
            }

            _elements = (IntPtr)pBlob;
            _elementsCount = (uint)(cbBlob / sizeof(TableElement));

            return true;
        }

        public void InitializeDebuggerReference(ulong[] debuggerPreparedExternalReferences)
        {
            this.debuggerPreparedExternalReferences = debuggerPreparedExternalReferences;
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the NativeReferences metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the NativeReferences blob</param>
        /// <returns>true when the NativeReferences blob was found in the given module, false when not</returns>
        public bool InitializeNativeReferences(NativeFormatModuleInfo module)
        {
            return Initialize(module, ReflectionMapBlob.NativeReferences);
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the NativeStatics metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the NativeStatics blob</param>
        /// <returns>true when the NativeStatics blob was found in the given module, false when not</returns>
        public bool InitializeNativeStatics(NativeFormatModuleInfo module)
        {
            return Initialize(module, ReflectionMapBlob.NativeStatics);
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the CommonFixupsTable metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the CommonFixupsTable blob</param>
        /// <returns>true when the CommonFixupsTable blob was found in the given module, false when not</returns>
        public bool InitializeCommonFixupsTable(NativeFormatModuleInfo module)
        {
            return Initialize(module, ReflectionMapBlob.CommonFixupsTable);
        }

        unsafe public uint GetRvaFromIndex(uint index)
        {
            // The usage of this API will need to go away since this is not fully portable
            // and we'll not be able to support this for CppCodegen.
            throw new PlatformNotSupportedException();
        }

        unsafe public IntPtr GetIntPtrFromIndex(uint index)
        {
            return GetAddressFromIndex(index);
        }

        unsafe public IntPtr GetFunctionPointerFromIndex(uint index)
        {
            return GetAddressFromIndex(index);
        }

        public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(uint index)
        {
            if (this.debuggerPreparedExternalReferences == null)
            {
                return RuntimeAugments.CreateRuntimeTypeHandle(GetIntPtrFromIndex(index));
            }
            else
            {
                return RuntimeAugments.CreateRuntimeTypeHandle((IntPtr)this.debuggerPreparedExternalReferences[index]);
            }
        }

        public IntPtr GetGenericDictionaryFromIndex(uint index)
        {
            return GetIntPtrFromIndex(index);
        }

        unsafe public IntPtr GetAddressFromIndex(uint index)
        {
            if (index >= _elementsCount)
                throw new BadImageFormatException();

            // TODO: indirection through IAT
            if (EEType.SupportsRelativePointers)
            {
                int* pRelPtr32 = &((int*)_elements)[index];
                return (IntPtr)((byte*)pRelPtr32 + *pRelPtr32);
            }

            return (IntPtr)(((void**)_elements)[index]);
        }
    }
}
