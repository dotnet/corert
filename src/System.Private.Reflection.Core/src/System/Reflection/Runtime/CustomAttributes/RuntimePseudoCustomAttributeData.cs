// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // The Runtime's implementation of a pseudo-CustomAttributeData.
    //
    internal sealed class RuntimePseudoCustomAttributeData : RuntimeCustomAttributeData
    {
        public RuntimePseudoCustomAttributeData(Type attributeType, IList<CustomAttributeTypedArgument> constructorArguments, IList<CustomAttributeNamedArgument> namedArguments)
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

        public sealed override ConstructorInfo Constructor
        {
            get
            {
                int numArguments = _constructorArguments.Count;
                if (numArguments == 0)
                    return ResolveAttributeConstructor(_attributeType, Array.Empty<Type>());

                Type[] expectedParameterTypes = new Type[numArguments];
                for (int i = 0; i < numArguments; i++)
                {
                    expectedParameterTypes[i] = _constructorArguments[i].ArgumentType;
                }
                return ResolveAttributeConstructor(_attributeType, expectedParameterTypes);
            }
        }

        internal sealed override String AttributeTypeString
        {
            get
            {
                return _attributeType.FormatTypeNameForReflection();
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

        private readonly Type _attributeType;
        private readonly ReadOnlyCollection<CustomAttributeTypedArgument> _constructorArguments;
        private readonly ReadOnlyCollection<CustomAttributeNamedArgument> _namedArguments;
    }
}
