// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Reflection;

namespace System.Runtime.InteropServices
{
    /// <summary>
    ///  Interface to expose MCG marshalling APIs to coreclr,the marshalredirect transform
    ///  redirect the public marshalling API calls in user code to MarshalAdapter functions.
    ///  This gives us an oppurtunity to intercept user's public marshal API call to see
    ///  if the requested operation is  MCG aware.
    /// </summary>
    public static partial class MarshalAdapter
    {
#pragma warning disable 618 // error CS0618: '%' is obsolete:
        //====================================================================
        // return the IUnknown* for an Object if the current context
        // is the one where the RCW was first seen. Will return null
        // otherwise.
        //====================================================================
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        {
            return McgMarshal.ObjectToComInterface(o, InternalTypes.IUnknown);
        }

        //====================================================================
        // return an Object for IUnknown
        //====================================================================
        public static Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk)
        {
            object obj = McgMarshal.ComInterfaceToObject(pUnk, InternalTypes.IUnknown);
#if CORECLR
            if (obj == null)
            {
                return Marshal.GetObjectForIUnknown(pUnk);
            }
#endif
            return obj;
        }

        public static IntPtr GetComInterfaceForObject<T, TInterface>(T o)
        {
            return GetComInterfaceForObject(o, typeof(TInterface));
        }

        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type T)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));

            if (T == null)
                throw new ArgumentNullException(nameof(T));

            IntPtr ptr = McgMarshal.ObjectToComInterface(o, T.TypeHandle);
#if CORECLR
            if (ptr == default(IntPtr))
            {
                return Marshal.GetComInterfaceForObject(o, T);
            }
#endif
            return ptr;
        }

        public static TDelegate GetDelegateForFunctionPointer<TDelegate>(IntPtr ptr)
        {
            return (TDelegate)(object)GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
        }

        //====================================================================
        // Checks if "t" is MCG generated , if not fallback to public  API
        //====================================================================
        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        {
            Delegate dlg = McgMarshal.GetPInvokeDelegateForStub(ptr, t.TypeHandle);
#if CORECLR
            if (dlg == null) // fall back to public marshal API
            {
                return Marshal.GetDelegateForFunctionPointer(ptr, t);
            }
#endif
            return dlg;
        }

        public static bool IsComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));
            return McgMarshal.IsComObject(o);
        }

        public static int ReleaseComObject(object o)
        {
            if (o == null)
                throw new ArgumentNullException(nameof(o));
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
#pragma warning restore 618
}
