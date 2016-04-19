// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Diagnostics.Contracts;
using Internal.Runtime.Augments;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// This class has all the helpers which are needed to provide the Exception support for WinRT and ClassicCOM
    /// </summary>
    public unsafe static partial class ExceptionHelpers
    {
        /// <summary>
        ///  This class is a helper class to call into IRestrictedErrorInfo methods.
        /// </summary>
        private static class RestrictedErrorInfoHelper
        {
            internal static bool GetErrorDetails(System.IntPtr pRestrictedErrorInfo, out string errMsg, out int hr, out string resErrMsg, out string errCapSid)
            {
                Contract.Assert(pRestrictedErrorInfo != IntPtr.Zero);
                IntPtr pErrDes, pResErrDes, pErrCapSid;

                pErrDes = pResErrDes = pErrCapSid = IntPtr.Zero;
                int result;
                try
                {
                    // Get the errorDetails associated with the restrictedErrorInfo.
                    __com_IRestrictedErrorInfo* pComRestrictedErrorInfo = (__com_IRestrictedErrorInfo*)pRestrictedErrorInfo;
                    result = CalliIntrinsics.StdCall<int>(
                       pComRestrictedErrorInfo->pVtable->pfnGetErrorDetails,
                       pRestrictedErrorInfo,
                       out pErrDes,
                       out hr,
                       out pResErrDes,
                       out pErrCapSid);

                    if (result >= 0)
                    {
                        // RestrictedErrorInfo details can be used since the pRestrictedErrorInfo has the same hr value as the hr returned by the native code.
                        errMsg = Interop.COM.ConvertBSTRToString(pErrDes);
                        resErrMsg = Interop.COM.ConvertBSTRToString(pResErrDes);
                        errCapSid = Interop.COM.ConvertBSTRToString(pErrCapSid);
                    }
                    else
                    {
                        errMsg = resErrMsg = errCapSid = null;
                        hr = 0;
                    }
                }
                finally
                {
                    if (pErrDes != IntPtr.Zero)
                        ExternalInterop.SysFreeString(pErrDes);
                    if (pResErrDes != IntPtr.Zero)
                        ExternalInterop.SysFreeString(pResErrDes);
                    if (pErrCapSid != IntPtr.Zero)
                        ExternalInterop.SysFreeString(pErrCapSid);
                }

                return result >= 0;
            }

            internal static void GetReference(System.IntPtr pRestrictedErrorInfo, out string errReference)
            {
                Contract.Assert(pRestrictedErrorInfo != IntPtr.Zero);
                IntPtr pReference = IntPtr.Zero;
                errReference = null;

                try
                {
                    __com_IRestrictedErrorInfo* pComRestrictedErrorInfo = (__com_IRestrictedErrorInfo*)pRestrictedErrorInfo;
                    int result = CalliIntrinsics.StdCall<int>(pComRestrictedErrorInfo->pVtable->pfnGetReference, pRestrictedErrorInfo, out pReference);
                    if (result >= 0)
                    {
                        errReference = Interop.COM.ConvertBSTRToString(pReference);
                    }
                    else
                    {
                        errReference = null;
                    }
                }
                finally
                {
                    if (pReference != IntPtr.Zero)
                        ExternalInterop.SysFreeString(pReference);
                }
            }
        }

        /// <summary>
        /// The method calls RoOriginateLanguageException. The method has all the logic in try, catch block to ensure that none of the exception helpers
        /// throw exception themselves.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        private static bool OriginateLanguageException(Exception ex)
        {
            IntPtr pUnk = IntPtr.Zero;
            HSTRING errorMsg = default(HSTRING);
            try
            {
                pUnk = McgMarshal.ObjectToComInterface(ex, InternalTypes.IUnknown);
                if (pUnk != IntPtr.Zero)
                {
                    RuntimeAugments.GenerateExceptionInformationForDump(ex, pUnk);

                    errorMsg = McgMarshal.StringToHString(ex.Message);

                    return ExternalInterop.RoOriginateLanguageException(ex.HResult, errorMsg, pUnk) >= 0;
                }
            }
            catch (Exception)
            {
                // We can't do anything here and hence simply swallow the exception
            }
            finally
            {
                McgMarshal.ComSafeRelease(pUnk);
                if (errorMsg.handle != IntPtr.Zero)
                {
                    ExternalInterop.WindowsDeleteString(errorMsg.handle.ToPointer());
                }
            }
            return false;
        }

#pragma warning disable 649, 169  // Field 'blah' is never assigned to/Field 'blah' is never used

        // Lets create vTables for these interfaces.
        private unsafe struct __com_ILanguageExceptionErrorInfo
        {
            internal __vtable_ILanguageExceptionErrorInfo* pVtable;
        }

        private unsafe struct __vtable_ILanguageExceptionErrorInfo
        {
            private IntPtr pfnQueryInterface;
            private IntPtr pfnAddRef;
            private IntPtr pfnRelease;
            internal System.IntPtr pfnGetLanguageException;
        }

        internal unsafe struct __com_IRestrictedErrorInfo
        {
            internal __vtable_IRestrictedErrorInfo* pVtable;
        }

        internal unsafe struct __vtable_IRestrictedErrorInfo
        {
            private IntPtr pfnQueryInterface;
            private IntPtr pfnAddRef;
            private IntPtr pfnRelease;
            internal System.IntPtr pfnGetErrorDetails;
            internal System.IntPtr pfnGetReference;
        }

#pragma warning restore 649, 169

        /// <summary>
        /// This method gets the mapping hr for the exception. and also does the right thing to propogate the hr correctly to the native layer.
        ///
        /// We check if the exception is a pure managed exception or an exception created from an hr that entered the system from native.
        /// a. If it is a pure managed exception we create an IUnknown ptr from the exception and RoOriginateLanguageException on it.
        ///    This helps us to preserve our managed exception and throw the same exception in case this exception roundtrips and hence preserve the call stack.
        ///    Since the API RoOriginateLanguageException is available only on windows blue, we can't do the same in win8. In desktop CLR we use the non-modern SDK API
        ///    GetErroInfo\SetErrorInfo combination to preserve managed exception but unfortunately we can't do this in .NET Native and hence we only our able to preserve the exception message and
        ///    type and end up getting a rough stacktrace PS - Even this behavior in win8 is possible only in debug mode as RoSetErrorReportingFlags is set to UseSetErrorInfo only in debug mode.
        ///
        /// b. In case the exception is created due to an hr that entered managed world via native call, we will have restrictederrorInfo associated with it. In this case
        ///    we do not RoOriginateLanguageException\RoOriginateError and rather preserve the exception stack trace by simply calling the SetRestrictedErrorInfo.
        ///
        /// c. PS - Due to the use of modern SDK we have no way to round trip exceptions in classicCOM scenarios any more.
        ///     This is because we can't use SetErrorInfo\GetErrorInfo APIs at all. Unfortunately we have no workaround for this even in windowsBlue!
        ///    With the use of IRestrictedErrorInfo has some disadvantages as we lose other info available with IErrorInfo in terms of HelpFile etc.
        ///
        /// d. This class puts all the logic in try, catch block to ensure that none of the exception helpers.
        ///  throw exception themselves.
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="isWinRTScenario"></param>
        /// <returns></returns>
        internal static int GetHRForExceptionWithErrorPropogationNoThrow(Exception ex, bool isWinRTScenario)
        {
            int hr = ex.HResult;

            if (hr == Interop.COM.COR_E_OBJECTDISPOSED && isWinRTScenario)
            {
                // Since ObjectDisposedException is projected to RO_E_CLOSED in WINRT we make sure to use the correct hr while updating the CRuntimeError object of Windows.
                hr = Interop.COM.RO_E_CLOSED;
            }

            try
            {
                // Check whether the exception has an associated RestrictedErrorInfo associated with it.
                if (isWinRTScenario)
                {
                    IntPtr pRestrictedErrorInfo;
                    object restrictedErrorInfo;
                    if (InteropExtensions.TryGetRestrictedErrorObject(ex, out restrictedErrorInfo) && restrictedErrorInfo != null)
                    {
                        // We have the restricted errorInfo associated with this object and hence this exception was created by an hr entering managed through native.
                        pRestrictedErrorInfo = McgMarshal.ObjectToComInterface(restrictedErrorInfo, InternalTypes.IRestrictedErrorInfo);
                        if (pRestrictedErrorInfo != IntPtr.Zero)
                        {
                            // We simply call SetRestrictedErrorInfo since we do not want to originate the exception again.
                            ExternalInterop.SetRestrictedErrorInfo(pRestrictedErrorInfo);
                            McgMarshal.ComSafeRelease(pRestrictedErrorInfo);
                        }
                    }
                    else
                    {
                        // we are in windows blue and hence we can preserve our exception so that we can reuse this exception in case it comes back and provide richer exception support.
                        OriginateLanguageException(ex);
                    }
                }
                else
                {
                    // We are either pre WinBlue or in classicCOM scenario and hence we can only RoOriginateError at this point.
                    // Desktop CLR uses SetErrorInfo and preserves the exception object which helps us give the same support as winBlue.
                    // Since .NET Native can only use modern SDK we have a compatibility break here by only preserving the restrictederrorMsg and exception type but the stack trace will be incorrect.

                    // Also RoOriginateError works only under the debugger since RoSetErrorReportingFlags is set to RO_ERROR_REPORTING_USESETERRORINFO.
                    // If we are not under the debugger we can't set this API since it is not part of the modernSDK and hence this will not work
                    // and will result in different behavior than the desktop.
                    HSTRING errorMsg = McgMarshal.StringToHString(ex.Message);
                    ExternalInterop.RoOriginateError(ex.HResult, errorMsg);
                    ExternalInterop.WindowsDeleteString(errorMsg.handle.ToPointer());
                }
            }
            catch (Exception)
            {
                // We can't throw an exception here and hence simply swallow it.
            }

            return hr;
        }

        /// <summary>
        /// This does a mapping from hr to the exception and also takes care of making default exception in case of classic COM as COMException.
        /// and in winrt and marshal APIs as Exception.
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="message"></param>
        /// <param name="createCOMException"></param>
        /// <returns></returns>
        internal static Exception GetMappingExceptionForHR(int errorCode, string message, bool createCOMException, bool hasErrorInfo)
        {
            if (errorCode >= 0)
            {
                return null;
            }

            Exception exception = null;

            bool shouldDisplayHR = false;

            switch (errorCode)
            {
                case __HResults.COR_E_NOTFINITENUMBER: // NotFiniteNumberException
                case __HResults.COR_E_ARITHMETIC:
                    exception = new ArithmeticException();
                    break;
                case __HResults.COR_E_ARGUMENT:
                case unchecked((int)0x800A01C1):
                case unchecked((int)0x800A01C2):
                case __HResults.CLR_E_BIND_UNRECOGNIZED_IDENTITY_FORMAT:
                    exception = new ArgumentException();

                    if (errorCode != __HResults.COR_E_ARGUMENT)
                        shouldDisplayHR = true;

                    break;
                case __HResults.E_BOUNDS:
                case __HResults.COR_E_ARGUMENTOUTOFRANGE:
                case __HResults.ERROR_NO_UNICODE_TRANSLATION:
                    exception = new ArgumentOutOfRangeException();

                    if (errorCode != __HResults.COR_E_ARGUMENTOUTOFRANGE)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_ARRAYTYPEMISMATCH:
                    exception = new ArrayTypeMismatchException();
                    break;
                case __HResults.COR_E_BADIMAGEFORMAT:
                case __HResults.CLDB_E_FILE_OLDVER:
                case __HResults.CLDB_E_INDEX_NOTFOUND:
                case __HResults.CLDB_E_FILE_CORRUPT:
                case __HResults.COR_E_NEWER_RUNTIME:
                case __HResults.COR_E_ASSEMBLYEXPECTED:
                case __HResults.ERROR_BAD_EXE_FORMAT:
                case __HResults.ERROR_EXE_MARKED_INVALID:
                case __HResults.CORSEC_E_INVALID_IMAGE_FORMAT:
                case __HResults.ERROR_NOACCESS:
                case __HResults.ERROR_INVALID_ORDINAL:
                case __HResults.ERROR_INVALID_DLL:
                case __HResults.ERROR_FILE_CORRUPT:
                case __HResults.COR_E_LOADING_REFERENCE_ASSEMBLY:
                case __HResults.META_E_BAD_SIGNATURE:
                    exception = new BadImageFormatException();

                    // Always show HR for BadImageFormatException
                    shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_CUSTOMATTRIBUTEFORMAT:
                    exception = new FormatException();
                    break; // CustomAttributeFormatException
                case __HResults.COR_E_DATAMISALIGNED:
                    exception = InteropExtensions.CreateDataMisalignedException(message); // TODO: Do we need to add msg here?
                    break;
                case __HResults.COR_E_DIVIDEBYZERO:
                case __HResults.CTL_E_DIVISIONBYZERO:
                    exception = new DivideByZeroException();

                    if (errorCode != __HResults.COR_E_DIVIDEBYZERO)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_DLLNOTFOUND:
#if ENABLE_WINRT
                    exception = new DllNotFoundException();
#endif
                    break;
                case __HResults.COR_E_DUPLICATEWAITOBJECT:
                    exception = new ArgumentException();
                    break; // DuplicateWaitObjectException
                case __HResults.COR_E_ENDOFSTREAM:
                case unchecked((int)0x800A003E):
                    exception = new System.IO.EndOfStreamException();

                    if (errorCode != __HResults.COR_E_ENDOFSTREAM)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_TYPEACCESS: // TypeAccessException
                case __HResults.COR_E_ENTRYPOINTNOTFOUND:
                    exception = new TypeLoadException();

                    break; // EntryPointNotFoundException
                case __HResults.COR_E_EXCEPTION:
                    exception = new Exception();
                    break;
                case __HResults.COR_E_DIRECTORYNOTFOUND:
                case __HResults.STG_E_PATHNOTFOUND:
                case __HResults.CTL_E_PATHNOTFOUND:
                    exception = new System.IO.DirectoryNotFoundException();

                    if (errorCode != __HResults.COR_E_DIRECTORYNOTFOUND)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_FILELOAD:
                case __HResults.FUSION_E_INVALID_PRIVATE_ASM_LOCATION:
                case __HResults.FUSION_E_SIGNATURE_CHECK_FAILED:
                case __HResults.FUSION_E_LOADFROM_BLOCKED:
                case __HResults.FUSION_E_CACHEFILE_FAILED:
                case __HResults.FUSION_E_ASM_MODULE_MISSING:
                case __HResults.FUSION_E_INVALID_NAME:
                case __HResults.FUSION_E_PRIVATE_ASM_DISALLOWED:
                case __HResults.FUSION_E_HOST_GAC_ASM_MISMATCH:
                case __HResults.COR_E_MODULE_HASH_CHECK_FAILED:
                case __HResults.FUSION_E_REF_DEF_MISMATCH:
                case __HResults.SECURITY_E_INCOMPATIBLE_SHARE:
                case __HResults.SECURITY_E_INCOMPATIBLE_EVIDENCE:
                case __HResults.SECURITY_E_UNVERIFIABLE:
                case __HResults.COR_E_FIXUPSINEXE:
                case __HResults.ERROR_TOO_MANY_OPEN_FILES:
                case __HResults.ERROR_SHARING_VIOLATION:
                case __HResults.ERROR_LOCK_VIOLATION:
                case __HResults.ERROR_OPEN_FAILED:
                case __HResults.ERROR_DISK_CORRUPT:
                case __HResults.ERROR_UNRECOGNIZED_VOLUME:
                case __HResults.ERROR_DLL_INIT_FAILED:
                case __HResults.FUSION_E_CODE_DOWNLOAD_DISABLED:
                case __HResults.CORSEC_E_MISSING_STRONGNAME:
                case __HResults.MSEE_E_ASSEMBLYLOADINPROGRESS:
                case __HResults.ERROR_FILE_INVALID:
                    exception = new System.IO.FileLoadException();

                    shouldDisplayHR = true;
                    break;
                case __HResults.COR_E_PATHTOOLONG:
                    exception = new System.IO.PathTooLongException();
                    break;
                case __HResults.COR_E_IO:
                case __HResults.CTL_E_DEVICEIOERROR:
                case unchecked((int)0x800A793C):
                case unchecked((int)0x800A793D):
                    exception = new System.IO.IOException();

                    if (errorCode != __HResults.COR_E_IO)
                        shouldDisplayHR = true;

                    break;
                case __HResults.ERROR_FILE_NOT_FOUND:
                case __HResults.ERROR_MOD_NOT_FOUND:
                case __HResults.ERROR_INVALID_NAME:
                case __HResults.CTL_E_FILENOTFOUND:
                case __HResults.ERROR_BAD_NET_NAME:
                case __HResults.ERROR_BAD_NETPATH:
                case __HResults.ERROR_NOT_READY:
                case __HResults.ERROR_WRONG_TARGET_NAME:
                case __HResults.INET_E_UNKNOWN_PROTOCOL:
                case __HResults.INET_E_CONNECTION_TIMEOUT:
                case __HResults.INET_E_CANNOT_CONNECT:
                case __HResults.INET_E_RESOURCE_NOT_FOUND:
                case __HResults.INET_E_OBJECT_NOT_FOUND:
                case __HResults.INET_E_DOWNLOAD_FAILURE:
                case __HResults.INET_E_DATA_NOT_AVAILABLE:
                case __HResults.ERROR_DLL_NOT_FOUND:
                case __HResults.CLR_E_BIND_ASSEMBLY_VERSION_TOO_LOW:
                case __HResults.CLR_E_BIND_ASSEMBLY_PUBLIC_KEY_MISMATCH:
                case __HResults.CLR_E_BIND_ASSEMBLY_NOT_FOUND:
                    exception = new System.IO.FileNotFoundException();

                    shouldDisplayHR = true;
                    break;
                case __HResults.COR_E_FORMAT:
                    exception = new FormatException();
                    break;
                case __HResults.COR_E_INDEXOUTOFRANGE:
                case unchecked((int)0x800a0009):
                    exception = new IndexOutOfRangeException();

                    if (errorCode != __HResults.COR_E_INDEXOUTOFRANGE)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_INVALIDCAST:
                    exception = new InvalidCastException();
                    break;
                case __HResults.COR_E_INVALIDCOMOBJECT:
                    exception = new InvalidComObjectException();
                    break;
                case __HResults.COR_E_INVALIDOLEVARIANTTYPE:
                    exception = new InvalidOleVariantTypeException();
                    break;
                case __HResults.COR_E_INVALIDOPERATION:
                case __HResults.E_ILLEGAL_STATE_CHANGE:
                case __HResults.E_ILLEGAL_METHOD_CALL:
                case __HResults.E_ILLEGAL_DELEGATE_ASSIGNMENT:
                case __HResults.APPMODEL_ERROR_NO_PACKAGE:
                    exception = new InvalidOperationException();

                    if (errorCode != __HResults.COR_E_INVALIDOPERATION)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_MARSHALDIRECTIVE:
                    exception = new MarshalDirectiveException();
                    break;
                case __HResults.COR_E_METHODACCESS: // MethodAccessException
                case __HResults.META_E_CA_FRIENDS_SN_REQUIRED: // MethodAccessException
                case __HResults.COR_E_FIELDACCESS:
                case __HResults.COR_E_MEMBERACCESS:
                    exception = new MemberAccessException();

                    if (errorCode != __HResults.COR_E_METHODACCESS)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_MISSINGFIELD: // MissingFieldException
                case __HResults.COR_E_MISSINGMETHOD: // MissingMethodException
                case __HResults.COR_E_MISSINGMEMBER:
                case unchecked((int)0x800A01CD):
                    exception = new MissingMemberException();
                    break;
                case __HResults.COR_E_MISSINGMANIFESTRESOURCE:
                    exception = new System.Resources.MissingManifestResourceException();
                    break;
                case __HResults.COR_E_NOTSUPPORTED:
                case unchecked((int)0x800A01B6):
                case unchecked((int)0x800A01BD):
                case unchecked((int)0x800A01CA):
                case unchecked((int)0x800A01CB):
                    exception = new NotSupportedException();

                    if (errorCode != __HResults.COR_E_NOTSUPPORTED)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_NULLREFERENCE:
                    exception = new NullReferenceException();
                    break;
                case __HResults.COR_E_OBJECTDISPOSED:
                case __HResults.RO_E_CLOSED:
                    // No default constructor
                    exception = new ObjectDisposedException(String.Empty);
                    break;
                case __HResults.COR_E_OPERATIONCANCELED:
#if ENABLE_WINRT
                    exception = new OperationCanceledException();
#endif
                    break;
                case __HResults.COR_E_OVERFLOW:
                case __HResults.CTL_E_OVERFLOW:
                    exception = new OverflowException();
                    break;
                case __HResults.COR_E_PLATFORMNOTSUPPORTED:
                    exception = new PlatformNotSupportedException(message);
                    break;
                case __HResults.COR_E_RANK:
                    exception = new RankException();
                    break;
                case __HResults.COR_E_REFLECTIONTYPELOAD:
#if ENABLE_WINRT
                    exception = new System.Reflection.ReflectionTypeLoadException(null, null);
#endif
                    break;
                case __HResults.COR_E_SECURITY:
                case __HResults.CORSEC_E_INVALID_STRONGNAME:
                case __HResults.CTL_E_PERMISSIONDENIED:
                case unchecked((int)0x800A01A3):
                case __HResults.CORSEC_E_INVALID_PUBLICKEY:
                case __HResults.CORSEC_E_SIGNATURE_MISMATCH:
                    exception = new System.Security.SecurityException();
                    break;
                case __HResults.COR_E_SAFEARRAYRANKMISMATCH:
                    exception = new SafeArrayRankMismatchException();
                    break;
                case __HResults.COR_E_SAFEARRAYTYPEMISMATCH:
                    exception = new SafeArrayTypeMismatchException();
                    break;
                case __HResults.COR_E_SERIALIZATION:
                    exception = ConstructExceptionUsingReflection(
                        "System.Runtime.Serialization.SerializationException, System.Runtime.Serialization.Primitives, Version=4.0.0.0",
                        message);
                    break;
                case __HResults.COR_E_SYNCHRONIZATIONLOCK:
                    exception = new System.Threading.SynchronizationLockException();
                    break;
                case __HResults.COR_E_TARGETINVOCATION:
                    exception = new System.Reflection.TargetInvocationException(null);
                    break;
                case __HResults.COR_E_TARGETPARAMCOUNT:
                    exception = new System.Reflection.TargetParameterCountException();
                    break;
                case __HResults.COR_E_TYPEINITIALIZATION:
                    exception = InteropExtensions.CreateTypeInitializationException(message);
                    break;
                case __HResults.COR_E_TYPELOAD:
                case __HResults.RO_E_METADATA_NAME_NOT_FOUND:
                case __HResults.CLR_E_BIND_TYPE_NOT_FOUND:
                    exception = new TypeLoadException();

                    if (errorCode != __HResults.COR_E_TYPELOAD)
                        shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_UNAUTHORIZEDACCESS:
                case __HResults.CTL_E_PATHFILEACCESSERROR:
                case unchecked((int)0x800A014F):
                    exception = new UnauthorizedAccessException();

                    shouldDisplayHR = true;

                    break;
                case __HResults.COR_E_VERIFICATION:
                    exception = new System.Security.VerificationException();
                    break;
                case __HResults.E_NOTIMPL:
                    exception = new NotImplementedException();
                    break;
                case __HResults.E_OUTOFMEMORY:
                case __HResults.CTL_E_OUTOFMEMORY:
                case unchecked((int)0x800A7919):
                    exception = new OutOfMemoryException();

                    if (errorCode != __HResults.E_OUTOFMEMORY)
                        shouldDisplayHR = true;

                    break;
#if ENABLE_WINRT
                case __HResults.E_XAMLPARSEFAILED:
                    exception = ConstructExceptionUsingReflection(
                        "Windows.UI.Xaml.Markup.XamlParseException, System.Runtime.WindowsRuntime.UI.Xaml, Version=4.0.0.0",
                        message);
                    break;
                case __HResults.E_ELEMENTNOTAVAILABLE:
                    exception = ConstructExceptionUsingReflection(
                        "Windows.UI.Xaml.Automation.ElementNotAvailableException, System.Runtime.WindowsRuntime.UI.Xaml, Version=4.0.0.0",
                        message);
                    break;
                case __HResults.E_ELEMENTNOTENABLED:
                    exception = ConstructExceptionUsingReflection(
                        "Windows.UI.Xaml.Automation.ElementNotEnabledException, System.Runtime.WindowsRuntime.UI.Xaml, Version=4.0.0.0", 
                        message);
                    break;
                case __HResults.E_LAYOUTCYCLE:
                    exception = ConstructExceptionUsingReflection(
                        "Windows.UI.Xaml.LayoutCycleException, System.Runtime.WindowsRuntime.UI.Xaml, Version=4.0.0.0", 
                        message);
                    break;
#endif // ENABLE_WINRT
                case __HResults.COR_E_AMBIGUOUSMATCH: // AmbiguousMatchException
                case __HResults.COR_E_APPLICATION: // ApplicationException
                case __HResults.COR_E_APPDOMAINUNLOADED: // AppDomainUnloadedException
                case __HResults.COR_E_CANNOTUNLOADAPPDOMAIN: // CannotUnloadAppDomainException
                case __HResults.COR_E_CODECONTRACTFAILED: // ContractException
                case __HResults.COR_E_CONTEXTMARSHAL: // ContextMarshalException
                case __HResults.CORSEC_E_CRYPTO: // CryptographicException
                case __HResults.CORSEC_E_CRYPTO_UNEX_OPER: // CryptographicUnexpectedOperationException
                case __HResults.COR_E_EXECUTIONENGINE: // ExecutionEngineException
                case __HResults.COR_E_INSUFFICIENTEXECUTIONSTACK: // InsufficientExecutionStackException
                case __HResults.COR_E_INVALIDFILTERCRITERIA: // InvalidFilterCriteriaException
                case __HResults.COR_E_INVALIDPROGRAM: // InvalidProgramException
                case __HResults.COR_E_MULTICASTNOTSUPPORTED: // MulticastNotSupportedException
                case __HResults.COR_E_REMOTING: // RemotingException
                case __HResults.COR_E_RUNTIMEWRAPPED: // RuntimeWrappedException
                case __HResults.COR_E_SERVER: // ServerException
                case __HResults.COR_E_STACKOVERFLOW: // StackOverflowException
                case __HResults.CTL_E_OUTOFSTACKSPACE: // StackOverflowException
                case __HResults.COR_E_SYSTEM: // SystemException
                case __HResults.COR_E_TARGET: // TargetException
                case __HResults.COR_E_THREADABORTED: // TargetException
                case __HResults.COR_E_THREADINTERRUPTED: // ThreadInterruptedException
                case __HResults.COR_E_THREADSTATE: // ThreadStateException
                case __HResults.COR_E_THREADSTART: // ThreadStartException
                case __HResults.COR_E_TYPEUNLOADED: // TypeUnloadedException
                case __HResults.CORSEC_E_POLICY_EXCEPTION: // PolicyException
                case __HResults.CORSEC_E_NO_EXEC_PERM: // PolicyException
                case __HResults.CORSEC_E_MIN_GRANT_FAIL: // PolicyException
                case __HResults.CORSEC_E_XMLSYNTAX: // XmlSyntaxException
                case __HResults.ISS_E_ALLOC_TOO_LARGE: // IsolatedStorageException
                case __HResults.ISS_E_BLOCK_SIZE_TOO_SMALL: // IsolatedStorageException
                case __HResults.ISS_E_CALLER: // IsolatedStorageException
                case __HResults.ISS_E_CORRUPTED_STORE_FILE: // IsolatedStorageException
                case __HResults.ISS_E_CREATE_DIR: // IsolatedStorageException
                case __HResults.ISS_E_CREATE_MUTEX: // IsolatedStorageException
                case __HResults.ISS_E_DEPRECATE: // IsolatedStorageException
                case __HResults.ISS_E_FILE_NOT_MAPPED: // IsolatedStorageException
                case __HResults.ISS_E_FILE_WRITE: // IsolatedStorageException
                case __HResults.ISS_E_GET_FILE_SIZE: // IsolatedStorageException
                case __HResults.ISS_E_ISOSTORE: // IsolatedStorageException
                case __HResults.ISS_E_LOCK_FAILED: // IsolatedStorageException
                case __HResults.ISS_E_MACHINE: // IsolatedStorageException
                case __HResults.ISS_E_MACHINE_DACL: // IsolatedStorageException
                case __HResults.ISS_E_MAP_VIEW_OF_FILE: // IsolatedStorageException
                case __HResults.ISS_E_OPEN_FILE_MAPPING: // IsolatedStorageException
                case __HResults.ISS_E_OPEN_STORE_FILE: // IsolatedStorageException
                case __HResults.ISS_E_PATH_LENGTH: // IsolatedStorageException
                case __HResults.ISS_E_SET_FILE_POINTER: // IsolatedStorageException
                case __HResults.ISS_E_STORE_NOT_OPEN: // IsolatedStorageException
                case __HResults.ISS_E_STORE_VERSION: // IsolatedStorageException
                case __HResults.ISS_E_TABLE_ROW_NOT_FOUND: // IsolatedStorageException
                case __HResults.ISS_E_USAGE_WILL_EXCEED_QUOTA: // IsolatedStorageException
                case __HResults.E_FAIL:
                default:
                    break;
            }

            if (exception == null)
            {
                if (createCOMException)
                {
                    exception = new COMException();
                    if (errorCode != __HResults.E_FAIL)
                        shouldDisplayHR = true;
                }
                else
                {
                    exception = new Exception();
                    if (errorCode != __HResults.COR_E_EXCEPTION)
                        shouldDisplayHR = true;
                 }
            }

            bool shouldConstructMessage = false;
            if (hasErrorInfo)
            {
                // If there is a IErrorInfo/IRestrictedErrorInfo, only construct a new error message if
                // the message is not available and do not use the shouldDisplayHR setting
                if (message == null)
                    shouldConstructMessage = true;
            }
            else
            {
                // If there is no IErrorInfo, use the shouldDisplayHR setting from the big switch/case above
                shouldConstructMessage = shouldDisplayHR;
            }

            if (shouldConstructMessage)
            {
                //
                // Append the HR into error message, just in case the app wants to look at the HR in
                // message to determine behavior.  We didn't expose HResult property until v4.5 and
                // GetHRFromException has side effects so probably Message was their only choice.
                // This behavior is probably not exactly the same as in desktop but it is fine to append
                // more message at the end. In any case, having the HR in the error message are helpful
                // to developers.
                // This makes sure:
                // 1. We always have a HR 0xNNNNNNNN in the message
                // 2. Put in a nice "Exception thrown from HRESULT" message if we can
                // 3. Wrap it in () if there is an existing message
                //

                // TODO: Add Symbolic Name into Messaage, convert 0x80020006 to DISP_E_UNKNOWNNAME
                string hrMessage = String.Format("{0} 0x{1:X}", SR.Excep_FromHResult, errorCode);

                message = ExternalInterop.GetMessage(errorCode);

                // Always make sure we have at least the HRESULT part in retail build or when the message
                // is empty.
                if (message == null)
                    message = hrMessage;
                else
                    message = message + " (" + hrMessage + ")";
            }

            if (message != null)
            {
                // Set message explicitly rather than calling constructor because certain ctors would append a
                // prefix to the message and that is not what we want
                InteropExtensions.SetExceptionMessage(exception, message);
            }

            InteropExtensions.SetExceptionErrorCode(exception, errorCode);

            return exception;
        }

        /// <summary>
        /// Construct exception dynamically using reflection.
        /// </summary>
        /// <param name="exceptionTypeName">Assembly-qualified exception type name</param>
        /// <param name="message">Message to use for exception creation (null = use parameterless constructor)</param>
        static Exception ConstructExceptionUsingReflection(string exceptionTypeName, string message)
        {
            Exception result = null;

            try
            {
                Type exceptionType = Type.GetType(exceptionTypeName);

                if (exceptionType != null)
                {
                    if (message == null)
                    {
                        result = (Exception)Activator.CreateInstance(exceptionType);
                    }
                    else
                    {
                        result = (Exception)Activator.CreateInstance(exceptionType, message);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore exceptions during exception construction - a default exception will be returned
            }
            return result;
        }

        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        static bool TryGetRestrictedErrorInfo(out IntPtr pRestrictedErrorInfo)
        {
            return ExternalInterop.GetRestrictedErrorInfo(out pRestrictedErrorInfo) >= 0 && pRestrictedErrorInfo != IntPtr.Zero;
        }

        /// <summary>
        /// This method returns a new Exception object given the HR value.
        ///
        /// 1. We check whether we have our own LanguageException associated with this hr. If so we simply use it since it helps preserve the stacktrace, message and type.
        ///    This is done using GetLanguageException API on ILanguageExceptionErrorInfo from IRestrictedErrorInfo. Since ILanguageExceptionErrorInfo is available only on Windows Blue
        ///    we can only do this WindowsBlue and above. In desktop CLR we could use GetErroInfo and check whether we have our IErroInfo and retrieve our own exception.
        ///    For Win8 in .NET Native we simply create the exception using the RestrictedErrorInfo and hence only able to give the exception with restrictedErrorMsg.
        /// 2. In case we do not have the languageException we simply check RestrictedErrorInfo for errorMsg and create an exception using
        ///   <errorMsg>\r\n<restrictedErrorMsg>. This is done for only windows blue. To be backward compatible we only use errorMsg for creating exception in win8.
        /// 3. PS - This class puts all the logic in try, catch block to ensure that none of the exception helpers
        ///  throw exception themselves.
        /// </summary>
        /// <param name="hr"></param>
        /// <param name="isWinRTScenario"></param>
        internal static Exception GetExceptionForHRInternalNoThrow(int hr, bool isWinRTScenario, bool isClassicCOM)
        {
            Exception ex;
            IntPtr pRestrictedErrorInfo = IntPtr.Zero;

            try
            {
                if (TryGetRestrictedErrorInfo(out pRestrictedErrorInfo))
                {
                    // This is to check whether we need to give post win8 behavior or not.
                    if (isWinRTScenario)
                    {
                        // Check whether the given IRestrictedErrorInfo object supports ILanguageExceptionErrorInfo
                        IntPtr pLanguageExceptionErrorInfo = McgMarshal.ComQueryInterfaceNoThrow(pRestrictedErrorInfo, ref Interop.COM.IID_ILanguageExceptionErrorInfo);
                        if (pLanguageExceptionErrorInfo != IntPtr.Zero)
                        {
                            // We have an LanguageExceptionErrorInfo.
                            IntPtr pUnk;
                            __com_ILanguageExceptionErrorInfo* pComLanguageExceptionErrorInfo = (__com_ILanguageExceptionErrorInfo*)pLanguageExceptionErrorInfo;
                            int result = CalliIntrinsics.StdCall<int>(pComLanguageExceptionErrorInfo->pVtable->pfnGetLanguageException, pLanguageExceptionErrorInfo, out pUnk);
                            McgMarshal.ComSafeRelease(pLanguageExceptionErrorInfo);

                            if (result >= 0 && pUnk != IntPtr.Zero)
                            {
                                try
                                {
                                    // Check whether the given pUnk is a managed exception.
                                    ComCallableObject ccw;
                                    if (ComCallableObject.TryGetCCW(pUnk, out ccw))
                                    {
                                        return ccw.TargetObject as Exception;
                                    }
                                }
                                finally
                                {
                                    McgMarshal.ComSafeRelease(pUnk);
                                }
                            }
                        }
                    }
                    String message = null, errorInfoReference = null;
                    string errMsg, errCapSid, resErrMsg;
                    int errHr;
                    object restrictedErrorInfo = null;

                    bool hasErrorInfo = false;
                    if (RestrictedErrorInfoHelper.GetErrorDetails(pRestrictedErrorInfo, out errMsg, out errHr, out resErrMsg, out errCapSid) && errHr == hr)
                    {
                        // RestrictedErrorInfo details can be used since the pRestrictedErrorInfo has the same hr value as the hr returned by the native code.
                        // We are in windows blue or above and hence the exceptionMsg is errMsg + "\r\n" + resErrMsg
                        message = String.IsNullOrEmpty(resErrMsg) ? errMsg : errMsg + "\r\n" + resErrMsg;
                        RestrictedErrorInfoHelper.GetReference(pRestrictedErrorInfo, out errorInfoReference);
                        restrictedErrorInfo = McgMarshal.ComInterfaceToObject(pRestrictedErrorInfo, InternalTypes.IRestrictedErrorInfo);

                        hasErrorInfo = true;
                    }

                    if (hr == Interop.COM.RO_E_CLOSED && isWinRTScenario)
                        hr = Interop.COM.COR_E_OBJECTDISPOSED;

                    // Now we simply need to set the description and the resDescription by adding an internal method.
                    ex = GetMappingExceptionForHR(hr, message, isClassicCOM, hasErrorInfo);

                    if (restrictedErrorInfo != null)
                    {
                        InteropExtensions.AddExceptionDataForRestrictedErrorInfo(ex, resErrMsg, errorInfoReference, errCapSid, restrictedErrorInfo);
                    }

                    return ex;
                }
            }
            catch (Exception)
            {
                // We can't do any thing here and hence we swallow the exception and get the corresponding hr.
            }
            finally
            {
                McgMarshal.ComSafeRelease(pRestrictedErrorInfo);
            }

            // We could not find any restrictedErrorInfo associated with this object and hence we simply use the hr to create the exception.
            return GetMappingExceptionForHR(hr, null, isClassicCOM, hasErrorInfo: false);
        }

        internal static bool ReportUnhandledError(Exception e)
        {
            System.IntPtr pRestrictedErrorInfo = IntPtr.Zero;

            if (e != null)
            {
                try
                {
#if ENABLE_WINRT
                    // Only report to the WinRT global exception handler in modern apps
                    WinRTInteropCallbacks callbacks = WinRTInterop.Callbacks;
                    if (callbacks == null || !callbacks.IsAppxModel())
                    {
                        return false;
                    }

                    // Get the IUnknown for the current exception and originate it as a langauge error in order to have
                    // Windows generate an IRestrictedErrorInfo corresponding to the exception object.  We can then
                    // notify the global error handler that this IRestrictedErrorInfo instance represents an exception that
                    // went unhandled in managed code.
                    if (OriginateLanguageException(e) && TryGetRestrictedErrorInfo(out pRestrictedErrorInfo))
                    {
                        return ExternalInterop.RoReportUnhandledError(pRestrictedErrorInfo) >= 0;
                    }
#else
                    return false;
#endif // ENABLE_WINRT
                }
                catch (Exception)
                {
                    // We can't give an exception in this code, so we simply swallow the exception here.
                }
                finally
                {
                    McgMarshal.ComSafeRelease(pRestrictedErrorInfo);
                }
            }
            // If we have got here, then some step of the pInvoke failed, which means the GEH was not invoked
            return false;
        }

        internal static Exception AttachRestrictedErrorInfo(Exception e)
        {
            // If there is no exception, then the restricted error info doesn't apply to it
            if (e != null)
            {
                System.IntPtr pRestrictedErrorInfo = IntPtr.Zero;

                try
                {
                    // Get the restricted error info for this thread and see if it may correlate to the current
                    // exception object.  Note that in general the thread's IRestrictedErrorInfo is not meant for
                    // exceptions that are marshaled Windows.Foundation.HResults and instead are intended for
                    // HRESULT ABI return values.   However, in many cases async APIs will set the thread's restricted
                    // error info as a convention in order to provide extended debugging information for the ErrorCode
                    // property.
                    if (TryGetRestrictedErrorInfo(out pRestrictedErrorInfo))
                    {
                        string description;
                        string restrictedDescription;
                        string capabilitySid;
                        int restrictedErrorInfoHResult;
                        if (RestrictedErrorInfoHelper.GetErrorDetails(
                                                            pRestrictedErrorInfo,
                                                            out description,
                                                            out restrictedErrorInfoHResult,
                                                            out restrictedDescription,
                                                            out capabilitySid) && (e.HResult == restrictedErrorInfoHResult))
                        {
                            // Since this is a special case where by convention there may be a correlation, there is not a
                            // guarantee that the restricted error info does belong to the async error code.  In order to
                            // reduce the risk that we associate incorrect information with the exception object, we need
                            // to apply a heuristic where we attempt to match the current exception's HRESULT with the
                            // HRESULT the IRestrictedErrorInfo belongs to.  If it is a match we will assume association
                            // for the IAsyncInfo case.
                            string errorReference;
                            RestrictedErrorInfoHelper.GetReference(pRestrictedErrorInfo, out errorReference);
                            object restrictedErrorInfo = McgMarshal.ComInterfaceToObject(pRestrictedErrorInfo, InternalTypes.IRestrictedErrorInfo);
                            InteropExtensions.AddExceptionDataForRestrictedErrorInfo(
                                                                                 e,
                                                                                 restrictedDescription,
                                                                                 errorReference,
                                                                                 capabilitySid,
                                                                                 restrictedErrorInfo);
                        }
                    }
                }
                catch (Exception)
                {
                    // If we can't get the restricted error info, then proceed as if it isn't associated with this
                    // error.
                }
                finally
                {
                    McgMarshal.ComSafeRelease(pRestrictedErrorInfo);
                }
            }
            return e;
        }
    }
}
