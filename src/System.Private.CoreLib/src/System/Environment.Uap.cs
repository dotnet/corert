// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
** Purpose: Provides some basic access to some environment 
** functionality.
**
**
============================================================*/

using System.Text;
using System.Collections;

namespace System
{
    internal static partial class Environment
    {
        public static unsafe String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            // Environment variable accessors are not approved modern API.
            // Behave as if the variable was not found in this case.
            return null;
        }
    }
}
