// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    internal static partial class MarshalImpl
    {
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        {
            return McgMarshal.ObjectToComInterface(o, InternalTypes.IUnknown);
        }

        public static Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk)
        {
            return McgMarshal.ComInterfaceToObject(pUnk, InternalTypes.IUnknown);
        }

        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        {
            return McgMarshal.GetPInvokeDelegateForStub(ptr, t.TypeHandle);
        }
        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type t)
        {
            if (o == null)
                throw new ArgumentNullException("o");

            if (t == null)
                throw new ArgumentNullException("type");

            return McgMarshal.ObjectToComInterface(o, t.TypeHandle);
        }
        
        public static bool IsComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException("o");
            return McgMarshal.IsCOMObject(o.GetType());
        }
        public static int ReleaseComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException("o");
            return McgMarshal.Release(o as __ComObject);            
        }

        public static int FinalReleaseComObject(object o)
        {
            return McgMarshal.FinalReleaseComObject(o);          
        }

        public static int QueryInterface(IntPtr pUnk, ref Guid iid, out IntPtr ppv)
        {
            int hr = 0;
            ppv = McgMarshal.ComQueryInterfaceNoThrow(pUnk, ref iid, out hr);
#if CORECLR
            if (ppv == default(IntPtr))
                return Marshal.QueryInterface(pUnk, ref iid, out ppv);
#endif
            return hr;
        }

        public static int AddRef(IntPtr pUnk)
        {
            return McgMarshal.ComAddRef(pUnk);
        }

        public static int Release(IntPtr pUnk)
        {
            return McgMarshal.ComRelease(pUnk);
        }

    }
}
