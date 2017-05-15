// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private bool isDebuggerPrepared;

        public bool IsInitialized() { return isDebuggerPrepared || !_moduleHandle.IsNull; }

        private unsafe bool Initialize(NativeFormatModuleInfo module, ReflectionMapBlob blobId)
        {
            if (module == null)
            {
                isDebuggerPrepared = true;
            }
            else
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
            }
            return true;
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
#if CORERT
            // The usage of this API will need to go away since this is not fully portable
            // and we'll not be able to support this for CppCodegen.
            throw new PlatformNotSupportedException();
#else
            Debug.Assert(!_moduleHandle.IsNull);

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
            int* pRelPtr32 = &((int*)_elements)[index];
            return (IntPtr)((byte*)pRelPtr32 + *pRelPtr32);
#else
            uint rva = GetRvaFromIndex(index);
            if ((rva & IndirectionConstants.RVAPointsToIndirection) != 0)
            {
                // indirect through IAT
                return *(IntPtr*)(_moduleHandle.ConvertRVAToPointer(rva & ~IndirectionConstants.RVAPointsToIndirection));
            }
            else
            {
                return (IntPtr)(_moduleHandle.ConvertRVAToPointer(rva));
            }
#endif
        }

        unsafe public IntPtr GetFunctionPointerFromIndex(uint index)
        {
#if CORERT
            if (index >= _elementsCount)
                throw new BadImageFormatException();

            // TODO: indirection through IAT
            int* pRelPtr32 = &((int*)_elements)[index];
            return (IntPtr)((byte*)pRelPtr32 + *pRelPtr32);
#else
            uint rva = GetRvaFromIndex(index);

            if ((rva & DynamicInvokeMapEntry.IsImportMethodFlag) == DynamicInvokeMapEntry.IsImportMethodFlag)
            {
                return *((IntPtr*)(_moduleHandle.ConvertRVAToPointer(rva & DynamicInvokeMapEntry.InstantiationDetailIndexMask)));
            }
            else
            {
                return (IntPtr)(_moduleHandle.ConvertRVAToPointer(rva));
            }
#endif
        }

        public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(uint index)
        {
            if (isDebuggerPrepared)
            {
                return typeof(int).TypeHandle;
            }

            return RuntimeAugments.CreateRuntimeTypeHandle(GetIntPtrFromIndex(index));
        }

        public IntPtr GetGenericDictionaryFromIndex(uint index)
        {
            return GetIntPtrFromIndex(index);
        }

#if CORERT
        unsafe public IntPtr GetFieldAddressFromIndex(uint index)
        {
            if (index >= _elementsCount)
                throw new BadImageFormatException();

            // TODO: indirection through IAT
            int* pRelPtr32 = &((int*)_elements)[index];
            return (IntPtr)((byte*)pRelPtr32 + *pRelPtr32);
        }
#endif

        public uint GetExternalNativeLayoutOffset(uint index)
        {
            // CoreRT is a bit more optimized than ProjectN. In ProjectN, some tables that reference data
            // in the native layout are constructed at NUTC compilation time, but the native layout is only 
            // generated at binder time, so we use the external references table to link the nutc-built
            // tables with their native layout dependencies.
            // 
            // In ProjectN, the nutc-built tables will be emitted with indices into the external references 
            // table, and the entries in the external references table will contain the offsets into the
            // native layout blob.
            //
            // In TypeManager based modules, since all tables and native layout blob are built together at the same time, we can
            // optimize by writing the native layout offsets directly into the table, without requiring the extra
            // lookup in the external references table.
            //
            if (_moduleHandle.IsTypeManager)
                return index;
            else
                return GetRvaFromIndex(index);
        }
    }
}
