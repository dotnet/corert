// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Collections.Generic;

using global::Internal.Metadata.NativeFormat;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.TypeLoader
{
    public static class MetadataReaderExtensions
    {
        /// <summary>
        /// Convert raw token to a typed metadata handle.
        /// </summary>
        /// <param name="token">Token - raw integral handle representation</param>
        /// <returns>Token converted to handle</param>
        public static unsafe Handle AsHandle(this int token)
        {
            return *(Handle*)&token;
        }

        /// <summary>
        /// Convert raw token to a typed metadata handle.
        /// </summary>
        /// <param name="token">Token - raw integral handle representation</param>
        /// <returns>Token converted to handle</param>
        public static unsafe Handle AsHandle(this uint token)
        {
            return *(Handle*)&token;
        }
    }
}
