// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Singleton module that managed types implemented internally, rather than in MCG-generated code.
    /// This works together with interface implementations in StandardInterfaces.cs
    /// NOTE: Interfaces defined here are implemented by CCWs, but user might be able to override them
    /// depending on the interface
    /// </summary>

    [EagerStaticClassConstruction]
    internal class InternalModule : McgModule
    {
        /// <summary>
        /// Index of each internal interface
        /// Each internal McgInterfaceData should have MarshalIndex field with one of the enum values
        /// </summary>
        internal enum Indexes : short
        {
            IUnknown = 0,
            IInspectable,
#if ENABLE_WINRT            
            ICustomPropertyProvider,
#endif
            IWeakReferenceSource,
            IWeakReference,
#if ENABLE_WINRT
            ICCW,
            IJupiterObject,
            IStringable,
            IActivationFactoryInternal,
            IManagedActivationFactory,
            IRestrictedErrorInfo,
#endif
            IMarshal,
#if ENABLE_WINRT
            HSTRING
#endif
        }

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
                null,                               // hashcodeVerify
                null,                               // interfaceTypeInfo_Hashtable
                null,                               // ccwTemplateData_Hashtable
                null,                               // classData_Hashtable
                null,                               // collectionData_Hashtable
                null                                // boxingData_Hashtable
                )
        {
            // Following code is disabled due to lazy static constructor dependency from McgModule which is
            // static eager constructor. Undo this when McgCurrentModule is using ModuleConstructorAttribute
#if EAGER_CTOR_WORKAROUND
                for (int i = 0; i < s_interfaceTypeInfo.Length; i++)
                {
                    Debug.Assert((s_interfaceTypeInfo[i].InterfaceData->Flags & McgInterfaceFlags.useSharedCCW) == 0);
                }
#endif
        }

        // IUnknown
        static internal McgInterfaceData s_IUnknown = new McgInterfaceData
        {
            ItfGuid = new Guid(0x00000000, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46),
            CcwVtable = __vtable_IUnknown.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IUnknown
        };

#if ENABLE_WINRT
        // IInspectable
        static internal McgInterfaceData s_IInspectable = new McgInterfaceData
        {
            ItfGuid = new Guid(unchecked((int)0xAF86E2E0u), unchecked((short)0xB12D), 0x4C6A, 0x9C, 0x5A, 0xD7, 0xAA, 0x65, 0x10, 0x1E, 0x90),
            CcwVtable = __vtable_IInspectable.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal | McgInterfaceFlags.isIInspectable,
            MarshalIndex = (short)Indexes.IInspectable
        };

        // ICustomPropertyProvider
        static internal McgInterfaceData s_ICustomPropertyProvider = new McgInterfaceData
        {
            ItfGuid = new Guid(0x7C925755, 0x3E48, 0x42B4, 0x86, 0x77, 0x76, 0x37, 0x22, 0x67, 0x3, 0x3F),
            CcwVtable = __vtable_ICustomPropertyProvider.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal | McgInterfaceFlags.isIInspectable,
            MarshalIndex = (short)Indexes.ICustomPropertyProvider
        };



        // IWeakReferenceSource
        static internal McgInterfaceData s_IWeakReferenceSource = new McgInterfaceData
        {
            ItfGuid = new Guid(0x00000038, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46),
            CcwVtable = __vtable_IWeakReferenceSource.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IWeakReferenceSource
        };

        // IWeakReference
        static internal McgInterfaceData s_IWeakReference = new McgInterfaceData
        {
            ItfGuid = new Guid(0x00000037, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46),
            CcwVtable = __vtable_IWeakReference.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IWeakReference
        };

        // ICCW
        static internal McgInterfaceData s_ICCW = new McgInterfaceData
        {
            ItfGuid = new Guid(0x64bd43f8, unchecked((short)0xBFEE), 0x4ec4, 0xb7, 0xeb, 0x29, 0x35, 0x15, 0x8d, 0xae, 0x21),
            CcwVtable = __vtable_ICCW.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.ICCW
        };

        // IJupiterObject
        static internal McgInterfaceData s_IJupiterObject = new McgInterfaceData
        {
            ItfGuid = new Guid(0x11d3b13a, 0x180e, 0x4789, 0xa8, 0xbe, 0x77, 0x12, 0x88, 0x28, 0x93, 0xe6),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IJupiterObject
        };

        // IStringable
        static internal McgInterfaceData s_IStringable = new McgInterfaceData
        {
            ItfGuid = new Guid(unchecked((int)0x96369f54), unchecked((short)0x8eb6), 0x48f0, 0xab, 0xce, 0xc1, 0xb2, 0x11, 0xe6, 0x27, 0xc3),
            CcwVtable = __vtable_IStringable.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IStringable
        };

        // IActivationFactoryInternal
        static internal McgInterfaceData s_IActivationFactoryInternal = new McgInterfaceData
        {
            ItfGuid = new Guid(0x00000035, 0x0000, 0x0000, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46),
            CcwVtable = __vtable_IActivationFactoryInternal.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IActivationFactoryInternal
        };


        // IManagedActivationFactory
        static internal McgInterfaceData s_IManagedActivationFactory = new McgInterfaceData
        {
            ItfGuid = new Guid(0x60D27C8D, 0x5F61, 0x4CCE, 0xB7, 0x51, 0x69, 0x0F, 0xAE, 0x66, 0xAA, 0x53),
            CcwVtable = __vtable_IManagedActivationFactory.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IManagedActivationFactory
        };

        // IRestrictedErrorInfo
        static internal McgInterfaceData s_IRestrictedErrorInfo = new McgInterfaceData
        {
            ItfGuid = new Guid(unchecked((int)0x82BA7092), 0x4C88, 0x427D, 0xA7, 0xBC, 0x16, 0xDD, 0x93, 0xFE, 0xB6, 0x7E),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IRestrictedErrorInfo
        };
#endif

        // IMarshal
        static internal McgInterfaceData s_IMarshal = new McgInterfaceData
        {
            ItfGuid = Interop.COM.IID_IMarshal,
            CcwVtable = __vtable_IMarshal.GetVtableFuncPtr(),
            Flags = McgInterfaceFlags.isInternal,
            MarshalIndex = (short)Indexes.IMarshal
        };

        // HSTRING, just needed for McgTypeInfo comparison
        static internal McgInterfaceData s_HSTRING = new McgInterfaceData
        {

        };

        static readonly McgInterfaceData[] s_interfaceData = new McgInterfaceData[] {
                s_IUnknown,
#if ENABLE_WINRT
                s_IInspectable,
                s_ICustomPropertyProvider,
                s_IWeakReferenceSource,
                s_IWeakReference,
                s_ICCW,
                s_IJupiterObject,
                s_IStringable,
                s_IActivationFactoryInternal,

                s_IManagedActivationFactory,
                s_IRestrictedErrorInfo,
#endif
                s_IMarshal,

#if ENABLE_WINRT
                s_HSTRING
#endif
        };
    }
}
