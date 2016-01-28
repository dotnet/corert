// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
#include <stdlib.h>
#include <string.h>
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
