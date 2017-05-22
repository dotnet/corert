// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection.Runtime.General
{
    // This interface's presence on a MemberInfo testates that
    //
    //    1. The MemberInfo implemented by Reflection.Core
    //    2. Is to be lumped into the "no metadata token" group for the purposes
    //       of the HasSameMetadataDefinitionAs() api.
    //
    internal interface IRuntimeMemberInfoWithNoMetadataDefinition
    {
    }
}
