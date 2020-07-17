// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security;
using Debug = System.Diagnostics.Debug;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerHelpers;
using Internal.Runtime.CompilerServices;

#if TARGET_64BIT
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This PInvokeMarshal class should provide full public Marshal 
    /// implementation for all things related to P/Invoke marshalling
    /// </summary>
    [CLSCompliant(false)]
    public partial class PInvokeMarshal
    {
        [ThreadStatic]
        internal static int s_lastWin32Error;

        public static int GetLastWin32Error()
        {
            return s_lastWin32Error;
        }

        public static void SetLastWin32Error(int errorCode)
        {
            s_lastWin32Error = errorCode;
        }

        public static int GetHRForLastWin32Error()
        {
            int dwLastError = GetLastWin32Error();
            if ((dwLastError & 0x80000000) == 0x80000000)
                return dwLastError;
            else
                return (dwLastError & 0x0000FFFF) | unchecked((int)0x80070000);
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        public static int GetHRForException(Exception e)
        {
            if (e == null)
            {
                return HResults.S_OK;
            }

            // @TODO: Setup IErrorInfo
            return e.HResult;
        }

        public static unsafe IntPtr AllocHGlobal(IntPtr cb)
        {
            return MemAlloc(cb);
        }

        public static unsafe IntPtr AllocHGlobal(int cb)
        {
            return AllocHGlobal((IntPtr)cb);
        }

        public static void FreeHGlobal(IntPtr hglobal)
        {
            MemFree(hglobal);
        }

        public static unsafe IntPtr AllocCoTaskMem(int cb)
        {
            IntPtr allocatedMemory = CoTaskMemAlloc(new UIntPtr(unchecked((uint)cb)));
            if (allocatedMemory == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
            return allocatedMemory;
        }

        public static void FreeCoTaskMem(IntPtr ptr)
        {
            CoTaskMemFree(ptr);
        }

        public static IntPtr SecureStringToGlobalAllocAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return s.MarshalToString(globalAlloc: true, unicode: false);
        }

        public static IntPtr SecureStringToGlobalAllocUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return s.MarshalToString(globalAlloc: true, unicode: true); ;
        }

        public static IntPtr SecureStringToCoTaskMemAnsi(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return s.MarshalToString(globalAlloc: false, unicode: false);
        }

        public static IntPtr SecureStringToCoTaskMemUnicode(SecureString s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return s.MarshalToString(globalAlloc: false, unicode: true);
        }

        public static unsafe void CopyToManaged(IntPtr source, Array destination, int startIndex, int length)
        {
            if (source == IntPtr.Zero)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (!destination.IsBlittable())
                throw new ArgumentException(nameof(destination), SR.Arg_CopyNonBlittableArray);
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Arg_CopyOutOfRange);
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.Arg_CopyOutOfRange);
            if ((uint)startIndex + (uint)length > (uint)destination.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Arg_CopyOutOfRange);

            nuint bytesToCopy = (nuint)length * destination.ElementSize;
            nuint startOffset = (nuint)startIndex * destination.ElementSize;

            fixed (byte* pDestination = &destination.GetRawArrayData())
            {
                byte* destinationData = pDestination + startOffset;
                Buffer.Memmove(destinationData, (byte*)source, bytesToCopy);
            }
        }

        public static unsafe void CopyToNative(Array source, int startIndex, IntPtr destination, int length)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!source.IsBlittable())
                throw new ArgumentException(nameof(source), SR.Arg_CopyNonBlittableArray);
            if (destination == IntPtr.Zero)
                throw new ArgumentNullException(nameof(destination));
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Arg_CopyOutOfRange);
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), SR.Arg_CopyOutOfRange);
            if ((uint)startIndex + (uint)length > (uint)source.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex), SR.Arg_CopyOutOfRange);

            nuint bytesToCopy = (nuint)length * source.ElementSize;
            nuint startOffset = (nuint)startIndex * source.ElementSize;

            fixed (byte* pSource = &source.GetRawArrayData())
            {
                byte* sourceData = pSource + startOffset;
                Buffer.Memmove((byte*)destination, sourceData, bytesToCopy);
            }
        }

        public static unsafe IntPtr UnsafeAddrOfPinnedArrayElement(Array arr, int index)
        {
            if (arr == null)
                throw new ArgumentNullException(nameof(arr));

            byte* p = (byte*)Unsafe.AsPointer(ref arr.GetRawArrayData()) + (nuint)index * arr.ElementSize;
            return (IntPtr)p;
        }

        #region Delegate marshalling

        private static object s_thunkPoolHeap;

        /// <summary>
        /// Return the stub to the pinvoke marshalling stub
        /// </summary>
        /// <param name="del">The delegate</param>
        public static IntPtr GetFunctionPointerForDelegate(Delegate del)
        {
            if (del == null)
                return IntPtr.Zero;

            NativeFunctionPointerWrapper fpWrapper = del.Target as NativeFunctionPointerWrapper;
            if (fpWrapper != null)
            {
                //
                // Marshalling a delegate created from native function pointer back into function pointer
                // This is easy - just return the 'wrapped' native function pointer
                //
                return fpWrapper.NativeFunctionPointer;
            }
            else
            {
                //
                // Marshalling a managed delegate created from managed code into a native function pointer
                //
                return GetPInvokeDelegates().GetValue(del, s_AllocateThunk ?? (s_AllocateThunk = AllocateThunk)).Thunk;
            }
        }

        /// <summary>
        /// Used to lookup whether a delegate already has thunk allocated for it
        /// </summary>
        private static ConditionalWeakTable<Delegate, PInvokeDelegateThunk> s_pInvokeDelegates;
        private static ConditionalWeakTable<Delegate, PInvokeDelegateThunk>.CreateValueCallback s_AllocateThunk;

        private static ConditionalWeakTable<Delegate, PInvokeDelegateThunk> GetPInvokeDelegates()
        {
            //
            // Create the dictionary on-demand to avoid the dependency in the McgModule.ctor
            // Otherwise NUTC will complain that McgModule being eager ctor depends on a deferred
            // ctor type
            //
            if (s_pInvokeDelegates == null)
            {
                Interlocked.CompareExchange(
                    ref s_pInvokeDelegates,
                    new ConditionalWeakTable<Delegate, PInvokeDelegateThunk>(),
                    null
                );
            }

            return s_pInvokeDelegates;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal unsafe struct ThunkContextData
        {
            public GCHandle Handle;        //  A weak GCHandle to the delegate
            public IntPtr FunctionPtr;     // Function pointer for open static delegates
        }

        internal sealed class PInvokeDelegateThunk
        {
            public IntPtr Thunk;        //  Thunk pointer
            public IntPtr ContextData;  //  ThunkContextData pointer which will be stored in the context slot of the thunk

            public PInvokeDelegateThunk(Delegate del)
            {

                Thunk = RuntimeAugments.AllocateThunk(s_thunkPoolHeap);
                Debug.Assert(Thunk != IntPtr.Zero);

                if (Thunk == IntPtr.Zero)
                {
                    // We've either run out of memory, or failed to allocate a new thunk due to some other bug. Now we should fail fast
                    Environment.FailFast("Insufficient number of thunks.");
                }
                else
                {
                    //
                    // Allocate unmanaged memory for GCHandle of delegate and function pointer of open static delegate
                    // We will store this pointer on the context slot of thunk data
                    //
                    ContextData = AllocHGlobal(2 * IntPtr.Size);
                    unsafe
                    {
                        ThunkContextData* thunkData = (ThunkContextData*)ContextData;

                        // allocate a weak GChandle for the delegate
                        thunkData->Handle = GCHandle.Alloc(del, GCHandleType.Weak);

                        // if it is an open static delegate get the function pointer
                        thunkData->FunctionPtr = del.GetRawFunctionPointerForOpenStaticDelegate();
                    }
                }
            }

            ~PInvokeDelegateThunk()
            {
                // Free the thunk
                RuntimeAugments.FreeThunk(s_thunkPoolHeap, Thunk);
                unsafe
                {
                    if (ContextData != IntPtr.Zero)
                    {
                        // free the GCHandle
                        GCHandle handle = ((ThunkContextData*)ContextData)->Handle;
                        if (handle != null)
                        {
                            handle.Free();
                        }

                        // Free the allocated context data memory
                        FreeHGlobal(ContextData);
                    }
                }
            }
        }

        private static PInvokeDelegateThunk AllocateThunk(Delegate del)
        {
            if (s_thunkPoolHeap == null)
            {
                // TODO: Free s_thunkPoolHeap if the thread lose the race
                Interlocked.CompareExchange(
                    ref s_thunkPoolHeap,
                    RuntimeAugments.CreateThunksHeap(RuntimeImports.GetInteropCommonStubAddress()),
                    null
                );
                Debug.Assert(s_thunkPoolHeap != null);
            }

            var delegateThunk = new PInvokeDelegateThunk(del);

            //
            //  For open static delegates set target to ReverseOpenStaticDelegateStub which calls the static function pointer directly
            //
            bool openStaticDelegate = del.GetRawFunctionPointerForOpenStaticDelegate() != IntPtr.Zero;

            IntPtr pTarget = RuntimeAugments.InteropCallbacks.GetDelegateMarshallingStub(del.GetTypeHandle(), openStaticDelegate);
            Debug.Assert(pTarget != IntPtr.Zero);

            RuntimeAugments.SetThunkData(s_thunkPoolHeap, delegateThunk.Thunk, delegateThunk.ContextData, pTarget);

            return delegateThunk;
        }

        /// <summary>
        /// Retrieve the corresponding P/invoke instance from the stub
        /// </summary>
        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, RuntimeTypeHandle delegateType)
        {
            if (ptr == IntPtr.Zero)
                return null;
            //
            // First try to see if this is one of the thunks we've allocated when we marshal a managed
            // delegate to native code
            // s_thunkPoolHeap will be null if there isn't any managed delegate to native
            //
            IntPtr pContext;
            IntPtr pTarget;
            if (s_thunkPoolHeap != null && RuntimeAugments.TryGetThunkData(s_thunkPoolHeap, ptr, out pContext, out pTarget))
            {
                GCHandle handle;
                unsafe
                {
                    // Pull out Handle from context
                    handle = ((ThunkContextData*)pContext)->Handle;
                }
                Delegate target = Unsafe.As<Delegate>(handle.Target);

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

            //
            // Otherwise, the stub must be a pure native function pointer
            // We need to create the delegate that points to the invoke method of a
            // NativeFunctionPointerWrapper derived class
            //
            IntPtr pDelegateCreationStub = RuntimeAugments.InteropCallbacks.GetForwardDelegateCreationStub(delegateType);
            Debug.Assert(pDelegateCreationStub != IntPtr.Zero);

            return CalliIntrinsics.Call<Delegate>(pDelegateCreationStub, ptr);
        }

        /// <summary>
        /// Retrieves the function pointer for the current open static delegate that is being called
        /// </summary>
        public static IntPtr GetCurrentCalleeOpenStaticDelegateFunctionPointer()
        {
            //
            // RH keeps track of the current thunk that is being called through a secret argument / thread
            // statics. No matter how that's implemented, we get the current thunk which we can use for
            // look up later
            //
            IntPtr pContext = RuntimeImports.GetCurrentInteropThunkContext();
            Debug.Assert(pContext != null);

            IntPtr fnPtr;
            unsafe
            {
                // Pull out function pointer for open static delegate
                fnPtr = ((ThunkContextData*)pContext)->FunctionPtr;
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
                handle = ((ThunkContextData*)pContext)->Handle;

            }

            T target = Unsafe.As<T>(handle.Target);

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

        [McgIntrinsics]
        private static unsafe class CalliIntrinsics
        {
            internal static T Call<T>(IntPtr pfn, IntPtr arg0) { throw new NotSupportedException(); }
        }
        #endregion

        #region String marshalling
        public static unsafe string PtrToStringUni(IntPtr ptr, int len)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));
            if (len < 0)
                throw new ArgumentException(nameof(len));

            return new string((char*)ptr, 0, len);
        }

        public static unsafe string PtrToStringUni(IntPtr ptr)
        {
            if (IntPtr.Zero == ptr)
            {
                return null;
            }
            else if (IsWin32Atom(ptr))
            {
                return null;
            }
            else
            {
                return new string((char*)ptr);
            }
        }

        public static unsafe void StringBuilderToUnicodeString(System.Text.StringBuilder stringBuilder, ushort* destination)
        {
            stringBuilder.UnsafeCopyTo((char*)destination);
        }

        public static unsafe void UnicodeStringToStringBuilder(ushort* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            stringBuilder.ReplaceBuffer((char*)newBuffer);
        }

        public static unsafe void StringBuilderToAnsiString(System.Text.StringBuilder stringBuilder, byte* pNative,
            bool bestFit, bool throwOnUnmappableChar)
        {
            int len;

            // Convert StringBuilder to UNICODE string

            // Optimize for the most common case. If there is only a single char[] in the StringBuilder,
            // get it and convert it to ANSI
            char[] buffer = stringBuilder.GetBuffer(out len);

            if (buffer != null)
            {
                fixed (char* pManaged = buffer)
                {
                    StringToAnsiString(pManaged, len, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
            else // Otherwise, convert StringBuilder to string and then convert to ANSI
            {
                string str = stringBuilder.ToString();

                // Convert UNICODE string to ANSI string
                fixed (char* pManaged = str)
                {
                    StringToAnsiString(pManaged, str.Length, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
        }

        public static unsafe void AnsiStringToStringBuilder(byte* newBuffer, System.Text.StringBuilder stringBuilder)
        {
            if (newBuffer == null)
                throw new ArgumentNullException(nameof(newBuffer));

            int lenAnsi;
            int lenUnicode;
            CalculateStringLength(newBuffer, out lenAnsi, out lenUnicode);

            if (lenUnicode > 0)
            {
                char[] buffer = new char[lenUnicode];
                fixed (char* pTemp = &buffer[0])
                {
                    ConvertMultiByteToWideChar(newBuffer,
                                               lenAnsi,
                                               pTemp,
                                               lenUnicode);
                }
                stringBuilder.ReplaceBuffer(buffer);
            }
            else
            {
                stringBuilder.Clear();
            }
        }

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        public static unsafe string AnsiStringToString(byte* pchBuffer)
        {
            if (pchBuffer == null)
            {
                return null;
            }

            int lenAnsi;
            int lenUnicode;
            CalculateStringLength(pchBuffer, out lenAnsi, out lenUnicode);

            string result = string.Empty;

            if (lenUnicode > 0)
            {
                result = string.FastAllocateString(lenUnicode);

                fixed (char* pTemp = result)
                {
                    ConvertMultiByteToWideChar(pchBuffer,
                                               lenAnsi,
                                               pTemp,
                                               lenUnicode);
                }
            }

            return result;
        }

        /// <summary>
        /// Convert UNICODE string to ANSI string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        public static unsafe byte* StringToAnsiString(string str, bool bestFit, bool throwOnUnmappableChar)
        {
            if (str != null)
            {
                int lenUnicode = str.Length;

                fixed (char* pManaged = str)
                {
                    return StringToAnsiString(pManaged, lenUnicode, null, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }

            return null;
        }

        /// <summary>
        /// Convert UNICODE wide char array to ANSI ByVal byte array.
        /// </summary>
        /// <remarks>
        /// * This version works with array instead string, it means that there will be NO NULL to terminate the array.
        /// * The buffer to store the byte array must be allocated by the caller and must fit managedArray.Length.
        /// </remarks>
        /// <param name="managedArray">UNICODE wide char array</param>
        /// <param name="pNative">Allocated buffer where the ansi characters must be placed. Could NOT be null. Buffer size must fit char[].Length.</param>
        public static unsafe void ByValWideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, int expectedCharCount,
            bool bestFit, bool throwOnUnmappableChar)
        {
            // Zero-init pNative if it is NULL
            if (managedArray == null)
            {
                Buffer.ZeroMemory((byte*)pNative, (nuint)expectedCharCount);
                return;
            }

            int lenUnicode = managedArray.Length;
            if (lenUnicode < expectedCharCount)
                throw new ArgumentException(SR.WrongSizeArrayInNStruct);

            fixed (char* pManaged = managedArray)
            {
                StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            }
        }

        public static unsafe void ByValAnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // This should never happen because it is a embedded array
            Debug.Assert(pNative != null);

            // This should never happen because the array is always allocated by the marshaller
            Debug.Assert(managedArray != null);

            // COMPAT: Use the managed array length as the maximum length of native buffer
            // This obviously doesn't make sense but desktop CLR does that
            int lenInBytes = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                ConvertMultiByteToWideChar(pNative,
                                           lenInBytes,
                                           pManaged,
                                           lenInBytes);
            }
        }

        public static unsafe void WideCharArrayToAnsiCharArray(char[] managedArray, byte* pNative, bool bestFit, bool throwOnUnmappableChar)
        {
            // Do nothing if array is NULL. This matches desktop CLR behavior
            if (managedArray == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (pNative == null)
                throw new ArgumentNullException(nameof(pNative));

            int lenUnicode = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            }
        }

        /// <summary>
        /// Convert ANSI ByVal byte array to UNICODE wide char array, best fit
        /// </summary>
        /// <remarks>
        /// * This version works with array instead to string, it means that the len must be provided and there will be NO NULL to
        /// terminate the array.
        /// * The buffer to the UNICODE wide char array must be allocated by the caller.
        /// </remarks>
        /// <param name="pNative">Pointer to the ANSI byte array. Could NOT be null.</param>
        /// <param name="lenInBytes">Maximum buffer size.</param>
        /// <param name="managedArray">Wide char array that has already been allocated.</param>
        public static unsafe void AnsiCharArrayToWideCharArray(byte* pNative, char[] managedArray)
        {
            // Do nothing if native is NULL. This matches desktop CLR behavior
            if (pNative == null)
                return;

            // Desktop CLR crash (AV at runtime) - we can do better in .NET Native
            if (managedArray == null)
                throw new ArgumentNullException(nameof(managedArray));

            // COMPAT: Use the managed array length as the maximum length of native buffer
            // This obviously doesn't make sense but desktop CLR does that
            int lenInBytes = managedArray.Length;
            fixed (char* pManaged = managedArray)
            {
                ConvertMultiByteToWideChar(pNative,
                                           lenInBytes,
                                           pManaged,
                                           lenInBytes);
            }
        }

        /// <summary>
        /// Convert a single UNICODE wide char to a single ANSI byte.
        /// </summary>
        /// <param name="managedArray">single UNICODE wide char value</param>
        public static unsafe byte WideCharToAnsiChar(char managedValue, bool bestFit, bool throwOnUnmappableChar)
        {
            // @TODO - we really shouldn't allocate one-byte arrays and then destroy it
            byte* nativeArray = StringToAnsiString(&managedValue, 1, null, /*terminateWithNull=*/false, bestFit, throwOnUnmappableChar);
            byte native = (*nativeArray);
            CoTaskMemFree(new IntPtr(nativeArray));
            return native;
        }

        /// <summary>
        /// Convert a single ANSI byte value to a single UNICODE wide char value, best fit.
        /// </summary>
        /// <param name="nativeValue">Single ANSI byte value.</param>
        public static unsafe char AnsiCharToWideChar(byte nativeValue)
        {
            char ch;
            ConvertMultiByteToWideChar(&nativeValue, 1, &ch, 1);
            return ch;
        }

        /// <summary>
        /// Convert UNICODE string to ANSI ByVal string.
        /// </summary>
        /// <remarks>This version is more efficient than StringToHGlobalAnsi in Interop\System\Runtime\InteropServices\Marshal.cs in that
        /// it could allocate single byte per character, instead of SystemMaxDBCSCharSize per char, and it can skip calling WideCharToMultiByte for ASCII string</remarks>
        /// <param name="str">Unicode string.</param>
        /// <param name="pNative"> Allocated buffer where the ansi string must be placed. Could NOT be null. Buffer size must fit str.Length.</param>
        public static unsafe void StringToByValAnsiString(string str, byte* pNative, int charCount, bool bestFit, bool throwOnUnmappableChar, bool truncate = true)
        {
            if (pNative == null)
                throw new ArgumentNullException(nameof(pNative));

            if (str != null)
            {
                // Truncate the string if it is larger than specified by SizeConst
                int lenUnicode;

                if (truncate)
                {
                    lenUnicode = str.Length;
                    if (lenUnicode >= charCount)
                        lenUnicode = charCount - 1;
                }
                else
                {
                    lenUnicode = charCount;
                }

                fixed (char* pManaged = str)
                {
                    StringToAnsiString(pManaged, lenUnicode, pNative, /*terminateWithNull=*/true, bestFit, throwOnUnmappableChar);
                }
            }
            else
            {
                (*pNative) = (byte)'\0';
            }
        }

        /// <summary>
        /// Convert ANSI string to unicode string, with option to free native memory. Calls generated by MCG
        /// </summary>
        /// <remarks>Input assumed to be zero terminated. Generates String.Empty for zero length string.
        /// This version is more efficient than ConvertToUnicode in src\Interop\System\Runtime\InteropServices\Marshal.cs in that it can skip calling
        /// MultiByteToWideChar for ASCII string, and it does not need another char[] buffer</remarks>
        public static unsafe string ByValAnsiStringToString(byte* pchBuffer, int charCount)
        {
            // Match desktop CLR behavior
            if (charCount == 0)
                throw new MarshalDirectiveException();

            int lenAnsi = GetAnsiStringLen(pchBuffer);
            int lenUnicode = charCount;

            string result = string.Empty;

            if (lenUnicode > 0)
            {
                char* unicodeBuf = stackalloc char[lenUnicode];
                int unicodeCharWritten = ConvertMultiByteToWideChar(pchBuffer,
                                                                    lenAnsi,
                                                                    unicodeBuf,
                                                                    lenUnicode);

                // If conversion failure, return empty string to match desktop CLR behavior
                if (unicodeCharWritten > 0)
                    result = new string(unicodeBuf, 0, unicodeCharWritten);
            }

            return result;
        }

        private static unsafe int GetAnsiStringLen(byte* pchBuffer)
        {
            byte* pchBufferOriginal = pchBuffer;
            while (*pchBuffer != 0)
            {
                pchBuffer++;
            }

            return (int)(pchBuffer - pchBufferOriginal);
        }

        // c# string (UTF-16) to UTF-8 encoded byte array
        private static unsafe byte* StringToAnsiString(char* pManaged, int lenUnicode, byte* pNative, bool terminateWithNull,
            bool bestFit, bool throwOnUnmappableChar)
        {
            bool allAscii = true;

            for (int i = 0; i < lenUnicode; i++)
            {
                if (pManaged[i] >= 128)
                {
                    allAscii = false;
                    break;
                }
            }

            int length;

            if (allAscii) // If all ASCII, map one UNICODE character to one ANSI char
            {
                length = lenUnicode;
            }
            else // otherwise, let OS count number of ANSI chars
            {
                length = GetByteCount(pManaged, lenUnicode);
            }

            if (pNative == null)
            {
                pNative = (byte*)CoTaskMemAlloc((System.UIntPtr)(length + 1));
            }
            if (allAscii) // ASCII conversion
            {
                byte* pDst = pNative;
                char* pSrc = pManaged;

                while (lenUnicode > 0)
                {
                    unchecked
                    {
                        *pDst++ = (byte)(*pSrc++);
                        lenUnicode--;
                    }
                }
            }
            else // Let OS convert
            {
                ConvertWideCharToMultiByte(pManaged,
                                           lenUnicode,
                                           pNative,
                                           length,
                                           bestFit,
                                           throwOnUnmappableChar);
            }

            // Zero terminate
            if (terminateWithNull)
                *(pNative + length) = 0;

            return pNative;
        }

        /// <summary>
        /// This is a auxiliary function that counts the length of the ansi buffer and
        ///  estimate the length of the buffer in Unicode. It returns true if all bytes
        ///  in the buffer are ANSII.
        /// </summary>
        private static unsafe bool CalculateStringLength(byte* pchBuffer, out int ansiBufferLen, out int unicodeBufferLen)
        {
            ansiBufferLen = 0;

            bool allAscii = true;

            {
                byte* p = pchBuffer;
                byte b = *p++;

                while (b != 0)
                {
                    if (b >= 128)
                    {
                        allAscii = false;
                    }

                    ansiBufferLen++;

                    b = *p++;
                }
            }

            if (allAscii)
            {
                unicodeBufferLen = ansiBufferLen;
            }
            else // If non ASCII, let OS calculate number of characters
            {
                unicodeBufferLen = GetCharCount(pchBuffer, ansiBufferLen);
            }
            return allAscii;
        }

        #endregion
    }
}
