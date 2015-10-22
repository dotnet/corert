// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

internal class Program
{
    private static int Main(string[] args)
    {
        //Console.WriteLine("Hello world");
        //"Hello world".StartsWith("Hell");
        var arr = new int[1];
        switch (arr.Length)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            case 2:
                return 2;
            case 4:
                return 4;
        }
        return 255;
    }
}

