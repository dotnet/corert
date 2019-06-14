// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Runtime.InteropServices
{
    internal class InternalTypes
    {
        internal static RuntimeTypeHandle IUnknown = typeof(System.Runtime.InteropServices.__com_IUnknown).TypeHandle;
        internal static RuntimeTypeHandle ICCW = typeof(System.Runtime.InteropServices.__com_ICCW).TypeHandle;
        internal static RuntimeTypeHandle IMarshal = typeof(System.Runtime.InteropServices.__com_IMarshal).TypeHandle;
        internal static RuntimeTypeHandle IDispatch = typeof(System.Runtime.InteropServices.__com_IDispatch).TypeHandle;
        internal static RuntimeTypeHandle IInspectable = typeof(System.Runtime.InteropServices.__com_IInspectable).TypeHandle;

#if ENABLE_MIN_WINRT
        internal static RuntimeTypeHandle IActivationFactoryInternal = typeof(System.Runtime.InteropServices.WindowsRuntime.IActivationFactoryInternal).TypeHandle;
#endif

#if ENABLE_WINRT
        internal static RuntimeTypeHandle ICustomPropertyProvider = typeof(System.Runtime.InteropServices.__com_ICustomPropertyProvider).TypeHandle;
        internal static RuntimeTypeHandle IWeakReferenceSource = typeof(System.Runtime.InteropServices.__com_IWeakReferenceSource).TypeHandle;
        internal static RuntimeTypeHandle IWeakReference = typeof(System.Runtime.InteropServices.__com_IWeakReference).TypeHandle;
        internal static RuntimeTypeHandle IJupiterObject = typeof(System.Runtime.InteropServices.__com_IJupiterObject).TypeHandle;
        internal static RuntimeTypeHandle IStringable = typeof(System.Runtime.InteropServices.__com_IStringable).TypeHandle;        
        internal static RuntimeTypeHandle IManagedActivationFactory = typeof(System.Runtime.InteropServices.WindowsRuntime.IManagedActivationFactory).TypeHandle;
        internal static RuntimeTypeHandle IRestrictedErrorInfo = typeof(System.Runtime.InteropServices.ExceptionHelpers.__com_IRestrictedErrorInfo).TypeHandle;
        internal static RuntimeTypeHandle HSTRING = typeof(System.Runtime.InteropServices.HSTRING).TypeHandle;
        internal static RuntimeTypeHandle ILanguageExceptionStackBackTrace = typeof(System.Runtime.InteropServices.ExceptionHelpers.__com_ILanguageExceptionStackBackTrace).TypeHandle;
#endif
    }

    /// <summary>
    /// Singleton module that managed types implemented internally, rather than in MCG-generated code.
    /// This works together with interface implementations in StandardInterfaces.cs
    /// NOTE: Interfaces defined here are implemented by CCWs, but user might be able to override them
    /// depending on the interface
    /// </summary>
    internal class InternalModule : McgModule
    {
        // The internal module is always lower priority than all other modules.
        private const int PriorityForInternalModule = -1;

        unsafe internal InternalModule()
            : base(
                PriorityForInternalModule,
                s_interfaceData,
                null,                               // CCWTemplateData
                null,                               // CCWTemplateInterfaceList
                null,                               // classData,
                null,                               // boxingData,
                null,                               // additionalClassData,
                null,                               // collectionData,
                null,                               // DelegateData
                null,                               // CCWFactories
                null,                               // structMarshalData
                null,                               // unsafeStructFieldOffsetData
                null,                               // interfaceMarshalData
                null                               // hashcodeVerify
                )
        {
            // Following code is disabled due to lazy static constructor dependency from McgModule which is
            // static eager constructor. Undo this when McgCurrentModule is using ModuleConstructorAttribute
#if EAGER_CTOR_WORKAROUND
                for (int i = 0; i < s_interfaceData.Length; i++)
                {
                    Debug.Assert((s_interfaceData[i].Flags & McgInterfaceFlags.useSharedCCW) == 0);
                }
#endif
        }
        // IUnknown
        internal static McgInterfaceData s_IUnknown = new McgInterfaceData
        {
            ItfType = InternalTypes.IUnknown,
            ItfGuid = Interop.COM.IID_IUnknown,
            CcwVtable = __vtable_IUnknown.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };

        // IInspectable
        internal static McgInterfaceData s_IInspectable = new McgInterfaceData
        {
            ItfType = InternalTypes.IInspectable,
            ItfGuid = Interop.COM.IID_IInspectable,
            CcwVtable = __vtable_IInspectable.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal | McgInterfaceFlags.isIInspectable,
        };

#if ENABLE_MIN_WINRT
        // IActivationFactoryInternal
        internal static McgInterfaceData s_IActivationFactoryInternal = new McgInterfaceData
        {
            ItfType = InternalTypes.IActivationFactoryInternal,
            ItfGuid = Interop.COM.IID_IActivationFactoryInternal,
            CcwVtable = __vtable_IActivationFactoryInternal.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };
#endif 

#if ENABLE_WINRT
        // ICustomPropertyProvider
        internal static McgInterfaceData s_ICustomPropertyProvider = new McgInterfaceData
        {
            ItfType = InternalTypes.ICustomPropertyProvider,
            ItfGuid = Interop.COM.IID_ICustomPropertyProvider,
            CcwVtable = __vtable_ICustomPropertyProvider.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal | McgInterfaceFlags.isIInspectable,
        };

        // IWeakReferenceSource
        internal static McgInterfaceData s_IWeakReferenceSource = new McgInterfaceData
        {
            ItfType = InternalTypes.IWeakReferenceSource,
            ItfGuid = Interop.COM.IID_IWeakReferenceSource,
            CcwVtable = __vtable_IWeakReferenceSource.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };

        // IWeakReference
        internal static McgInterfaceData s_IWeakReference = new McgInterfaceData
        {
            ItfType = InternalTypes.IWeakReference,
            ItfGuid = Interop.COM.IID_IWeakReference,
            CcwVtable = __vtable_IWeakReference.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };
#endif
        // ICCW
        internal static McgInterfaceData s_ICCW = new McgInterfaceData
        {
            ItfType = InternalTypes.ICCW,
            ItfGuid = Interop.COM.IID_ICCW,
            CcwVtable = __vtable_ICCW.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };    

#if ENABLE_WINRT
        // IJupiterObject
        internal static McgInterfaceData s_IJupiterObject = new McgInterfaceData
        {
            ItfType = InternalTypes.IJupiterObject,
            ItfGuid = Interop.COM.IID_IJupiterObject,
            Flags = McgInterfaceFlags.isInternal,
        };

        // IStringable
        internal static McgInterfaceData s_IStringable = new McgInterfaceData
        {
            ItfType = InternalTypes.IStringable,
            ItfGuid = Interop.COM.IID_IStringable,
            CcwVtable = __vtable_IStringable.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };

        // IManagedActivationFactory
        internal static McgInterfaceData s_IManagedActivationFactory = new McgInterfaceData
        {
            ItfType = InternalTypes.IManagedActivationFactory,
            ItfGuid = Interop.COM.IID_IManagedActivationFactory,
            CcwVtable = __vtable_IManagedActivationFactory.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };

        // IRestrictedErrorInfo
        internal static McgInterfaceData s_IRestrictedErrorInfo = new McgInterfaceData
        {
            ItfType = InternalTypes.IRestrictedErrorInfo,
            ItfGuid = Interop.COM.IID_IRestrictedErrorInfo,
            Flags = McgInterfaceFlags.isInternal,
        };
#endif
        // IMarshal
        internal static McgInterfaceData s_IMarshal = new McgInterfaceData
        {
            ItfType = InternalTypes.IMarshal,
            ItfGuid = Interop.COM.IID_IMarshal,
            CcwVtable = __vtable_IMarshal.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };

        // IDispatch
        internal static McgInterfaceData s_IDispatch = new McgInterfaceData
        {
            ItfType = InternalTypes.IDispatch,
            ItfGuid = Interop.COM.IID_IDispatch,
            CcwVtable = __vtable_IDispatch.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
        };
#if ENABLE_WINRT
        // HSTRING, just needed for TypeHandle comparison
        internal static McgInterfaceData s_HSTRING = new McgInterfaceData
        {
            ItfType = InternalTypes.HSTRING
        };

        // ILanguageExceptionStackBackTrace
        internal static McgInterfaceData s_ILanguageExceptionStackBackTrace = new McgInterfaceData
        {
            ItfType = InternalTypes.ILanguageExceptionStackBackTrace,
            ItfGuid = Interop.COM.IID_ILanguageExceptionStackBackTrace,
            Flags = McgInterfaceFlags.isInternal,
            CcwVtable = System.Runtime.InteropServices.ExceptionHelpers.__vtable_ILanguageExceptionStackBackTrace.GetVtableFuncPtr(),
        };
#endif

        static readonly McgInterfaceData[] s_interfaceData = new McgInterfaceData[] {
                s_IUnknown,
                s_IInspectable,
#if ENABLE_WINRT
                s_ICustomPropertyProvider,
                s_IWeakReferenceSource,
                s_IWeakReference,
#endif
                s_ICCW,
#if ENABLE_MIN_WINRT
                s_IActivationFactoryInternal,
#endif

#if ENABLE_WINRT
                s_IJupiterObject,
                s_IStringable,
                s_IManagedActivationFactory,
                s_IRestrictedErrorInfo,
#endif
                s_IMarshal,
                s_IDispatch,

#if ENABLE_WINRT
                s_HSTRING,
                s_ILanguageExceptionStackBackTrace
#endif
        };
    }
}
