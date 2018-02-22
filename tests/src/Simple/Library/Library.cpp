#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif
#include <stdio.h>

#ifndef _WIN32
#define __stdcall
#endif

// typedef for shared lib exported methods
typedef int(__stdcall *f_ReturnsPrimitiveInt)();
typedef bool(__stdcall *f_ReturnsPrimitiveBool)();
typedef char(__stdcall *f_ReturnsPrimitiveChar)();
typedef void(__stdcall *f_EnsureManagedClassLoaders)();

int main()
{
#ifdef _WIN32
    HINSTANCE handle = LoadLibrary("Library.dll");
#elif __APPLE__
    void *handle = dlopen("Library.dylib", RTLD_LAZY);
#else
    void *handle = dlopen("Library.so", RTLD_LAZY);
#endif

    if (!handle)
        return 1;

#ifdef _WIN32
    f_ReturnsPrimitiveInt returnsPrimitiveInt = (f_ReturnsPrimitiveInt)GetProcAddress(handle, "ReturnsPrimitiveInt");
    f_ReturnsPrimitiveBool returnsPrimitiveBool = (f_ReturnsPrimitiveBool)GetProcAddress(handle, "ReturnsPrimitiveBool");
    f_ReturnsPrimitiveChar returnsPrimitiveChar = (f_ReturnsPrimitiveChar)GetProcAddress(handle, "ReturnsPrimitiveChar");
    f_EnsureManagedClassLoaders ensureManagedClassLoaders = (f_EnsureManagedClassLoaders)GetProcAddress(handle, "EnsureManagedClassLoaders");
#else
    f_ReturnsPrimitiveInt returnsPrimitiveInt = dlsym(handle, "ReturnsPrimitiveInt");
    f_ReturnsPrimitiveBool returnsPrimitiveBool = dlsym(handle, "ReturnsPrimitiveBool");
    f_ReturnsPrimitiveChar returnsPrimitiveChar = dlsym(handle, "ReturnsPrimitiveChar");
    f_EnsureManagedClassLoaders ensureManagedClassLoaders = dlsym(handle, "EnsureManagedClassLoaders");
#endif

    if (returnsPrimitiveInt() != 10)
        return 1;

    if (!returnsPrimitiveBool())
        return 1;

    if (returnsPrimitiveChar() != 'a')
        return 1;

    // As long as no unmanaged exception is thrown
    // managed class loaders were initialized successfully
    ensureManagedClassLoaders();

    return 100;
}
