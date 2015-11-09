// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// The following type is present only because the C# compiler assumes this type always exists
//

namespace System
{
    // This type will be used only for static fields.  It will simply be the address of the static field in 
    // the loaded PE image.
    internal unsafe struct RuntimeFieldHandle
    {
#pragma warning disable 649 // Field 'blah' is never assigned to, and will always have its default value
        internal byte* m_pbStaticFieldData;
#pragma warning restore
    }
}
