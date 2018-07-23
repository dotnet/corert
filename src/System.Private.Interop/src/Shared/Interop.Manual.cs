// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//

//
// All P/invokes used by internal Interop code goes here
//
// !!IMPORTANT!!
//
// Do not rely on MCG to generate marshalling code for these p/invokes as MCG might not see them at all
// due to not seeing dependency to those calls (before the MCG generated code is generated). Instead,
// always manually marshal the arguments

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;


// TODO : Remove the remaning methods in this file to correct interop*.cs files under Interop folder
partial class Interop
{
#if TARGET_CORE_API_SET
    internal const string CORE_SYNCH_L2 = "api-ms-win-core-synch-l1-2-0.dll";
#else
    //
    // Define dll names for previous version of Windows earlier than Windows 8
    //
    internal const string CORE_SYNCH_L2 = "kernel32.dll";
    internal const string CORE_COM = "ole32.dll";
    internal const string CORE_COM_AUT = "OleAut32.dll";

#endif   

    internal unsafe partial class COM
    {
        //
        // IIDs
        //
        internal static Guid IID_IUnknown = new Guid(0x00000000, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IMarshal = new Guid(0x00000003, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IAgileObject = new Guid(unchecked((int)0x94ea2b94), unchecked((short)0xe9cc), 0x49e0, 0xc0, 0xff, 0xee, 0x64, 0xca, 0x8f, 0x5b, 0x90);
        internal static Guid IID_IContextCallback = new Guid(0x000001da, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IEnterActivityWithNoLock = new Guid(unchecked((int)0xd7174f82), 0x36b8, 0x4aa8, 0x80, 0x0a, 0xe9, 0x63, 0xab, 0x2d, 0xfa, 0xb9);
        internal static Guid IID_ICustomPropertyProvider = new Guid(unchecked(((int)(0x7C925755u))), unchecked(((short)(0x3E48))), unchecked(((short)(0x42B4))), 0x86, 0x77, 0x76, 0x37, 0x22, 0x67, 0x3, 0x3F);
        internal static Guid IID_IInspectable = new Guid(unchecked((int)0xAF86E2E0), unchecked((short)0xB12D), 0x4c6a, 0x9C, 0x5A, 0xD7, 0xAA, 0x65, 0x10, 0x1E, 0x90);
        internal static Guid IID_IWeakReferenceSource = new Guid(0x00000038, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IWeakReference = new Guid(0x00000037, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IFindDependentWrappers = new Guid(0x04b3486c, 0x4687, 0x4229, 0x8d, 0x14, 0x50, 0x5a, 0xb5, 0x84, 0xdd, 0x88);
        internal static Guid IID_IStringable = new Guid(unchecked((int)0x96369f54), unchecked((short)0x8eb6), 0x48f0, 0xab, 0xce, 0xc1, 0xb2, 0x11, 0xe6, 0x27, 0xc3);
        internal static Guid IID_IRestrictedErrorInfo = new Guid(unchecked((int)0x82ba7092), unchecked((short)0x4c88), unchecked((short)0x427d), 0xa7, 0xbc, 0x16, 0xdd, 0x93, 0xfe, 0xb6, 0x7e);
        internal static Guid IID_ILanguageExceptionErrorInfo = new Guid(unchecked((int)0x04a2dbf3), unchecked((short)0xdf83), unchecked((short)0x116c), 0x09, 0x46, 0x08, 0x12, 0xab, 0xf6, 0xe0, 0x7d);
        internal static Guid IID_INoMarshal = new Guid(unchecked((int)0xECC8691B), unchecked((short)0xC1DB), 0x4DC0, 0x85, 0x5E, 0x65, 0xF6, 0xC5, 0x51, 0xAF, 0x49);
        internal static Guid IID_IStream = new Guid(0x0000000C, 0x0000, 0x0000, 0xC0, 0x00, 0x00,0x00,0x00, 0x00,0x00, 0x46);
        internal static Guid IID_ISequentialStream = new Guid(unchecked((int)0x0C733A30), 0x2A1C, 0x11CE, 0xAD, 0xE5, 0x00, 0xAA, 0x00, 0x44, 0x77, 0x3D);
        internal static Guid IID_IDispatch = new Guid(0x00020400, 0x0000, 0x0000, 0xc0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        internal static Guid IID_IManagedActivationFactory = new Guid(0x60D27C8D, 0x5F61, 0x4CCE, 0xB7, 0x51, 0x69, 0x0F, 0xAE, 0x66, 0xAA, 0x53);
        internal static Guid IID_IActivationFactoryInternal = new Guid(0x00000035, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);
        // CBE53FB5-F967-4258-8D34-42F5E25833DE
        internal static Guid IID_ILanguageExceptionStackBackTrace = new Guid(unchecked((int)0xCBE53FB5), unchecked((short)0xF967), 0x4258, 0x8D, 0x34, 0x42, 0xF5, 0xE2, 0x58, 0x33,0xDE);
        //
        // Jupiter IIDs.
        //
        // Note that the Windows sources refer to these IIDs via different names:
        //
        //      IClrServices        = Windows.UI.Xaml.Hosting.IReferenceTrackerHost
        //      IJupiterObject      = Windows.UI.Xaml.Hosting.IReferenceTracker
        //      ICCW                = Windows.UI.Xaml.Hosting.IReferenceTrackerTarget
        //
        internal static Guid IID_ICLRServices = new Guid(0x29a71c6a, 0x3c42, 0x4416, 0xa3, 0x9d, 0xe2, 0x82, 0x5a, 0x07, 0xa7, 0x73);
        internal static Guid IID_IJupiterObject = new Guid(0x11d3b13a, 0x180e, 0x4789, 0xa8, 0xbe, 0x77, 0x12, 0x88, 0x28, 0x93, 0xe6);
        internal static Guid IID_ICCW = new Guid(0x64bd43f8, unchecked((short)0xbfee), 0x4ec4, 0xb7, 0xeb, 0x29, 0x35, 0x15, 0x8d, 0xae, 0x21);

        //
        // CLSIDs
        //
        internal static Guid CLSID_InProcFreeMarshaler = new Guid(0x0000033A, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46);


        [StructLayout(LayoutKind.Sequential)]
        internal struct MULTI_QI
        {
            internal IntPtr pIID;
            internal IntPtr pItf;
            internal int hr;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COSERVERINFO
        {
            internal int  Reserved1;
            internal IntPtr Name;
            internal IntPtr AuthInfo;
            internal int Reserved2;
        }

        [Flags]
        internal enum CLSCTX : int
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_SERVER = CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        static unsafe internal string ConvertBSTRToString(IntPtr pBSTR)
        {
            String myString = null;

            if (pBSTR != default(IntPtr))
            {
                myString = new String((char*)pBSTR, 0, (int)ExternalInterop.SysStringLen(pBSTR));
            }

            return myString;
        }

        //
        // Constants and enums
        //
        internal enum MSHCTX : uint
        {
            MSHCTX_LOCAL = 0,           // unmarshal context is local (eg.shared memory)
            MSHCTX_NOSHAREDMEM = 1,     // unmarshal context has no shared memory access
            MSHCTX_DIFFERENTMACHINE = 2,// unmarshal context is on a different machine
            MSHCTX_INPROC = 3,          // unmarshal context is on different thread
        }

        [Flags]
        internal enum MSHLFLAGS : uint
        {
            MSHLFLAGS_NORMAL = 0,       // normal marshaling via proxy/stub
            MSHLFLAGS_TABLESTRONG = 1,  // keep object alive; must explicitly release
            MSHLFLAGS_TABLEWEAK = 2,    // doesn't hold object alive; still must release
            MSHLFLAGS_NOPING = 4        // remote clients dont 'ping' to keep objects alive
        }

        internal enum STREAM_SEEK : uint
        {
            STREAM_SEEK_SET = 0,
            STREAM_SEEK_CUR = 1,
            STREAM_SEEK_END = 2
        }

        //
        // HRESULTs
        //
        internal const int S_OK = 0;
        internal const int S_FALSE = 0x00000001;
        internal const int E_FAIL = unchecked((int)0x80004005);
        internal const int E_OUTOFMEMORY = unchecked((int)0x8007000E);
        internal const int E_NOTIMPL = unchecked((int)0x80004001);
        internal const int E_NOINTERFACE = unchecked((int)0x80004002);
        internal const int E_INVALIDARG = unchecked((int)0x80070057);
        internal const int E_BOUNDS = unchecked((int)0x8000000B);
        internal const int E_POINTER = unchecked((int)0x80004003);
        internal const int E_CHANGED_STATE = unchecked((int)0x8000000C);
        internal const int COR_E_OBJECTDISPOSED = unchecked((int)0x80131622);
        internal const int RO_E_CLOSED = unchecked((int)0x80000013);

        internal const int TYPE_E_TYPEMISMATCH = unchecked((int)0x80028CA0);
        internal const int DISP_E_OVERFLOW = unchecked((int)0x8002000A);

        internal const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
        internal const int CLASS_E_CLASSNOTAVAILABLE = unchecked((int)0x80040111);

        /// <summary>
        /// Error indicates that you are accessing a CCW whose target object has already been garbage
        /// collected while the CCW still has non-0 jupiter ref counts
        /// </summary>
        internal const int COR_E_ACCESSING_CCW = unchecked((int)0x80131544);

#pragma warning disable 649, 169 // Field 'blah' is never assigned to/Field 'blah' is never used

        // I use __vtbl to distingush from MCG vtables that are used for CCWs
        internal struct __vtbl_IUnknown
        {
            // IUnknown methods
            internal IntPtr pfnQueryInterface;
            internal IntPtr pfnAddRef;
            internal IntPtr pfnRelease;
        }

        internal struct __vtbl_ISequentialStream
        {
            __vtbl_IUnknown parent;

            internal IntPtr pfnRead;
            internal IntPtr pfnWrite;
        }

        internal unsafe struct __IStream
        {
            internal __vtbl_IStream* vtbl;
        }

        internal struct __vtbl_IStream
        {
            __vtbl_ISequentialStream parent;

            internal IntPtr pfnSeek;
            internal IntPtr pfnSetSize;
            internal IntPtr pfnCopyTo;
            internal IntPtr pfnCommit;
            internal IntPtr pfnRevert;
            internal IntPtr pfnLockRegion;
            internal IntPtr pfnUnlockRegion;
            internal IntPtr pfnStat;
            internal IntPtr pfnClone;
        }

        internal unsafe struct __IMarshal
        {
            internal __vtbl_IMarshal* vtbl;
        }

        internal struct __vtbl_IMarshal
        {
            __vtbl_IUnknown parent;

            internal IntPtr pfnGetUnmarshalClass;
            internal IntPtr pfnGetMarshalSizeMax;
            internal IntPtr pfnMarshalInterface;
            internal IntPtr pfnUnmarshalInterface;
            internal IntPtr pfnReleaseMarshalData;
            internal IntPtr pfnDisconnectObject;
        }

        internal unsafe struct __IContextCallback
        {
            internal __vtbl_IContextCallback* vtbl;
        }

        internal struct __vtbl_IContextCallback
        {
            __vtbl_IUnknown parent;

            internal IntPtr pfnContextCallback;
        }

        internal struct ComCallData
        {
            internal uint dwDispid;
            internal uint dwReserved;
            internal IntPtr pUserDefined;
        }
#pragma warning restore 649, 169
    }
}
