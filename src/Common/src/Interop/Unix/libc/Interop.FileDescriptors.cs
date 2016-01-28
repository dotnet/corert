// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

internal static partial class Interop
{
    internal static partial class Sys
    {
        internal static class FileDescriptors
        {
            internal const int STDIN_FILENO = 0;
            internal const int STDOUT_FILENO = 1;
            internal const int STDERR_FILENO = 2;
        }
    }
}
