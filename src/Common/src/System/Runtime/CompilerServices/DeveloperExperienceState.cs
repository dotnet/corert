// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.CompilerServices
{
    [DeveloperExperienceModeOnly]
    internal static class DeveloperExperienceState
    {
        public static bool DeveloperExperienceModeEnabled
        {
            get
            {
                return true;  // ILC will rewrite to this "return false" if run with "/buildType:ret"
            }
        }
    }
}

