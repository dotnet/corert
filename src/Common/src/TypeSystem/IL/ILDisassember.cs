// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.IL
{
    public struct ILDisassember
    {
        private byte[] _ilBytes;
        private MethodIL _methodIL;
        private ILTypeNameFormatter _typeNameFormatter;
        private int _currentOffset;

        public ILDisassember(MethodIL methodIL)
        {
            _methodIL = methodIL;
            _ilBytes = methodIL.GetILBytes();
            _currentOffset = 0;
            _typeNameFormatter = null;
        }

        #region Type/member/signature name formatting
        private ILTypeNameFormatter TypeNameFormatter
        {
            get
            {
                if (_typeNameFormatter == null)
                {
                    // Find the owning module so that the type name formatter can remove
                    // redundant assembly name qualifiers in type names.
                    TypeDesc owningTypeDefinition = _methodIL.OwningMethod.OwningType;
                    ModuleDesc owningModule = owningTypeDefinition is MetadataType ?
                        ((MetadataType)owningTypeDefinition).Module : null;

                    _typeNameFormatter = new ILTypeNameFormatter(owningModule);
                }
                return _typeNameFormatter;
            }
        }

        public string FormatType(TypeDesc type)
        {
            // Types referenced from the IL show as instantiated over generic parameter.
            // E.g. "initobj !0" becomes "initobj !T"
            TypeDesc typeInContext = type.InstantiateSignature(
                _methodIL.OwningMethod.OwningType.Instantiation, _methodIL.OwningMethod.Instantiation);
            if (typeInContext.HasInstantiation)
                return this.TypeNameFormatter.FormatNameWithValueClassPrefix(typeInContext);
            return this.TypeNameFormatter.FormatName(typeInContext);
        }

        private string FormatOwningType(TypeDesc type)
        {
            // Special case primitive types: we don't want to use short names here
            if (type.IsPrimitive || type.IsString || type.IsObject)
            {
                MetadataType mdType = (MetadataType)type;
                return String.Concat(mdType.Namespace, ".", mdType.Name);
            }
            return FormatType(type);
        }

        private string FormatMethodSignature(MethodDesc method)
        {
            StringBuilder sb = new StringBuilder();

            MethodSignature signature = method.Signature;

            FormatSignaturePrefix(signature, sb);
            sb.Append(' ');
            sb.Append(FormatOwningType(method.OwningType));
            sb.Append("::");
            sb.Append(method.Name);
            sb.Append('(');
            FormatSignatureArgumentList(signature, sb);
            sb.Append(')');

            return sb.ToString();
        }

        private string FormatMethodSignature(MethodSignature signature)
        {
            StringBuilder sb = new StringBuilder();

            FormatSignaturePrefix(signature, sb);
            sb.Append('(');
            FormatSignatureArgumentList(signature, sb);
            sb.Append(')');

            return sb.ToString();
        }

        private void FormatSignaturePrefix(MethodSignature signature, StringBuilder sb)
        {
            if (!signature.IsStatic)
                sb.Append("instance ");

            sb.Append(this.TypeNameFormatter.FormatNameWithValueClassPrefix(signature.ReturnType));
        }

        private void FormatSignatureArgumentList(MethodSignature signature, StringBuilder sb)
        {
            for (int i = 0; i < signature.Length; i++)
            {
                if (i != 0)
                    sb.Append(", ");

                sb.Append(this.TypeNameFormatter.FormatNameWithValueClassPrefix(signature[i]));
            }
        }

        private string FormatFieldSignature(FieldDesc field)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(this.TypeNameFormatter.FormatNameWithValueClassPrefix(field.FieldType));
            sb.Append(' ');
            sb.Append(FormatOwningType(field.OwningType));
            sb.Append("::");
            sb.Append(field.Name);

            return sb.ToString();
        }

        private string FormatStringLiteral(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length + 2);

            sb.Append('"');

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\')
                    sb.Append("\\\\");
                else if (s[i] == '\t')
                    sb.Append("\\t");
                else if (s[i] == '"')
                    sb.Append("\\\"");
                else if (s[i] == '\n')
                    sb.Append("\\n");
                else
                    sb.Append(s[i]);
            }
            sb.Append('"');

            return sb.ToString();
        }

        private string FormatToken(int token)
        {
            object obj = _methodIL.GetObject(token);
            if (obj is MethodDesc)
                return FormatMethodSignature((MethodDesc)obj);
            else if (obj is FieldDesc)
                return FormatFieldSignature((FieldDesc)obj);
            else if (obj is MethodSignature)
                return FormatMethodSignature((MethodSignature)obj);
            else if (obj is TypeDesc)
                return FormatType((TypeDesc)obj);
            else
            {
                Debug.Assert(obj is string, "NYI: " + obj.GetType());
                return FormatStringLiteral((string)obj);
            }
        }
        #endregion

        #region Instruction decoding
        private byte ReadILByte()
        {
            return _ilBytes[_currentOffset++];
        }

        private UInt16 ReadILUInt16()
        {
            UInt16 val = (UInt16)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8));
            _currentOffset += 2;
            return val;
        }

        private UInt32 ReadILUInt32()
        {
            UInt32 val = (UInt32)(_ilBytes[_currentOffset] + (_ilBytes[_currentOffset + 1] << 8) + (_ilBytes[_currentOffset + 2] << 16) + (_ilBytes[_currentOffset + 3] << 24));
            _currentOffset += 4;
            return val;
        }

        private int ReadILToken()
        {
            return (int)ReadILUInt32();
        }

        private ulong ReadILUInt64()
        {
            ulong value = ReadILUInt32();
            value |= (((ulong)ReadILUInt32()) << 32);
            return value;
        }

        private unsafe float ReadILFloat()
        {
            uint value = ReadILUInt32();
            return *(float*)(&value);
        }

        private unsafe double ReadILDouble()
        {
            ulong value = ReadILUInt64();
            return *(double*)(&value);
        }

        public static string FormatOffset(int offset)
        {
            return "IL_" + offset.ToString("X4");
        }

        public bool HasNextInstruction
        {
            get
            {
                return _currentOffset < _ilBytes.Length;
            }
        }

        public int CodeSize
        {
            get
            {
                return _ilBytes.Length;
            }
        }

        public string GetNextInstruction()
        {
            string opCodeName = FormatOffset(_currentOffset) + ": ";

        again:

            ILOpcode opCode = (ILOpcode)ReadILByte();
            if (opCode == ILOpcode.prefix1)
            {
                opCode = (ILOpcode)(0x100 + ReadILByte());
            }

            opCodeName += opCode.ToString().Replace("_", ".");

            switch (opCode)
            {
                case ILOpcode.ldarg_s:
                case ILOpcode.ldarga_s:
                case ILOpcode.starg_s:
                case ILOpcode.ldloc_s:
                case ILOpcode.ldloca_s:
                case ILOpcode.stloc_s:
                case ILOpcode.ldc_i4_s:
                    return opCodeName + " " + ReadILByte();

                case ILOpcode.unaligned:
                    opCodeName += " " + ReadILByte() + " ";
                    goto again;

                case ILOpcode.ldarg:
                case ILOpcode.ldarga:
                case ILOpcode.starg:
                case ILOpcode.ldloc:
                case ILOpcode.ldloca:
                case ILOpcode.stloc:
                    return opCodeName + " " + ReadILUInt16();

                case ILOpcode.ldc_i4:
                    return opCodeName + " " + ReadILUInt32();

                case ILOpcode.ldc_r4:
                    return opCodeName + " " + ReadILFloat();

                case ILOpcode.ldc_i8:
                    return opCodeName + " " + ReadILUInt64();

                case ILOpcode.ldc_r8:
                    return opCodeName + " " + ReadILDouble();

                case ILOpcode.jmp:
                case ILOpcode.call:
                case ILOpcode.calli:
                case ILOpcode.callvirt:
                case ILOpcode.cpobj:
                case ILOpcode.ldobj:
                case ILOpcode.ldstr:
                case ILOpcode.newobj:
                case ILOpcode.castclass:
                case ILOpcode.isinst:
                case ILOpcode.unbox:
                case ILOpcode.ldfld:
                case ILOpcode.ldflda:
                case ILOpcode.stfld:
                case ILOpcode.ldsfld:
                case ILOpcode.ldsflda:
                case ILOpcode.stsfld:
                case ILOpcode.stobj:
                case ILOpcode.box:
                case ILOpcode.newarr:
                case ILOpcode.ldelema:
                case ILOpcode.ldelem:
                case ILOpcode.stelem:
                case ILOpcode.unbox_any:
                case ILOpcode.refanyval:
                case ILOpcode.mkrefany:
                case ILOpcode.ldtoken:
                case ILOpcode.ldftn:
                case ILOpcode.ldvirtftn:
                case ILOpcode.initobj:
                case ILOpcode.constrained:
                case ILOpcode.sizeof_:
                    return opCodeName + " " + FormatToken(ReadILToken());

                case ILOpcode.br_s:
                case ILOpcode.leave_s:
                case ILOpcode.brfalse_s:
                case ILOpcode.brtrue_s:
                case ILOpcode.beq_s:
                case ILOpcode.bge_s:
                case ILOpcode.bgt_s:
                case ILOpcode.ble_s:
                case ILOpcode.blt_s:
                case ILOpcode.bne_un_s:
                case ILOpcode.bge_un_s:
                case ILOpcode.bgt_un_s:
                case ILOpcode.ble_un_s:
                case ILOpcode.blt_un_s:
                    return opCodeName + " " + FormatOffset((sbyte)ReadILByte() + _currentOffset);

                case ILOpcode.br:
                case ILOpcode.leave:
                case ILOpcode.brfalse:
                case ILOpcode.brtrue:
                case ILOpcode.beq:
                case ILOpcode.bge:
                case ILOpcode.bgt:
                case ILOpcode.ble:
                case ILOpcode.blt:
                case ILOpcode.bne_un:
                case ILOpcode.bge_un:
                case ILOpcode.bgt_un:
                case ILOpcode.ble_un:
                case ILOpcode.blt_un:
                    return opCodeName + " " + FormatOffset((int)ReadILUInt32() + _currentOffset);

                case ILOpcode.switch_:
                    {
                        opCodeName = "switch (";
                        uint count = ReadILUInt32();
                        int jmpBase = _currentOffset + (int)(4 * count);
                        for (uint i = 0; i < count; i++)
                        {
                            if (i != 0)
                                opCodeName += ", ";
                            int delta = (int)ReadILUInt32();
                            opCodeName += FormatOffset(jmpBase + delta);
                        }
                        opCodeName += ")";
                        return opCodeName;
                    }

                default:
                    return opCodeName;
            }
        }
        #endregion

        #region Helpers
        private class ILTypeNameFormatter : TypeNameFormatter
        {
            private ModuleDesc _thisModule;

            public ILTypeNameFormatter(ModuleDesc thisModule)
            {
                _thisModule = thisModule;
            }

            public string FormatNameWithValueClassPrefix(TypeDesc type)
            {
                if (!type.IsSignatureVariable
                    && type.IsDefType
                    && !type.IsPrimitive
                    && !type.IsObject
                    && !type.IsString)
                {
                    string prefix = type.IsValueType ? "valuetype " : "class ";
                    return String.Concat(prefix, FormatName(type));
                }

                return FormatName(type);
            }

            public override string FormatName(PointerType type)
                => String.Concat(FormatNameWithValueClassPrefix(type.ParameterType), "*");

            public override string FormatName(SignatureMethodVariable type)
                => String.Concat("!!", type.Index.ToStringInvariant());

            public override string FormatName(SignatureTypeVariable type)
                => String.Concat("!", type.Index.ToStringInvariant());

            public override string FormatName(GenericParameterDesc type)
            {
                if (type.Kind == GenericParameterKind.Type)
                    return "!" + type.ToString(); // TODO: should we require a Name property for this?
                return "!!" + type.ToString();
            }

            public override string FormatName(InstantiatedType type)
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(FormatName(type.GetTypeDefinition()));
                sb.Append('<');

                foreach (var arg in type.Instantiation)
                    sb.Append(FormatNameWithValueClassPrefix(arg));

                sb.Append('>');

                return sb.ToString();
            }

            public override string FormatName(ByRefType type)
                => String.Concat(FormatNameWithValueClassPrefix(type.ParameterType), "&");

            public override string FormatName(ArrayType type)
                => String.Concat(FormatNameWithValueClassPrefix(type.ElementType), "[", new String(',', type.Rank - 1), "]");

            protected override string FormatNameForNamespaceType(MetadataType type)
            {
                switch (type.Category)
                {
                    case TypeFlags.Void:
                        return "void";
                    case TypeFlags.Boolean:
                        return "bool";
                    case TypeFlags.Char:
                        return "char";
                    case TypeFlags.SByte:
                        return "int8";
                    case TypeFlags.Byte:
                        return "uint8";
                    case TypeFlags.Int16:
                        return "int16";
                    case TypeFlags.UInt16:
                        return "uint16";
                    case TypeFlags.Int32:
                        return "int32";
                    case TypeFlags.UInt32:
                        return "uint32";
                    case TypeFlags.Int64:
                        return "int64";
                    case TypeFlags.UInt64:
                        return "uint64";
                    case TypeFlags.IntPtr:
                        return "native int";
                    case TypeFlags.UIntPtr:
                        return "native uint";
                    case TypeFlags.Single:
                        return "float32";
                    case TypeFlags.Double:
                        return "float64";
                }

                if (type.IsString)
                    return "string";
                if (type.IsObject)
                    return "object";

                string ns = type.Namespace;

                ModuleDesc owningModule = type.Module;
                if (owningModule != _thisModule)
                {
                    Debug.Assert(owningModule is IAssemblyDesc);
                    string owningModuleName = ((IAssemblyDesc)owningModule).GetName().Name;
                    return ns.Length > 0 ?
                        String.Concat("[", owningModuleName, "]", ns, ".", type.Name) :
                        String.Concat("[", owningModuleName, "]", type.Name);
                }

                return ns.Length > 0 ? String.Concat(ns, ".", type.Name) : type.Name;
            }

            protected override string FormatNameForNestedType(MetadataType containingType, MetadataType nestedType)
                => String.Concat(FormatName(containingType), "/", nestedType.Name);
        }
        #endregion
    }
}
