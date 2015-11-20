// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
