// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    //
    // NameMangler is reponsible for giving extern C/C++ names to managed types, methods and fields
    //
    // The key invariant is that the mangled names are independent on the compilation order.
    //
    public class NameMangler
    {
        private readonly Compilation _compilation;

        public NameMangler(Compilation compilation)
        {
            _compilation = compilation;
        }

        //
        // Turn a name into a valid C/C++ identifier
        //
        internal string SanitizeName(string s, bool typeName = false)
        {
            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z')))
                {
                    if (sb != null)
                        sb.Append(c);
                    continue;
                }

                if ((c >= '0') && (c <= '9'))
                {
                    // C identifiers cannot start with a digit. Prepend underscores.
                    if (i == 0)
                    {
                        if (sb == null)
                            sb = new StringBuilder(s.Length + 2);
                        sb.Append("_");
                    }
                    if (sb != null)
                        sb.Append(c);
                    continue;
                }

                if (sb == null)
                    sb = new StringBuilder(s, 0, i, s.Length);

                // For CppCodeGen, replace "." (C# namespace separator) with "::" (C++ namespace separator)
                if (typeName && c == '.' && _compilation.IsCppCodeGen)
                {
                    sb.Append("::");
                    continue;
                }

                // Everything else is replaced by underscore.
                // TODO: We assume that there won't be collisions with our own or C++ built-in identifiers.
                sb.Append("_");
            }
            return (sb != null) ? sb.ToString() : s;
        }

        /// <summary>
        /// Dictionary given a mangled name for a given <see cref="TypeDesc"/>
        /// </summary>
        private ImmutableDictionary<TypeDesc, string> _mangledTypeNames = ImmutableDictionary<TypeDesc, string>.Empty;

        /// <summary>
        /// Given a set of names <param name="set"/> check if <param name="origName"/>
        /// is unique, if not add a numbered suffix until it becomes unique.
        /// </summary>
        /// <param name="origName">Name to check for uniqueness.</param>
        /// <param name="set">Set of names already used.</param>
        /// <returns>A name based on <param name="origName"/> that is not part of <param name="set"/>.</returns>
        private string DisambiguateName(string origName, ISet<string> set)
        {
            int iter = 0;
            string result = origName;
            while (set.Contains(result))
            {
                result = string.Concat(origName, "_", (iter++).ToStringInvariant());
            }
            return result;
        }

        public string GetMangledTypeName(TypeDesc type)
        {
            string mangledName;
            if (_mangledTypeNames.TryGetValue(type, out mangledName))
                return mangledName;

            return ComputeMangledTypeName(type);
        }

        /// <summary>
        /// If given <param name="type"/> is an <see cref="EcmaType"/> precompute its mangled type name
        /// along with all the other types from the same module as <param name="type"/>.
        /// Otherwise, it is a constructed type and to the EcmaType's mangled name we add a suffix to
        /// show what kind of constructed type it is (e.g. appending __Array for an array type).
        /// </summary>
        /// <param name="type">Type to mangled</param>
        /// <returns>Mangled name for <param name="type"/>.</returns>
        private string ComputeMangledTypeName(TypeDesc type)
        {
            if (type is EcmaType)
            {
                EcmaType ecmaType = (EcmaType)type;

                string prependAssemblyName = SanitizeName(((EcmaAssembly)ecmaType.EcmaModule).GetName().Name);

                var deduplicator = new HashSet<string>();

                // Add consistent names for all types in the module, independent on the order in which
                // they are compiled
                lock (this)
                {
                    foreach (MetadataType t in ((EcmaType)type).EcmaModule.GetAllTypes())
                    {
                        string name = t.GetFullName();

                        // Include encapsulating type
                        MetadataType containingType = t.ContainingType;
                        while (containingType != null)
                        {
                            name = containingType.GetFullName() + "_" + name;
                            containingType = containingType.ContainingType;
                        }

                        name = SanitizeName(name, true);

                        if (_compilation.IsCppCodeGen)
                        {
                            // Always generate a fully qualified name
                            name = "::" + prependAssemblyName + "::" + name;
                        }
                        else
                        {
                            name = prependAssemblyName + "_" + name;
                        }

                        // Ensure that name is unique and update our tables accordingly.
                        name = DisambiguateName(name, deduplicator);
                        deduplicator.Add(name);
                        _mangledTypeNames = _mangledTypeNames.Add(t, name);
                    }
                }

                return _mangledTypeNames[type];
            }


            string mangledName;

            switch (type.Category)
            {
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    // mangledName = "Array<" + GetSignatureCPPTypeName(((ArrayType)type).ElementType) + ">";
                    mangledName = GetMangledTypeName(((ArrayType)type).ElementType) + "__Array";
                    if (!type.IsSzArray)
                        mangledName += "Rank" + ((ArrayType)type).Rank.ToStringInvariant();
                    break;
                case TypeFlags.ByRef:
                    mangledName = GetMangledTypeName(((ByRefType)type).ParameterType) + "__ByRef";
                    break;
                case TypeFlags.Pointer:
                    mangledName = GetMangledTypeName(((PointerType)type).ParameterType) + "__Pointer";
                    break;
                default:
                    // Case of a generic type. If `type' is a type definition we use the type name
                    // for mangling, otherwise we use the mangling of the type and its generic type
                    // parameters, e.g. A <B> becomes A__B.
                    var typeDefinition = type.GetTypeDefinition();
                    if (typeDefinition != type)
                    {
                        mangledName = GetMangledTypeName(typeDefinition);

                        var inst = type.Instantiation;
                        for (int i = 0; i < inst.Length; i++)
                        {
                            string instArgName = GetMangledTypeName(inst[i]);
                            if (_compilation.IsCppCodeGen)
                                instArgName = instArgName.Replace("::", "_");
                            mangledName += "__" + instArgName;
                        }
                    }
                    else
                    {
                        mangledName = SanitizeName(((MetadataType)type).GetFullName(), true);
                    }
                    break;
            }

            lock (this)
            {
                // Ensure that name is unique and update our tables accordingly.
                _mangledTypeNames = _mangledTypeNames.Add(type, mangledName);
            }

            return mangledName;
        }

        private ImmutableDictionary<MethodDesc, string> _mangledMethodNames = ImmutableDictionary<MethodDesc, string>.Empty;

        public string GetMangledMethodName(MethodDesc method)
        {
            string mangledName;
            if (_mangledMethodNames.TryGetValue(method, out mangledName))
                return mangledName;

            return ComputeMangledMethodName(method);
        }

        private string ComputeMangledMethodName(MethodDesc method)
        {
            string prependTypeName = null;
            if (!_compilation.IsCppCodeGen)
                prependTypeName = GetMangledTypeName(method.OwningType);

            if (method is EcmaMethod)
            {
                var deduplicator = new HashSet<string>();

                // Add consistent names for all methods of the type, independent on the order in which
                // they are compiled
                lock (this)
                {
                    foreach (var m in method.OwningType.GetMethods())
                    {
                        string name = SanitizeName(m.Name);

                        name = DisambiguateName(name, deduplicator);
                        deduplicator.Add(name);

                        if (prependTypeName != null)
                            name = prependTypeName + "__" + name;

                        _mangledMethodNames = _mangledMethodNames.Add(m, name);
                    }
                }

                return _mangledMethodNames[method];
            }


            string mangledName;

            var methodDefinition = method.GetTypicalMethodDefinition();
            if (methodDefinition != method)
            {
                mangledName = GetMangledMethodName(methodDefinition.GetMethodDefinition());

                var inst = method.Instantiation;
                for (int i = 0; i < inst.Length; i++)
                {
                    string instArgName = GetMangledTypeName(inst[i]);
                    if (_compilation.IsCppCodeGen)
                        instArgName = instArgName.Replace("::", "_");
                    mangledName += "__" + instArgName;
                }
            }
            else
            {
                // Assume that Name is unique for all other methods
                mangledName = SanitizeName(method.Name);
            }

            if (prependTypeName != null)
                mangledName = prependTypeName + "__" + mangledName;

            lock (this)
            {
                _mangledMethodNames = _mangledMethodNames.Add(method, mangledName);
            }

            return mangledName;
        }

        private ImmutableDictionary<FieldDesc, string> _mangledFieldNames = ImmutableDictionary<FieldDesc, string>.Empty;

        public string GetMangledFieldName(FieldDesc field)
        {
            string mangledName;
            if (_mangledFieldNames.TryGetValue(field, out mangledName))
                return mangledName;

            return ComputeMangledFieldName(field);
        }

        private string ComputeMangledFieldName(FieldDesc field)
        {
            string prependTypeName = null;
            if (!_compilation.IsCppCodeGen)
                prependTypeName = GetMangledTypeName(field.OwningType);

            if (field is EcmaField)
            {
                var deduplicator = new HashSet<string>();

                // Add consistent names for all fields of the type, independent on the order in which
                // they are compiled
                lock (this)
                {
                    foreach (var f in field.OwningType.GetFields())
                    {
                        string name = SanitizeName(f.Name);

                        name = DisambiguateName(name, deduplicator);
                        deduplicator.Add(name);

                        if (prependTypeName != null)
                            name = prependTypeName + "__" + name;

                        _mangledFieldNames = _mangledFieldNames.Add(f, name);
                    }
                }

                return _mangledFieldNames[field];
            }


            string mangledName = SanitizeName(field.Name);

            if (prependTypeName != null)
                mangledName = prependTypeName + "__" + mangledName;

            lock (this)
            {
                _mangledFieldNames = _mangledFieldNames.Add(field, mangledName);
            }

            return mangledName;
        }

        private string _compilationUnitPrefix;

        /// <summary>
        /// Prefix to prepend to compilation unit global symbols to make them disambiguated between different .obj files.
        /// </summary>
        public string CompilationUnitPrefix
        {
            get
            {
                if (_compilationUnitPrefix == null)
                {
                    string systemModuleName = ((EcmaAssembly)_compilation.TypeSystemContext.SystemModule).GetName().Name;

                    // TODO: just something to get Runtime.Base compiled
                    if (systemModuleName != "System.Private.CoreLib")
                    {
                        _compilationUnitPrefix = systemModuleName.Replace(".", "_");
                    }
                    else
                    {
                        _compilationUnitPrefix = SanitizeName(Path.GetFileNameWithoutExtension(_compilation.Options.OutputFilePath));
                    }
                }
                return _compilationUnitPrefix;
            }
        }
    }
}
