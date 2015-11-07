// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Api surface definition for interfaces that all MetadataTypes must implement

    public abstract partial class MetadataType : DefType
    {
        /// <summary>
        /// The interfaces explicitly declared as implemented by this MetadataType. Duplicates are not permitted.
        /// These correspond to the InterfaceImpls of a type in metadata
        /// </summary>
        public abstract DefType[] ExplicitlyImplementedInterfaces
        {
            get;
        }
    }
}
