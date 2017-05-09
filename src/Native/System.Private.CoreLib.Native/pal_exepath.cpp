// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <cstdlib>
#include <cstring>
#include <assert.h>
#include <dirent.h>
#include <dlfcn.h>
#include <limits.h>
#include <set>
#include <string>
#include <string.h>
#include <sys/stat.h>
#if defined(__FreeBSD__)
#include <sys/types.h>
#include <sys/param.h>
#endif
#if defined(HAVE_SYS_SYSCTL_H) || defined(__FreeBSD__)
#include <sys/sysctl.h>
#endif
#if defined(__APPLE__)
#include <mach-o/dyld.h>
#endif
#include <unistd.h>

#if defined(__linux__)
#define symlinkEntrypointExecutable "/proc/self/exe"
#elif !defined(__APPLE__)
#define symlinkEntrypointExecutable "/proc/curproc/exe"
#endif

extern "C" bool CoreLibNative_GetEntrypointExecutableAbsolutePath(char **buf)
{
    bool result = false;
    *buf = NULL;
    
    // Get path to the executable for the current process using
    // platform specific means.
#if defined(__APPLE__)
    
    // On Mac, we ask the OS for the absolute path to the entrypoint executable
    uint32_t lenActualPath = 0;
    if (_NSGetExecutablePath(nullptr, &lenActualPath) == -1)
    {
        // OSX has placed the actual path length in lenActualPath,
        // so re-attempt the operation
        char *pResizedPath = (char *)malloc(sizeof(char) * lenActualPath);
        if (_NSGetExecutablePath(pResizedPath, &lenActualPath) == 0)
        {
            *buf = pResizedPath;
            result = true;
        }
    }
#elif defined (__FreeBSD__)
    static const int name[] = {
        CTL_KERN, KERN_PROC, KERN_PROC_PATHNAME, -1
    };
    char *path = (char *)malloc(sizeof(char) * PATH_MAX);
    size_t len;

    len = sizeof(path);
    if (sysctl(name, 4, path, &len, nullptr, 0) == 0)
    {
        *buf = path;
        result = true;
    }
#elif defined(__NetBSD__) && defined(KERN_PROC_PATHNAME)
    static const int name[] = {
        CTL_KERN, KERN_PROC_ARGS, -1, KERN_PROC_PATHNAME,
    };
    char *path = (char *)malloc(sizeof(char) * MAXPATHLEN);
    size_t len;

    len = sizeof(path);
    if (sysctl(name, __arraycount(name), path, &len, NULL, 0) != -1)
    {
        *buf = path;
        result = true;
    }
#else
    // On other OSs, return the symlink that will be resolved 
    // to fetch the entrypoint EXE absolute path, inclusive of filename.
    char *realPath = (char *)malloc(sizeof(char) * PATH_MAX);
    if (realpath(symlinkEntrypointExecutable, realPath) != nullptr && realPath[0] != '\0')
    {
        *buf = realPath;
        result = true;
    }
#endif 

    return result;
}
