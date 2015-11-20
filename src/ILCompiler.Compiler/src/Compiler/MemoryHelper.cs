// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler
{
    internal static unsafe class MemoryHelper
    {
        public static void FillMemory(byte* dest, byte fill, int count)
        {
            for (; count > 0; count--)
            {
                *dest = fill;
                dest++;
            }
        }
    }
}
