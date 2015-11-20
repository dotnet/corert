// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

namespace ILCompiler.CppCodeGen
{
    class CppWriter
    {
        Compilation _compilation;

        private void SetWellKnownTypeSignatureName(WellKnownType wellKnownType, string mangledSignatureName)
        {
            var type = _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
            _compilation.GetRegisteredType(type).MangledSignatureName = mangledSignatureName;
        }

        public CppWriter(Compilation compilation)
        {
            _compilation = compilation;

            SetWellKnownTypeSignatureName(WellKnownType.Void, "void");
            SetWellKnownTypeSignatureName(WellKnownType.Boolean, "uint8_t");
            SetWellKnownTypeSignatureName(WellKnownType.Char, "uint16_t");
            SetWellKnownTypeSignatureName(WellKnownType.SByte, "int8_t");
            SetWellKnownTypeSignatureName(WellKnownType.Byte, "uint8_t");
            SetWellKnownTypeSignatureName(WellKnownType.Int16, "int16_t");
            SetWellKnownTypeSignatureName(WellKnownType.UInt16, "uint16_t");
            SetWellKnownTypeSignatureName(WellKnownType.Int32, "int32_t");
            SetWellKnownTypeSignatureName(WellKnownType.UInt32, "uint32_t");
            SetWellKnownTypeSignatureName(WellKnownType.Int64, "int64_t");
            SetWellKnownTypeSignatureName(WellKnownType.UInt64, "uint64_t");
            SetWellKnownTypeSignatureName(WellKnownType.IntPtr, "intptr_t");
            SetWellKnownTypeSignatureName(WellKnownType.UIntPtr, "uintptr_t");
            SetWellKnownTypeSignatureName(WellKnownType.Single, "float");
            SetWellKnownTypeSignatureName(WellKnownType.Double, "double");

            // TODO: For now, ensure that all types/methods referenced by unmanaged helpers are present
            var stringType = _compilation.TypeSystemContext.GetWellKnownType(WellKnownType.String);
            AddInstanceFields(stringType);

            var stringArrayType = stringType.MakeArrayType();
            _compilation.AddType(stringArrayType);
            _compilation.MarkAsConstructed(stringArrayType);
        }

        public string GetCppSignatureTypeName(TypeDesc type)
        {
            var reg = _compilation.GetRegisteredType(type);

            string mangledName = reg.MangledSignatureName;
            if (mangledName != null)
                return mangledName;

            // TODO: Use friendly names for enums
            if (type.IsEnum)
                mangledName = _compilation.GetRegisteredType(type.UnderlyingType).MangledSignatureName;
            else
                mangledName = GetCppTypeName(type);

            if (!type.IsValueType && !type.IsByRef && !type.IsPointer)
                mangledName += "*";

            reg.MangledSignatureName = mangledName;
            return mangledName;
        }

        public string GetCppMethodDeclaration(MethodDesc method, bool implementation, string externalMethodName = null)
        {
            StringBuilder sb = new StringBuilder();

            var methodSignature = method.Signature;

            if (!implementation)
            {
                if (externalMethodName != null)
                {
                    sb.Append("extern \"C\" ");
                }
                else
                {
                    sb.Append("static ");
                }
            }
            sb.Append(GetCppSignatureTypeName(methodSignature.ReturnType));
            sb.Append(" ");
            if (implementation)
            {
                sb.Append(GetCppTypeName(method.OwningType));
                sb.Append("::");
            }
            if (externalMethodName != null)
            {
                sb.Append(externalMethodName);
            }
            else
            {
                sb.Append(GetCppMethodName(method));
            }
            sb.Append("(");
            bool hasThis = !methodSignature.IsStatic;
            int argCount = methodSignature.Length;
            if (hasThis)
                argCount++;

            List<string> parameterNames = null;
            IEnumerable<string> parameters = _compilation.TypeSystemContext.GetParameterNamesForMethod(method);
            if (parameters != null)
            {
                parameterNames = new List<string>(parameters);
                if (parameterNames.Count != 0)
                {
                    System.Diagnostics.Debug.Assert(parameterNames.Count == argCount);
                }
                else
                {
                    parameterNames = null;
                }
            }

            for (int i = 0; i < argCount; i++)
            {
                if (hasThis)
                {
                    if (i == 0)
                    {
                        var thisType = method.OwningType;
                        if (thisType.IsValueType)
                            thisType = thisType.MakeByRefType();
                        sb.Append(GetCppSignatureTypeName(thisType));
                    }
                    else
                    {
                        sb.Append(GetCppSignatureTypeName(methodSignature[i - 1]));
                    }
                }
                else
                {
                    sb.Append(GetCppSignatureTypeName(methodSignature[i]));
                }
                if (implementation)
                {
                    sb.Append(" ");

                    if (parameterNames != null)
                    {
                        sb.Append(SanitizeCppVarName(parameterNames[i]));
                    }
                    else
                    {
                        sb.Append("_a");
                        sb.Append(i.ToString());
                    }
                }
                if (i != argCount - 1)
                    sb.Append(", ");

            }
            sb.Append(")");
            if (!implementation)
                sb.Append(";");

            return sb.ToString();
        }

        public string GetCppMethodCallParamList(MethodDesc method)
        {
            StringBuilder sb = new StringBuilder();

            var methodSignature = method.Signature;

            bool hasThis = !methodSignature.IsStatic;
            int argCount = methodSignature.Length;
            if (hasThis)
                argCount++;

            List<string> parameterNames = null;
            IEnumerable<string> parameters = _compilation.TypeSystemContext.GetParameterNamesForMethod(method);
            if (parameters != null)
            {
                parameterNames = new List<string>(parameters);
                if (parameterNames.Count != 0)
                {
                    System.Diagnostics.Debug.Assert(parameterNames.Count == argCount);
                }
                else
                {
                    parameterNames = null;
                }
            }

            for (int i = 0; i < argCount; i++)
            {
                if (parameterNames != null)
                {
                    sb.Append(SanitizeCppVarName(parameterNames[i]));
                }
                else
                {
                    sb.Append("_a");
                    sb.Append(i.ToString());
                }
                if (i != argCount - 1)
                    sb.Append(", ");
            }
            return sb.ToString();
        }

        public string GetCppTypeName(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.ByRef:
                case TypeFlags.Pointer:
                    return GetCppSignatureTypeName(((ParameterizedType)type).ParameterType) + "*";
                default:
                    return _compilation.NameMangler.GetMangledTypeName(type);
            }
        }

        public string GetCppMethodName(MethodDesc method)
        {
            return _compilation.NameMangler.GetMangledMethodName(method);
        }

        public string GetCppFieldName(FieldDesc field)
        {
            return _compilation.NameMangler.GetMangledFieldName(field);
        }

        public string GetCppStaticFieldName(FieldDesc field)
        {
            TypeDesc type = field.OwningType;
            string typeName = GetCppTypeName(type);
            return typeName.Replace("::", "__") + "__" + _compilation.NameMangler.GetMangledFieldName(field);
        }

        public string SanitizeCppVarName(string varName)
        {
            // TODO: name mangling robustness
            if (varName == "errno") // some names collide with CRT headers
                varName += "_";

            return varName;
        }

        string CompileSpecialMethod(MethodDesc method, SpecialMethodKind kind)
        {
            StringBuilder builder = new StringBuilder();
            switch (kind)
            {
                case SpecialMethodKind.PInvoke:
                case SpecialMethodKind.RuntimeImport:
                    {
                        EcmaMethod ecmaMethod = method as EcmaMethod;

                        string importName = kind == SpecialMethodKind.PInvoke ?
                            method.GetPInvokeMethodMetadata().Name : ecmaMethod.GetRuntimeImportEntryPointName();

                        if (importName == null)
                            importName = method.Name;

                        // TODO: hacky special-case
                        if (importName != "memmove" && importName != "malloc") // some methods are already declared by the CRT headers
                        {
                            builder.AppendLine(GetCppMethodDeclaration(method, false, importName));
                        }
                        builder.AppendLine(GetCppMethodDeclaration(method, true));
                        builder.AppendLine("{");
                        builder.Append("    ");
                        if (GetCppSignatureTypeName(method.Signature.ReturnType) != "void")
                        {
                            builder.Append("return ");
                        }

                        builder.AppendLine("::" + importName + "(" + GetCppMethodCallParamList(method) + ");");
                        builder.AppendLine("}");

                        return builder.ToString();
                    }

                default:
                    // TODO: hacky special-case
                    if (method.Name == "BlockCopy")
                        return null;

                    return GetCppMethodDeclaration(method, true) + " { throw 0xC000C000; }" + Environment.NewLine;
            }
        }

        public void CompileMethod(MethodDesc method)
        {
            _compilation.Log.WriteLine("Compiling " + method.ToString());

            SpecialMethodKind kind = method.DetectSpecialMethodKind();

            if (kind != SpecialMethodKind.Unknown)
            {
                string specialMethodCode = CompileSpecialMethod(method, kind);
                _compilation.GetRegisteredMethod(method).MethodCode = specialMethodCode;
                return;
            }

            var methodIL = _compilation.GetMethodIL(method);
            if (methodIL == null)
                return;

            string methodCode;
            try
            {
                var ilImporter = new ILImporter(_compilation, this, method, methodIL);

                CompilerTypeSystemContext typeSystemContext = _compilation.TypeSystemContext;

                if (!_compilation.Options.NoLineNumbers)
                {
                    IEnumerable<ILSequencePoint> sequencePoints = typeSystemContext.GetSequencePointsForMethod(method);
                    if (sequencePoints != null)
                        ilImporter.SetSequencePoints(sequencePoints);
                }

                IEnumerable<LocalVariable> localVariables = typeSystemContext.GetLocalVariableNamesForMethod(method);
                if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);

                IEnumerable<string> parameters = typeSystemContext.GetParameterNamesForMethod(method);
                if (parameters != null)
                    ilImporter.SetParameterNames(parameters);

                methodCode = ilImporter.Compile();
            }
            catch (Exception e)
            {
                _compilation.Log.WriteLine(e.Message + " (" + method + ")");

                methodCode = GetCppMethodDeclaration(method, true) + " { throw 0xC000C000; }" + Environment.NewLine;
            }

            _compilation.GetRegisteredMethod(method).MethodCode = methodCode;
        }

        TextWriter Out
        {
            get
            {
                return _compilation.Out;
            }
        }

        StringBuilder _statics;
        StringBuilder _gcStatics;
        StringBuilder _threadStatics;
        StringBuilder _gcThreadStatics;

        // Base classes and valuetypes has to be emitted before they are used.
        HashSet<RegisteredType> _emittedTypes;

        // Writes the type details (minimal or detailed) to the specified writer (which points to a source file
        // corresponding to an EcmaModule).
        void OutputTypes(bool full, TextWriter outWriter)
        {
            if (full)
            {
                _statics = new StringBuilder();
                _gcStatics = new StringBuilder();
                _threadStatics = new StringBuilder();
                _gcThreadStatics = new StringBuilder();
            }

            if (full)
                _emittedTypes = new HashSet<RegisteredType>();
            foreach (var t in _compilation.RegisteredTypes.ToArray())
            {
                if (t.Type.IsByRef || t.Type.IsPointer)
                    continue;

                // Base class types and valuetype instantance field types may be emitted out-of-order to make them 
                // appear before they are used.
                if (_emittedTypes != null && _emittedTypes.Contains(t))
                    continue;

                OutputType(t, full, outWriter);
            }
            if (full)
                _emittedTypes = null;

            if (full)
            {
                outWriter.WriteLine();
                outWriter.WriteLine("static struct {");
                outWriter.Write(_statics.ToString());
                outWriter.WriteLine("} __statics;");

                // TODO: Register GC statics with GC
                outWriter.WriteLine();
                outWriter.WriteLine("static struct {");
                outWriter.Write(_gcStatics.ToString());
                outWriter.WriteLine("} __gcStatics;");

                outWriter.WriteLine();
                // @TODO_SDM: do for real - note: the 'extra' series are just testing the init syntax for 0-length arrays, they should be removed
                // TODO: preinitialized 0-length arrays are not supported in CLang
                outWriter.WriteLine("#ifdef _MSC_VER");
                outWriter.WriteLine("static StaticGcDesc __gcStaticsDescs = { 1, { { sizeof(__gcStatics), 0 }, { 123, 456 }, { 789, 101112 } } };");
                outWriter.WriteLine("#else");
                outWriter.WriteLine("static StaticGcDesc __gcStaticsDescs;");
                outWriter.WriteLine("#endif");

                outWriter.WriteLine();
                outWriter.WriteLine("static SimpleModuleHeader __module = { &__gcStatics, &__gcStaticsDescs };");


                _statics = null;
                _gcStatics = null;
                _threadStatics = null;
                _gcThreadStatics = null;
            }
        }

        void OutputType(RegisteredType t, bool full, TextWriter outWriterOverride = null)
        {
            if (_emittedTypes != null)
            {
                if (!t.Type.IsValueType)
                {
                    var baseType = t.Type.BaseType;
                    if (baseType != null)
                    {
                        var baseRegistration = _compilation.GetRegisteredType(baseType);
                        if (!_emittedTypes.Contains(baseRegistration))
                        {
                            OutputType(baseRegistration, full, outWriterOverride);
                        }
                    }
                }

                foreach (var field in t.Type.GetFields())
                {
                    if (!_compilation.GetRegisteredField(field).IncludedInCompilation)
                        continue;

                    var fieldType = field.FieldType;
                    if (fieldType.IsValueType && !fieldType.IsPrimitive && !field.IsStatic)
                    {
                        var fieldTypeRegistration = _compilation.GetRegisteredType(fieldType);
                        if (!_emittedTypes.Contains(fieldTypeRegistration))
                        {
                            OutputType(fieldTypeRegistration, full, outWriterOverride);
                        }
                    }
                }

                _emittedTypes.Add(t);
            }

            // Get the StreamWriter for the type, unless it was overridden by the caller.
            TextWriter swType = outWriterOverride;
            if (swType == null)
            {
                swType = _compilation.GetOutWriterForType(t.Type);
            }

            string mangledName = GetCppTypeName(t.Type);

            int nesting = 0;
            int current = 0;
            for (;;)
            {
                int sep = mangledName.IndexOf("::", current);
                if (sep < 0)
                    break;

                swType.Write("namespace " + mangledName.Substring(current, sep - current) + " { ");
                current = sep + 2;

                nesting++;
            }

            if (full)
            {
                swType.Write("class " + mangledName.Substring(current));
                if (!t.Type.IsValueType)
                {
                    var baseType = t.Type.BaseType;
                    if (baseType != null)
                    {
                        swType.Write(" : public " + GetCppTypeName(baseType));
                    }
                }
                swType.WriteLine(" { public:");
                if (t.IncludedInCompilation)
                {
                    swType.WriteLine("static MethodTable * __getMethodTable();");
                }
                if (t.VirtualSlots != null)
                {
                    int baseSlots = 0;
                    var baseType = t.Type.BaseType;
                    while (baseType != null)
                    {
                        var baseReg = _compilation.GetRegisteredType(baseType);
                        if (baseReg.VirtualSlots != null)
                            baseSlots += baseReg.VirtualSlots.Count;
                        baseType = baseType.BaseType;
                    }

                    for (int slot = 0; slot < t.VirtualSlots.Count; slot++)
                    {
                        MethodDesc virtualMethod = t.VirtualSlots[slot];
                        swType.WriteLine(GetCodeForVirtualMethod(virtualMethod, baseSlots + slot));
                    }
                }
                if (t.Type.IsDelegate)
                {
                    swType.WriteLine(GetCodeForDelegate(t.Type));
                }
                foreach (var field in t.Type.GetFields())
                {
                    if (!_compilation.GetRegisteredField(field).IncludedInCompilation)
                        continue;
                    if (field.IsStatic)
                    {
                        TypeDesc fieldType = field.FieldType;
                        StringBuilder builder;
                        if (!fieldType.IsValueType)
                        {
                            _gcStatics.Append(GetCppSignatureTypeName(fieldType));
                            builder = _gcStatics;
                        }
                        else
                        {
                            // TODO: Valuetype statics with GC references
                            _statics.Append(GetCppSignatureTypeName(fieldType));
                            builder = _statics;
                        }
                        builder.AppendLine(" " + GetCppStaticFieldName(field) + ";");
                    }
                    else
                    {
                        swType.WriteLine(GetCppSignatureTypeName(field.FieldType) + " " + GetCppFieldName(field) + ";");
                    }
                }
                if (t.Type.GetMethod(".cctor", null) != null)
                {
                    _statics.AppendLine("bool __cctor_" + GetCppTypeName(t.Type).Replace("::", "__") + ";");
                }

                if (t.Methods != null)
                {
                    foreach (var m in t.Methods)
                    {
                        if (m.IncludedInCompilation)
                            OutputMethod(m, swType);
                    }
                }
                swType.Write("};");
            }
            else
            {
                swType.Write("class " + mangledName.Substring(current) + ";");
            }

            while (nesting > 0)
            {
                swType.Write(" };");
                nesting--;
            }
            swType.WriteLine();
        }

        void OutputMethod(RegisteredMethod m, TextWriter outWriterMethod)
        {
            // Write the method declaration to the specified writer
            outWriterMethod.WriteLine(GetCppMethodDeclaration(m.Method, false));
        }

        void AppendSlotTypeDef(StringBuilder sb, MethodDesc method)
        {
            var methodSignature = method.Signature;

            sb.Append("typedef ");
            sb.Append(GetCppSignatureTypeName(methodSignature.ReturnType));
            sb.Append("(*__slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(")(");

            bool hasThis = !methodSignature.IsStatic;
            int argCount = methodSignature.Length;
            if (hasThis)
                argCount++;
            for (int i = 0; i < argCount; i++)
            {
                if (hasThis)
                {
                    if (i == 0)
                    {
                        sb.Append(GetCppSignatureTypeName(method.OwningType));
                    }
                    else
                    {
                        sb.Append(GetCppSignatureTypeName(methodSignature[i - 1]));
                    }
                }
                else
                {
                    sb.Append(GetCppSignatureTypeName(methodSignature[i]));
                }
                if (i != argCount - 1)
                    sb.Append(", ");
            }
            sb.AppendLine(");");
        }


        String GetCodeForDelegate(TypeDesc delegateType)
        {
            StringBuilder sb = new StringBuilder();

            MethodDesc method = delegateType.GetMethod("Invoke", null);

            AppendSlotTypeDef(sb, method);

            sb.Append("static __slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(" __invoke__");
            sb.Append(GetCppMethodName(method));
            sb.Append("(void * pThis) { return (__slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(")(((");
            sb.Append(GetCppSignatureTypeName(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.MulticastDelegate)));
            sb.Append(")pThis)->m_functionPointer);");
            sb.AppendLine(" };");

            return sb.ToString();
        }

        String GetCodeForVirtualMethod(MethodDesc method, int slot)
        {
            StringBuilder sb = new StringBuilder();

            AppendSlotTypeDef(sb, method);

            sb.Append("static __slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(" __getslot__");
            sb.Append(GetCppMethodName(method));
            sb.Append("(void * pThis) { return (__slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(")*((void **)(*((RawEEType **)pThis) + 1) + ");
            sb.Append(slot.ToString());
            sb.AppendLine("); };");

            return sb.ToString();
        }

        void AppendVirtualSlots(StringBuilder sb, TypeDesc implType, TypeDesc declType)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                AppendVirtualSlots(sb, implType, baseType);

            var reg = _compilation.GetRegisteredType(declType);
            if (reg.VirtualSlots != null)
            {
                for (int i = 0; i < reg.VirtualSlots.Count; i++)
                {
                    MethodDesc declMethod = reg.VirtualSlots[i];
                    MethodDesc implMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestMetadataType());

                    sb.Append("(void*)&");
                    sb.Append(GetCppTypeName(implMethod.OwningType));
                    sb.Append("::");
                    sb.Append(GetCppMethodName(implMethod));
                    sb.Append(",");
                }
            }
        }

        String GetCodeForType(TypeDesc type)
        {
            StringBuilder sb = new StringBuilder();

            int totalSlots = 0;
            TypeDesc t = type;
            while (t != null)
            {
                var reg = _compilation.GetRegisteredType(t);
                if (reg.VirtualSlots != null)
                    totalSlots += reg.VirtualSlots.Count;
                t = t.BaseType;
            }

            sb.Append("MethodTable * ");
            sb.Append(GetCppTypeName(type));
            sb.AppendLine("::__getMethodTable() {");

            sb.Append("static struct {");
            // sb.Append(GCDesc);
            sb.Append("RawEEType EEType;");
            if (totalSlots != 0)
            {
                sb.Append("void * slots[");
                sb.Append(totalSlots);
                sb.Append("];");
            }
            sb.AppendLine("} mt = {");
            // gcdesc
            if (type.IsString)
            {
                // String has non-standard layout
                // component size = 2, flags = 0, base size = 2 ptrs + dword + first char
                sb.Append("{ sizeof(uint16_t), 0, 2 * sizeof(void*) + sizeof(int32_t) + 2, ");
            }
            else
            if (type.IsArray && ((ArrayType)type).Rank == 1)
            {
                sb.Append("{ sizeof(");
                sb.Append(GetCppSignatureTypeName(((ArrayType)type).ElementType)); // component size
                sb.Append("), 4 /* MTFlag_IsArray */, 3 * sizeof(void*), "); // flags, baseSize
            }
            else
            if (type.IsArray)
            {
                Debug.Assert(((ArrayType)type).Rank > 1);
                sb.Append("{ sizeof(");
                sb.Append(GetCppSignatureTypeName(((ArrayType)type).ElementType)); // component size
                sb.Append("), 4 /* MTFlag_IsArray */, 3 * sizeof(void*) + "); // flags, baseSize
                sb.Append(((ArrayType)type).Rank.ToString());
                sb.Append("* sizeof(int32_t) * 2, ");
            }
            else
            {
                // sizeof(void*) == size of object header
                sb.Append("{ 0, 0, AlignBaseSize(sizeof(void*)+sizeof("); // component size, flags, baseSize
                sb.Append(GetCppTypeName(type));
                sb.Append(")), ");
            }

            // base type
            if (type.IsArray)
            {
                sb.Append(GetCppTypeName(((ArrayType)type).ElementType));
                sb.Append("::__getMethodTable()");
            }
            else
            {
                var baseType = type.BaseType;
                if (baseType != null)
                {
                    sb.Append(GetCppTypeName(type.BaseType));
                    sb.Append("::__getMethodTable()");
                }
                else
                {
                    sb.Append("NULL");
                }
            }
            sb.AppendLine("},");

            // virtual slots
            if (_compilation.GetRegisteredType(type).Constructed)
                AppendVirtualSlots(sb, type, type);

            sb.AppendLine("};");
            sb.AppendLine("return (MethodTable *)&mt.EEType;");
            sb.AppendLine("}");

            return sb.ToString();
        }

        void AddInstanceFields(TypeDesc type)
        {
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    _compilation.AddField(field);
                    var fieldType = field.FieldType;
                    if (fieldType.IsValueType && !fieldType.IsPrimitive)
                        AddInstanceFields(fieldType);
                }
            }
        }

        public void OutputCode()
        {
            foreach (var t in _compilation.RegisteredTypes.ToArray())
            {
                // Add all instance fields for valuetype types
                if (t.Type.IsValueType)
                    AddInstanceFields(t.Type);
            }

            // Write common prototypes and include information
            // for cross module references in a header
            GenerateCrossModuleReferenceHeader();

            // Generate the code for the sources files we have generated
            foreach (var t in _compilation.RegisteredTypes)
            {
                // Get the writer for the type
                TextWriter swOut = _compilation.GetOutWriterForType(t.Type);

                if (t.IncludedInCompilation)
                {
                    swOut.WriteLine(GetCodeForType(t.Type));
                }

                if (t.Methods != null)
                {
                    foreach (var m in t.Methods)
                    {
                        if (m.MethodCode != null)
                        {
                            swOut.WriteLine(m.MethodCode);
                        }
                    }
                }
            }

            if (_compilation.MainMethod != null)
            {
                var mainMethod = _compilation.MainMethod;

                // Stub for main method
                Out.WriteLine("int main(int argc, char * argv[]) { ");

                Out.WriteLine("if (__initialize_runtime() != 0) return -1;");
                Out.WriteLine("__register_module(&__module);");
                Out.WriteLine("ReversePInvokeFrame frame; __reverse_pinvoke(&frame);");
                Out.WriteLine();

                bool voidReturn = mainMethod.Signature.ReturnType.IsVoid;
                if (!voidReturn) Out.Write("int ret = ");
                Out.Write(GetCppTypeName(mainMethod.OwningType));
                Out.Write("::");
                Out.Write(GetCppMethodName(mainMethod));
                if (mainMethod.Signature.Length > 0)
                {
                    var stringType = mainMethod.Context.GetWellKnownType(WellKnownType.String);
                    var arrayOfStringType = stringType.Context.GetArrayType(stringType);
                    Out.WriteLine("((" + GetCppSignatureTypeName(arrayOfStringType) + ")__get_commandline_args(argc-1,argv+1));");
                }
                else
                    Out.WriteLine("();");
                Out.WriteLine();

                Out.WriteLine("__reverse_pinvoke_return(&frame);");
                Out.WriteLine("__shutdown_runtime();");

                if (voidReturn) Out.WriteLine(voidReturn ? "return 0;" : "return ret;");
                Out.WriteLine("}");
            }

            // Dispose all generated Streamwriters from compilation
            _compilation.DisposeCppOutWriters();
        }

        private void GenerateCrossModuleReferenceHeader()
        {
            // Form the path where the header will be generated.
            string pathHeader = Path.Combine(_compilation.CPPOutPath, "appdependency.h");
            TextWriter outHeader = new StreamWriter(File.Create(pathHeader));
            outHeader.WriteLine("// This is a compile-time generated file and any changes made to it will be lost after the next compilation.");
            outHeader.WriteLine();

            // Write out the basic prototypes
            OutputTypes(false, outHeader);
            outHeader.WriteLine();
            
            // Now write the detailed type definitions alongwith information about statics, GC statics, etc
            OutputTypes(true, outHeader);
            outHeader.WriteLine();

            outHeader.Dispose();
        }
    }
}