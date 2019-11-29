#ifdef _WIN32
#include "windows.h"
#else
#include "dlfcn.h"
#define __stdcall
#endif
#include "stdio.h"
#ifdef _WIN32
#define __symLoad GetProcAddress
#else
#define __symLoad dlsym
#endif
int handleErrors(void *handle, int cond)
{

    switch (cond)
    {
    case 0:
    {
        if (!handle)
        {

            printf("Unable to load library, err: %s", dlerror());
            dlclose(handle);
            return 1;
        }
    };

    case 1:
    {
        const char *dlsym_error = dlerror();
        if (dlsym_error)
        {
            printf("Unable to load symbol,err: %s", dlerror());
            dlclose(handle);
            return 1;
        }
    };
    break;
    };

    return 0;
}

int callSumFunc(char *path, char *funcName, int a, int b)
{
//Call sum function defined in C# shared library
#ifdef _WIN32
    HINSTANCE handle = LoadLibrary(path);
#else
    void *handle = dlopen(path, RTLD_LAZY);
#endif

    if (handleErrors(handle, 0))
    {
        return 0;
    } //Error loading library

    typedef int(__stdcall * myFunc)();

    dlerror();
    myFunc MyImport = __symLoad(handle, funcName);

    if (handleErrors(handle, 1))
    {
        return 0;
    } //Error loading symbol

    int result = MyImport(a, b);
    dlclose(handle);
    return result;
}

char *callSumStringFunc(char *path, char *funcName, char *a, char *b)
{

/* Library loading */
#ifdef _WIN32
    HINSTANCE handle = LoadLibrary(path);
#else
    void *handle = dlopen(path, RTLD_LAZY);
#endif

    if (handleErrors(handle, 0))
    {
        return 0;
    } //Error loading library

    /*Declare a typedef*/
    typedef char *(__stdcall * myFunc)();

    dlerror();

    /* Import Symbol named funcName */
    myFunc MyImport = __symLoad(handle, funcName);

    if (handleErrors(handle, 1))
    {
        return 0;
    } //Error loading symbol

    /* The C# function will return a pointer */
    char *result = MyImport(a, b);

    dlclose(handle);

    return result;
}
