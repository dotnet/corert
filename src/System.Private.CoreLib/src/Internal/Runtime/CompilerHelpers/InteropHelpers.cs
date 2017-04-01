// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using Interlocked = System.Threading.Interlocked;
using Internal.Runtime.Augments;
using System.Runtime;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// These methods are used to throw exceptions from generated code.
    /// </summary>
    internal static class InteropHelpers
    {
        internal static unsafe byte* StringToAnsi(String str)
        {
            if (str == null)
                return null;

            // CORERT-TODO: Use same encoding as the rest of the interop
            var encoding = Encoding.UTF8;

            fixed (char* pStr = str)
            {
                int stringLength = str.Length;
                int bufferLength = encoding.GetByteCount(pStr, stringLength);
                byte *buffer = (byte*)PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(void*)(bufferLength+1)).ToPointer();
                encoding.GetBytes(pStr, stringLength, buffer, bufferLength);
                *(buffer + bufferLength) = 0;
                return buffer;
            }
        }
        internal static unsafe void  StringToAnsiFixedArray(String str, byte *buffer, int length)
        {
            if (buffer == null)
                return;

            Debug.Assert(str.Length >= length);

            var encoding = Encoding.UTF8;
            fixed (char* pStr = str)
            {
                int bufferLength = encoding.GetByteCount(pStr, length);
                encoding.GetBytes(pStr, length, buffer, bufferLength);
                *(buffer + bufferLength) = 0;
            }
        }

        public static unsafe string AnsiStringToString(byte* buffer)
        {
            if (buffer == null)
                return String.Empty;

            int length = strlen(buffer);

            return AnsiStringToStringFixedArray(buffer, length);
            
        }

        public static unsafe string AnsiStringToStringFixedArray(byte* buffer, int length)
        {
            if (buffer == null)
                return String.Empty;

            string result = String.Empty;

            if (length > 0)
            {
                result = new String(' ', length);

                fixed (char* pTemp = result)
                {
                    int charCount = Encoding.UTF8.GetCharCount(buffer, length);
                    // TODO: support ansi semantics in windows
                    Encoding.UTF8.GetChars(buffer, charCount, pTemp, length);
                }
            }
            return result;
        }

        internal static unsafe void StringToUnicodeFixedArray(String str, UInt16* buffer, int length)
        {
            if (buffer == null)
                return;

            Debug.Assert(str.Length >= length);

            fixed (char* pStr = str)
            {
                int size = length * sizeof(char);
                Buffer.MemoryCopy(pStr, buffer, size, size);
                *(buffer + length) = 0;
            }
        }

        internal static unsafe string UnicodeToStringFixedArray(UInt16* buffer, int length)
        {
            if (buffer == null)
                return String.Empty;

            string result = String.Empty;

            if (length > 0)
            {
                result = new String(' ', length);

                fixed (char* pTemp = result)
                {
                    int size = length * sizeof(char);
                    Buffer.MemoryCopy(buffer, pTemp, size, size);
                }
            }
            return result;
        }

        internal static char[] GetEmptyStringBuilderBuffer(StringBuilder sb)
        {
            // CORERT-TODO: Reuse buffer from string builder where possible?
            return new char[sb.Capacity + 1];
        }

        internal static unsafe IntPtr ResolvePInvoke(MethodFixupCell* pCell)
        {
            if (pCell->Target != IntPtr.Zero)
                return pCell->Target;

            return ResolvePInvokeSlow(pCell);
        }

        internal static unsafe IntPtr ResolvePInvokeSlow(MethodFixupCell* pCell)
        {
            ModuleFixupCell* pModuleCell = pCell->Module;
            IntPtr hModule = pModuleCell->Handle;
            if (hModule == IntPtr.Zero)
            {
                FixupModuleCell(pModuleCell);
                hModule = pModuleCell->Handle;
            }

            FixupMethodCell(hModule, pCell);
            return pCell->Target;
        }

        internal static unsafe IntPtr TryResolveModule(string moduleName)
        {
            IntPtr hModule = IntPtr.Zero;

            // Try original name first
            hModule = LoadLibrary(moduleName);
            if (hModule != IntPtr.Zero) return hModule;

#if PLATFORM_UNIX
            const string PAL_SHLIB_PREFIX = "lib";
#if PLATFORM_OSX
            const string PAL_SHLIB_SUFFIX = ".dylib";
#else
            const string PAL_SHLIB_SUFFIX = ".so";
#endif

             // Try prefix+name+suffix
            hModule = LoadLibrary(PAL_SHLIB_PREFIX + moduleName + PAL_SHLIB_SUFFIX);
            if (hModule != IntPtr.Zero) return hModule;

            // Try name+suffix
            hModule = LoadLibrary(moduleName + PAL_SHLIB_SUFFIX);
            if (hModule != IntPtr.Zero) return hModule;

            // Try prefix+name
            hModule = LoadLibrary(PAL_SHLIB_PREFIX + moduleName);
            if (hModule != IntPtr.Zero) return hModule;
#endif
            return IntPtr.Zero;
        }

        internal static unsafe IntPtr LoadLibrary(string moduleName)
        {
            IntPtr hModule;

#if !PLATFORM_UNIX
            hModule = Interop.mincore.LoadLibraryEx(moduleName, IntPtr.Zero, 0);
#else
            hModule = Interop.Sys.LoadLibrary(moduleName);
#endif

            return hModule;
        }

        internal static unsafe void FreeLibrary(IntPtr hModule)
        {
#if !PLATFORM_UNIX
            Interop.mincore.FreeLibrary(hModule);
#else
            Interop.Sys.FreeLibrary(hModule);
#endif
        }

        internal static unsafe void FixupModuleCell(ModuleFixupCell* pCell)
        {
            byte* pModuleName = (byte*)pCell->ModuleName;
            string moduleName = Encoding.UTF8.GetString(pModuleName, strlen(pModuleName));

            IntPtr hModule = TryResolveModule(moduleName);
            if (hModule != IntPtr.Zero)
            {
                var oldValue = Interlocked.CompareExchange(ref pCell->Handle, hModule, IntPtr.Zero);
                if (oldValue != IntPtr.Zero)
                {
                    // Some other thread won the race to fix it up.
                    FreeLibrary(hModule);
                }
            }
            else
            {
                // TODO: should be DllNotFoundException, but layering...
                throw new TypeLoadException(moduleName);
            }
        }

        internal static unsafe void FixupMethodCell(IntPtr hModule, MethodFixupCell* pCell)
        {
            byte* methodName = (byte*)pCell->MethodName;

#if !PLATFORM_UNIX
            pCell->Target = Interop.mincore.GetProcAddress(hModule, methodName);
#else
            pCell->Target = Interop.Sys.GetProcAddress(hModule, methodName);
#endif
            if (pCell->Target == IntPtr.Zero)
            {
                // TODO: Shoud be EntryPointNotFoundException, but layering...
                throw new TypeLoadException(Encoding.UTF8.GetString(methodName, strlen(methodName)));
            }
        }

        internal static unsafe int strlen(byte* pString)
        {
            byte* p = pString;
            while (*p != 0) p++;
            return checked((int)(p - pString));
        }

        internal unsafe static void* CoTaskMemAllocAndZeroMemory(global::System.IntPtr size)
        {
            void* ptr;
            ptr = PInvokeMarshal.CoTaskMemAlloc((UIntPtr)(void*)size).ToPointer();

            // PInvokeMarshal.CoTaskMemAlloc will throw OOMException if out of memory
            Debug.Assert(ptr != null);

            Buffer.ZeroMemory((byte*)ptr, size.ToInt64());
            return ptr;
        }

        internal unsafe static void CoTaskMemFree(void* p)
        {
            PInvokeMarshal.CoTaskMemFree((IntPtr)p);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal unsafe struct ThunkContextData
        {
            public GCHandle Handle;        //  A weak GCHandle to the delegate
            public IntPtr FunctionPtr;     // Function pointer for open static delegates
        };

        internal static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            IntPtr pContext = RuntimeImports.GetCurrentInteropThunkContext();
            Debug.Assert(pContext != null);

            IntPtr fnPtr;
            unsafe
            {
                // Pull out function pointer for open static delegate
                fnPtr = (*((ThunkContextData*)pContext)).FunctionPtr;
                
                // free the memory here
                CoTaskMemFree((void*)pContext);
            }

            Debug.Assert(fnPtr != null);
            return fnPtr;
        }

        /// <summary>
        /// Retrieves the current delegate that is being called
        /// </summary>
        public static T GetCurrentCalleeDelegate<T>() where T : class // constraint can't be System.Delegate
        {
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = RuntimeImports.GetCurrentInteropThunkContext();

            Debug.Assert(pContext != null);

            GCHandle handle;
            unsafe
            {
                // Pull out Handle from context
                handle = (*((ThunkContextData*)pContext)).Handle;

            }

            T target = InteropExtensions.UncheckedCast<T>(handle.Target);

            //
            // The delegate might already been garbage collected
            // User should use GC.KeepAlive or whatever ways necessary to keep the delegate alive
            // until they are done with the native function pointer
            //
            if (target == null)
            {
                Environment.FailFast(SR.Delegate_GarbageCollected);
            }
            return target;

        }
    
    internal static IntPtr GetStubForPInvokeDelegate(Delegate del)
        {
            if (del == null)
                return IntPtr.Zero;

            return AllocateThunk(del);
        }

        private static object s_thunkPoolHeap;


        private static IntPtr AllocateThunk(Delegate del)
        {
            //TODO: cache del->thunk here

            if (s_thunkPoolHeap == null)
            {
                s_thunkPoolHeap = RuntimeAugments.CreateThunksHeap(RuntimeImports.GetInteropCommonStubAddress());
                Debug.Assert(s_thunkPoolHeap != null);
            }

            IntPtr pThunk = RuntimeAugments.AllocateThunk(s_thunkPoolHeap);
            Debug.Assert(pThunk != IntPtr.Zero);

            if (pThunk == IntPtr.Zero)
            {
                // We've either run out of memory, or failed to allocate a new thunk due to some other bug. Now we should fail fast
                Environment.FailFast("Insufficient number of thunks.");
                return IntPtr.Zero;
            }
            else
            {

                McgPInvokeDelegateData pinvokeDelegateData;
                if (!RuntimeAugments.InteropCallbacks.TryGetMarshallerDataForDelegate(del.GetTypeHandle(), out pinvokeDelegateData))
                {
                    Environment.FailFast("Couldn't find marshalling stubs for delegate.");
                }
                IntPtr pContext;
                unsafe
                {
                    //
                    // Allocate unmanaged memory for GCHandle of delegate and function pointer of open static delegate
                    // We will store this pointer on the context slot of thunk data
                    //
                    pContext = (IntPtr)CoTaskMemAllocAndZeroMemory((System.IntPtr)(2 * (IntPtr.Size)));
                    ThunkContextData* thunkData = (ThunkContextData*)pContext;

                    (*thunkData).Handle = GCHandle.Alloc(del, GCHandleType.Weak);
                    (*thunkData).FunctionPtr = del.GetRawFunctionPointerForOpenStaticDelegate();
                }

                //
                //  For open static delegates set target to ReverseOpenStaticDelegateStub which calls the static function pointer directly
                //
                IntPtr pTarget = del.GetRawFunctionPointerForOpenStaticDelegate() == IntPtr.Zero ? pinvokeDelegateData.ReverseStub : pinvokeDelegateData.ReverseOpenStaticDelegateStub;

                RuntimeAugments.SetThunkData(s_thunkPoolHeap, pThunk, pContext, pTarget);

                return pThunk;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct ModuleFixupCell
        {
            public IntPtr Handle;
            public IntPtr ModuleName;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MethodFixupCell
        {
            public IntPtr Target;
            public IntPtr MethodName;
            public ModuleFixupCell* Module;
        }
    }
}
