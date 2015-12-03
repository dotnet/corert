// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Internal.DeveloperExperience
{
    public static class DeveloperExperienceConnectorConsole
    {
        // This method is targeted by the MainMethodInjector transform. This method exists in various DeveloperExperience.*.dll's.
        // The MainMethodInjector chooses an appropriate one (or none) depending on the build configuration and app structure.
        //
        // The Console DeveloperExperience is chosen if the main exe has a reference to System.Console. This is for internal Microsoft use only.
        // It should remain small enough that we don't bother shutting it off on retail builds.
        public static void Initialize()
        {
            DeveloperExperience.Default = new DeveloperExperienceConsole();
            return;
        }
    }
}


