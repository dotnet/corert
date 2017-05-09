// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace System
{
    public static partial class AppContext
    {
        public static string BaseDirectory
        {
            get
            {
                string path;
                bool found = Interop.Sys.GetEntrypointExecutableAbsolutePath(out path);
                if (!found)
                {
                  // TODO: throw appropriate exception
                   throw new TypeLoadException("GetEntrypointExecutableAbsolutePath failed");
                }
                return path.Substring(0, path.LastIndexOf('/'));
            }
        }
    }
}
