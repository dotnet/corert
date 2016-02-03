// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <assert.h>

extern "C" int32_t CoreLibNative_GetEnvironmentVariable(const char* variable, char** result)
{
    assert(result != NULL);

    // Read the environment variable
    *result = getenv(variable);

    if (*result == NULL)
    {
        return 0;
    }

    size_t resultSize = strlen(*result);

    // Return -1 if the size overflows an integer so that we can throw on managed side
    if ((size_t)(int32_t)resultSize != resultSize)
    {
        *result = NULL;
        return -1;
    }

    return (int32_t)resultSize;
}

extern "C" int32_t CoreLibNative_GetMachineName(char* hostNameBuffer, int32_t hostNameBufferLength)
{
    assert(hostNameBuffer != NULL && hostNameBufferLength > 0);

    int32_t res = gethostname(hostNameBuffer, hostNameBufferLength);
    if (res < 0)
        return res;

    // If the hostname is truncated, it is unspecified whether the returned buffer includes a terminating null byte.
    hostNameBuffer[hostNameBufferLength - 1] = '\0';

    // truncate the domain from the host name if it exist    
    char *pDot = strchr(hostNameBuffer, '.');
    if (pDot != NULL)
    {
        *pDot = '\0';    
    }
    
    return strlen(hostNameBuffer);
}
