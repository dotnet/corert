// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Extension to TargetDetails related to code generation
    partial class TargetDetails
    {
        public TargetDetails(TargetArchitecture architecture, TargetOS targetOS, TargetAbi abi, MaximumSimdVectorLength simdVectorLength)
            : this(architecture, targetOS, abi)
        {
            MaximumSimdVectorLength = simdVectorLength;
        }

        public MaximumSimdVectorLength MaximumSimdVectorLength
        {
            get;
        }
    }

    public enum MaximumSimdVectorLength
    {
        None,
        VectorLength16,
        VectorLength32,
    }
}
