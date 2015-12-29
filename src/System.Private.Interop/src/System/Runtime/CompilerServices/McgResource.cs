// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
