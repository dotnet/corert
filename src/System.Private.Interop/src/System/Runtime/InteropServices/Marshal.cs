// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This file provides an implementation of the pieces of the Marshal class which are required by the Interop
// API contract but are not provided by the version of Marshal which is part of the Redhawk test library.
// This partial class is combined with the version from the Redhawk test library, in order to provide the
// Marshal implementation for System.Private.CoreLib.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;

namespace System.Runtime.InteropServices
{
    public static partial class Marshal
    {


        private const long HIWORDMASK = unchecked((long)0xffffffffffff0000L);

        // Win32 has the concept of Atoms, where a pointer can either be a pointer
        // or an int.  If it's less than 64K, this is guaranteed to NOT be a
        // pointer since the bottom 64K bytes are reserved in a process' page table.
        // We should be careful about deallocating this stuff.  Extracted to
        // a function to avoid C# problems with lack of support for IntPtr.
        // We have 2 of these methods for slightly different semantics for NULL.
        private static bool IsWin32Atom(IntPtr ptr)
        {
            long lPtr = (long)ptr;
            return 0 == (lPtr & HIWORDMASK);
        }

        private static bool IsNotWin32Atom(IntPtr ptr)
        {
            long lPtr = (long)ptr;
            return 0 != (lPtr & HIWORDMASK);
        }

        //====================================================================
        // The default character size for the system. This is always 2 because
        // the framework only runs on UTF-16 systems.
        //====================================================================
        public static readonly int SystemDefaultCharSize = 2;

        //====================================================================
        // The max DBCS character size for the system.
        //====================================================================
        public static readonly int SystemMaxDBCSCharSize = PInvokeMarshal.GetSystemMaxDBCSCharSize();

        public static unsafe String PtrToStringAnsi(IntPtr ptr)
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
                int nb = lstrlenA(ptr);

                if (nb == 0)
                {
                    return string.Empty;
                }
                else
                {
                    return ConvertToUnicode(ptr, nb);
                }
            }
        }

        public static unsafe String PtrToStringAnsi(IntPtr ptr, int len)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));
            if (len < 0)
                throw new ArgumentException(nameof(len));

            return ConvertToUnicode(ptr, len);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr, int len)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));
            if (len < 0)
                throw new ArgumentException(nameof(len));

            return new String((char*)ptr, 0, len);
        }

        public static unsafe String PtrToStringUni(IntPtr ptr)
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
                return new String((char*)ptr);
            }
        }

        public static String PtrToStringAuto(IntPtr ptr, int len)
        {
            // Ansi platforms are no longer supported
            return PtrToStringUni(ptr, len);
        }

        public static String PtrToStringAuto(IntPtr ptr)
        {
            // Ansi platforms are no longer supported
            return PtrToStringUni(ptr);
        }

        //====================================================================
        // SizeOf()
        //====================================================================

        /// <summary>
        /// Returns the size of an instance of a value type.
        /// </summary>
        public static int SizeOf<T>()
        {
            return SizeOf(typeof(T));
        }

        public static int SizeOf<T>(T structure)
        {
            return SizeOf<T>();
        }

        public static int SizeOf(Object structure)
        {
            if (structure == null)
                throw new ArgumentNullException(nameof(structure));
            // we never had a check for generics here
            Contract.EndContractBlock();

            return SizeOfHelper(structure.GetType(), true);
        }

        [Pure]
        public static int SizeOf(Type t)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));
            if (t.TypeHandle.IsGenericType() || t.TypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(t));
            Contract.EndContractBlock();

            return SizeOfHelper(t, true);
        }

        private static int SizeOfHelper(Type t, bool throwIfNotMarshalable)
        {
            RuntimeTypeHandle typeHandle = t.TypeHandle;

            RuntimeTypeHandle unsafeStructType;
            if (McgModuleManager.TryGetStructUnsafeStructType(typeHandle, out unsafeStructType))
            {
                return unsafeStructType.GetValueTypeSize();
            }

            if (!typeHandle.IsBlittable() && !typeHandle.IsValueType())
            {
                throw new MissingInteropDataException(SR.StructMarshalling_MissingInteropData, t);
            }
            else
            {
                return typeHandle.GetValueTypeSize();
            }
        }

        //====================================================================
        // OffsetOf()
        //====================================================================
        public static IntPtr OffsetOf(Type t, String fieldName)
        {
            if (t == null)
                throw new ArgumentNullException(nameof(t));

            if (String.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));

            if (t.TypeHandle.IsGenericType() || t.TypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(t));

            Contract.EndContractBlock();

            return OffsetOfHelper(t, fieldName);
        }

        private static IntPtr OffsetOfHelper(Type t, String fieldName)
        {
            bool structExists;
            uint offset;
            if (McgModuleManager.TryGetStructFieldOffset(t.TypeHandle, fieldName, out structExists, out offset))
            {
                return new IntPtr(offset);
            }

            // if we can find the struct but couldn't find its field, throw Argument Exception
            if (structExists)
            {
                throw new ArgumentException(SR.Format(SR.Argument_OffsetOfFieldNotFound, t.TypeHandle.GetDisplayName()), nameof(fieldName));
            }
            else
            {
                throw new MissingInteropDataException(SR.StructMarshalling_MissingInteropData, t);
            }
        }

        public static IntPtr OffsetOf<T>(String fieldName)
        {
            return OffsetOf(typeof(T), fieldName);
        }

        //====================================================================
        // Copy blocks from CLR arrays to native memory.
        //====================================================================
        public static void Copy(int[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(char[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(short[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(long[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(float[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(double[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(byte[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        public static void Copy(IntPtr[] source, int startIndex, IntPtr destination, int length)
        {
            CopyToNative(source, startIndex, destination, length);
        }

        private static void CopyToNative(Array source, int startIndex, IntPtr destination, int length)
        {
            InteropExtensions.CopyToNative(source, startIndex, destination, length);
        }

        //====================================================================
        // Copy blocks from native memory to CLR arrays
        //====================================================================
        public static void Copy(IntPtr source, int[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, char[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, short[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, long[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, float[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, double[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, byte[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        public static void Copy(IntPtr source, IntPtr[] destination, int startIndex, int length)
        {
            CopyToManaged(source, destination, startIndex, length);
        }

        private static void CopyToManaged(IntPtr source, Array destination, int startIndex, int length)
        {
            InteropExtensions.CopyToManaged(source, destination, startIndex, length);
        }


        //====================================================================
        // Read from memory
        //====================================================================


        public static unsafe byte ReadByte(IntPtr ptr, int ofs)
        {
            byte* addr = (byte*)ptr + ofs;
            return *addr;
        }

        public static byte ReadByte(IntPtr ptr)
        {
            return ReadByte(ptr, 0);
        }

        public static unsafe short ReadInt16(IntPtr ptr, int ofs)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x1) == 0)
            {
                // aligned read
                return *((short*)addr);
            }
            else
            {
                // unaligned read
                short val;
                byte* valPtr = (byte*)&val;
                valPtr[0] = addr[0];
                valPtr[1] = addr[1];
                return val;
            }
        }

        public static short ReadInt16(IntPtr ptr)
        {
            return ReadInt16(ptr, 0);
        }

        public static unsafe int ReadInt32(IntPtr ptr, int ofs)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x3) == 0)
            {
                // aligned read
                return *((int*)addr);
            }
            else
            {
                // unaligned read
                int val;
                byte* valPtr = (byte*)&val;
                valPtr[0] = addr[0];
                valPtr[1] = addr[1];
                valPtr[2] = addr[2];
                valPtr[3] = addr[3];
                return val;
            }
        }

        public static int ReadInt32(IntPtr ptr)
        {
            return ReadInt32(ptr, 0);
        }

        public static IntPtr ReadIntPtr([MarshalAs(UnmanagedType.AsAny), In] Object ptr, int ofs)
        {
#if WIN32
            return (IntPtr)ReadInt32(ptr, ofs);
#else
            return (IntPtr)ReadInt64(ptr, ofs);
#endif
        }

        public static IntPtr ReadIntPtr(IntPtr ptr, int ofs)
        {
#if WIN32
            return (IntPtr)ReadInt32(ptr, ofs);
#else
            return (IntPtr)ReadInt64(ptr, ofs);
#endif
        }

        public static IntPtr ReadIntPtr(IntPtr ptr)
        {
#if WIN32
            return (IntPtr)ReadInt32(ptr, 0);
#else
            return (IntPtr)ReadInt64(ptr, 0);
#endif
        }

        public static unsafe long ReadInt64(IntPtr ptr, int ofs)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x7) == 0)
            {
                // aligned read
                return *((long*)addr);
            }
            else
            {
                // unaligned read
                long val;
                byte* valPtr = (byte*)&val;
                valPtr[0] = addr[0];
                valPtr[1] = addr[1];
                valPtr[2] = addr[2];
                valPtr[3] = addr[3];
                valPtr[4] = addr[4];
                valPtr[5] = addr[5];
                valPtr[6] = addr[6];
                valPtr[7] = addr[7];
                return val;
            }
        }

        public static long ReadInt64(IntPtr ptr)
        {
            return ReadInt64(ptr, 0);
        }

        //====================================================================
        // Write to memory
        //====================================================================
        public static unsafe void WriteByte(IntPtr ptr, int ofs, byte val)
        {
            byte* addr = (byte*)ptr + ofs;
            *addr = val;
        }

        public static void WriteByte(IntPtr ptr, byte val)
        {
            WriteByte(ptr, 0, val);
        }

        public static unsafe void WriteInt16(IntPtr ptr, int ofs, short val)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x1) == 0)
            {
                // aligned write
                *((short*)addr) = val;
            }
            else
            {
                // unaligned write
                byte* valPtr = (byte*)&val;
                addr[0] = valPtr[0];
                addr[1] = valPtr[1];
            }
        }

        public static void WriteInt16(IntPtr ptr, short val)
        {
            WriteInt16(ptr, 0, val);
        }

        public static void WriteInt16(IntPtr ptr, int ofs, char val)
        {
            WriteInt16(ptr, ofs, (short)val);
        }

        public static void WriteInt16([In, Out]Object ptr, int ofs, char val)
        {
            WriteInt16(ptr, ofs, (short)val);
        }

        public static void WriteInt16(IntPtr ptr, char val)
        {
            WriteInt16(ptr, 0, (short)val);
        }

        public static unsafe void WriteInt32(IntPtr ptr, int ofs, int val)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x3) == 0)
            {
                // aligned write
                *((int*)addr) = val;
            }
            else
            {
                // unaligned write
                byte* valPtr = (byte*)&val;
                addr[0] = valPtr[0];
                addr[1] = valPtr[1];
                addr[2] = valPtr[2];
                addr[3] = valPtr[3];
            }
        }

        public static void WriteInt32(IntPtr ptr, int val)
        {
            WriteInt32(ptr, 0, val);
        }

        public static void WriteIntPtr(IntPtr ptr, int ofs, IntPtr val)
        {
#if WIN32
            WriteInt32(ptr, ofs, (int)val);
#else
            WriteInt64(ptr, ofs, (long)val);
#endif
        }

        public static void WriteIntPtr([MarshalAs(UnmanagedType.AsAny), In, Out] Object ptr, int ofs, IntPtr val)
        {
#if WIN32
            WriteInt32(ptr, ofs, (int)val);
#else
            WriteInt64(ptr, ofs, (long)val);
#endif
        }

        public static void WriteIntPtr(IntPtr ptr, IntPtr val)
        {
#if WIN32
            WriteInt32(ptr, 0, (int)val);
#else
            WriteInt64(ptr, 0, (long)val);
#endif
        }

        public static unsafe void WriteInt64(IntPtr ptr, int ofs, long val)
        {
            byte* addr = (byte*)ptr + ofs;
            if ((unchecked((int)addr) & 0x7) == 0)
            {
                // aligned write
                *((long*)addr) = val;
            }
            else
            {
                // unaligned write
                byte* valPtr = (byte*)&val;
                addr[0] = valPtr[0];
                addr[1] = valPtr[1];
                addr[2] = valPtr[2];
                addr[3] = valPtr[3];
                addr[4] = valPtr[4];
                addr[5] = valPtr[5];
                addr[6] = valPtr[6];
                addr[7] = valPtr[7];
            }
        }

        public static void WriteInt64(IntPtr ptr, long val)
        {
            WriteInt64(ptr, 0, val);
        }

        //====================================================================
        // GetHRForLastWin32Error
        //====================================================================
        public static int GetHRForLastWin32Error()
        {
            int dwLastError = GetLastWin32Error();
            if ((dwLastError & 0x80000000) == 0x80000000)
            {
                return dwLastError;
            }
            else
            {
                return (dwLastError & 0x0000FFFF) | unchecked((int)0x80070000);
            }
        }

        public static Exception GetExceptionForHR(int errorCode, IntPtr errorInfo)
        {
#if ENABLE_WINRT
            if (errorInfo != new IntPtr(-1))
            {
                throw new PlatformNotSupportedException();
            }

            return ExceptionHelpers.GetMappingExceptionForHR(
                errorCode,
                message: null,
                createCOMException: false,
                hasErrorInfo: false);
#else
            throw new PlatformNotSupportedException("GetExceptionForHR");
#endif // ENABLE_WINRT
        }

        //====================================================================
        // Throws a CLR exception based on the HRESULT.
        //====================================================================
        public static void ThrowExceptionForHR(int errorCode)
        {
            ThrowExceptionForHRInternal(errorCode, new IntPtr(-1));
        }

        public static void ThrowExceptionForHR(int errorCode, IntPtr errorInfo)
        {
            ThrowExceptionForHRInternal(errorCode, errorInfo);
        }

        private static void ThrowExceptionForHRInternal(int errorCode, IntPtr errorInfo)
        {
            if (errorCode < 0)
            {
                throw GetExceptionForHR(errorCode, errorInfo);
            }
        }

        //====================================================================
        // Memory allocation and deallocation.
        //====================================================================
        public static unsafe IntPtr ReAllocHGlobal(IntPtr pv, IntPtr cb)
        {
            return PInvokeMarshal.MemReAlloc(pv, cb);
        }

        private static unsafe void ConvertToAnsi(string source, IntPtr pbNativeBuffer, int cbNativeBuffer)
        {
            Debug.Assert(source != null);
            Debug.Assert(pbNativeBuffer != IntPtr.Zero);
            Debug.Assert(cbNativeBuffer >= (source.Length + 1) * SystemMaxDBCSCharSize, "Insufficient buffer length passed to ConvertToAnsi");

            fixed (char* pch = source)
            {
                int convertedBytes =
                    PInvokeMarshal.ConvertWideCharToMultiByte(pch, source.Length, (byte*)pbNativeBuffer, cbNativeBuffer);
                ((byte*)pbNativeBuffer)[convertedBytes] = 0;
            }
        }

        private static unsafe string ConvertToUnicode(IntPtr sourceBuffer, int cbSourceBuffer)
        {
            if (IsWin32Atom(sourceBuffer))
            {
                throw new ArgumentException(SR.Arg_MustBeStringPtrNotAtom);
            }

            if (sourceBuffer == IntPtr.Zero || cbSourceBuffer == 0)
            {
                return String.Empty;
            }
            // MB_PRECOMPOSED is the default.
            int charsRequired = PInvokeMarshal.GetCharCount((byte*)sourceBuffer, cbSourceBuffer);

            if (charsRequired == 0)
            {
                throw new ArgumentException(SR.Arg_InvalidANSIString);
            }

            char[] wideChars = new char[charsRequired + 1];
            fixed (char* pWideChars = &wideChars[0])
            {
                int converted = PInvokeMarshal.ConvertMultiByteToWideChar((byte*)sourceBuffer,
                                                                    cbSourceBuffer,
                                                                    pWideChars,
                                                                    wideChars.Length);
                if (converted == 0)
                {
                    throw new ArgumentException(SR.Arg_InvalidANSIString);
                }

                wideChars[converted] = '\0';
                return new String(pWideChars);
            }
        }

        private static unsafe int lstrlenA(IntPtr sz)
        {
            Debug.Assert(sz != IntPtr.Zero);

            byte* pb = (byte*)sz;
            byte* start = pb;
            while (*pb != 0)
            {
                ++pb;
            }

            return (int)(pb - start);
        }

        private static unsafe int lstrlenW(IntPtr wsz)
        {
            Debug.Assert(wsz != IntPtr.Zero);

            char* pc = (char*)wsz;
            char* start = pc;
            while (*pc != 0)
            {
                ++pc;
            }

            return (int)(pc - start);
        }

        // Zero out the buffer pointed to by ptr, making sure that the compiler cannot
        // replace the zeroing with a nop
        private static unsafe void SecureZeroMemory(IntPtr ptr, int bytes)
        {
            Debug.Assert(ptr != IntPtr.Zero);
            Debug.Assert(bytes >= 0);

            byte* pBuffer = (byte*)ptr;
            for (int i = 0; i < bytes; ++i)
            {
                Volatile.Write(ref pBuffer[i], 0);
            }
        }

        //====================================================================
        // String convertions.
        //====================================================================
        public static unsafe IntPtr StringToHGlobalAnsi(String s)
        {
            if (s == null)
            {
                return IntPtr.Zero;
            }
            else
            {
                int nb = (s.Length + 1) * SystemMaxDBCSCharSize;

                // Overflow checking
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException(nameof(s));

                IntPtr hglobal = PInvokeMarshal.MemAlloc(new IntPtr(nb));
                ConvertToAnsi(s, hglobal, nb);
                return hglobal;
            }
        }

        public static unsafe IntPtr StringToHGlobalUni(String s)
        {
            if (s == null)
            {
                return IntPtr.Zero;
            }
            else
            {
                int nb = (s.Length + 1) * 2;

                // Overflow checking
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException(nameof(s));

                IntPtr hglobal = PInvokeMarshal.MemAlloc(new IntPtr(nb));
                fixed (char* firstChar = s)
                {
                    InteropExtensions.Memcpy(hglobal, new IntPtr(firstChar), nb);
                }
                return hglobal;
            }
        }

        public static IntPtr StringToHGlobalAuto(String s)
        {
            // Ansi platforms are no longer supported
            return StringToHGlobalUni(s);
        }

        //====================================================================
        // return the IUnknown* for an Object if the current context
        // is the one where the RCW was first seen. Will return null
        // otherwise.
        //====================================================================
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        {
            if (o == null)
            {
                throw new ArgumentNullException(nameof(o));
            }
            return MarshalAdapter.GetIUnknownForObject(o);
        }

        //====================================================================
        // return an Object for IUnknown
        //====================================================================
        public static Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk)
        {
            if (pUnk == default(IntPtr))
            {
                throw new ArgumentNullException(nameof(pUnk));
            }
            return MarshalAdapter.GetObjectForIUnknown(pUnk);
        }

        //====================================================================
        // check if the object is classic COM component
        //====================================================================
        public static bool IsComObject(Object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o), SR.Arg_InvalidHandle);

            return McgComHelpers.IsComObject(o);
        }

        public static unsafe IntPtr StringToCoTaskMemUni(String s)
        {
            if (s == null)
            {
                return IntPtr.Zero;
            }
            else
            {
                int nb = (s.Length + 1) * 2;

                // Overflow checking
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException(nameof(s));

                IntPtr hglobal = PInvokeMarshal.CoTaskMemAlloc(new UIntPtr((uint)nb));

                if (hglobal == IntPtr.Zero)
                {
                    throw new OutOfMemoryException();
                }
                else
                {
                    fixed (char* firstChar = s)
                    {
                        InteropExtensions.Memcpy(hglobal, new IntPtr(firstChar), nb);
                    }
                    return hglobal;
                }
            }
        }

        public static unsafe IntPtr StringToCoTaskMemAnsi(String s)
        {
            if (s == null)
            {
                return IntPtr.Zero;
            }
            else
            {
                int nb = (s.Length + 1) * SystemMaxDBCSCharSize;

                // Overflow checking
                if (nb < s.Length)
                    throw new ArgumentOutOfRangeException(nameof(s));

                IntPtr hglobal = PInvokeMarshal.CoTaskMemAlloc(new UIntPtr((uint)nb));

                if (hglobal == IntPtr.Zero)
                {
                    throw new OutOfMemoryException();
                }
                else
                {
                    ConvertToAnsi(s, hglobal, nb);
                    return hglobal;
                }
            }
        }

        public static IntPtr StringToCoTaskMemAuto(String s)
        {
            // Ansi platforms are no longer supported
            return StringToCoTaskMemUni(s);
        }

        //====================================================================
        // release the COM component and if the reference hits 0 zombie this object
        // further usage of this Object might throw an exception
        //====================================================================
        public static int ReleaseComObject(Object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));

            __ComObject co = null;

            // Make sure the obj is an __ComObject.
            try
            {
                co = (__ComObject)o;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Argument_ObjNotComObject, nameof(o));
            }

            return McgMarshal.Release(co);
        }

        //====================================================================
        // release the COM component and zombie this object
        // further usage of this Object might throw an exception
        //====================================================================
        public static Int32 FinalReleaseComObject(Object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));
            Contract.EndContractBlock();

            __ComObject co = null;

            // Make sure the obj is an __ComObject.
            try
            {
                co = (__ComObject)o;
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(SR.Argument_ObjNotComObject, nameof(o));
            }

            co.FinalReleaseSelf();

            return 0;
        }

        //====================================================================
        // IUnknown Helpers
        //====================================================================

        public static int /* HRESULT */ QueryInterface(IntPtr /* IUnknown */ pUnk, ref Guid iid, out IntPtr ppv)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            return McgMarshal.ComQueryInterfaceWithHR(pUnk, ref iid, out ppv);
        }

        public static int /* ULONG */ AddRef(IntPtr /* IUnknown */ pUnk)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            return McgMarshal.ComAddRef(pUnk);
        }

        public static int /* ULONG */ Release(IntPtr /* IUnknown */ pUnk)
        {
            if (pUnk == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pUnk));

            // This is documented to have "undefined behavior" when the ref count is already zero, so
            // let's not AV if we can help it
            return McgMarshal.ComSafeRelease(pUnk);
        }

        public static IntPtr ReAllocCoTaskMem(IntPtr pv, int cb)
        {
            IntPtr pNewMem = PInvokeMarshal.CoTaskMemReAlloc(pv, new IntPtr(cb));
            if (pNewMem == IntPtr.Zero && cb != 0)
            {
                throw new OutOfMemoryException();
            }

            return pNewMem;
        }

        //====================================================================
        // BSTR allocation and dealocation.
        //====================================================================
        public static void FreeBSTR(IntPtr ptr)
        {
            if (IsNotWin32Atom(ptr))
            {
                ExternalInterop.SysFreeString(ptr);
            }
        }

        public static unsafe IntPtr StringToBSTR(String s)
        {
            if (s == null)
                return IntPtr.Zero;

            // Overflow checking
            if (s.Length + 1 < s.Length)
                throw new ArgumentOutOfRangeException(nameof(s));

            fixed (char* pch = s)
            {
                IntPtr bstr = new IntPtr(ExternalInterop.SysAllocStringLen(pch, (uint)s.Length));
                if (bstr == IntPtr.Zero)
                    throw new OutOfMemoryException();

                return bstr;
            }
        }

        public static String PtrToStringBSTR(IntPtr ptr)
        {
            return PtrToStringUni(ptr, (int)ExternalInterop.SysStringLen(ptr));
        }

        public static void ZeroFreeBSTR(IntPtr s)
        {
            SecureZeroMemory(s, (int)ExternalInterop.SysStringLen(s) * 2);
            FreeBSTR(s);
        }

        public static void ZeroFreeCoTaskMemAnsi(IntPtr s)
        {
            SecureZeroMemory(s, lstrlenA(s));
            FreeCoTaskMem(s);
        }

        public static void ZeroFreeCoTaskMemUnicode(IntPtr s)
        {
            SecureZeroMemory(s, lstrlenW(s));
            FreeCoTaskMem(s);
        }

        public static void ZeroFreeGlobalAllocAnsi(IntPtr s)
        {
            SecureZeroMemory(s, lstrlenA(s));
            FreeHGlobal(s);
        }

        public static void ZeroFreeGlobalAllocUnicode(IntPtr s)
        {
            SecureZeroMemory(s, lstrlenW(s));
            FreeHGlobal(s);
        }

        /// <summary>
        /// Returns the unmanaged function pointer for this delegate
        /// </summary>
        public static IntPtr GetFunctionPointerForDelegate(Delegate d)
        {
            if (d == null)
                throw new ArgumentNullException(nameof(d));

            return PInvokeMarshal.GetStubForPInvokeDelegate(d);
        }

        public static IntPtr GetFunctionPointerForDelegate<TDelegate>(TDelegate d)
        {
            return GetFunctionPointerForDelegate((Delegate)(object)d);
        }

        //====================================================================
        // Marshals data from a native memory block to a preallocated structure class.
        //====================================================================

        private static unsafe void PtrToStructureHelper(IntPtr ptr, Object structure)
        {
            RuntimeTypeHandle structureTypeHandle = structure.GetType().TypeHandle;

            // Boxed struct start at offset 1 (EEType* at offset 0) while class start at offset 0
            int offset = structureTypeHandle.IsValueType() ? 1 : 0;

            if (structureTypeHandle.IsBlittable() && structureTypeHandle.IsValueType())
            {
                int structSize = Marshal.SizeOf(structure);
                InteropExtensions.PinObjectAndCall(structure,
                    unboxedStructPtr =>
                    {
                        InteropExtensions.Memcpy(
                            (IntPtr)((IntPtr*)unboxedStructPtr + offset),   // safe (need to adjust offset as it could be class)
                            ptr,                                            // unsafe (no need to adjust as it is always struct)
                            structSize
                        );
                    });
                return;
            }

            IntPtr unmarshalStub;
            if (McgModuleManager.TryGetStructUnmarshalStub(structureTypeHandle, out unmarshalStub))
            {
                InteropExtensions.PinObjectAndCall(structure,
                    unboxedStructPtr =>
                    {
                        CalliIntrinsics.Call<int>(
                            unmarshalStub,
                            (void*)ptr,                                     // unsafe (no need to adjust as it is always struct)
                            ((void*)((IntPtr*)unboxedStructPtr + offset))   // safe (need to adjust offset as it could be class)
                        );
                    });
                return;
            }

            throw new MissingInteropDataException(SR.StructMarshalling_MissingInteropData, structure.GetType());
        }

        //====================================================================
        // Creates a new instance of "structuretype" and marshals data from a
        // native memory block to it.
        //====================================================================
        [MethodImplAttribute(MethodImplOptions.NoInlining)] // Methods containing StackCrawlMark local var has to be marked non-inlineable
        public static Object PtrToStructure(IntPtr ptr, Type structureType)
        {
            // Boxing the struct here is important to ensure that the original copy is written to,
            // not the autoboxed copy

            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (structureType == null)
                throw new ArgumentNullException(nameof(structureType));

            Object boxedStruct = InteropExtensions.RuntimeNewObject(structureType.TypeHandle);
            PtrToStructureHelper(ptr, boxedStruct);
            return boxedStruct;
        }

        public static T PtrToStructure<T>(IntPtr ptr)
        {
            return (T)PtrToStructure(ptr, typeof(T));
        }

        public static void PtrToStructure(IntPtr ptr, Object structure)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (structure == null)
                throw new ArgumentNullException(nameof(structure));

            RuntimeTypeHandle structureTypeHandle = structure.GetType().TypeHandle;
            if (structureTypeHandle.IsValueType())
            {
                throw new ArgumentException(nameof(structure), SR.Argument_StructMustNotBeValueClass);
            }

            PtrToStructureHelper(ptr, structure);
        }

        public static void PtrToStructure<T>(IntPtr ptr, T structure)
        {
            PtrToStructure(ptr, (object)structure);
        }

        //====================================================================
        // Marshals data from a structure class to a native memory block.
        // If the structure contains pointers to allocated blocks and
        // "fDeleteOld" is true, this routine will call DestroyStructure() first.
        //====================================================================
        public static unsafe void StructureToPtr(Object structure, IntPtr ptr, bool fDeleteOld)
        {
            if (structure == null)
                throw new ArgumentNullException(nameof(structure));

            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (fDeleteOld)
            {
                DestroyStructure(ptr, structure.GetType());
            }

            RuntimeTypeHandle structureTypeHandle = structure.GetType().TypeHandle;

            if (structureTypeHandle.IsGenericType() || structureTypeHandle.IsGenericTypeDefinition())
            {
                throw new ArgumentException(nameof(structure), SR.Argument_NeedNonGenericObject);
            }

            // Boxed struct start at offset 1 (EEType* at offset 0) while class start at offset 0
            int offset = structureTypeHandle.IsValueType() ? 1 : 0;

            bool isBlittable = false; // whether Mcg treat this struct as blittable struct
            IntPtr marshalStub;
            if (McgModuleManager.TryGetStructMarshalStub(structureTypeHandle, out marshalStub))
            {
                if (marshalStub != IntPtr.Zero)
                {
                    InteropExtensions.PinObjectAndCall(structure,
                        unboxedStructPtr =>
                        {
                            CalliIntrinsics.Call<int>(
                                marshalStub,
                                ((void*)((IntPtr*)unboxedStructPtr + offset)),  // safe (need to adjust offset as it could be class)
                                (void*)ptr                                      // unsafe (no need to adjust as it is always struct)
                            );
                        });
                    return;
                }
                else
                {
                    isBlittable = true;
                }
            }

            if (isBlittable || structureTypeHandle.IsBlittable()) // blittable
            {
                int structSize = Marshal.SizeOf(structure);
                InteropExtensions.PinObjectAndCall(structure,
                    unboxedStructPtr =>
                    {
                        InteropExtensions.Memcpy(
                            ptr,                                            // unsafe (no need to adjust as it is always struct)
                            (IntPtr)((IntPtr*)unboxedStructPtr + offset),   // safe (need to adjust offset as it could be class)
                            structSize
                        );
                    });
                return;
            }

            throw new MissingInteropDataException(SR.StructMarshalling_MissingInteropData, structure.GetType());
        }

        public static void StructureToPtr<T>(T structure, IntPtr ptr, bool fDeleteOld)
        {
            StructureToPtr((object)structure, ptr, fDeleteOld);
        }

        //====================================================================
        // DestroyStructure()
        //
        //====================================================================
        public static void DestroyStructure(IntPtr ptr, Type structuretype)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (structuretype == null)
                throw new ArgumentNullException(nameof(structuretype));

            RuntimeTypeHandle structureTypeHandle = structuretype.TypeHandle;

            if (structureTypeHandle.IsGenericType() || structureTypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, "t");

            if (structureTypeHandle.IsEnum() ||
                structureTypeHandle.IsInterface() ||
                InteropExtensions.AreTypesAssignable(typeof(Delegate).TypeHandle, structureTypeHandle))
            {
                throw new ArgumentException(SR.Argument_MustHaveLayoutOrBeBlittable, structureTypeHandle.GetDisplayName());
            }

            Contract.EndContractBlock();

            DestroyStructureHelper(ptr, structuretype);
        }

        private static unsafe void DestroyStructureHelper(IntPtr ptr, Type structuretype)
        {
            RuntimeTypeHandle structureTypeHandle = structuretype.TypeHandle;

            // Boxed struct start at offset 1 (EEType* at offset 0) while class start at offset 0
            int offset = structureTypeHandle.IsValueType() ? 1 : 0;

            if (structureTypeHandle.IsBlittable())
            {
                // ok to call with blittable structure, but no work to do in this case.
                return;
            }

            IntPtr destroyStructureStub;
            bool hasInvalidLayout;
            if (McgModuleManager.TryGetDestroyStructureStub(structureTypeHandle, out destroyStructureStub, out hasInvalidLayout))
            {
                if (hasInvalidLayout)
                    throw new ArgumentException(SR.Argument_MustHaveLayoutOrBeBlittable, structureTypeHandle.GetDisplayName());

                // DestroyStructureStub == IntPtr.Zero means its fields don't need to be destroied
                if (destroyStructureStub != IntPtr.Zero)
                {
                    CalliIntrinsics.Call<int>(
                        destroyStructureStub,
                        (void*)ptr                                     // unsafe (no need to adjust as it is always struct)
                    );
                }

                return;
            }

            //  Didn't find struct marshal data
            throw new MissingInteropDataException(SR.StructMarshalling_MissingInteropData, structuretype);
        }

        public static void DestroyStructure<T>(IntPtr ptr)
        {
            DestroyStructure(ptr, typeof(T));
        }

        public static IntPtr GetComInterfaceForObject<T, TInterface>(T o)
        {
            return GetComInterfaceForObject(o, typeof(TInterface));
        }

        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T)
        {
            return MarshalAdapter.GetComInterfaceForObject(o, T);
        }

        public static TDelegate GetDelegateForFunctionPointer<TDelegate>(IntPtr ptr)
        {
            return (TDelegate)(object)GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
        }

        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        {
            // Validate the parameters
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(ptr));

            if (t == null)
                throw new ArgumentNullException(nameof(t));
            Contract.EndContractBlock();

            if (t.TypeHandle.IsGenericType() || t.TypeHandle.IsGenericTypeDefinition())
                throw new ArgumentException(SR.Argument_NeedNonGenericType, nameof(t));

            bool isDelegateType = InteropExtensions.AreTypesAssignable(t.TypeHandle, typeof(MulticastDelegate).TypeHandle) ||
                                  InteropExtensions.AreTypesAssignable(t.TypeHandle, typeof(Delegate).TypeHandle);

            if (!isDelegateType)
                throw new ArgumentException(SR.Arg_MustBeDelegateType, nameof(t));

            return MarshalAdapter.GetDelegateForFunctionPointer(ptr, t);
        }

        //====================================================================
        // GetNativeVariantForObject()
        //
        //====================================================================
        public static void GetNativeVariantForObject<T>(T obj, IntPtr pDstNativeVariant)
        {
            GetNativeVariantForObject((object)obj, pDstNativeVariant);
        }

        public static unsafe void GetNativeVariantForObject(Object obj, /* VARIANT * */ IntPtr pDstNativeVariant)
        {
            // Obsolete
            if (pDstNativeVariant == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pDstNativeVariant));

            if (obj != null && (obj.GetType().TypeHandle.IsGenericType() || obj.GetType().TypeHandle.IsGenericTypeDefinition()))
                throw new ArgumentException(SR.Argument_NeedNonGenericObject, nameof(obj));

            Contract.EndContractBlock();

            Variant* pVariant = (Variant*)pDstNativeVariant;
            *pVariant = new Variant(obj);
        }

        //====================================================================
        // GetObjectForNativeVariant()
        //
        //====================================================================
        public static unsafe T GetObjectForNativeVariant<T>(IntPtr pSrcNativeVariant)
        {
            return (T)GetObjectForNativeVariant(pSrcNativeVariant);
        }

        public static unsafe Object GetObjectForNativeVariant(/* VARIANT * */ IntPtr pSrcNativeVariant)
        {
            // Obsolete
            if (pSrcNativeVariant == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pSrcNativeVariant));
            Contract.EndContractBlock();

            Variant* pNativeVar = (Variant*)pSrcNativeVariant;
            return pNativeVar->ToObject();
        }

        //====================================================================
        // GetObjectsForNativeVariants()
        //
        //====================================================================
        public static unsafe Object[] GetObjectsForNativeVariants(IntPtr aSrcNativeVariant, int cVars)
        {
            // Obsolete
            if (aSrcNativeVariant == IntPtr.Zero)
                throw new ArgumentNullException(nameof(aSrcNativeVariant));

            if (cVars < 0)
                throw new ArgumentOutOfRangeException(nameof(cVars), SR.ArgumentOutOfRange_NeedNonNegNum);
            Contract.EndContractBlock();

            Object[] obj = new Object[cVars];
            IntPtr aNativeVar = aSrcNativeVariant;
            for (int i = 0; i < cVars; i++)
            {
                obj[i] = GetObjectForNativeVariant(aNativeVar);
                aNativeVar = aNativeVar + sizeof(Variant);
            }

            return obj;
        }

        public static T[] GetObjectsForNativeVariants<T>(IntPtr aSrcNativeVariant, int cVars)
        {
            object[] objects = GetObjectsForNativeVariants(aSrcNativeVariant, cVars);
            T[] result = null;

            if (objects != null)
            {
                result = new T[objects.Length];
                Array.Copy(objects, result, objects.Length);
            }

            return result;
        }

        //====================================================================
        // UnsafeAddrOfPinnedArrayElement()
        //
        // IMPORTANT NOTICE: This method does not do any verification on the
        // array. It must be used with EXTREME CAUTION since passing in
        // an array that is not pinned or in the fixed heap can cause
        // unexpected results !
        //====================================================================
        public static IntPtr UnsafeAddrOfPinnedArrayElement(Array arr, int index)
        {
            if (arr == null)
                throw new ArgumentNullException(nameof(arr));

            if (index < 0 || index >= arr.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            Contract.EndContractBlock();

            int offset = checked(index * arr.GetElementSize());

            return arr.GetAddrOfPinnedArrayFromEETypeField() + offset;
        }

        public static IntPtr UnsafeAddrOfPinnedArrayElement<T>(T[] arr, int index)
        {
            return UnsafeAddrOfPinnedArrayElement((Array)arr, index);
        }

        //====================================================================
        // This method binds to the specified moniker.
        //====================================================================
        public static Object BindToMoniker(String monikerName)
        {
#if TARGET_CORE_API_SET // BindMoniker not available in core API set
            throw new PlatformNotSupportedException();
#else
            Object obj = null;
            IBindCtx bindctx = null;
            ExternalInterop.CreateBindCtx(0, out bindctx);

            UInt32 cbEaten;
            IMoniker pmoniker = null;
            ExternalInterop.MkParseDisplayName(bindctx, monikerName, out cbEaten, out pmoniker);

            ExternalInterop.BindMoniker(pmoniker, 0, ref Interop.COM.IID_IUnknown, out obj);
            return obj;
#endif
        }

#if ENABLE_WINRT
        public static Type GetTypeFromCLSID(Guid clsid)
        {
            // @TODO - if this is something we recognize, create a strongly-typed RCW
            // Otherwise, create a weakly typed RCW
            throw new PlatformNotSupportedException();
        }

        //====================================================================
        // Return a unique Object given an IUnknown.  This ensures that you
        //  receive a fresh object (we will not look in the cache to match up this
        //  IUnknown to an already existing object).  This is useful in cases
        //  where you want to be able to call ReleaseComObject on a RCW
        //  and not worry about other active uses of said RCW.
        //====================================================================
        public static Object GetUniqueObjectForIUnknown(IntPtr unknown)
        {
            throw new PlatformNotSupportedException();
        }

        public static bool AreComObjectsAvailableForCleanup()
        {
            throw new PlatformNotSupportedException();
        }

        public static IntPtr CreateAggregatedObject(IntPtr pOuter, Object o)
        {
            throw new PlatformNotSupportedException();
        }

        public static IntPtr CreateAggregatedObject<T>(IntPtr pOuter, T o)
        {
            return CreateAggregatedObject(pOuter, (object)o);
        }

        public static Object CreateWrapperOfType(Object o, Type t)
        {
            throw new PlatformNotSupportedException();
        }

        public static TWrapper CreateWrapperOfType<T, TWrapper>(T o)
        {
            return (TWrapper)CreateWrapperOfType(o, typeof(TWrapper));
        }

        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T, CustomQueryInterfaceMode mode)
        {
            // Obsolete
            throw new PlatformNotSupportedException();
        }

        public static int GetExceptionCode()
        {
            // Obsolete
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// <para>Returns the first valid COM slot that GetMethodInfoForSlot will work on
        /// This will be 3 for IUnknown based interfaces and 7 for IDispatch based interfaces. </para>
        /// </summary>
        public static int GetStartComSlot(Type t)
        {
            throw new PlatformNotSupportedException();
        }

        //====================================================================
        // Given a managed object that wraps an ITypeInfo, return its name
        //====================================================================
        public static String GetTypeInfoName(ITypeInfo typeInfo)
        {
            throw new PlatformNotSupportedException();
        }
#endif  //ENABLE_WINRT

        public static byte ReadByte(Object ptr, int ofs)
        {
            // Obsolete
            throw new PlatformNotSupportedException("ReadByte");
        }

        public static short ReadInt16(Object ptr, int ofs)
        {
            // Obsolete
            throw new PlatformNotSupportedException("ReadInt16");
        }

        public static int ReadInt32(Object ptr, int ofs)
        {
            // Obsolete
            throw new PlatformNotSupportedException("ReadInt32");
        }

        public static long ReadInt64(Object ptr, int ofs)
        {
            // Obsolete
            throw new PlatformNotSupportedException("ReadInt64");
        }

        public static void WriteByte(Object ptr, int ofs, byte val)
        {
            // Obsolete
            throw new PlatformNotSupportedException("WriteByte");
        }

        public static void WriteInt16(Object ptr, int ofs, short val)
        {
            // Obsolete
            throw new PlatformNotSupportedException("WriteInt16");
        }

        public static void WriteInt32(Object ptr, int ofs, int val)
        {
            // Obsolete
            throw new PlatformNotSupportedException("WriteInt32");
        }

        public static void WriteInt64(Object ptr, int ofs, long val)
        {
            // Obsolete
            throw new PlatformNotSupportedException("WriteInt64");
        }

        public static void ChangeWrapperHandleStrength(Object otp, bool fIsWeak)
        {
            throw new PlatformNotSupportedException("ChangeWrapperHandleStrength");
        }

        public static void CleanupUnusedObjectsInCurrentContext()
        {
            // RCW cleanup implemented in native code in CoreCLR, and uses a global list to indicate which objects need to be collected. In
            // CoreRT, RCWs are implemented in managed code and their cleanup is normally accomplished using finalizers. Implementing
            // this method in a more complicated way (without calling WaitForPendingFinalizers) is non-trivial because it possible for timing
            // problems to occur when competing with finalizers.
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public static void Prelink(MethodInfo m)
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));
            Contract.EndContractBlock();

            // Note: This method is effectively a no-op in ahead-of-time compilation scenarios. In CoreCLR and Desktop, this will pre-generate
            // the P/Invoke, but everything is pre-generated in CoreRT.
        }

        public static void PrelinkAll(Type c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c));
            Contract.EndContractBlock();

            MethodInfo[] mi = c.GetMethods();
            if (mi != null)
            {
                for (int i = 0; i < mi.Length; i++)
                {
                    Prelink(mi[i]);
                }
            }
        }
    }
}
