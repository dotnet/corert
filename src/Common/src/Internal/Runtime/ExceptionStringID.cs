// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Internal.Runtime
{
    /// <summary>
    /// Represents an ID of a localized exception string.
    /// </summary>
    public enum ExceptionStringID
    {
        // As a general guideline, try to use the same ID as mscorrc.rc in CoreCLR uses for the same string.
        // Remove the IDS_ prefix, camel-case, and remove all underscores.

        ClassLoadGeneral,
        ClassLoadMissingMethodRva,

        EeMissingMethod,
        EeMissingField,

        EeFileLoadErrorGeneric,
    }
}
