// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
  Type:  MissingMetadataException
**
==============================================================*/

using System;

namespace System.Reflection
{
    public sealed class MissingMetadataException : TypeAccessException
    {
        public MissingMetadataException()
        {
        }

        public MissingMetadataException(String message)
            : base(message)
        {
        }
    }
}
