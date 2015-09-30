// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

internal class Program
{
    public virtual void foo()
    {
    }

    private static void Main(string[] args)
    {
        var o = new Program();
        o.foo();
    }
}

