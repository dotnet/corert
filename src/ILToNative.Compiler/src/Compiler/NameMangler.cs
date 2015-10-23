// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILToNative
{
    //
    // NameMangler is reponsible for giving extern C/C++ names to managed types, methods and fields
    //
    // The key invariant is that the mangled names are independent on the compilation order.
    //
    public class NameMangler
    {
        readonly Compilation _compilation;

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

        ImmutableDictionary<TypeDesc, string> _mangledTypeNames = ImmutableDictionary<TypeDesc, string>.Empty;

        public string GetMangledTypeName(TypeDesc type)
        {
            string mangledName;
            if (_mangledTypeNames.TryGetValue(type, out mangledName))
                return mangledName;

            return ComputeMangledTypeName(type);
        }

        private string ComputeMangledTypeName(TypeDesc type)
        {
            if (type is EcmaType)
            {
                string prependAssemblyName = SanitizeName(((EcmaType)type).Module.GetName().Name);

                var deduplicator = new HashSet<string>();

                // Add consistent names for all types in the module, independent on the order in which 
                // they are compiled
                lock (this)
                {
                    foreach (var t in ((EcmaType)type).Module.GetAllTypes())
                    {
                        string name = t.Name;

                        // Include encapsulating type
                        TypeDesc containingType = ((EcmaType)t).ContainingType;
                        while (containingType != null)
                        {
                            name = containingType.Name + "_" + name;
                            containingType = ((EcmaType)containingType).ContainingType;
                        }

                        name = SanitizeName(name, true);

                        if (deduplicator.Contains(name))
                        {
                            string nameWithIndex;
                            for (int index = 1; ; index++)
                            {
                                nameWithIndex = name + "_" + index.ToString(CultureInfo.InvariantCulture);
                                if (!deduplicator.Contains(nameWithIndex))
                                    break;
                            }
                            name = nameWithIndex;
                        }
                        deduplicator.Add(name);

                        if (_compilation.IsCppCodeGen)
                            name = prependAssemblyName + "::" + name;
                        else
                            name = prependAssemblyName + "_" + name;

                        _mangledTypeNames = _mangledTypeNames.Add(t, name);
                    }
                }

                return _mangledTypeNames[type];
            }


            string mangledName;

            switch (type.Category)
            {
                case TypeFlags.Array:
                    // mangledName = "Array<" + GetSignatureCPPTypeName(((ArrayType)type).ElementType) + ">";
                    mangledName = GetMangledTypeName(((ArrayType)type).ElementType) + "__Array";
                    if (((ArrayType)type).Rank != 1)
                        mangledName += "Rank" + ((ArrayType)type).Rank.ToString();
                    break;
                case TypeFlags.ByRef:
                    mangledName = GetMangledTypeName(((ByRefType)type).ParameterType) + "__ByRef";
                    break;
                case TypeFlags.Pointer:
                    mangledName = GetMangledTypeName(((PointerType)type).ParameterType) + "__Pointer";
                    break;
                default:
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
                        mangledName = SanitizeName(type.Name, true);
                    }
                    break;
            }

            lock (this)
            {
                _mangledTypeNames = _mangledTypeNames.Add(type, mangledName);
            }

            return mangledName;
        }

        ImmutableDictionary<MethodDesc, string> _mangledMethodNames = ImmutableDictionary<MethodDesc, string>.Empty;

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

                        if (deduplicator.Contains(name))
                        {
                            string nameWithIndex;
                            for (int index = 1; ; index++)
                            {
                                nameWithIndex = name + "_" + index.ToString(CultureInfo.InvariantCulture);
                                if (!deduplicator.Contains(nameWithIndex))
                                    break;
                            }
                            name = nameWithIndex;
                        }
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

        ImmutableDictionary<FieldDesc, string> _mangledFieldNames = ImmutableDictionary<FieldDesc, string>.Empty;

        //
        // Mangled field names are really only useful to identify RVA mapped fields.
        //
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

                        if (deduplicator.Contains(name))
                        {
                            string nameWithIndex;
                            for (int index = 1; ; index++)
                            {
                                nameWithIndex = name + "_" + index.ToString(CultureInfo.InvariantCulture);
                                if (!deduplicator.Contains(nameWithIndex))
                                    break;
                            }
                            name = nameWithIndex;
                        }
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
    }
}
