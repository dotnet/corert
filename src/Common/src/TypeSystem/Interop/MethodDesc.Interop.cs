// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace Internal.TypeSystem
{
    public abstract partial class MethodDesc
    {
        /// <summary>
        /// Gets a value indicating whether this method is a (native unmanaged) platform invoke.
        /// Use <see cref="GetPInvokeMethodImportMetadata"/> to retrieve the platform invoke detail information.
        /// </summary>
        public virtual bool IsPInvokeImpl
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// If <see cref="IsPInvokeImpl"/> is true, retrieves the metadata related to the platform invoke.
        /// </summary>
        public virtual PInvokeMetadata GetPInvokeMethodImportMetadata()
        {
            return default(PInvokeMetadata);
        }
    }

    public enum CharSet
    {
        Unknown,
        Auto,
        Ansi,
        Unicode,
    }

    /// <summary>
    /// Represents details about a pinvokeimpl method import.
    /// </summary>
    public struct PInvokeMetadata
    {
        public readonly string Name;

        public readonly CharSet CharSet;

        public PInvokeMetadata(string name, CharSet charSet)
        {
            Name = name;
            CharSet = charSet;
        }
    }

}
