// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler
{
    public abstract class PreInitFixupInfo : IComparable<PreInitFixupInfo>
    {
        /// <summary>
        /// Offset into the blob
        /// </summary>
        public int Offset { get; }

        public PreInitFixupInfo(int offset)
        {
            Offset = offset;
        }

        int IComparable<PreInitFixupInfo>.CompareTo(PreInitFixupInfo other)
        {
            return this.Offset - other.Offset;
        }

        /// <summary>
        /// Writes fixup data into current ObjectDataBuilder. Caller needs to make sure ObjectDataBuilder is
        /// at correct offset before writing.
        /// </summary>
        public abstract void WriteData(ref ObjectDataBuilder builder, NodeFactory factory);
    }

    public class PreInitTypeFixupInfo : PreInitFixupInfo
    {
        public TypeDesc TypeFixup { get; }

        public PreInitTypeFixupInfo(int offset, TypeDesc type)
            :base(offset)
        {
            TypeFixup = type;
        }

        public override void WriteData(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            builder.EmitPointerRelocOrIndirectionReference(factory.NecessaryTypeSymbol(TypeFixup));
        }
    }

    public class PreInitMethodFixupInfo : PreInitFixupInfo
    {
        public MethodDesc MethodFixup { get; }

        public PreInitMethodFixupInfo(int offset, MethodDesc method)
            : base(offset)
        {
            MethodFixup = method;
        }

        public override void WriteData(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            builder.EmitPointerReloc(factory.ExactCallableAddress(MethodFixup));
        }
    }

    public class PreInitFieldFixupInfo : PreInitFixupInfo
    {
        public FieldDesc FieldFixup { get; }

        public PreInitFieldFixupInfo(int offset, FieldDesc field)
            : base(offset)
        {
            FieldFixup = field;
        }

        public override void WriteData(ref ObjectDataBuilder builder, NodeFactory factory)
        {
            MetadataType type = (MetadataType)FieldFixup.OwningType;

            // Do not support fixing up fields from external modules
            if (!factory.CompilationModuleGroup.ContainsType(type))
                throw new BadImageFormatException();

            ISymbolNode staticBase = FieldFixup.HasGCStaticBase ? factory.TypeGCStaticsSymbol(type) : factory.TypeNonGCStaticsSymbol(type);
            builder.EmitPointerReloc(staticBase, FieldFixup.Offset.AsInt);
        }
    }

    public class PreInitFieldInfo
    {
        public FieldDesc Field { get; }

        /// <summary>
        /// The type of the real field data. This could be a subtype of the field type.
        /// </summary>
        public TypeDesc Type { get; }

        /// <summary>
        /// Points to the underlying contents of the data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Start offset of the real contents in the data.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Number of elements, if this is a frozen array.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// List of fixup to be apply to the data blob
        /// This is needed for information that can't be encoded into blob ahead of time before codegen
        /// </summary>
        private List<PreInitFixupInfo> FixupInfos;

        public PreInitFieldInfo(FieldDesc field, TypeDesc type, byte[] data, int offset, int length, List<PreInitFixupInfo> fixups)
        {
            Field = field;
            Type = type;
            Data = data;
            Offset = offset;
            Length = length;
            FixupInfos = fixups;

            if (FixupInfos != null)
                FixupInfos.Sort();
        }

        public void WriteData(ref ObjectDataBuilder builder, NodeFactory factory, bool relocsOnly = false)
        {
            int offset = Offset;

            if (FixupInfos != null)
            {
                int startOffset = builder.CountBytes;

                for (int i = 0; i < FixupInfos.Count; ++i)
                {
                    var fixupInfo = FixupInfos[i];

                    // do we have overlapping fixups?
                    if (fixupInfo.Offset < offset)
                        throw new BadImageFormatException();

                    if (!relocsOnly)
                    {
                        // emit bytes before fixup
                        builder.EmitBytes(Data, offset, fixupInfo.Offset - offset);
                    }

                    // write the fixup
                    FixupInfos[i].WriteData(ref builder, factory);

                    // move pointer past the fixup
                    offset = Offset + builder.CountBytes - startOffset;
                }
            }

            if (offset > Data.Length)
                throw new BadImageFormatException();
            
            if (!relocsOnly)
            {
                // Emit remaining bytes
                builder.EmitBytes(Data, offset, Data.Length - offset);
            }
        }

        public static List<PreInitFieldInfo> GetPreInitFieldInfos(TypeDesc type, bool hasGCStaticBase)
        {
            List<PreInitFieldInfo> list = null;

            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic || field.IsThreadStatic)
                    continue;

                if (field.HasGCStaticBase != hasGCStaticBase)
                    continue;

                var dataField = GetPreInitDataField(field);
                if (dataField != null)
                {
                    if (list == null)
                        list = new List<PreInitFieldInfo>();
                    list.Add(ConstructPreInitFieldInfo(field, dataField));
                }
            }

            return list;
        }

        /// <summary>
        /// Retrieves the corresponding static preinitialized data field by looking at various attributes
        /// </summary>
        private static FieldDesc GetPreInitDataField(FieldDesc thisField)
        {
            Debug.Assert(thisField.IsStatic);

            var field = thisField as EcmaField;
            if (field == null)
                return null;

            var decoded = field.GetDecodedCustomAttribute("System.Runtime.CompilerServices", "InitDataBlobAttribute");
            if (decoded == null)
                return null;

            var decodedValue = decoded.Value;
            if (decodedValue.FixedArguments.Length != 2)
                throw new BadImageFormatException();

            var typeDesc = decodedValue.FixedArguments[0].Value as TypeDesc;
            if (typeDesc == null)
                throw new BadImageFormatException(); 

            if (decodedValue.FixedArguments[1].Type != field.Context.GetWellKnownType(WellKnownType.String))
                throw new BadImageFormatException(); 

            var fieldName = (string)decodedValue.FixedArguments[1].Value;
            var dataField = typeDesc.GetField(fieldName);
            if (dataField== null)
                throw new BadImageFormatException();

            return dataField;
        }

        /// <summary>
        /// Extract preinitialize data as byte[] from a RVA field, and perform necessary validations.
        /// </summary>
        private static PreInitFieldInfo ConstructPreInitFieldInfo(FieldDesc field, FieldDesc dataField)
        {
            if (!dataField.HasRva)
                throw new BadImageFormatException();

            var ecmaDataField = dataField as EcmaField;
            if (ecmaDataField == null)
                throw new NotSupportedException();

            var rvaData = ecmaDataField.GetFieldRvaData();
            var fieldType = field.FieldType;
            int elementCount;
            int realDataOffset;
            TypeDesc realDataType = null;

            //
            // Construct fixups
            //
            List<PreInitFixupInfo> fixups = null;

            var typeFixupAttrs = ecmaDataField.GetDecodedCustomAttributes("System.Runtime.CompilerServices", "TypeHandleFixupAttribute");
            foreach (var typeFixupAttr in typeFixupAttrs)
            {
                if (typeFixupAttr.FixedArguments[0].Type != field.Context.GetWellKnownType(WellKnownType.Int32))
                    throw new BadImageFormatException();

                int offset = (int)typeFixupAttr.FixedArguments[0].Value;
                var typeArg = typeFixupAttr.FixedArguments[1].Value;
                var fixupType = typeArg as TypeDesc;
                if (fixupType == null)
                {
                    if (typeArg is string fixupTypeName)
                    {
                        fixupType = CustomAttributeTypeNameParser.GetTypeByCustomAttributeTypeName(ecmaDataField.Module, fixupTypeName, throwIfNotFound: true);
                    }
                    else
                    {
                        throw new BadImageFormatException();
                    }
                }

                fixups = fixups ?? new List<PreInitFixupInfo>();

                if (offset == 0 && fieldType.IsSzArray)
                {
                    // For array field, offset 0 is the element type handle followed by the element count
                    realDataType = fixupType;
                }
                else
                {
                    fixups.Add(new PreInitTypeFixupInfo(offset, fixupType));
                }
            }

            var methodFixupAttrs = ecmaDataField.GetDecodedCustomAttributes("System.Runtime.CompilerServices", "MethodAddrFixupAttribute");
            foreach (var methodFixupAttr in methodFixupAttrs)
            {
                if (methodFixupAttr.FixedArguments[0].Type != field.Context.GetWellKnownType(WellKnownType.Int32))
                    throw new BadImageFormatException();

                int offset = (int)methodFixupAttr.FixedArguments[0].Value;
                TypeDesc fixupType = methodFixupAttr.FixedArguments[1].Value as TypeDesc;
                if (fixupType == null)
                    throw new BadImageFormatException();

                string methodName = methodFixupAttr.FixedArguments[2].Value as string;
                if (methodName == null)
                    throw new BadImageFormatException();

                var method = fixupType.GetMethod(methodName, signature : null);
                if (method == null)
                    throw new BadImageFormatException();

                fixups = fixups ?? new List<PreInitFixupInfo>();

                fixups.Add(new PreInitMethodFixupInfo(offset, method));
            }

            var fieldFixupAttrs = ecmaDataField.GetDecodedCustomAttributes("System.Runtime.CompilerServices", "FieldAddrFixupAttribute");
            foreach (var fieldFixupAttr in fieldFixupAttrs)
            {
                if (fieldFixupAttr.FixedArguments[0].Type != field.Context.GetWellKnownType(WellKnownType.Int32))
                    throw new BadImageFormatException();

                int offset = (int)fieldFixupAttr.FixedArguments[0].Value;
                TypeDesc fixupType = fieldFixupAttr.FixedArguments[1].Value as TypeDesc;
                if (fixupType == null)
                    throw new BadImageFormatException();

                string fieldName = fieldFixupAttr.FixedArguments[2].Value as string;
                if (fieldName == null)
                    throw new BadImageFormatException();

                var fixupField = fixupType.GetField(fieldName);
                if (fixupField == null)
                    throw new BadImageFormatException();

                if (!fixupField.IsStatic)
                    throw new BadImageFormatException();

                fixups = fixups ?? new List<PreInitFixupInfo>();

                fixups.Add(new PreInitFieldFixupInfo(offset, fixupField));
            }
            
            if (fieldType.IsValueType || fieldType.IsPointer)
            {
                elementCount = -1;
                realDataOffset = 0;
                realDataType = fieldType;
            }
            else if (fieldType.IsSzArray)
            {
                // Offset 0 is the element type handle fixup followed by the element count
                if (realDataType == null)
                    throw new BadImageFormatException();

                int ptrSize = fieldType.Context.Target.PointerSize;
                elementCount = rvaData[ptrSize] | rvaData[ptrSize + 1] << 8 | rvaData[ptrSize + 2] << 16 | rvaData[ptrSize + 3] << 24;
                realDataOffset = ptrSize * 2;
                realDataType = realDataType.MakeArrayType();
            }
            else
            {
                throw new NotSupportedException();
            }

            return new PreInitFieldInfo(field, realDataType, rvaData, realDataOffset, elementCount, fixups);
        }

        public static int FieldDescCompare(PreInitFieldInfo fieldInfo1, PreInitFieldInfo fieldInfo2)
        {
            return fieldInfo1.Field.Offset.AsInt - fieldInfo2.Field.Offset.AsInt;
        }
    }
}
