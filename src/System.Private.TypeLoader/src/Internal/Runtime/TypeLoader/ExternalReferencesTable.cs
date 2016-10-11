// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using Internal.Runtime;
using Internal.Runtime.Augments;
using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    public struct ExternalReferencesTable
    {
        private IntPtr _RVAs;
        private uint _RVAsCount;
        private IntPtr _moduleHandle;

        public bool IsInitialized() { return (_moduleHandle != IntPtr.Zero); }

        private unsafe bool Initialize(IntPtr moduleHandle, ReflectionMapBlob blobId)
        {
            _moduleHandle = moduleHandle;

            byte* pBlob;
            uint cbBlob;
            if (!RuntimeAugments.FindBlob(moduleHandle, (int)blobId, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
            {
                _RVAs = IntPtr.Zero;
                _RVAsCount = 0;
                return false;
            }

            _RVAs = (IntPtr)pBlob;
            _RVAsCount = (uint)(cbBlob / sizeof(IntPtr));
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
            throw new PlatformNotSupportedException();
        }

        unsafe public IntPtr GetIntPtrFromIndex(uint index)
        {
            Debug.Assert(_moduleHandle != IntPtr.Zero);

            if (index >= _RVAsCount)
                throw new BadImageFormatException();

            return ((IntPtr*)_RVAs)[index];
        }

        public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(uint index)
        {
            return RuntimeAugments.CreateRuntimeTypeHandle(GetIntPtrFromIndex(index));
        }
    }
}