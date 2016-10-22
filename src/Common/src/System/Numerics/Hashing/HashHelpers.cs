// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Numerics.Hashing
{
    // Please change the corresponding file in corefx if this is changed.

    internal static class HashHelpers
    {
        public static int Combine(int h1, int h2)
        {
            // This should get optimized to use a single ROL instruction.
            uint shift5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)shift5 + h1) ^ h2;
        }
    }
}
