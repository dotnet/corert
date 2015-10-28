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
        private const int MaxEnvVariableValueLength = 32767;  // maximum length for environment variable name and value

        public static IDictionary GetEnvironmentVariables()
        {
            // Environment variable accessors are not approved modern API.
            // Behave as if no environment variables are defined in this case.
            return new ListDictionaryInternal(); ;
        }

        public static void SetEnvironmentVariable(String variable, String value)
        {
            CheckEnvironmentVariableName(variable);

            if (value.Length >= MaxEnvVariableValueLength)
                throw new ArgumentException(SR.Argument_LongEnvVarValue);

            // Environment variable accessors are not approved modern API.
            // so we throw PlatformNotSupportedException.
            throw new PlatformNotSupportedException();
        }

        private static void CheckEnvironmentVariableName(String variable)
        {
            if (variable == null)
                throw new ArgumentNullException("variable");

            if (variable.Length == 0)
                throw new ArgumentException(SR.Argument_StringZeroLength);

            if (variable[0] == '\0')
                throw new ArgumentException(SR.Argument_StringFirstCharIsZero);

            // Make sure the environment variable name isn't longer than the 
            // max limit on environment variable values.  (MSDN is ambiguous 
            // on whether this check is necessary.)
            if (variable.Length >= MaxEnvVariableValueLength)
                throw new ArgumentException(SR.Argument_LongEnvVarValue);

            if (variable.IndexOf('=') != -1)
                throw new ArgumentException(SR.Argument_IllegalEnvVarName);
        }
    }
}
