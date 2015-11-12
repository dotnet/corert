// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed class EcmaMethod : MethodDesc, EcmaModule.IEntityHandleObject
    {
        static class MethodFlags
        {
            public const int BasicMetadataCache     = 0x0001;
            public const int Virtual                = 0x0002;
            public const int NewSlot                = 0x0004;
            public const int Abstract               = 0x0008;
            public const int Final                  = 0x0010;
            public const int NoInlining             = 0x0020;
            public const int AggressiveInlining     = 0x0040;

            public const int AttributeMetadataCache = 0x0100;
            public const int Intrinsic            = 0x0200;
        };

        EcmaType _type;
        MethodDefinitionHandle _handle;

        // Cached values
        ThreadSafeFlags _methodFlags;
        MethodSignature _signature;
        string _name;
        TypeDesc[] _genericParameters; // TODO: Optional field?

        internal EcmaMethod(EcmaType type, MethodDefinitionHandle handle)
        {
            _type = type;
            _handle = handle;

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            this.ToString();
#endif
        }

        EntityHandle EcmaModule.IEntityHandleObject.Handle
        {
            get
            {
                return _handle;
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
            BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetMethodDefinition(_handle).Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(Module, signatureReader);
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
 
        public EcmaModule Module
        {
            get
            {
                return _type.Module;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public MethodDefinitionHandle Handle
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

                flags |= MethodFlags.BasicMetadataCache;
            }

            // Fetching custom attribute based properties is more expensive, so keep that under
            // a separate cache that might not be accessed very frequently.
            if ((mask & MethodFlags.AttributeMetadataCache) != 0)
            {
                var metadataReader = this.MetadataReader;
                var methodDefinition = metadataReader.GetMethodDefinition(_handle);

                foreach (var attributeHandle in methodDefinition.GetCustomAttributes())
                {
                    StringHandle namespaceHandle, nameHandle;
                    if (!metadataReader.GetAttributeNamespaceAndName(attributeHandle, out namespaceHandle, out nameHandle))
                        continue;

                    if (metadataReader.StringComparer.Equals(namespaceHandle, "System.Runtime.CompilerServices"))
                    {
                        if (metadataReader.StringComparer.Equals(nameHandle, "IntrinsicAttribute"))
                        {
                            flags |= MethodFlags.Intrinsic;
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

        public override bool IsIntrinsic
        {
            get
            {
                return (GetMethodFlags(MethodFlags.AttributeMetadataCache | MethodFlags.Intrinsic) & MethodFlags.Intrinsic) != 0;
            }
        }

        public MethodAttributes Attributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).Attributes;
            }
        }

        public MethodImplAttributes ImplAttributes
        {
            get
            {
                return MetadataReader.GetMethodDefinition(_handle).ImplAttributes;
            }
        }

        private string InitializeName()
        {
            var metadataReader = MetadataReader;
            var name = metadataReader.GetString(metadataReader.GetMethodDefinition(_handle).Name);
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

        void ComputeGenericParameters()
        {
            var genericParameterHandles = MetadataReader.GetMethodDefinition(_handle).GetGenericParameters();
            int count = genericParameterHandles.Count;
            if (count > 0)
            {
                TypeDesc[] genericParameters = new TypeDesc[count];
                int i = 0;
                foreach (var genericParameterHandle in genericParameterHandles)
                {
                    genericParameters[i++] = new EcmaGenericParameter(Module, genericParameterHandle);
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
            return MetadataReader.HasCustomAttribute(MetadataReader.GetMethodDefinition(_handle).GetCustomAttributes(),
                attributeNamespace, attributeName);
        }

        public override string ToString()
        {
            return _type.ToString() + "." + Name;
        }

        public override bool IsPInvokeImpl
        {
            get
            {
                return (((int)Attributes & (int)MethodAttributes.PinvokeImpl) != 0);
            }
        }

        public override PInvokeMetadata GetPInvokeMethodImportMetadata()
        {
            if (!IsPInvokeImpl)
                return default(PInvokeMetadata);

            MetadataReader metadataReader = MetadataReader;
            MethodImport import = metadataReader.GetMethodDefinition(_handle).GetImport();
            string name = metadataReader.GetString(import.Name);

            CharSet charSet;
            switch (import.Attributes & MethodImportAttributes.CharSetMask)
            {
                case MethodImportAttributes.CharSetAnsi:
                    charSet = CharSet.Ansi;
                    break;
                case MethodImportAttributes.CharSetUnicode:
                    charSet = CharSet.Unicode;
                    break;
                case MethodImportAttributes.CharSetAuto:
                    charSet = CharSet.Auto;
                    break;
                default:
                    charSet = CharSet.Unknown;
                    break;
            }

            return new PInvokeMetadata(name, charSet);
        }
    }

    public static class EcmaMethodExtensions
    {
        public static bool IsPublic(this EcmaMethod method) { return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; }
        public static bool IsPrivate(this EcmaMethod method) { return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private; }
        public static bool IsStatic(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Static) != 0; }
        public static bool IsFinal(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Final) != 0; }
        public static bool IsHideBySig(this EcmaMethod method) { return (method.Attributes & MethodAttributes.HideBySig) != 0; }
        public static bool IsAbstract(this EcmaMethod method) { return (method.Attributes & MethodAttributes.Abstract) != 0; }
        public static bool IsSpecialName(this EcmaMethod method) { return (method.Attributes & MethodAttributes.SpecialName) != 0; }
    }
}
