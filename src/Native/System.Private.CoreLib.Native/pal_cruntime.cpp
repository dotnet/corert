// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <stdlib.h>
#include <assert.h>
#include <stdio.h>


extern "C" int CoreLibNative_DoubleToString(double value, char *format, char *buffer, int bufferLength)
{
    assert(buffer != NULL && format != NULL);
    
    // return number of characters written to the buffer. if the return value greater than bufferLength 
    // means the number of characters would be written to the buffer if there is enough space
    return snprintf(buffer, bufferLength, format, value);
}
