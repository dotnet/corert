// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILToNative
{
    //
    // NameMangler is reponsible for giving extern C/C++ names to types, methods and fields
    //
    public class NameMangler
    {
        readonly Compilation _compilation;

        public NameMangler(Compilation compilation)
        {
            _compilation = compilation;
        }

        // Turn a name into a valid identifier
        private static string SanitizeName(string s)
        {
            // TODO: Handle Unicode, etc.
            s = s.Replace("`", "_");
            s = s.Replace("<", "_");
            s = s.Replace(">", "_");
            s = s.Replace("$", "_");
            return s;
        }

        int _unique = 1;
        HashSet<String> _deduplicator = new HashSet<String>();

        internal string GetMangledTypeName(TypeDesc type)
        {
            var reg = _compilation.GetRegisteredType(type);

            string mangledName = reg.MangledName;
            if (mangledName != null)
                return mangledName;

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
                    mangledName = type.Name;

                    // Include encapsulating type
                    TypeDesc def = type.GetTypeDefinition();
                    TypeDesc containingType = (def is EcmaType) ? ((EcmaType)def).ContainingType : null;
                    while (containingType != null)
                    {
                        mangledName = containingType.Name + "__" + mangledName;

                        containingType = ((EcmaType)containingType).ContainingType;
                    }

                    mangledName = SanitizeName(mangledName);

                    mangledName = mangledName.Replace(".", _compilation.IsCppCodeGen ? "::" : "_");

                    // TODO: the special handling for "Interop" is needed due to type name / namespace name clashes;
                    //       find a better solution
                    if (type.HasInstantiation || _deduplicator.Contains(mangledName) || mangledName == "Interop")
                        mangledName = mangledName + "_" + _unique++;
                    _deduplicator.Add(mangledName);

                    break;
            }

            reg.MangledName = mangledName;
            return mangledName;
        }

        internal string GetMangledMethodName(MethodDesc method)
        {
            var reg = _compilation.GetRegisteredMethod(method);

            string mangledName = reg.MangledName;
            if (mangledName != null)
                return mangledName;

            RegisteredType regType = _compilation.GetRegisteredType(method.OwningType);

            mangledName = SanitizeName(method.Name);

            mangledName = mangledName.Replace(".", "_"); // To handle names like .ctor

            if (!_compilation.IsCppCodeGen)
                mangledName = GetMangledTypeName(method.OwningType) + "__" + mangledName;

            RegisteredMethod rm = regType.Methods;
            bool dedup = false;
            while (rm != null)
            {
                if (rm.MangledName != null && rm.MangledName == mangledName)
                {
                    dedup = true;
                    break;
                }

                rm = rm.Next;
            }
            if (dedup)
                mangledName = mangledName + "_" + regType.UniqueMethod++;

            reg.MangledName = mangledName;

            reg.Next = regType.Methods;
            regType.Methods = reg;

            return mangledName;
        }
    }
}
