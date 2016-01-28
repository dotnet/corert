// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.Augments;

namespace System.Globalization
{
    public partial class CultureInfo : IFormatProvider
    {
        /// <summary>
        /// Gets the default user culture from WinRT, if available.
        /// </summary>
        /// <remarks>
        /// This method may return null, if there is no default user culture or if WinRT isn't available.
        /// </remarks>
        private static CultureInfo GetUserDefaultCultureCacheOverride()
        {
            return CultureInfo.InvariantCulture;
        }

        private static CultureInfo GetUserDefaultCulture()
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
