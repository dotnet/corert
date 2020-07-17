// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Augments;
using Internal.Reflection.Core.Execution;
using System.Reflection.Metadata;

namespace System.Reflection.Runtime.CustomAttributes.EcmaFormat
{
    //
    // The Runtime's implementation of CustomAttributeData for normal metadata-based attributes
    //
    internal sealed class EcmaFormatCustomAttributeData : RuntimeCustomAttributeData
    {
        internal EcmaFormatCustomAttributeData(MetadataReader reader, CustomAttributeHandle customAttributeHandle)
        {
            _reader = reader;
            _customAttribute = reader.GetCustomAttribute(customAttributeHandle);
        }

        public sealed override Type AttributeType
        {
            get
            {
                Type lazyAttributeType = _lazyAttributeType;
                if (lazyAttributeType == null)
                {
                    EntityHandle ctorType;
                    EcmaMetadataHelpers.GetAttributeTypeDefRefOrSpecHandle(_reader, _customAttribute.Constructor, out ctorType);
                    lazyAttributeType = _lazyAttributeType = ((Handle)ctorType).Resolve(_reader, new TypeContext(null, null));
                }
                return lazyAttributeType;
            }
        }

        public sealed override ConstructorInfo Constructor
        {
            get
            {
                MetadataReader reader = _reader;
                HandleKind constructorHandleType = _customAttribute.Constructor.Kind;

                if (constructorHandleType == HandleKind.MethodDefinition)
                {
                    throw new NotImplementedException();
                }
                else if (constructorHandleType == HandleKind.MemberReference)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new BadImageFormatException();
                }
            }
        }

        internal sealed override String AttributeTypeString
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal sealed override IList<CustomAttributeTypedArgument> GetConstructorArguments(bool throwIfMissingMetadata)
        {
            if (_fixedArguments != null)
                return _fixedArguments;

            IList<CustomAttributeNamedArgument> newNamedArguments;
            IList<CustomAttributeTypedArgument> newFixedArguments;
            bool metadataWasMissing;

            LoadArgumentInfo(throwIfMissingMetadata, out newNamedArguments, out newFixedArguments, out metadataWasMissing);
            if (metadataWasMissing)
            {
                return null;
            }
            
            _namedArguments = newNamedArguments;
            _fixedArguments = newFixedArguments;

            return newFixedArguments;
        }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal sealed override IList<CustomAttributeNamedArgument> GetNamedArguments(bool throwIfMissingMetadata)
        {
            if (_namedArguments != null)
                return _namedArguments;

            IList<CustomAttributeNamedArgument> newNamedArguments;
            IList<CustomAttributeTypedArgument> newFixedArguments;
            bool metadataWasMissing;

            LoadArgumentInfo(throwIfMissingMetadata, out newNamedArguments, out newFixedArguments, out metadataWasMissing);
            if (metadataWasMissing)
            {
                return null;
            }

            _namedArguments = newNamedArguments;
            _fixedArguments = newFixedArguments;

            return newNamedArguments;
        }

        // Equals/GetHashCode no need to override (they just implement reference equality but desktop never unified these things.)

        private void LoadArgumentInfo(bool throwIfMissingMetadata, out IList<CustomAttributeNamedArgument> namedArguments, out IList<CustomAttributeTypedArgument> fixedArguments, out bool metadataWasMissing)
        {
            LowLevelListWithIList<CustomAttributeNamedArgument> newNamedArguments = new LowLevelListWithIList<CustomAttributeNamedArgument>();
            LowLevelListWithIList<CustomAttributeTypedArgument> newFixedArguments = new LowLevelListWithIList<CustomAttributeTypedArgument>();
            ReflectionTypeProvider typeProvider = new ReflectionTypeProvider(throwIfMissingMetadata);

            CustomAttributeValue<RuntimeTypeInfo> customAttributeValue = _customAttribute.DecodeValue(typeProvider);
            foreach (CustomAttributeTypedArgument<RuntimeTypeInfo> fixedArgument in customAttributeValue.FixedArguments)
            {
                newFixedArguments.Add(WrapInCustomAttributeTypedArgument(fixedArgument.Value, fixedArgument.Type));
            }

            foreach (CustomAttributeNamedArgument<RuntimeTypeInfo> ecmaNamedArgument in customAttributeValue.NamedArguments)
            {
                bool isField = ecmaNamedArgument.Kind == CustomAttributeNamedArgumentKind.Field;
                CustomAttributeTypedArgument typedArgument = WrapInCustomAttributeTypedArgument(ecmaNamedArgument.Value, ecmaNamedArgument.Type);
                newNamedArguments.Add(ReflectionAugments.CreateCustomAttributeNamedArgument(this.AttributeType, ecmaNamedArgument.Name, isField, typedArgument));
            }

            if (newFixedArguments.Count == 0)
                fixedArguments = Array.Empty<CustomAttributeTypedArgument>();
            else
                fixedArguments = newFixedArguments;

            if (newNamedArguments.Count == 0)
                namedArguments = Array.Empty<CustomAttributeNamedArgument>();
            else
                namedArguments = newNamedArguments;

            metadataWasMissing = typeProvider.ExceptionOccurred;
        }

        private volatile IList<CustomAttributeNamedArgument> _namedArguments;
        private volatile IList<CustomAttributeTypedArgument> _fixedArguments;

        private readonly MetadataReader _reader;
        private readonly CustomAttribute _customAttribute;

        private volatile Type _lazyAttributeType;
    }
}
