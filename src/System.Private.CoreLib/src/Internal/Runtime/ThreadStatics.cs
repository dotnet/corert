// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using Internal.Runtime.CompilerHelpers;

namespace Internal.Runtime
{
    /// <summary>
    /// This class is used by ReadyToRun helpers to get access to thread static fields of a type
    /// and to allocate required TLS memory.
    /// </summary>
    internal static class ThreadStatics
    {
        /// <summary>
        /// This method is called from a ReadyToRun helper to get base address of thread
        /// static storage for the given type.
        /// </summary>
        internal static unsafe object GetThreadStaticBaseForType(TypeManagerSlot* pModuleData, Int32 typeTlsIndex)
        {
            // Get the array that holds thread static memory blocks for each type in the given module
            Int32 moduleIndex = pModuleData->ModuleIndex;
            object[] storage = (object[])RuntimeImports.RhGetThreadStaticStorageForModule(moduleIndex);

            // Check whether thread static storage has already been allocated for this module and type.
            if ((storage != null) && (typeTlsIndex < storage.Length) && (storage[typeTlsIndex] != null))
            {
                return storage[typeTlsIndex];
            }

            // This the first access to the thread statics of the type corresponding to typeTlsIndex.
            // Make sure there is enough storage allocated to hold it.
            storage = EnsureThreadStaticStorage(moduleIndex, storage, requiredSize: typeTlsIndex + 1);

            // Allocate an object that will represent a memory block for all thread static fields of the type
            object threadStaticBase = AllocateThreadStaticStorageForType(pModuleData->TypeManager, typeTlsIndex);
            storage[typeTlsIndex] = threadStaticBase;

            return threadStaticBase;

        }

        /// <summary>
        /// if it is required, this method extends thread static storage of the given module
        /// to the specified size and then registers the memory with the runtime.
        /// </summary>
        private static object[] EnsureThreadStaticStorage(Int32 moduleIndex, object[] existingStorage, Int32 requiredSize)
        {
            if ((existingStorage != null) && (requiredSize < existingStorage.Length))
            {
                return existingStorage;
            }

            object[] newStorage = new object[requiredSize];
            if (existingStorage != null)
            {
                Array.Copy(existingStorage, newStorage, existingStorage.Length);
            }

            // Install the newly created array as thread static storage for the given module
            // on the current thread. This call can fail due to a failure to allocate/extend required
            // internal thread specific resources.
            if (!RuntimeImports.RhSetThreadStaticStorageForModule(newStorage, moduleIndex))
            {
                throw new OutOfMemoryException();
            }

            return newStorage;
        }

        /// <summary>
        /// This method allocates an object that represents a memory block for all thread static fields of the type
        /// that corresponds to the specified TLS index.
        /// </summary>
        private static unsafe object AllocateThreadStaticStorageForType(TypeManagerHandle typeManager, Int32 typeTlsIndex)
        {
            Int32 length;
            IntPtr* threadStaticRegion;

            // Get a pointer to the beginning of the module's Thread Static section. Then get a pointer
            // to the EEType that represents a memory map for thread statics storage.
            threadStaticRegion = (IntPtr*)RuntimeImports.RhGetModuleSection(typeManager, ReadyToRunSectionType.ThreadStaticRegion, out length);
            return RuntimeImports.RhNewObject(new EETypePtr(threadStaticRegion[typeTlsIndex]));
        }
    }
}
