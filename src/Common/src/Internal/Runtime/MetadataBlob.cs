// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Runtime
{
    internal enum ReflectionMapBlob
    {
        TypeMap                                     = 1,
        ArrayMap                                    = 2,
        GenericInstanceMap                          = 3,
        GenericParameterMap                         = 4,
        BlockReflectionTypeMap                      = 5,
        InvokeMap                                   = 6,
        VirtualInvokeMap                            = 7,
        CommonFixupsTable                           = 8,
        FieldAccessMap                              = 9,
        CCtorContextMap                             = 10,
        DiagGenericInstanceMap                      = 11,
        DiagGenericParameterMap                     = 12,
        EmbeddedMetadata                            = 13,
        DefaultConstructorMap                       = 14,
        UnboxingAndInstantiatingStubMap             = 15,
        InvokeInstantiations                        = 16, // unused
        PointerTypeMap                              = 17,
        GenericVirtualMethodTable                   = 18,
        InterfaceGenericVirtualMethodTable          = 19,

        // Reflection template types/methods blobs:
        TypeTemplateMap                             = 21,
        GenericMethodsTemplateMap                   = 22,
        DynamicInvokeTemplateData                   = 23,
        //Native layout blobs:
        NativeLayoutInfo                            = 30, // Created by MDIL binder
        NativeReferences                            = 31, // Created by MDIL binder
        GenericsHashtable                           = 32, // Created by MDIL binder
        NativeStatics                               = 33, // Created by MDIL binder
        StaticsInfoHashtable                        = 34, // Created by MDIL binder
        GenericMethodsHashtable                     = 35, // Created by MDIL binder
        ExactMethodInstantiationsHashtable          = 36, // Created by MDIL binder
    }
}
