// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;

namespace Internal.Reflection.Tracing
{
    internal interface ITraceableTypeMember
    {
        // Returns the Name value *without recursing into the public Name implementation.*
        String MemberName { get; }

        // Returns the DeclaringType value *without recursing into the public DeclaringType implementation.*
        Type ContainingType { get; }
    }
}
