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

        public static void Exit(int exitCode)
        {
            // Store apps have their lifetime managed by the PLM
            throw new PlatformNotSupportedException();
        }
    }
}
