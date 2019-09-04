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
#if _X86_
#ifdef PLATFORM_UNIX
#define STDMETHODCALLTYPE  __attribute__((stdcall))
#else
#define STDMETHODCALLTYPE  __stdcall
#endif // PLATFORM_UNIX
#else
#define STDMETHODCALLTYPE
#endif // _X86_
