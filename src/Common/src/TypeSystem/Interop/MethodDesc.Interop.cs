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
        /// Use <see cref="GetPInvokeMethodMetadata"/> to retrieve the platform invoke detail information.
        /// </summary>
        public virtual bool IsPInvoke
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// If <see cref="IsPInvoke"/> is true, retrieves the metadata related to the platform invoke.
        /// </summary>
        public virtual PInvokeMetadata GetPInvokeMethodMetadata()
        {
            return default(PInvokeMetadata);
        }
    }

    [Flags]
    public enum PInvokeAttributes : short
    {
        None = 0,
        ExactSpelling = 1,
        CharSetAnsi = 2,
        CharSetUnicode = 4,
        CharSetAuto = 6,
        CharSetMask = 6,
        BestFitMappingEnable = 16,
        BestFitMappingDisable = 32,
        BestFitMappingMask = 48,
        SetLastError = 64,
        CallingConventionWinApi = 256,
        CallingConventionCDecl = 512,
        CallingConventionStdCall = 768,
        CallingConventionThisCall = 1024,
        CallingConventionFastCall = 1280,
        CallingConventionMask = 1792,
        ThrowOnUnmappableCharEnable = 4096,
        ThrowOnUnmappableCharDisable = 8192,
        ThrowOnUnmappableCharMask = 12288
    }

    /// <summary>
    /// Represents details about a pinvokeimpl method import.
    /// </summary>
    public struct PInvokeMetadata
    {
        public readonly string Name;

        public readonly PInvokeAttributes Attributes;

        public PInvokeMetadata(string name, PInvokeAttributes attributes)
        {
            Name = name;
            Attributes = attributes;
        }
    }

}
