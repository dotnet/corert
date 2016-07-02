// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Diagnostics;
using Internal.Runtime.Augments;

namespace System
{
    public class Exception
    {
        private void Init()
        {
            _message = null;
            HResult = __HResults.COR_E_EXCEPTION;
        }

        public Exception()
        {
            Init();
        }

        public Exception(String message)
        {
            Init();
            _message = message;
        }

        // Creates a new Exception.  All derived classes should 
        // provide this constructor.
        // Note: the stack trace is not started until the exception 
        // is thrown
        // 
        public Exception(String message, Exception innerException)
        {
            Init();
            _message = message;
            _innerException = innerException;
        }


        public virtual String Message
        {
            get
            {
                if (_message == null)
                {
                    String className = GetClassName();
                    return SR.Format(SR.Exception_WasThrown, className);
                }
                else
                {
                    return _message;
                }
            }
        }

        public virtual IDictionary Data
        {
            get
            {
                if (_data == null)
                    _data = new ListDictionaryInternal();

                return _data;
            }
        }

        #region Interop Helpers

        internal class __RestrictedErrorObject
        {
            private object _realErrorObject;

            internal __RestrictedErrorObject(object errorObject)
            {
                _realErrorObject = errorObject;
            }

            public object RealErrorObject
            {
                get
                {
                    return _realErrorObject;
                }
            }
        }

        internal void AddExceptionDataForRestrictedErrorInfo(
            string restrictedError,
            string restrictedErrorReference,
            string restrictedCapabilitySid,
            object restrictedErrorObject)
        {
            IDictionary dict = Data;
            if (dict != null)
            {
                dict.Add("RestrictedDescription", restrictedError);
                dict.Add("RestrictedErrorReference", restrictedErrorReference);
                dict.Add("RestrictedCapabilitySid", restrictedCapabilitySid);

                // Keep the error object alive so that user could retrieve error information
                // using Data["RestrictedErrorReference"]
                dict.Add("__RestrictedErrorObject", (restrictedErrorObject == null ? null : new __RestrictedErrorObject(restrictedErrorObject)));
            }
        }

        internal bool TryGetRestrictedErrorObject(out object restrictedErrorObject)
        {
            restrictedErrorObject = null;
            if (Data != null && Data.Contains("__RestrictedErrorObject"))
            {
                __RestrictedErrorObject restrictedObject = Data["__RestrictedErrorObject"] as __RestrictedErrorObject;
                if (restrictedObject != null)
                {
                    restrictedErrorObject = restrictedObject.RealErrorObject;
                    return true;
                }
            }

            return false;
        }

        internal bool TryGetRestrictedErrorDetails(out string restrictedError, out string restrictedErrorReference, out string restrictedCapabilitySid)
        {
            if (Data != null && Data.Contains("__RestrictedErrorObject"))
            {
                // We might not need to store this value any more.
                restrictedError = (string)Data["RestrictedDescription"];
                restrictedErrorReference = (string)Data["restrictedErrorReference"];
                restrictedCapabilitySid = (string)Data["RestrictedCapabilitySid"];
                return true;
            }
            else
            {
                restrictedError = null;
                restrictedErrorReference = null;
                restrictedCapabilitySid = null;
                return false;
            }
        }

        /// <summary>
        /// Allow System.Private.Interop to set HRESULT of an exception
        /// </summary>
        internal void SetErrorCode(int hr)
        {
            HResult = hr;
        }

        /// <summary>
        /// Allow System.Private.Interop to set message of an exception
        /// </summary>
        internal void SetMessage(string msg)
        {
            _message = msg;
        }

        #endregion

        private string GetClassName()
        {
            Type thisType = this.GetType();
            ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
            if (callbacks != null)
            {
                String preferredString = callbacks.GetBetterDiagnosticInfoIfAvailable(thisType.TypeHandle);
                if (preferredString != null)
                    return preferredString;
            }

            // If all else fails, fall back to the classic GetType().ToString() behavior (which will likely
            // provide an unfriendly-looking string on Project N.)
            return thisType.ToString();
        }



        // Retrieves the lowest exception (inner most) for the given Exception.
        // This will traverse exceptions using the innerException property.
        //
        public virtual Exception GetBaseException()
        {
            Exception inner = InnerException;
            Exception back = this;

            while (inner != null)
            {
                back = inner;
                inner = inner.InnerException;
            }

            return back;
        }

        // Returns the inner exception contained in this exception
        // 
        public Exception InnerException
        {
            get { return _innerException; }
        }


        private string GetStackTrace(bool needFileInfo)
        {
            return this.StackTrace;
        }

        public virtual String HelpLink
        {
            get
            {
                return _helpURL;
            }

            set
            {
                _helpURL = value;
            }
        }

        public virtual String Source
        {
            get
            {
                if (_source == null && HasBeenThrown)
                {
                    _source = "<unknown>";
                }
                return _source;
            }
            set
            {
                _source = value;
            }
        }

        public override String ToString()
        {
            return ToString(true, true);
        }

        private String ToString(bool needFileLineInfo, bool needMessage)
        {
            String message = (needMessage ? Message : null);
            String s;

            if (message == null || message.Length <= 0)
            {
                s = GetClassName();
            }
            else
            {
                s = GetClassName() + ": " + message;
            }

            if (_innerException != null)
            {
                s = s + " ---> " + _innerException.ToString(needFileLineInfo, needMessage) + Environment.NewLine +
                "   " + SR.Exception_EndOfInnerExceptionStack;
            }

            string stackTrace = GetStackTrace(needFileLineInfo);
            if (stackTrace != null)
            {
                s += Environment.NewLine + stackTrace;
            }

            return s;
        }

        // WARNING: We allow diagnostic tools to directly inspect these three members (_message, _innerException and _HResult)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.
        internal String _message;
        private IDictionary _data;
        private Exception _innerException;
        private String _helpURL;
        private String _source;         // Mainly used by VB. 

        private int _HResult;     // HResult

        public int HResult
        {
            get { return _HResult; }
            protected set { _HResult = value; }
        }

        // Returns the stack trace as a string.  If no stack trace is
        // available, null is returned.
        public virtual String StackTrace
        {
            get
            {
                if (!HasBeenThrown)
                    return null;

                return StackTraceHelper.FormatStackTrace(GetStackIPs(), true);
            }
        }

        internal IntPtr[] GetStackIPs()
        {
            IntPtr[] ips = new IntPtr[_idxFirstFreeStackTraceEntry];
            if (_corDbgStackTrace != null)
            {
                Array.Copy(_corDbgStackTrace, ips, ips.Length);
            }
            return ips;
        }

        // WARNING: We allow diagnostic tools to directly inspect these two members (_corDbgStackTrace and _idxFirstFreeStackTraceEntry)
        // See https://github.com/dotnet/corert/blob/master/Documentation/design-docs/diagnostics/diagnostics-tools-contract.md for more details. 
        // Please do not change the type, the name, or the semantic usage of this member without understanding the implication for tools. 
        // Get in touch with the diagnostics team if you have questions.

        // _corDbgStackTrace: Do not rename: This is for the use of the CorDbg interface. Contains the stack trace as an array of EIP's (ordered from
        // most nested call to least.) May also include a few "special" IP's from the SpecialIP class:
        private IntPtr[] _corDbgStackTrace;
        private int _idxFirstFreeStackTraceEntry;

        private void AppendStackFrame(IntPtr IP, bool isFirstRethrowFrame)
        {
            if (this is OutOfMemoryException)
                return;  // Allocating arrays in an OOM situation might be counterproductive...

            if (_idxFirstFreeStackTraceEntry == 0)
            {
                _corDbgStackTrace = new IntPtr[16];
            }
            else if (isFirstRethrowFrame)
            {
                // For the first frame after rethrow, we replace the last entry in the stack trace with the IP
                // of the rethrow.  This is overwriting the IP of where control left the corresponding try 
                // region for the catch that is rethrowing.
                _corDbgStackTrace[_idxFirstFreeStackTraceEntry - 1] = IP;
                return;
            }

            if (_idxFirstFreeStackTraceEntry >= _corDbgStackTrace.Length)
                GrowStackTrace();

            _corDbgStackTrace[_idxFirstFreeStackTraceEntry++] = IP;
            return;
        }

        private void GrowStackTrace()
        {
            IntPtr[] newArray = new IntPtr[_corDbgStackTrace.Length * 2];
            for (int i = 0; i < _corDbgStackTrace.Length; i++)
            {
                newArray[i] = _corDbgStackTrace[i];
            }
            _corDbgStackTrace = newArray;
        }

        private bool HasBeenThrown
        {
            get
            {
                return _idxFirstFreeStackTraceEntry != 0;
            }
        }

        private enum RhEHFrameType
        {
            RH_EH_FIRST_FRAME = 1,
            RH_EH_FIRST_RETHROW_FRAME = 2,
        }

        [RuntimeExport("AppendExceptionStackFrame")]
        private static void AppendExceptionStackFrame(Exception ex, IntPtr IP, int flags)
        {
            // This method is called by the runtime's EH dispatch code and is not allowed to leak exceptions
            // back into the dispatcher.
            try
            {
                bool isFirstFrame = (flags & (int)RhEHFrameType.RH_EH_FIRST_FRAME) != 0;
                bool isFirstRethrowFrame = (flags & (int)RhEHFrameType.RH_EH_FIRST_RETHROW_FRAME) != 0;

                ex.AppendStackFrame(IP, isFirstRethrowFrame);
                if (isFirstFrame)
                {
                    unsafe
                    {
                        fixed (char* exceptionTypeName = ex.GetType().ToString())
                        fixed (char* exceptionMessage = ex.Message)
                            RuntimeImports.RhpEtwExceptionThrown(exceptionTypeName, exceptionMessage, IP, ex.HResult);
                    }
                }
            }
            catch
            {
                // We may end up with a confusing stack trace or a confusing ETW trace log, but at least we
                // can continue to dispatch this exception.
            }
        }

        //==================================================================================================================
        // Support for ExceptionDispatchInfo class - imports and exports the stack trace.
        //==================================================================================================================

        internal EdiCaptureState CaptureEdiState()
        {
            IntPtr[] stackTrace = _corDbgStackTrace;
            if (stackTrace != null)
            {
                IntPtr[] newStackTrace = new IntPtr[stackTrace.Length];
                Array.Copy(stackTrace, 0, newStackTrace, 0, stackTrace.Length);
                stackTrace = newStackTrace;
            }
            return new EdiCaptureState() { StackTrace = stackTrace };
        }

        internal void RestoreEdiState(EdiCaptureState ediCaptureState)
        {
            IntPtr[] stackTrace = ediCaptureState.StackTrace;
            int idxFirstFreeStackTraceEntry = 0;
            if (stackTrace != null)
            {
                IntPtr[] newStackTrace = new IntPtr[stackTrace.Length + 1];
                Array.Copy(stackTrace, 0, newStackTrace, 0, stackTrace.Length);
                stackTrace = newStackTrace;
                while (stackTrace[idxFirstFreeStackTraceEntry] != (IntPtr)0)
                    idxFirstFreeStackTraceEntry++;
                stackTrace[idxFirstFreeStackTraceEntry++] = StackTraceHelper.SpecialIP.EdiSeparator;
            }

            // Since EDI can be created at various points during exception dispatch (e.g. at various frames on the stack) for the same exception instance,
            // they can have different data to be restored. Thus, to ensure atomicity of restoration from each EDI, perform the restore under a lock.
            lock (s_EDILock)
            {
                _corDbgStackTrace = stackTrace;
                _idxFirstFreeStackTraceEntry = idxFirstFreeStackTraceEntry;
            }
        }

        internal struct EdiCaptureState
        {
            public IntPtr[] StackTrace;
        }

        // This is the object against which a lock will be taken
        // when attempt to restore the EDI. Since its static, its possible
        // that unrelated exception object restorations could get blocked
        // for a small duration but that sounds reasonable considering
        // such scenarios are going to be extremely rare, where timing
        // matches precisely.
        private static Object s_EDILock = new Object();

        /// <summary>
        /// This is the binary format for serialized exceptions that get saved into a special buffer that is
        /// known to WER (by way of a runtime API) and will be saved into triage dumps.  This format is known
        /// to SOS, so any changes must update CurrentSerializationVersion and have corresponding updates to 
        /// SOS.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SERIALIZED_EXCEPTION_HEADER
        {
            internal IntPtr ExceptionEEType;
            internal Int32 HResult;
            internal Int32 StackTraceElementCount;
            // IntPtr * N  : StackTrace elements
        }
        internal const int CurrentSerializationSignature = 0x31305845;  // 'EX01'

        /// <summary>
        /// This method performs the serialization of one Exception object into the returned byte[].  
        /// </summary>
        internal unsafe byte[] SerializeForDump()
        {
            checked
            {
                int nStackTraceElements = _idxFirstFreeStackTraceEntry;
                int cbBuffer = sizeof(SERIALIZED_EXCEPTION_HEADER) + (nStackTraceElements * IntPtr.Size);

                byte[] buffer = new byte[cbBuffer];
                fixed (byte* pBuffer = buffer)
                {
                    SERIALIZED_EXCEPTION_HEADER* pHeader = (SERIALIZED_EXCEPTION_HEADER*)pBuffer;
                    pHeader->HResult = _HResult;
                    pHeader->ExceptionEEType = m_pEEType;
                    pHeader->StackTraceElementCount = nStackTraceElements;
                    IntPtr* pStackTraceElements = (IntPtr*)(pBuffer + sizeof(SERIALIZED_EXCEPTION_HEADER));
                    for (int i = 0; i < nStackTraceElements; i++)
                    {
                        pStackTraceElements[i] = _corDbgStackTrace[i];
                    }
                }

                return buffer;
            }
        }
    }
}
