// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Class through which all type system thrown exceptions should be routed.
    /// </summary>
    public static class TypeSystemExceptionHelpers
    {
        // TODO: Figure out the story for exception messages. The runtime type loader
        // likely can't depend on resource manager.

        // NOTE: These exception messages are CLR-compatible. Do not modify.
        private const string ClassLoadGeneral = "Could not load type '{0}' from assembly '{1}'.";
        private const string ClassLoadMissingMethodRva = "Could not load type '{0}' from assembly '{1}' because the method '{2}' has no implementation (no RVA).";
        private const string MissingMethod = "Method not found: '{0}'.";
        private const string MissingField = "Field not found: '{0}'.";

        public static Exception CreateClassLoadGeneralException(string nestedTypeName, ModuleDesc module)
        {
            // Not including the containing type name is a CLR-compatible behavior. Do not modify.
            string message = String.Format(ClassLoadGeneral, nestedTypeName, module.DisplayName());
            return new TypeSystemException.TypeLoadException(message);
        }

        public static Exception CreateClassLoadGeneralException(string @namespace, string name, ModuleDesc module)
        {
            string typeName = String.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
            string message = String.Format(ClassLoadGeneral, typeName, module.DisplayName());
            return new TypeSystemException.TypeLoadException(message);
        }

        public static Exception CreateClassLoadMissingMethodRvaException(MethodDesc method)
        {
            string owningModule = (method.OwningType as MetadataType)?.Module.DisplayName();

            string message = String.Format(ClassLoadMissingMethodRva,
                ExceptionTypeNameFormatter.Instance.FormatName(method.OwningType),
                owningModule,
                method.Name);
            return new TypeSystemException.TypeLoadException(message);
        }

        public static Exception CreateMissingMethodException(TypeDesc owningType, string methodName, MethodSignature signature)
        {
            string formattedName = ExceptionTypeNameFormatter.Instance.FormatMethod(owningType, methodName, signature);
            string message = String.Format(MissingMethod, formattedName);
            return new TypeSystemException.MissingMethodException(message);
        }

        public static Exception CreateMissingFieldException(TypeDesc owningType, string fieldName, TypeDesc fieldType)
        {
            // fieldType is currently unused, but we might want to capture that in the exception in the future.
            string formattedName = ExceptionTypeNameFormatter.Instance.FormatName(owningType) + "." + fieldName;
            string message = String.Format(MissingField, formattedName);
            return new TypeSystemException.MissingFieldException(message);
        }

        private static string DisplayName(this ModuleDesc module)
        {
            // TODO: format this explicitly
            return module.ToString();
        }

        #region SigFormat-compatible name formatter.
        /// <summary>
        /// Provides a name formatter that is compatible with SigFormat.cpp in the CLR.
        /// </summary>
        private class ExceptionTypeNameFormatter : TypeNameFormatter
        {
            public static ExceptionTypeNameFormatter Instance { get; } = new ExceptionTypeNameFormatter();

            public override void AppendName(StringBuilder sb, PointerType type)
            {
                FormatName(type.ParameterType);
                sb.Append('*');
            }

            public override void AppendName(StringBuilder sb, GenericParameterDesc type)
            {
                string prefix = type.Kind == GenericParameterKind.Type ? "!" : "!!";
                sb.Append(prefix);
                sb.Append(type.Name);
            }

            public override void AppendName(StringBuilder sb, SignatureTypeVariable type)
            {
                sb.Append("!");
                sb.Append(type.Index.ToStringInvariant());
            }

            public override void AppendName(StringBuilder sb, SignatureMethodVariable type)
            {
                sb.Append("!!");
                sb.Append(type.Index.ToStringInvariant());
            }

            public override void AppendName(StringBuilder sb, FunctionPointerType type)
            {
                MethodSignature signature = type.Signature;

                AppendName(sb, signature.ReturnType);

                sb.Append(" (");
                for (int i = 0; i < signature.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    AppendName(sb, signature[i]);
                }

                // TODO: Append '...' for vararg methods

                sb.Append(')');
            }

            public override void AppendName(StringBuilder sb, ByRefType type)
            {
                FormatName(type.ParameterType);
                sb.Append(" ByRef");
            }

            public override void AppendName(StringBuilder sb, ArrayType type)
            {
                AppendName(sb, type.ElementType);
                sb.Append('[');
                
                // NOTE: We're ignoring difference between SzArray and MdArray rank 1 for SigFormat.cpp compat.
                sb.Append(',', type.Rank - 1);

                sb.Append(']');
            }

            protected override void AppendNameForInstantiatedType(StringBuilder sb, DefType type)
            {
                AppendName(sb, type.GetTypeDefinition());
                sb.Append('<');

                for (int i = 0; i < type.Instantiation.Length; i++)
                {
                    if (i > 0)
                        sb.Append(", ");
                    AppendName(sb, type.Instantiation[i]);
                }

                sb.Append('>');
            }

            protected override void AppendNameForNamespaceType(StringBuilder sb, DefType type)
            {
                if (type.IsPrimitive)
                {
                    sb.Append(type.Name);
                }
                else
                {
                    string ns = type.Namespace;
                    if (ns.Length > 0)
                    {
                        sb.Append(ns);
                        sb.Append('.');
                    }
                    sb.Append(type.Name);
                }
            }

            protected override void AppendNameForNestedType(StringBuilder sb, DefType nestedType, DefType containingType)
            {
                // NOTE: We're ignoring the containing type for compatiblity with SigFormat.cpp
                sb.Append(nestedType.Name);
            }

            public string FormatMethod(TypeDesc owningType, string methodName, MethodSignature signature)
            {
                StringBuilder sb = new StringBuilder();

                if (signature != null)
                {
                    sb.Append(Instance.FormatName(signature.ReturnType));
                    sb.Append(' ');
                }

                sb.Append(Instance.FormatName(owningType));
                sb.Append('.');
                sb.Append(methodName);

                if (signature != null)
                {
                    sb.Append('(');
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append(", ");
                        }

                        sb.Append(Instance.FormatName(signature[i]));
                    }
                    sb.Append(')');
                }

                return sb.ToString();
            }
        }
        #endregion
    }
}
