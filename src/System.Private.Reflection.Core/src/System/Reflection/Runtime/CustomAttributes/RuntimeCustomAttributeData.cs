// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Extensibility;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;
using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // Common base class for the Runtime's implementation of CustomAttributeData.
    //
    internal abstract partial class RuntimeCustomAttributeData : ExtensibleCustomAttributeData
    {
        public abstract override Type AttributeType
        {
            get;
        }

        public sealed override IList<CustomAttributeTypedArgument> ConstructorArguments
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.CustomAttributeData_ConstructorArguments(this);
#endif

                return new ReadOnlyCollection<CustomAttributeTypedArgument>(GetConstructorArguments(throwIfMissingMetadata: true));
            }
        }

        // Equals/GetHashCode no need to override (they just implement reference equality but desktop never unified these things.)

        public sealed override IList<CustomAttributeNamedArgument> NamedArguments
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.CustomAttributeData_NamedArguments(this);
#endif

                return new ReadOnlyCollection<CustomAttributeNamedArgument>(GetNamedArguments(throwIfMissingMetadata: true));
            }
        }

        public sealed override String ToString()
        {
            try
            {
                String ctorArgs = "";
                IList<CustomAttributeTypedArgument> constructorArguments = GetConstructorArguments(throwIfMissingMetadata: false);
                if (constructorArguments == null)
                    return base.ToString();
                for (int i = 0; i < constructorArguments.Count; i++)
                    ctorArgs += String.Format(i == 0 ? "{0}" : ", {0}", ComputeTypedArgumentString(constructorArguments[i], typed: false));

                String namedArgs = "";
                IList<CustomAttributeNamedArgument> namedArguments = GetNamedArguments(throwIfMissingMetadata: false);
                if (namedArguments == null)
                    return base.ToString();
                for (int i = 0; i < namedArguments.Count; i++)
                {
                    CustomAttributeNamedArgument namedArgument = namedArguments[i];

                    // Legacy: Desktop sets "typed" to "namedArgument.ArgumentType != typeof(Object)" - on Project N, this property is not available
                    // (nor conveniently computable as it's not captured in the Project N metadata.) The only consequence is that for
                    // the rare case of fields and properties typed "Object", we won't decorate the argument value with its actual type name.
                    bool typed = true;
                    namedArgs += String.Format(
                        i == 0 && ctorArgs.Length == 0 ? "{0} = {1}" : ", {0} = {1}",
                        namedArgument.MemberName,
                        ComputeTypedArgumentString(namedArgument.TypedValue, typed));
                }

                return String.Format("[{0}({1}{2})]", AttributeTypeString, ctorArgs, namedArgs);
            }
            catch (MissingMetadataException)
            {
                return base.ToString();
            }
        }

        internal abstract String AttributeTypeString { get; }

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal abstract IList<CustomAttributeTypedArgument> GetConstructorArguments(bool throwIfMissingMetadata);

        //
        // If throwIfMissingMetadata is false, returns null rather than throwing a MissingMetadataException.
        //
        internal abstract IList<CustomAttributeNamedArgument> GetNamedArguments(bool throwIfMissingMetadata);

        //
        // Computes the ToString() value for a CustomAttributeTypedArgument struct.
        //
        private static String ComputeTypedArgumentString(CustomAttributeTypedArgument cat, bool typed)
        {
            Type argumentType = cat.ArgumentType;
            if (argumentType == null)
                return cat.ToString();

            FoundationTypes foundationTypes = ReflectionCoreExecution.ExecutionDomain.FoundationTypes;
            Object value = cat.Value;
            TypeInfo argumentTypeInfo = argumentType.GetTypeInfo();
            if (argumentTypeInfo.IsEnum)
                return String.Format(typed ? "{0}" : "({1}){0}", value, argumentType.FullName);

            if (value == null)
                return String.Format(typed ? "null" : "({0})null", argumentType.Name);

            if (argumentType.Equals(foundationTypes.SystemString))
                return String.Format("\"{0}\"", value);

            if (argumentType.Equals(foundationTypes.SystemChar))
                return String.Format("'{0}'", value);

            if (argumentType.Equals(foundationTypes.SystemType))
                return String.Format("typeof({0})", ((Type)value).FullName);

            else if (argumentType.IsArray)
            {
                String result = null;
                IList<CustomAttributeTypedArgument> array = value as IList<CustomAttributeTypedArgument>;

                Type elementType = argumentType.GetElementType();
                result = String.Format(@"new {0}[{1}] {{ ", elementType.GetTypeInfo().IsEnum ? elementType.FullName : elementType.Name, array.Count);

                for (int i = 0; i < array.Count; i++)
                    result += String.Format(i == 0 ? "{0}" : ", {0}", ComputeTypedArgumentString(array[i], elementType != foundationTypes.SystemObject));

                return result += " }";
            }

            return String.Format(typed ? "{0}" : "({1}){0}", value, argumentType.Name);
        }
    }
}
