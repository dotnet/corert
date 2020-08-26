// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include "stdio.h"

extern "C" int Add(int a, int b);
extern "C" int Subtract(int a, int b);
extern "C" bool Not(bool b);

int main()
{
    if (Add(2, 3) != 5)
        return 1;

    if (Subtract(3, 1) != 2)
        return 1;

    if (!Not(false))
        return 1;

    return 100;
}
