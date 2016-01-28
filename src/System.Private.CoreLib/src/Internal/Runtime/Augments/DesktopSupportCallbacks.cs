// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>
    /// This helper class is used to provide desktop support quirks to the runtime.
    /// This is currently used to provide resources to console lab tests. To activate the quirks,
    /// set up an instance of a class derived from this one using the method
    ///
    /// Internal.Runtime.Augments.RuntimeAugments.InitializeDesktopSupportCallbacks(DesktopSupportCallbacks callbacks);
    ///
    /// </summary>
    [CLSCompliant(false)]
    public abstract class DesktopSupportCallbacks
    {
        /// <summary>
        /// Helper function to open an "application package" file. It actually returns a Stream
        /// but we cannot use that signature here due to layering limitations.
        /// </summary>
        /// <param name="fileName">File path / name</param>
        /// <returns>An initialized Stream instance or null when the file doesn't exist;
        /// throws when compat quirks are not enabled</returns>
        public abstract object OpenFileIfExists(string fileName);
    }
}
