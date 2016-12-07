// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Debug = System.Diagnostics.Debug;

#if CORERT
using TableElement = System.IntPtr;
#else
using TableElement = System.UInt32;
#endif

namespace Internal.Runtime.TypeLoader
{
    public struct ExternalReferencesTable
    {
        private IntPtr _elements;
        private uint _elementsCount;
        private IntPtr _moduleHandle;

        public bool IsInitialized() { return (_moduleHandle != IntPtr.Zero); }

        private unsafe bool Initialize(IntPtr moduleHandle, ReflectionMapBlob blobId)
        {
            _moduleHandle = moduleHandle;

            byte* pBlob;
            uint cbBlob;
            if (!RuntimeAugments.FindBlob(moduleHandle, (int)blobId, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
            {
                _elements = IntPtr.Zero;
                _elementsCount = 0;
                return false;
            }

            _elements = (IntPtr)pBlob;
            _elementsCount = (uint)(cbBlob / sizeof(TableElement));
            return true;
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the NativeReferences metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the NativeReferences blob</param>
        /// <returns>true when the NativeReferences blob was found in the given module, false when not</returns>
        public bool InitializeNativeReferences(IntPtr moduleHandle)
        {
            return Initialize(moduleHandle, ReflectionMapBlob.NativeReferences);
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the NativeStatics metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the NativeStatics blob</param>
        /// <returns>true when the NativeStatics blob was found in the given module, false when not</returns>
        public bool InitializeNativeStatics(IntPtr moduleHandle)
        {
            return Initialize(moduleHandle, ReflectionMapBlob.NativeStatics);
        }

        /// <summary>
        /// Initialize ExternalReferencesTable using the CommonFixupsTable metadata blob on a given module.
        /// </summary>
        /// <param name="moduleHandle">Module handle is used to locate the CommonFixupsTable blob</param>
        /// <returns>true when the CommonFixupsTable blob was found in the given module, false when not</returns>
        public bool InitializeCommonFixupsTable(IntPtr moduleHandle)
        {
            return Initialize(moduleHandle, ReflectionMapBlob.CommonFixupsTable);
        }

        unsafe public uint GetRvaFromIndex(uint index)
        {
#if CORERT
            // The usage of this API will need to go away since this is not fully portable
            // and we'll not be able to support this for CppCodegen.
            throw new PlatformNotSupportedException();
#else
            Debug.Assert(_moduleHandle != IntPtr.Zero);

            if (index >= _elementsCount)
                throw new BadImageFormatException();

            return ((TableElement*)_elements)[index];
#endif
        }

        unsafe public IntPtr GetIntPtrFromIndex(uint index)
        {
#if CORERT
            if (index >= _elementsCount)
                throw new BadImageFormatException();

            // TODO: indirection through IAT

            return ((TableElement*)_elements)[index];
#else
            uint rva = GetRvaFromIndex(index);
            if ((rva & 0x80000000) != 0)
            {
                // indirect through IAT
                return *(IntPtr*)((byte*)_moduleHandle + (rva & ~0x80000000));
            }
            else
            {
                return (IntPtr)((byte*)_moduleHandle + rva);
            }
#endif
        }

        unsafe public IntPtr GetFunctionPointerFromIndex(uint index)
        {
#if CORERT
            if (index >= _elementsCount)
                throw new BadImageFormatException();

            // TODO: indirection through IAT

            return ((IntPtr*)_elements)[index];
#else
            uint rva = GetRvaFromIndex(index);

            if ((rva & DynamicInvokeMapEntry.IsImportMethodFlag) == DynamicInvokeMapEntry.IsImportMethodFlag)
            {
                return *((IntPtr*)((byte*)_moduleHandle + (rva & DynamicInvokeMapEntry.InstantiationDetailIndexMask)));
            }
            else
            {
                return (IntPtr)((byte*)_moduleHandle + rva);
            }
#endif
        }

        public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(uint index)
        {
            return RuntimeAugments.CreateRuntimeTypeHandle(GetIntPtrFromIndex(index));
        }
    }
}