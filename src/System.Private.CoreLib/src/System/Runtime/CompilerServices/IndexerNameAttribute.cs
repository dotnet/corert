// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class IndexerNameAttribute : Attribute
    {
        public IndexerNameAttribute(String indexerName)
        {
        }
    }
}
