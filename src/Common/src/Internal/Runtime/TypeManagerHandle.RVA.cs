// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Internal.Runtime;

namespace System.Runtime
{
    public partial struct TypeManagerHandle : IEquatable<TypeManagerHandle>
    {
        // This is a partial definition of the TypeManager struct which is defined in TypeManager.h
        [StructLayout(LayoutKind.Sequential)]
        private struct TypeManager
        {
            public IntPtr OsHandle;
        }

        public bool IsTypeManager
        {
            get
            {
                unsafe
                {
                    return (((int)(byte*)_handleValue) & 0x1) == 0x1;
                }
            }
        }

        private IntPtr AsOsModuleIntPtr
        {
            get
            {
                Debug.Assert(!IsTypeManager);
                return _handleValue;
            }
        }

        private unsafe TypeManager* AsTypeManagerPtr
        {
            get
            {
                Debug.Assert(IsTypeManager);
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
                if (IsTypeManager)
                    return AsTypeManagerPtr->OsHandle;
                else
                    return AsOsModuleIntPtr;
            }
        }

        [CLSCompliant(false)]
        public unsafe byte* ConvertRVAToPointer(int rva)
        {
            return ConvertRVAToPointer((uint)rva);
        }

        [CLSCompliant(false)]
        public unsafe byte* ConvertRVAToPointer(uint rva)
        {
#if PROJECTN
            return ((byte*)OsModuleBase) + rva;
#else
            Environment.FailFast("RVA fixups not supported");
            return null;
#endif
        }

        public string LowLevelToString()
        {
            return _handleValue.LowLevelToString();
        }
    }
}
