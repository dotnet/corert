// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Internal.Runtime
{
    /// <summary>
    /// TypeManagerHandle represents an AOT module in MRT based runtimes.
    /// These handles are either a pointer to an OS module, or a pointer to a TypeManager
    /// When this is a pointer to a TypeManager, then the pointer will have its lowest bit
    /// set to indicate that it is a TypeManager pointer instead of OS module.
    /// </summary>
    public partial struct TypeManagerHandle
    {
        private IntPtr _handleValue;

        // This is a partial definition of the TypeManager struct which is defined in TypeManager.h
        [StructLayout(LayoutKind.Sequential)]
        private struct TypeManager
        {
            public IntPtr OsHandle;
            public IntPtr ReadyToRunHeader;
            public IntPtr DispatchMap;
        }

        public TypeManagerHandle(IntPtr handleValue)
        {
            _handleValue = handleValue;
        }

        public IntPtr GetIntPtrUNSAFE()
        {
            return _handleValue;
        }

        public bool IsNull
        {
            get
            {
                return _handleValue == IntPtr.Zero;
            }
        }

        private unsafe TypeManager* AsTypeManagerPtr
        {
            get
            {
                unsafe
                {
                    return (TypeManager*)(((byte*)(void*)_handleValue) - 1);
                }
            }
        }

        public unsafe IntPtr OsModuleBase
        {
            get
            {
                return AsTypeManagerPtr->OsHandle;
            }
        }

        public unsafe IntPtr DispatchMap
        {
            get
            {
                return AsTypeManagerPtr->DispatchMap;
            }
        }
    }
}
