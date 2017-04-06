// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.IO
{
    public partial class FileLoadException
    {
        private static string FormatFileLoadExceptionMessage(string fileName, int hResult)
        {
            return fileName == null ? SR.IO_FileLoad : SR.Format(SR.IO_FileLoad_FileName, fileName);
        }
    }
}
