// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
//  System.Private.CoreLib cannot directly interop with WinRT because the interop DLL depends on System.Private.CoreLib which causes circular dependency. 
//  To enable System.Private.CoreLib to call WinRT, we do have another assembly System.Private.WinRTInterop.CoreLib.dll which does the interop with WinRT 
//  and to allow System.Private.CoreLib to call System.Private.WinRTInterop.CoreLib we do the following trick
//      o   RmtGen tool will inject code WinRT.Initialize() to the app before the app Main method while building it 
//      o   the injected code will just call the System.Private.CoreLib code Internal.Runtime.Augments.Initialize and pass the interface 
//          WinRTInteropCallbacks which System.Private.CoreLib can call to interact with the WinRT
//

using System;

namespace Internal.Runtime.Augments
{
    public static class WinRTInterop
    {
        [CLSCompliant(false)]
        public static void Initialize(WinRTInteropCallbacks winRTCallback)
        {
            if (s_winRTCallback != null)
            {
                throw new InvalidOperationException(SR.InvalidOperation_Calling);
            }
            s_winRTCallback = winRTCallback;
        }

        [CLSCompliant(false)]
        public static WinRTInteropCallbacks Callbacks
        {
            get
            {
                if (s_winRTCallback == null)
                {
                    // We cannot use a localized exception message here because we depend on WinRT to get the resource
                    // string and here WinRT is not initialized yet.
                    throw new InvalidOperationException(c_EarlyCallingExceptionMessage);
                }

                return s_winRTCallback;
            }
        }

        //
        // This non throw version of the Callbacks property as we got some code runs early enough 
        // which can cause a problem if we throw exception.
        // This property shouldn't be used in other cases and instead Callbacks should be used
        //
        internal static WinRTInteropCallbacks UnsafeCallbacks
        {
            get { return s_winRTCallback; }
        }

        private static WinRTInteropCallbacks s_winRTCallback;
        private const string c_EarlyCallingExceptionMessage = "WinRT Interop has not been initialized yet. If trying to access something in a static variable initialization or static constructor try to do this work lazily on first use instead.";
    }

    [CLSCompliant(false)]
    public abstract class WinRTInteropCallbacks
    {
        public abstract int GetJapaneseEraCount();
        public abstract bool GetJapaneseEraInfo(int era, out DateTimeOffset startDate, out string eraName, out string abbreviatedEraName);
        public abstract int GetHijriDateAdjustment();
        public abstract string GetLanguageDisplayName(string cultureName);
        public abstract string GetRegionDisplayName(string isoCountryCode);
        public abstract Object GetUserDefaultCulture();
        public abstract void SetGlobalDefaultCulture(Object culture);
        public abstract Object GetCurrentWinRTDispatcher();
        public abstract string GetFolderPath(Environment.SpecialFolder specialFolder, Environment.SpecialFolderOption specialFolderOption);
        public abstract void PostToWinRTDispatcher(Object dispatcher, Action<object> action, object state);
        public abstract bool IsAppxModel();
        public abstract bool ReportUnhandledError(Exception ex);
        public abstract void SetCOMWeakReferenceTarget(object weakReference, object target);
        public abstract object GetCOMWeakReferenceTarget(object weakReference);
        public abstract object ReadFileIntoStream(string name);
        public abstract void InitTracingStatusChanged(Action<bool> tracingStatusChanged);
        public abstract void TraceOperationCompletion(int traceLevel, int source, Guid platformId, ulong operationId, int status);
        public abstract void TraceOperationCreation(int traceLevel, int source, Guid platformId, ulong operationId, string operationName, ulong relatedContext);
        public abstract void TraceOperationRelation(int traceLevel, int source, Guid platformId, ulong operationId, int relation);
        public abstract void TraceSynchronousWorkCompletion(int traceLevel, int source, int work);
        public abstract void TraceSynchronousWorkStart(int traceLevel, int source, Guid platformId, ulong operationId, int work);
    }
}
