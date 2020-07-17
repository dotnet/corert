// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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


