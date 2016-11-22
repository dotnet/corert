// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Base type for all type system exceptions.
    /// </summary>
    public abstract class TypeSystemException : Exception
    {
        private string[] _arguments;

        /// <summary>
        /// Gets the resource string identifier.
        /// </summary>
        public ExceptionStringID StringID { get; }

        /// <summary>
        /// Gets the formatting arguments for the exception string.
        /// </summary>
        public IReadOnlyList<string> Arguments
        {
            get
            {
                return _arguments;
            }
        }

        public override string Message
        {
            get
            {
                return GetExceptionString(StringID, _arguments);
            }
        }

        public TypeSystemException(ExceptionStringID id, params string[] args)
        {
            StringID = id;
            _arguments = args;
        }

        private static string GetExceptionString(ExceptionStringID id, string[] args)
        {
            // TODO: Share the strings and lookup logic with System.Private.CoreLib.
            return "[TEMPORARY EXCEPTION MESSAGE] " + id.ToString() + ": " + String.Join(", ", args);
        }

        /// <summary>
        /// The exception that is thrown when type-loading failures occur.
        /// </summary>
        public class TypeLoadException : TypeSystemException
        {
            public string TypeName { get; }

            public string AssemblyName { get; }

            private TypeLoadException(ExceptionStringID id, string typeName, string assemblyName, string messageArg)
                : base(id, new string[] { typeName, assemblyName, messageArg })
            {
                TypeName = typeName;
                AssemblyName = assemblyName;
            }

            private TypeLoadException(ExceptionStringID id, string typeName, string assemblyName)
                : base(id, new string[] { typeName, assemblyName })
            {
                TypeName = typeName;
                AssemblyName = assemblyName;
            }

            public TypeLoadException(string nestedTypeName, ModuleDesc module)
                : this(ExceptionStringID.ClassLoadGeneral, nestedTypeName, Format.Module(module))
            {
            }

            public TypeLoadException(string @namespace, string name, ModuleDesc module)
                : this(ExceptionStringID.ClassLoadGeneral, Format.Type(@namespace, name), Format.Module(module))
            {
            }

            public TypeLoadException(ExceptionStringID id, MethodDesc method)
                : this(id, Format.Type(method.OwningType), Format.OwningModule(method), Format.Method(method))
            {
            }

            public TypeLoadException(ExceptionStringID id, TypeDesc type, string messageArg)
                : this(id, Format.Type(type), Format.OwningModule(type), messageArg)
            {
            }

            public TypeLoadException(ExceptionStringID id, TypeDesc type)
                : this(id, Format.Type(type), Format.OwningModule(type))
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a class member that does not exist
        /// or that is not declared as public.
        /// </summary>
        public abstract class MissingMemberException : TypeSystemException
        {
            protected internal MissingMemberException(ExceptionStringID id, params string[] args)
                : base(id, args)
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a method that does not exist.
        /// </summary>
        public class MissingMethodException : MissingMemberException
        {
            public MissingMethodException(ExceptionStringID id, params string[] args)
                : base(id, args)
            {
            }

            public MissingMethodException(TypeDesc owningType, string methodName, MethodSignature signature)
                : this(ExceptionStringID.MissingMethod, Format.Method(owningType, methodName, signature))
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when there is an attempt to access a field that does not exist.
        /// </summary>
        public class MissingFieldException : MissingMemberException
        {
            public MissingFieldException(ExceptionStringID id, params string[] args)
                : base(id, args)
            {
            }

            public MissingFieldException(TypeDesc owningType, string fieldName)
                : this(ExceptionStringID.MissingField, Format.Field(owningType, fieldName))
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when an attempt to access a file that does not exist on disk fails.
        /// </summary>
        public class FileNotFoundException : TypeSystemException
        {
            public FileNotFoundException(ExceptionStringID id, string fileName)
                : base(id, fileName)
            {
            }
        }

        /// <summary>
        /// The exception that is thrown when a program contains invalid Microsoft intermediate language (MSIL) or metadata.
        /// Generally this indicates a bug in the compiler that generated the program.
        /// </summary>
        public class InvalidProgramException : TypeSystemException
        {
            public InvalidProgramException(ExceptionStringID id, MethodDesc method)
                : base(id, Format.Method(method))
            {
            }
        }

        #region Formatting helpers

        private static class Format
        {
            public static string OwningModule(MethodDesc method)
            {
                return OwningModule(method.OwningType);
            }

            public static string OwningModule(TypeDesc type)
            {
                return Module((type as MetadataType)?.Module);
            }

            public static string Module(ModuleDesc module)
            {
                if (module == null)
                    return "?";

                IAssemblyDesc assembly = module as IAssemblyDesc;
                if (assembly != null)
                {
                    return assembly.GetName().FullName;
                }
                else
                {
                    Debug.Assert(false, "Multi-module assemblies");
                    return module.ToString();
                }
            }

            public static string Type(TypeDesc type)
            {
                return ExceptionTypeNameFormatter.Instance.FormatName(type);
            }

            public static string Type(string @namespace, string name)
            {
                return String.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
            }

            public static string Field(TypeDesc owningType, string fieldName)
            {
                return Type(owningType) + "." + fieldName;
            }

            public static string Method(MethodDesc method)
            {
                return Method(method.OwningType, method.Name, method.Signature);
            }

            public static string Method(TypeDesc owningType, string methodName, MethodSignature signature)
            {
                StringBuilder sb = new StringBuilder();

                if (signature != null)
                {
                    sb.Append(ExceptionTypeNameFormatter.Instance.FormatName(signature.ReturnType));
                    sb.Append(' ');
                }

                sb.Append(ExceptionTypeNameFormatter.Instance.FormatName(owningType));
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

                        sb.Append(ExceptionTypeNameFormatter.Instance.FormatName(signature[i]));
                    }
                    sb.Append(')');
                }

                return sb.ToString();
            }

            /// <summary>
            /// Provides a name formatter that is compatible with SigFormat.cpp in the CLR.
            /// </summary>
            private class ExceptionTypeNameFormatter : TypeNameFormatter
            {
                public static ExceptionTypeNameFormatter Instance { get; } = new ExceptionTypeNameFormatter();

                public override void AppendName(StringBuilder sb, PointerType type)
                {
                    AppendName(sb, type.ParameterType);
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
                    AppendName(sb, type.ParameterType);
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
            }
        }

        #endregion
    }
}
