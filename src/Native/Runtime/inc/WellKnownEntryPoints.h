//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
    WKM_BINDERINTRINSIC_DEBUGBREAK,         // Inject a breakpoint instead of actually calling this method
    WKM_BINDERINTRINSIC_GETRETURNADDRESS,   // Get the return address from the function that called this method
    WKM_BINDERINTRINSIC_TAILCALL_RHPTHROWEX,// Tail-call the throw helper.
};

#endif
