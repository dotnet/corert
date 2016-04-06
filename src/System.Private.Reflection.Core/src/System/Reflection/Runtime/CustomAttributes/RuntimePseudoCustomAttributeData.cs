// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Collections.ObjectModel;
using global::System.Reflection.Runtime.Types;
using global::System.Reflection.Runtime.General;

using global::Internal.LowLevelLinq;
using global::Internal.Reflection.Core;
using global::Internal.Reflection.Core.NonPortable;
using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // The Runtime's implementation of a pseudo-CustomAttributeData.
    //
    internal sealed class RuntimePseudoCustomAttributeData : RuntimeCustomAttributeData
    {
        public RuntimePseudoCustomAttributeData(RuntimeType attributeType, IList<CustomAttributeTypedArgument> constructorArguments, IList<CustomAttributeNamedArgument> namedArguments)
        {
            _attributeType = attributeType;
            if (constructorArguments == null)
                constructorArguments = Array.Empty<CustomAttributeTypedArgument>();
            _constructorArguments = new ReadOnlyCollection<CustomAttributeTypedArgument>(constructorArguments);
            if (namedArguments == null)
                namedArguments = Array.Empty<CustomAttributeNamedArgument>();
            _namedArguments = new ReadOnlyCollection<CustomAttributeNamedArgument>(namedArguments);
            return;
        }

        public sealed override Type AttributeType
        {
            get
            {
                return _attributeType;
            }
        }

        internal sealed override String AttributeTypeString
        {
            get
            {
                return _attributeType.FormatTypeName();
            }
        }

        internal sealed override IList<CustomAttributeTypedArgument> GetConstructorArguments(bool throwIfMissingMetadata)
        {
            return _constructorArguments;
        }

        internal sealed override IList<CustomAttributeNamedArgument> GetNamedArguments(bool throwIfMissingMetadata)
        {
            return _namedArguments;
        }

        // Equals/GetHashCode no need to override (they just implement reference equality but desktop never unified these things.)

        private RuntimeType _attributeType;
        private ReadOnlyCollection<CustomAttributeTypedArgument> _constructorArguments;
        private ReadOnlyCollection<CustomAttributeNamedArgument> _namedArguments;
    }
}
