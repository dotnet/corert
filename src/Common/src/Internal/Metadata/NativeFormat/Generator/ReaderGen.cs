// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This class generates most of the implementation of the MetadataReader for the ProjectN format,
// ensuring that the contract defined by CsPublicGen2 is implemented.The generated file is
// 'NativeFormatReaderGen.cs', and any missing implementation is the supplied in the human-authored
// source counterpart 'NativeFormatReader.cs'.
//

class ReaderGen : CsWriter
{
    public ReaderGen(string fileName)
        : base(fileName)
    {
    }

    public void EmitSource()
    {
        WriteLine("#pragma warning disable 649");
        WriteLine("#pragma warning disable 169");
        WriteLine("#pragma warning disable 282 // There is no defined ordering between fields in multiple declarations of partial class or struct");
        WriteLine();

        WriteLine("using System;");
        WriteLine("using System.Reflection;");
        WriteLine("using System.Collections.Generic;");
        WriteLine();

        OpenScope("namespace Internal.Metadata.NativeFormat");

        foreach (var record in SchemaDef.RecordSchema)
        {
            EmitRecord(record);
            EmitHandle(record);
        }

        EmitOpaqueHandle();
        EmitMetadataReader();

        CloseScope("Internal.Metadata.NativeFormat");
    }

    private void EmitRecord(RecordDef record)
    {
        OpenScope($"public partial struct {record.Name}");

        WriteLine("internal MetadataReader _reader;");
        WriteLine($"internal {record.Name}Handle _handle;");

        OpenScope($"public {record.Name}Handle Handle");
        OpenScope("get");
        WriteLine("return _handle;");
        CloseScope();
        CloseScope("Handle");

        foreach (var member in record.Members)
        {
            if ((member.Flags & MemberDefFlags.NotPersisted) != 0)
                continue;

            string memberType = member.GetMemberType();
            string fieldType = member.GetMemberType(MemberTypeKind.ReaderField);

            string fieldName = member.GetMemberFieldName();

            string description = member.GetMemberDescription();
            if (description != null)
                WriteDocComment(description);
            OpenScope($"public {memberType} {member.Name}");
            OpenScope("get");
            if (fieldType != memberType)
                WriteLine($"return ({memberType}){fieldName};");
            else
                WriteLine($"return {fieldName};");
            CloseScope();
            CloseScope(member.Name);

            WriteLineIfNeeded();
            WriteLine($"internal {fieldType} {fieldName};");
        }

        CloseScope(record.Name);
    }

    private void EmitHandle(RecordDef record)
    {
        string handleName = $"{record.Name}Handle";

        OpenScope($"public partial struct {handleName}");

        OpenScope("public override bool Equals(object obj)");
        WriteLine($"if (obj is {handleName})");
        WriteLine($"    return _value == (({handleName})obj)._value;");
        WriteLine("else if (obj is Handle)");
        WriteLine("    return _value == ((Handle)obj)._value;");
        WriteLine("else");
        WriteLine("    return false;");
        CloseScope("Equals");

        OpenScope($"public bool Equals({handleName} handle)");
        WriteLine("return _value == handle._value;");
        CloseScope("Equals");

        OpenScope("public bool Equals(Handle handle)");
        WriteLine("return _value == handle._value;");
        CloseScope("Equals");

        OpenScope("public override int GetHashCode()");
        WriteLine("return (int)_value;");
        CloseScope("GetHashCode");

        WriteLineIfNeeded();
        WriteLine("internal int _value;");

        OpenScope($"internal {handleName}(Handle handle) : this(handle._value)");
        CloseScope();

        OpenScope($"internal {handleName}(int value)");
        WriteLine("HandleType hType = (HandleType)(value >> 24);");
        WriteLine($"if (!(hType == 0 || hType == HandleType.{record.Name} || hType == HandleType.Null))");
        WriteLine("    throw new ArgumentException();");
        WriteLine($"_value = (value & 0x00FFFFFF) | (((int)HandleType.{record.Name}) << 24);");
        WriteLine("_Validate();");
        CloseScope();

        OpenScope($"public static implicit operator  Handle({handleName} handle)");
        WriteLine("return new Handle(handle._value);");
        CloseScope("Handle");

        OpenScope("internal int Offset");
        OpenScope("get");
        WriteLine("return (this._value & 0x00FFFFFF);");
        CloseScope();
        CloseScope("Offset");

        OpenScope($"public {record.Name} Get{record.Name}(MetadataReader reader)");
        WriteLine($"return reader.Get{record.Name}(this);");
        CloseScope($"Get{record.Name}");

        OpenScope("public bool IsNull(MetadataReader reader)");
        WriteLine("return reader.IsNull(this);");
        CloseScope("IsNull");

        OpenScope("public Handle ToHandle(MetadataReader reader)");
        WriteLine("return reader.ToHandle(this);");
        CloseScope("ToHandle");

        WriteScopeAttribute("[System.Diagnostics.Conditional(\"DEBUG\")]");
        OpenScope("internal void _Validate()");
        WriteLine($"if ((HandleType)((_value & 0xFF000000) >> 24) != HandleType.{record.Name})");
        WriteLine("    throw new ArgumentException();");
        CloseScope("_Validate");

        OpenScope("public override String ToString()");
        WriteLine("return String.Format(\"{0:X8}\", _value);");
        CloseScope("ToString");

        CloseScope(handleName);
    }

    private void EmitOpaqueHandle()
    {
        OpenScope("public partial struct Handle");

        foreach (var record in SchemaDef.RecordSchema)
        {
            string handleName = $"{record.Name}Handle";

            OpenScope($"public {handleName} To{handleName}(MetadataReader reader)");
            WriteLine($"return new {handleName}(this);");
            CloseScope($"To{handleName}");
        }

        CloseScope("Handle");
    }

    private void EmitMetadataReader()
    {
        OpenScope("public partial class MetadataReader");

        foreach (var record in SchemaDef.RecordSchema)
        {
            OpenScope($"public {record.Name} Get{record.Name}({record.Name}Handle handle)");
            if (record.Name == "ConstantStringValue")
            {
                WriteLine("if (IsNull(handle))");
                WriteLine("    return new ConstantStringValue();");
            }
            WriteLine($"var record = new {record.Name}() {{ _reader = this, _handle = handle }};");
            WriteLine("var offset = (uint)handle.Offset;");
            foreach (var member in record.Members)
            {
                if ((member.Flags & MemberDefFlags.NotPersisted) != 0)
                    continue;
                WriteLine($"offset = _streamReader.Read(offset, out record.{member.GetMemberFieldName()});");
            }
            WriteLine("return record;");
            CloseScope($"Get{record.Name}");
        }

        foreach (var record in SchemaDef.RecordSchema)
        {
            OpenScope($"internal Handle ToHandle({record.Name}Handle handle)");
            WriteLine("return new Handle(handle._value);");
            CloseScope("ToHandle");
        }

        foreach (var record in SchemaDef.RecordSchema)
        {
            string handleName = $"{record.Name}Handle";

            OpenScope($"internal {handleName} To{handleName}(Handle handle)");
            WriteLine($"return new {handleName}(handle._value);");
            CloseScope($"To{handleName}");
        }

        foreach (var record in SchemaDef.RecordSchema)
        {
            OpenScope($"internal bool IsNull({record.Name}Handle handle)");
            WriteLine("return (handle._value & 0x00FFFFFF) == 0;");
            CloseScope("IsNull");
        }

        CloseScope("MetadataReader");
    }
}
