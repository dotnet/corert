// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Internal.NativeFormat
{
#if NATIVEFORMAT_PUBLICWRITER
    public
#else
    internal
#endif
    static class NativeFormatWriterExtensions
    {
        public static byte[] Save(this NativeWriter writer)
        {
            MemoryStream ms = new MemoryStream();
            writer.Save(ms);
            return ms.ToArray();
        }
    }
}
