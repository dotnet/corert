// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;

using Internal.LowLevelLinq;
using Internal.Reflection.Core;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Tracing;
using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.CustomAttributes
{
    //
    // Common base class for the Runtime's implementation of CustomAttributeData.
    //
    internal abstract partial class RuntimeCustomAttributeData : CustomAttributeData
    {
        // The 2.0 CustomAttributeData.AttributeType is non-overridable and is implemented as { get { return Constructor.DeclaredType; } }
        // We don't fully support the Constructor property at this time so we'll have it return a dummy ConstructorInfo that only implements DeclaredType
        // and reintroduce the virtual AttributeType our subclasses expect.
        public new abstract Type AttributeType { get; }

        public sealed override ConstructorInfo Constructor
        {
            get
            {
                if (string.Empty.Length != 0) throw new NotImplementedException(); // This silly looking line marks that this is a tide-over implementation only.
                return new ConstructorInfoImplementingOnlyDeclaringType(AttributeType);
            }
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
                    return LastResortToString;
                for (int i = 0; i < constructorArguments.Count; i++)
                    ctorArgs += String.Format(i == 0 ? "{0}" : ", {0}", ComputeTypedArgumentString(constructorArguments[i], typed: false));

                String namedArgs = "";
                IList<CustomAttributeNamedArgument> namedArguments = GetNamedArguments(throwIfMissingMetadata: false);
                if (namedArguments == null)
                    return LastResortToString;
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
                return LastResortToString;
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

        private string LastResortToString
        {
            get
            {
                // This emulates Object.ToString() for consistency with prior .Net Native implementations. 
                return GetType().ToString();
            }
        }

        private sealed class ConstructorInfoImplementingOnlyDeclaringType : ConstructorInfo
        {
            internal ConstructorInfoImplementingOnlyDeclaringType(Type declaringType)
            {
                _declaringType = declaringType;
            }

            public sealed override Type DeclaringType => _declaringType;

            public sealed override MethodAttributes Attributes { get { throw NotImplemented.ByDesign; } }
            public sealed override RuntimeMethodHandle MethodHandle { get { throw NotImplemented.ByDesign; } }
            public sealed override string Name { get { throw NotImplemented.ByDesign; } }
            public sealed override Type ReflectedType { get { throw NotImplemented.ByDesign; } }
            public sealed override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture) { throw NotImplemented.ByDesign; }
            public sealed override ParameterInfo[] GetParameters() { throw NotImplemented.ByDesign; }
            public sealed override MethodImplAttributes GetMethodImplementationFlags() { throw NotImplemented.ByDesign; }
            public sealed override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture) { throw NotImplemented.ByDesign; }
            public sealed override bool IsDefined(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }
            public sealed override object[] GetCustomAttributes(bool inherit) { throw NotImplemented.ByDesign; }
            public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) { throw NotImplemented.ByDesign; }

            public sealed override CallingConventions CallingConvention { get { throw NotImplemented.ByDesign; } }
            public sealed override bool ContainsGenericParameters { get { throw NotImplemented.ByDesign; } }
            public sealed override IEnumerable<CustomAttributeData> CustomAttributes { get { throw NotImplemented.ByDesign; } }
            public sealed override Type[] GetGenericArguments() { throw NotImplemented.ByDesign; }
            public sealed override MethodBody GetMethodBody() { throw NotImplemented.ByDesign; }
            public sealed override bool IsGenericMethod { get { throw NotImplemented.ByDesign; } }
            public sealed override bool IsGenericMethodDefinition { get { throw NotImplemented.ByDesign; } }
            public sealed override MemberTypes MemberType { get { throw NotImplemented.ByDesign; } }
            public sealed override int MetadataToken { get { throw NotImplemented.ByDesign; } }
            public sealed override MethodImplAttributes MethodImplementationFlags { get { throw NotImplemented.ByDesign; } }
            public sealed override Module Module { get { throw NotImplemented.ByDesign; } }

            public sealed override bool Equals(object obj) { throw NotImplemented.ByDesign; }
            public sealed override int GetHashCode() { throw NotImplemented.ByDesign; }
            public sealed override string ToString() => base.ToString();

            private readonly Type _declaringType;
        }
    }
}
