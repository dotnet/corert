// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// Provide support for "well known methods" that are identifiable by tokens from the compiler.
//

#ifndef _PROJECTNWELLKNOWNMETHODS_H
#define _PROJECTNWELLKNOWNMETHODS_H

// Keep in sync with src\Nutc\inc\WellKnownEntryPoints.h

enum WellKnownEntryPoint
{
    WKM_CLASSWITHMISSINGCONSTRUCTOR,        // Fallback default constructor for types with no default constructor
    WKM_GETTHREADSTATICSFORDYNAMICTYPE,     // TypeLoader's helper to get thread static pointer for dynamic type
    WKM_ACTIVATORCREATEINSTANCEANY,         // Allocates and initializes value types or reference types for universal generic types
    WKM_GENERICLOOKUP,                      // Perform a generic lookup using the type loader
    WKM_GENERICLOOKUPANDALLOCOBJECT,        // Perform a generic lookup for a method and call it
    WKM_GENERICLOOKUPANDCALLCTOR,           // Perform a generic lookup for a method and call it
    WKM_GENERICLOOKUPANDALLOCARRAY,         // Perform a generic lookup and call a method passing the lookup result as an argument
    WKM_GENERICLOOKUPANDCAST,               // Perform a generic lookup and call a method passing the lookup result as an argument
    WKM_GENERICLOOKUPANDCHECKARRAYELEMTYPE, // Perform a generic lookup and call a method passing the lookup result as an argument
    WKM_BINDERINTRINSIC_GCSTRESS_FINALIZE,  // The GCStress objects Finalize method
    WKM_DBL2INT_OVF,                        // Convert double to int with overflow check
    WKM_DBL2LNG_OVF,                        // Convert double to long with overflow check
    WKM_DBL2ULNG_OVF,                       // Convert double to ulong with overflow check
    WKM_FLT2INT_OVF,                        // Convert float to int with overflow check
    WKM_FLT2LNG_OVF,                        // Convert float to long with overflow check
    WKM_LMUL_OVF,                           // Int64 multiplication with overflow check
    WKM_ULMUL_OVF,                          // UInt64 multiplication with overflow check
};

#endif
