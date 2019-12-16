// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
/*
On unix make sure to compile using -ldl flag.
*/
#include <unistd.h>
#include <stdlib.h>
#include <stdio.h>
#include "loadlibrary.c"
#define path "../bin/Debug/netstandard2.0/linux-x64/native/NativeLibrary.so"

int main()
{
    //Check if the library file exists
    if (access(path, F_OK) == -1)
    {
        puts("Couldn't find library at the specified path");
        return 0;
    }

    // Sum two integers
    int sum = callSumFunc(path, "add", 2, 8);
    printf("The sum is %d \n", sum);

    //Concatenate two strings
    char *sumstring = callSumStringFunc(path, "sumstring", "ok", "ko");
    printf("The concatenated string is %s \n", sumstring);

    //Free string
    free(sumstring);
}
