// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.CompilerServices
{
    //
    // DLL interface to the localization infrastructure.
    //
    public static class McgResource
    {
        public static string GetResourceString(string resourceKey, string defaultString)
        {
            return SR.GetResourceString(resourceKey, defaultString);
        }
    }
}
