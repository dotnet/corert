// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Text
{
    public static class EncodingExtension
    {
        public static int GetCodePage(this Encoding encoding)
        {
            return encoding.CodePage;
        }
    }
}
