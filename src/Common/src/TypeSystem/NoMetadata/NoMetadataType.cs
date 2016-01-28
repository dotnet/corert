// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.TypeSystem;

namespace Internal.TypeSystem.NoMetadata
{
    /// <summary>
    /// Base type for types that had metadata at one point, but that metadata is 
    /// not accessible for the lifetime of the TypeSystemContext
    /// </summary>
    public abstract class NoMetadataType : DefType
    {
    }
}
