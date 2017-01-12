// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        /// <summary>
        /// Retrieves the metadata related to the parameters of the method.
        /// </summary>
        public virtual ParameterMetadata[] GetParameterMetadata()
        {
            return default(ParameterMetadata[]);
        }
    }

    [Flags]
    public enum ParameterMetadataAttributes
    {
        None = 0,
        In = 1,
        Out = 2,
        Optional = 16,
        HasDefault = 4096,
        HasFieldMarshal = 8192
    }

    public struct ParameterMetadata
    {
        private  readonly ParameterMetadataAttributes _attributes;
        public readonly MarshalAsDescriptor MarshalAsDescriptor;
        public readonly int Index;

        public bool In { get { return (_attributes & ParameterMetadataAttributes.In) == ParameterMetadataAttributes.In; } }
        public bool Out { get { return (_attributes & ParameterMetadataAttributes.Out) == ParameterMetadataAttributes.Out; } }
        public bool Return { get { return Index == 0;  } }
        public bool Optional { get { return (_attributes & ParameterMetadataAttributes.Optional) == ParameterMetadataAttributes.Optional;  } }
        public bool HasDefault { get { return (_attributes & ParameterMetadataAttributes.HasDefault) == ParameterMetadataAttributes.HasDefault; } }
        public bool HasFieldMarshal { get { return (_attributes & ParameterMetadataAttributes.HasFieldMarshal) == ParameterMetadataAttributes.HasFieldMarshal; } }


        public ParameterMetadata(int index, ParameterMetadataAttributes attributes, MarshalAsDescriptor marshalAsDescriptor)
        {
            Index = index;
            _attributes = attributes;
            MarshalAsDescriptor = marshalAsDescriptor;
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

        public readonly string Module;

        public readonly PInvokeAttributes Attributes;

        public PInvokeMetadata(string module, string entrypoint, PInvokeAttributes attributes)
        {
            Name = entrypoint;
            Module = module;
            Attributes = attributes;
        }

        /// <summary>
        /// Converts unmanaged calling convention encoded as PInvokeAttributes to unmanaged 
        /// calling convention encoded as MethodSignatureFlags.
        /// </summary>
        public static MethodSignatureFlags GetUnmanagedCallingConvention(PInvokeAttributes attributes)
        {
            switch (attributes & PInvokeAttributes.CallingConventionMask)
            {
                case PInvokeAttributes.CallingConventionWinApi:
                    return MethodSignatureFlags.UnmanagedCallingConventionStdCall; // TODO: CDecl for varargs
                case PInvokeAttributes.CallingConventionCDecl:
                    return MethodSignatureFlags.UnmanagedCallingConventionCdecl;
                case PInvokeAttributes.CallingConventionStdCall:
                    return MethodSignatureFlags.UnmanagedCallingConventionStdCall;
                case PInvokeAttributes.CallingConventionThisCall:
                    return MethodSignatureFlags.UnmanagedCallingConventionThisCall;
                default:
                    throw new BadImageFormatException();
            }
        }
    }
}
