// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Threading;

using Internal.Metadata.NativeFormat;
using Internal.Runtime;
using Internal.Runtime.CompilerServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    public sealed class NativeFormatMethod : MethodDesc, NativeFormatMetadataUnit.IHandleObject
    {
        private static class MethodFlags
        {
            public const int BasicMetadataCache = 0x0001;
            public const int Virtual = 0x0002;
            public const int NewSlot = 0x0004;
            public const int Abstract = 0x0008;
            public const int Final = 0x0010;
            public const int NoInlining = 0x0020;
            public const int AggressiveInlining = 0x0040;
            public const int RuntimeImplemented = 0x0080;
            public const int InternalCall = 0x0100;
            public const int Synchronized = 0x0200;

            public const int AttributeMetadataCache = 0x1000;
            public const int Intrinsic = 0x2000;
            public const int UnmanagedCallersOnly = 0x4000;
            public const int RuntimeExport = 0x8000;
        };

        private NativeFormatType _type;
        private MethodHandle _handle;

        // Cached values
        private ThreadSafeFlags _methodFlags;
        private MethodSignature _signature;
        private string _name;
        private TypeDesc[] _genericParameters; // TODO: Optional field?

        internal NativeFormatMethod(NativeFormatType type, MethodHandle handle)
        {
            _type = type;
            _handle = handle;

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            InitializeName();
#endif
        }

        Handle NativeFormatMetadataUnit.IHandleObject.Handle
        {
            get
            {
                return _handle;
            }
        }

        NativeFormatType NativeFormatMetadataUnit.IHandleObject.Container
        {
            get
            {
                return _type;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _type.Module.Context;
            }
        }

        public override TypeDesc OwningType
        {
            get
            {
                return _type;
            }
        }

        private MethodSignature InitializeSignature()
        {
            var metadataReader = MetadataReader;

            NativeFormatSignatureParser parser = new NativeFormatSignatureParser(MetadataUnit, MetadataReader.GetMethod(_handle).Signature, metadataReader);
            var signature = parser.ParseMethodSignature();
            return (_signature = signature);
        }

        public override MethodSignature Signature
        {
            get
            {
                if (_signature == null)
                    return InitializeSignature();
                return _signature;
            }
        }

        public NativeFormatModule NativeFormatModule
        {
            get
            {
                return _type.NativeFormatModule;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public NativeFormatMetadataUnit MetadataUnit
        {
            get
            {
                return _type.MetadataUnit;
            }
        }

        public MethodHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int InitializeMethodFlags(int mask)
        {
            int flags = 0;

            if ((mask & MethodFlags.BasicMetadataCache) != 0)
            {
                var methodAttributes = Attributes;
                var methodImplAttributes = ImplAttributes;

                if ((methodAttributes & MethodAttributes.Virtual) != 0)
                    flags |= MethodFlags.Virtual;

                if ((methodAttributes & MethodAttributes.NewSlot) != 0)
                    flags |= MethodFlags.NewSlot;

                if ((methodAttributes & MethodAttributes.Abstract) != 0)
                    flags |= MethodFlags.Abstract;

                if ((methodAttributes & MethodAttributes.Final) != 0)
                    flags |= MethodFlags.Final;

                if ((methodImplAttributes & MethodImplAttributes.NoInlining) != 0)
                    flags |= MethodFlags.NoInlining;

                if ((methodImplAttributes & MethodImplAttributes.AggressiveInlining) != 0)
                    flags |= MethodFlags.AggressiveInlining;

                if ((methodImplAttributes & MethodImplAttributes.Runtime) != 0)
                    flags |= MethodFlags.RuntimeImplemented;

                if ((methodImplAttributes & MethodImplAttributes.InternalCall) != 0)
                    flags |= MethodFlags.InternalCall;

                if ((methodImplAttributes & MethodImplAttributes.Synchronized) != 0)
                    flags |= MethodFlags.Synchronized;

                flags |= MethodFlags.BasicMetadataCache;
            }

            // Fetching custom attribute based properties is more expensive, so keep that under
            // a separate cache that might not be accessed very frequently.
            if ((mask & MethodFlags.AttributeMetadataCache) != 0)
            {
                var metadataReader = this.MetadataReader;
                var methodDefinition = MetadataReader.GetMethod(_handle);

                foreach (var attributeHandle in methodDefinition.CustomAttributes)
                {
                    ConstantStringValueHandle nameHandle;
                    string namespaceName;

                    if (!metadataReader.GetAttributeNamespaceAndName(attributeHandle, out namespaceName, out nameHandle))
                        continue;

                    if (namespaceName.Equals("System.Runtime.CompilerServices"))
                    {
                        if (nameHandle.StringEquals("IntrinsicAttribute", metadataReader))
                        {
                            flags |= MethodFlags.Intrinsic;
                        }
                    }
                    else
                    if (namespaceName.Equals("System.Runtime.InteropServices"))
                    {
                        if (nameHandle.StringEquals("UnmanagedCallersOnlyAttribute", metadataReader))
                        {
                            flags |= MethodFlags.UnmanagedCallersOnly;
                        }
                    }
                    else
                    if (namespaceName.Equals("System.Runtime"))
                    {
                        if (nameHandle.StringEquals("RuntimeExportAttribute", metadataReader))
                        {
                            flags |= MethodFlags.RuntimeExport;
                        }
                    }
                }

                flags |= MethodFlags.AttributeMetadataCache;
            }

            Debug.Assert((flags & mask) != 0);
            _methodFlags.AddFlags(flags);

            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMethodFlags(int mask)
        {
            int flags = _methodFlags.Value & mask;
            if (flags != 0)
                return flags;
            return InitializeMethodFlags(mask);
        }

        public override bool IsVirtual
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Virtual) & MethodFlags.Virtual) != 0;
            }
        }

        public override bool IsNewSlot
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.NewSlot) & MethodFlags.NewSlot) != 0;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Abstract) & MethodFlags.Abstract) != 0;
            }
        }

        public override bool IsFinal
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Final) & MethodFlags.Final) != 0;
            }
        }

        public override bool IsNoInlining
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.NoInlining) & MethodFlags.NoInlining) != 0;
            }
        }

        public override bool IsAggressiveInlining
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.AggressiveInlining) & MethodFlags.AggressiveInlining) != 0;
            }
        }

        public override bool IsRuntimeImplemented
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.RuntimeImplemented) & MethodFlags.RuntimeImplemented) != 0;
            }
        }

        public override bool IsIntrinsic
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.Intrinsic) & MethodFlags.Intrinsic) != 0;
            }
        }

        public override bool IsInternalCall
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.InternalCall) & MethodFlags.InternalCall) != 0;
            }
        }

        public override bool IsSynchronized
        {
            get
            {
                return (GetMethodFlags(MethodFlags.BasicMetadataCache | MethodFlags.Synchronized) & MethodFlags.Synchronized) != 0;
            }
        }

        public override bool IsUnmanagedCallersOnly
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.UnmanagedCallersOnly) & MethodFlags.UnmanagedCallersOnly) != 0;
            }
        }

        public override bool IsRuntimeExport
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.RuntimeExport) & MethodFlags.RuntimeExport) != 0;
            }
        }

        public override bool IsDefaultConstructor
        {
            get
            {
                MethodAttributes attributes = Attributes;
                return attributes.IsRuntimeSpecialName() 
                    && attributes.IsPublic()
                    && Signature.Length == 0
                    && Name == ".ctor"
                    && !_type.IsAbstract;
            }
        }

        public MethodAttributes Attributes
        {
            get
            {
                return MetadataReader.GetMethod(_handle).Flags;
            }
        }

        public MethodImplAttributes ImplAttributes
        {
            get
            {
                return MetadataReader.GetMethod(_handle).ImplFlags;
            }
        }

        private string InitializeName()
        {
            var metadataReader = MetadataReader;
            var name = metadataReader.GetString(MetadataReader.GetMethod(_handle).Name);
            return (_name = name);
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    return InitializeName();
                return _name;
            }
        }

        private void ComputeGenericParameters()
        {
            var genericParameterHandles = MetadataReader.GetMethod(_handle).GenericParameters;
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new NativeFormatGenericParameter(MetadataUnit, genericParameterHandle);
                }
                Interlocked.CompareExchange(ref _genericParameters, genericParameters, null);
            }
            else
            {
                _genericParameters = TypeDesc.EmptyTypes;
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                if (_genericParameters == null)
                    ComputeGenericParameters();
                return new Instantiation(_genericParameters);
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return MetadataReader.HasCustomAttribute(MetadataReader.GetMethod(_handle).CustomAttributes,
                attributeNamespace, attributeName);
        }

        public override MethodNameAndSignature NameAndSignature
        {
            get
            {
                int handleAsToken = _handle.ToInt();

                TypeManagerHandle moduleHandle = Internal.Runtime.TypeLoader.ModuleList.Instance.GetModuleForMetadataReader(MetadataReader);
                return new MethodNameAndSignature(Name, RuntimeSignature.CreateFromMethodHandle(moduleHandle, handleAsToken));
            }
        }
    }
}
