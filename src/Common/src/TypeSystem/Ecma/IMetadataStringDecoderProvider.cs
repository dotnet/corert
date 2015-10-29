// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection.Metadata;

namespace Internal.TypeSystem.Ecma
{
    /// <summary>
    /// Interface implemented by TypeSystemContext to provide MetadataStringDecoder
    /// instance for MetadataReaders created by the type system.
    /// </summary>
    public interface IMetadataStringDecoderProvider
    {
        MetadataStringDecoder GetMetadataStringDecoder();
    }
}
