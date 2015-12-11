// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  ResourceLocation
**
==============================================================*/

using global::System;

namespace System.Reflection
{
    [Flags]
    public enum ResourceLocation
    {
        ContainedInAnotherAssembly = 2,
        ContainedInManifestFile = 4,
        Embedded = 1,
    }
}

