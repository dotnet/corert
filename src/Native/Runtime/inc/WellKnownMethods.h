//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

//
// Provide support for "well known methods". These are methods known to the binder and runtime and identified
// purely by a native callable name. If your module defines a native callable method with one of these names
// then we expect it to conform to the corresponding contract. See WellKnownMethodList.h for a list of these
// names.
//

#ifndef __WELLKNOWNMETHODS_INCLUDED
#define __WELLKNOWNMETHODS_INCLUDED

// Define enum constants of the form WKM_<name> for each well known method. WKM_COUNT is defined as the number
// of methods we currently know about.
#define DEFINE_WELL_KNOWN_METHOD(_name) WKM_##_name,
enum WellKnownMethodIds
{
#include "WellKnownMethodList.h"
    WKM_COUNT
};
#undef DEFINE_WELL_KNOWN_METHOD

// Define an array of well known method names which are indexed by the enums defined above.
#define DEFINE_WELL_KNOWN_METHOD(_name) #_name,
extern __declspec(selectany) const char * const g_rgWellKnownMethodNames[] = 
{
#include "WellKnownMethodList.h"
};
#undef DEFINE_WELL_KNOWN_METHOD

#endif // !__WELLKNOWNMETHODS_INCLUDED
