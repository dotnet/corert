// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;

namespace ReadyToRun.TestHarness
{
    /// <summary>
    /// A set of helper to manipulate paths into a canonicalized form to ensure user-provided paths
    /// match those in the ETW log.
    /// </summary>
    static class PathExtensions
    {
        internal static string ToAbsolutePath(this string argValue) => Path.GetFullPath(argValue);

        internal static string ToAbsoluteDirectoryPath(this string argValue) => argValue.ToAbsolutePath().StripTrailingDirectorySeparators();
        
        internal static string StripTrailingDirectorySeparators(this string str)
        {
            if (String.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            while (str.Length > 0 && str[str.Length - 1] == Path.DirectorySeparatorChar)
            {
                str = str.Remove(str.Length - 1);
            }

            return str;
        }
    }
}
