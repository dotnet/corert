// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices
{
    internal static partial class MarshalImpl
    {
        public static IntPtr /* IUnknown* */ GetIUnknownForObject(Object o)
        {
            return McgMarshal.ObjectToComInterface(o, McgModuleManager.IUnknown);
        }

        public static Object GetObjectForIUnknown(IntPtr /* IUnknown* */ pUnk)
        {
            return McgMarshal.ComInterfaceToObject(pUnk, McgModuleManager.IUnknown);
        }

        public static Delegate GetDelegateForFunctionPointer(IntPtr ptr, Type t)
        {
            return McgModuleManager.GetPInvokeDelegateForStub(ptr, t.TypeHandle);
        }
        public static IntPtr /* IUnknown* */ GetComInterfaceForObject(Object o, Type t)
        {
            if (o == null)
                throw new ArgumentNullException("o");

            if (t == null)
                throw new ArgumentNullException("type");

            RuntimeTypeHandle interfaceTypeHandle = t.TypeHandle;
            McgTypeInfo secondTypeInfo;
            McgTypeInfo mcgTypeInfo = McgModuleManager.GetTypeInfoFromTypeHandle(interfaceTypeHandle, out secondTypeInfo);
            if (mcgTypeInfo.IsNull)
            {
#if CORECLR
                return default(IntPtr);
#else
                throw new MissingInteropDataException(SR.ComTypeMarshalling_MissingInteropData, t);
#endif
            }
            return McgMarshal.ObjectToComInterface(o, mcgTypeInfo);
        }
    }
}
