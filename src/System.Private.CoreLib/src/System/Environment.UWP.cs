// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public static partial class Environment
    {
        public unsafe static String ExpandEnvironmentVariables(String name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            // Environment variable accessors are not approved modern API.
            // Behave as if no variables are defined in this case.
            return name;
        }

        public unsafe static String GetEnvironmentVariable(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException("variable");

            // Environment variable accessors are not approved modern API.
            // Behave as if the variable was not found in this case.
            return null;
        }

        public static string MachineName
        {
            get
            {
                // Store apps don't support MachineName
                throw new PlatformNotSupportedException();
            }
        }
    }
}
