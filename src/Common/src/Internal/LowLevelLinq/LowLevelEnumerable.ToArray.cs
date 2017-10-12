// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Internal.LowLevelLinq
{
    internal static partial class LowLevelEnumerable
    {
        public static T[] ToArray<T>(this IEnumerable<T> values)
        {
            Debug.Assert(values != null);

            List<T> list = new List<T>();
            foreach (T value in values)
            {
                list.Add(value);
            }
            return list.ToArray();
        }
    }
}
