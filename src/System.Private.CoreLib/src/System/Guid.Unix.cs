// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;

namespace System
{
    partial struct Guid
    {
        // This will create a new guid.  Since we've now decided that constructors should 0-init,
        // we need a method that allows users to create a guid.
        public static Guid NewGuid()
        {
            // CoCreateGuid should never return Guid.Empty, since it attempts to maintain some
            // uniqueness guarantees.  It should also never return a known GUID, but it's unclear
            // how extensively it checks for known values.
            Contract.Ensures(Contract.Result<Guid>() != Guid.Empty);

            Interop.Sys.CreateGuid(out Guid g);
            return g;
        }
    }
}
