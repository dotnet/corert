// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // Base class for a policy that maps dispense scenarios to the caching algorithm used.
    //
    internal abstract class DispenserPolicy
    {
        public abstract DispenserAlgorithm GetAlgorithm(DispenserScenario scenario);
    }
}


