// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


// ***
// Define default C export attributes
// ***
#ifdef _WIN32
#define DLL_EXPORT         extern "C" __declspec(dllexport)
#else
#define DLL_EXPORT         extern "C" __attribute((visibility("default")))
#endif // _WIN32


// ***
// Define default call conventions
// ***
#ifndef _X86_

#define DEFAULT_CALL_CONV
#define __cdecl
#define __stdcall

#else // _X86_

#ifndef __stdcall
#define __stdcall          __attribute__((stdcall))
#endif

#ifdef PLATFORM_UNIX
#define DEFAULT_CALL_CONV
#else
#define DEFAULT_CALL_CONV  __stdcall
#endif

#endif // _X86_


#define STDMETHODCALLTYPE  DEFAULT_CALL_CONV
