// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Internal.JitInterface
{
    internal class JitHost
    {
        private object _keepAlive; // Keeps callback delegates alive

        private Dictionary<IntPtr, GCHandle> _pinnedStrings = new Dictionary<IntPtr, GCHandle>();

        public IntPtr UnmanagedInstance
        {
            get; private set;
        }

        public unsafe JitHost()
        {
            const int numCallbacks = 5;
            
            IntPtr* callbacks = (IntPtr*)Marshal.AllocCoTaskMem(sizeof(IntPtr) * numCallbacks);
            Object[] delegates = new Object[numCallbacks];

            var d0 = new __allocateMemory(allocateMemory);
            callbacks[0] = Marshal.GetFunctionPointerForDelegate(d0);
            delegates[0] = d0;

            var d1 = new __freeMemory(freeMemory);
            callbacks[1] = Marshal.GetFunctionPointerForDelegate(d1);
            delegates[1] = d1;

            var d2 = new __getIntConfigValue(getIntConfigValue);
            callbacks[2] = Marshal.GetFunctionPointerForDelegate(d2);
            delegates[2] = d2;

            var d3 = new __getStringConfigValue(getStringConfigValue);
            callbacks[3] = Marshal.GetFunctionPointerForDelegate(d3);
            delegates[3] = d3;

            var d4 = new __freeStringConfigValue(freeStringConfigValue);
            callbacks[4] = Marshal.GetFunctionPointerForDelegate(d4);
            delegates[4] = d4;

            _keepAlive = delegates;

            IntPtr instance = Marshal.AllocCoTaskMem(sizeof(IntPtr));
            *(IntPtr**)instance = callbacks;

            UnmanagedInstance = instance;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate IntPtr __allocateMemory(IntPtr _this, IntPtr size, bool usePageAllocator);
        private IntPtr allocateMemory(IntPtr _this, IntPtr size, bool usePageAllocator)
        {
            return Marshal.AllocCoTaskMem((int)size);
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate void __freeMemory(IntPtr _this, IntPtr block, bool usePageAllocator);
        private void freeMemory(IntPtr _this, IntPtr block, bool usePageAllocator)
        {
            Marshal.FreeCoTaskMem(block);
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate int __getIntConfigValue(IntPtr _this, IntPtr name, int defaultValue);
        private int getIntConfigValue(IntPtr _this, IntPtr name, int defaultValue)
        {
            return defaultValue;
        }

        private IntPtr GetPinnedStringHandle(string s)
        {
            GCHandle handle = GCHandle.Alloc(s, GCHandleType.Pinned);
            IntPtr resultHandle = handle.AddrOfPinnedObject();
            _pinnedStrings[resultHandle] = handle;
            return resultHandle;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate IntPtr __getStringConfigValue(IntPtr _this, IntPtr name);
        private IntPtr getStringConfigValue(IntPtr _this, IntPtr name)
        {
            // Use GetPinnedStringHandle to pin the result string.
            return IntPtr.Zero;
        }

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        delegate void __freeStringConfigValue(IntPtr _this, IntPtr nameHandle);
        private void freeStringConfigValue(IntPtr _this, IntPtr valueHandle)
        {
            if (valueHandle == IntPtr.Zero)
                return;

            GCHandle gcHandle = _pinnedStrings[valueHandle];
            gcHandle.Free();
            _pinnedStrings.Remove(valueHandle);
        }
    }
}
