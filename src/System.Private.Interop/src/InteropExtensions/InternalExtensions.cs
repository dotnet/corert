// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// Internal APIs available on ProjectN but not in ProjectK
    /// </summary>
    static class InternalExtensions
    {
        // Converts the DateTime instance into an OLE Automation compatible
        // double date.
        internal static double ToOADate(this DateTime dateTime)
        {
            throw new NotSupportedException("ToOADate");
        }

        internal static long DoubleDateToTicks(this double value)
        {
            throw new NotSupportedException("DoubleDateToTicks");
        }
    }
}
