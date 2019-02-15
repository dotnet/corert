// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Diagnostics.Tracing;

namespace Microsoft.Win32
{
#if !ES_BUILD_PN
    [SuppressUnmanagedCodeSecurityAttribute()]
#endif
    internal static class UnsafeNativeMethods
    {   
#if !ES_BUILD_PN
        [SuppressUnmanagedCodeSecurityAttribute()]
#endif
        internal static unsafe class ManifestEtw
        {
            //
            // Constants error coded returned by ETW APIs
            //

            // The event size is larger than the allowed maximum (64k - header).
            internal const int ERROR_ARITHMETIC_OVERFLOW = 534;

            // Occurs when filled buffers are trying to flush to disk, 
            // but disk IOs are not happening fast enough. 
            // This happens when the disk is slow and event traffic is heavy. 
            // Eventually, there are no more free (empty) buffers and the event is dropped.
            internal const int ERROR_NOT_ENOUGH_MEMORY = 8;

            internal const int ERROR_MORE_DATA = 0xEA;
            internal const int ERROR_NOT_SUPPORTED = 50;
            internal const int ERROR_INVALID_PARAMETER = 0x57;

            //
            // ETW Methods
            //

            internal const int EVENT_CONTROL_CODE_DISABLE_PROVIDER = 0;
            internal const int EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
            internal const int EVENT_CONTROL_CODE_CAPTURE_STATE = 2;

            //
            // Callback
            //
            internal unsafe delegate void EtwEnableCallback(
                [In] ref Guid sourceId,
                [In] int isEnabled,
                [In] byte level,
                [In] long matchAnyKeywords,
                [In] long matchAllKeywords,
                [In] EVENT_FILTER_DESCRIPTOR* filterData,
                [In] void* callbackContext
                );

            //
            // Registration APIs
            //
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventRegister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe uint EventRegister(
                        [In] ref Guid providerId,
                        [In]EtwEnableCallback enableCallback,
                        [In]void* callbackContext,
                        [In][Out] ref long registrationHandle
                        );

            // 
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventUnregister", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern uint EventUnregister([In] long registrationHandle);

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteString", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            internal static extern unsafe int EventWriteString(
                    [In] long registrationHandle,
                    [In] byte level,
                    [In] long keyword,
                    [In] string msg
                    );

            [StructLayout(LayoutKind.Sequential)]
            unsafe internal struct EVENT_FILTER_DESCRIPTOR
            {
                public long Ptr;
                public int Size;
                public int Type;
            };

            /// <summary>
            ///  Call the ETW native API EventWriteTransfer and checks for invalid argument error. 
            ///  The implementation of EventWriteTransfer on some older OSes (Windows 2008) does not accept null relatedActivityId.
            ///  So, for these cases we will retry the call with an empty Guid.
            /// </summary>
            internal static int EventWriteTransferWrapper(long registrationHandle,
                                                         ref EventDescriptor eventDescriptor,
                                                         Guid* activityId,
                                                         Guid* relatedActivityId,
                                                         int userDataCount,
                                                         EventProvider.EventData* userData)
            {
                int HResult = EventWriteTransfer(registrationHandle, ref eventDescriptor, activityId, relatedActivityId, userDataCount, userData);
                if (HResult == ERROR_INVALID_PARAMETER && relatedActivityId == null)
                {
                    Guid emptyGuid = Guid.Empty;
                    HResult = EventWriteTransfer(registrationHandle, ref eventDescriptor, activityId, &emptyGuid, userDataCount, userData);
                }

                return HResult;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventWriteTransfer", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
#if !ES_BUILD_PN
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
#endif
            private static extern int EventWriteTransfer(
                    [In] long registrationHandle,
                    [In] ref EventDescriptor eventDescriptor,
                    [In] Guid* activityId,
                    [In] Guid* relatedActivityId,
                    [In] int userDataCount,
                    [In] EventProvider.EventData* userData
                    );

            internal enum ActivityControl : uint
            {
                EVENT_ACTIVITY_CTRL_GET_ID = 1,
                EVENT_ACTIVITY_CTRL_SET_ID = 2,
                EVENT_ACTIVITY_CTRL_CREATE_ID = 3,
                EVENT_ACTIVITY_CTRL_GET_SET_ID = 4,
                EVENT_ACTIVITY_CTRL_CREATE_SET_ID = 5
            };

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventActivityIdControl", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
#if !ES_BUILD_PN
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
#endif
            internal static extern int EventActivityIdControl([In] ActivityControl ControlCode, [In][Out] ref Guid ActivityId);

            internal enum EVENT_INFO_CLASS
            {
                BinaryTrackInfo,
                SetEnableAllKeywords,
                SetTraits,
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EventSetInformation", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
#if !ES_BUILD_PN
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
#endif
            internal static extern int EventSetInformation(
                [In] long registrationHandle,
                [In] EVENT_INFO_CLASS informationClass,
                [In] void* eventInformation,
                [In] int informationLength);

            // Support for EnumerateTraceGuidsEx
            internal enum TRACE_QUERY_INFO_CLASS
            {
                TraceGuidQueryList,
                TraceGuidQueryInfo,
                TraceGuidQueryProcess,
                TraceStackTracingInfo,
                MaxTraceSetInfoClass
            };
#if PLATFORM_WINDOWS
            internal struct TRACE_GUID_INFO
            {
                public int InstanceCount;
                public int Reserved;
            };

            internal struct TRACE_PROVIDER_INSTANCE_INFO
            {
                public int NextOffset;
                public int EnableCount;
                public int Pid;
                public int Flags;
            };
#endif
#pragma warning disable CS0649
            internal struct TRACE_ENABLE_INFO
            {
                public int IsEnabled;
                public byte Level;
                public byte Reserved1;
                public ushort LoggerId;
                public int EnableProperty;
                public int Reserved2;
                public long MatchAnyKeyword;
                public long MatchAllKeyword;
            };
#pragma warning restore CS0649

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage")]
            [DllImport(Win32Native.ADVAPI32, ExactSpelling = true, EntryPoint = "EnumerateTraceGuidsEx", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
#if !ES_BUILD_PN
            [SuppressUnmanagedCodeSecurityAttribute]        // Don't do security checks 
#endif
            internal static extern int EnumerateTraceGuidsEx(
                TRACE_QUERY_INFO_CLASS TraceQueryInfoClass,
                void* InBuffer,
                int InBufferSize,
                void* OutBuffer,
                int OutBufferSize,
                ref int ReturnLength);
        }
    }

    internal static class Win32Native
    {
#if ES_BUILD_PCL
        private const string CoreProcessThreadsApiSet = "api-ms-win-core-processthreads-l1-1-0";
        private const string CoreLocalizationApiSet = "api-ms-win-core-localization-l1-2-0";
#else
        private const string CoreProcessThreadsApiSet = "kernel32.dll";
        private const string CoreLocalizationApiSet = "kernel32.dll";
#endif

        internal const string KERNEL32 = "kernel32.dll";
        internal const string ADVAPI32 = "advapi32.dll";

        [System.Security.SecuritySafeCritical]
        // Gets an error message for a Win32 error code.
        internal static string GetMessage(int errorCode)
        {
#if FEATURE_MANAGED_ETW
            return Interop.Kernel32.GetMessage(errorCode);
#else
            return "ErrorCode: " + errorCode;
#endif // FEATURE_MANAGED_ETW
        }

        private const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        private const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        private const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;
    }
}

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern uint GetCurrentProcessId();
    }
}
