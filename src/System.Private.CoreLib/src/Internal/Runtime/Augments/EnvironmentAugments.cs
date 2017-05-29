// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.Augments
{
    /// <summary>For internal use only.  Exposes runtime functionality to the Environments implementation in corefx.</summary>
    public static partial class EnvironmentAugments
    {
        public static string GetEnvironmentVariable(string variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));
            return GetEnvironmentVariableCore(variable);
        }

        public static void SetEnvironmentVariable(string variable, string value)
        {
            ValidateVariableAndValue(variable, ref value);
            SetEnvironmentVariableCore(variable, value);
        }

        private static void ValidateVariableAndValue(string variable, ref string value)
        {
            const int MaxEnvVariableValueLength = 32767;

            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            if (variable.Length == 0)
                throw new ArgumentException(SR.Argument_StringZeroLength, nameof(variable));

            if (variable[0] == '\0')
                throw new ArgumentException(SR.Argument_StringFirstCharIsZero, nameof(variable));

            if (variable.Length >= MaxEnvVariableValueLength)
                throw new ArgumentException(SR.Argument_LongEnvVarValue, nameof(variable));

            if (variable.IndexOf('=') != -1)
                throw new ArgumentException(SR.Argument_IllegalEnvVarName, nameof(variable));

            if (string.IsNullOrEmpty(value) || value[0] == '\0')
            {
                // Explicitly null out value if it's empty
                value = null;
            }
            else if (value.Length >= MaxEnvVariableValueLength)
            {
                throw new ArgumentException(SR.Argument_LongEnvVarValue, nameof(value));
            }
        }

        // TODO Perf: Once CoreCLR gets PopulateEnvironmentVariables(), get rid of GetEnvironmentVariables() and have 
        // corefx call PopulateEnvironmentVariables() instead so we don't have to create a dictionary just to copy it into
        // another dictionary.
        public static IDictionary GetEnvironmentVariables()
        {
            IDictionary dictionary = new Dictionary<string, string>(EnumerateEnvironmentVariables());
            return dictionary;
        }

        public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
                return GetEnvironmentVariable(variable);
            throw new NotImplementedException();
        }

        public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
            {
                SetEnvironmentVariable(variable, value);
                return;
            }
            throw new NotImplementedException();
        }

        public static IDictionary GetEnvironmentVariables(EnvironmentVariableTarget target)
        {
            if (target == EnvironmentVariableTarget.Process)
                return GetEnvironmentVariables();
            throw new NotImplementedException();
        }
    }
}
