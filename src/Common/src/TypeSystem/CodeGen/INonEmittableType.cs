﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.TypeSystem
{
    /// <summary>
    /// Used to mark TypeDesc types that are not part of the core type system
    /// that should never be turned into an EEType.
    /// </summary>
    public interface INonEmittableType
    { }
}
