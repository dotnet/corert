// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;
using System.Collections;
using System.Runtime;
using System.Runtime.InteropServices;
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

            return Marshal.PtrToStringAnsi(Interop.Sys.GetEnv(variable));
        }

        public static IDictionary GetEnvironmentVariables()
        {
            if ("".Length != 0)
                throw new NotImplementedException(); // TODO: https://github.com/dotnet/corert/issues/3688 Need to implement GetEnvironmentVariables() properly.
            return new LowLevelListDictionary();
        }

        public static IDictionary GetEnvironmentVariables(EnvironmentVariableTarget target) { throw new NotImplementedException(); }
        public static string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target) { throw new NotImplementedException(); }
        public static void SetEnvironmentVariable(string variable, string value) { throw new NotImplementedException(); }
        public static void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target) { throw new NotImplementedException(); }
    }
}
