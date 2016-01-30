// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;

using ILCompiler.SymbolReader;
using ILCompiler.DependencyAnalysisFramework;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.Runtime;

using Internal.IL;
using ILCompiler.DependencyAnalysis;

namespace ILCompiler.CppCodeGen
{
    internal class CppWriter
    {
        private Compilation _compilation;

        private void SetWellKnownTypeSignatureName(WellKnownType wellKnownType, string mangledSignatureName)
        {
            var type = _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
            _cppSignatureNames.Add(type, mangledSignatureName);
        }

        public CppWriter(Compilation compilation)
        {
            _compilation = compilation;

            _out = new StreamWriter(File.Create(compilation.Options.OutputFilePath));

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

            BuildExternCSignatureMap();
        }

        private Dictionary<TypeDesc, string> _cppSignatureNames = new Dictionary<TypeDesc, string>();

        public string GetCppSignatureTypeName(TypeDesc type)
        {
            string mangledName;
            if (_cppSignatureNames.TryGetValue(type, out mangledName))
                return mangledName;

            // TODO: Use friendly names for enums
            if (type.IsEnum)
                mangledName = GetCppSignatureTypeName(type.UnderlyingType);
            else
                mangledName = GetCppTypeName(type);

            if (!type.IsValueType && !type.IsByRef && !type.IsPointer)
                mangledName += "*";

            _cppSignatureNames.Add(type, mangledName);

            return mangledName;
        }

        // extern "C" methods are sometimes referenced via different signatures.
        // _externCSignatureMap contains the canonical signature of the extern "C" import. References
        // via other signatures are required to use casts.
        private Dictionary<string, MethodSignature> _externCSignatureMap = new Dictionary<string, MethodSignature>();

        private void BuildExternCSignatureMap()
        {
            foreach (var nodeAlias in _compilation.NodeFactory.NodeAliases)
            {
                var methodNode = (CppMethodCodeNode)nodeAlias.Key;
                _externCSignatureMap.Add(nodeAlias.Value, methodNode.Method.Signature);
            }
        }

        public string GetCppMethodDeclaration(MethodDesc method, bool implementation, string externalMethodName = null, MethodSignature methodSignature = null)
        {
            StringBuilder sb = new StringBuilder();

            if (methodSignature == null)
                methodSignature = method.Signature;

            if (externalMethodName != null)
            {
                sb.Append("extern \"C\" ");
            }
            else
            {
                if (!implementation)
                {
                    sb.Append("static ");
                }
            }
            sb.Append(GetCppSignatureTypeName(methodSignature.ReturnType));
            sb.Append(" ");
            if (externalMethodName != null)
            {
                sb.Append(externalMethodName);
            }
            else
            {
                if (implementation)
                {
                    sb.Append(GetCppTypeName(method.OwningType));
                    sb.Append("::");
                }
                sb.Append(GetCppMethodName(method));
            }
            sb.Append("(");
            bool hasThis = !methodSignature.IsStatic;
            int argCount = methodSignature.Length;
            if (hasThis)
                argCount++;

            List<string> parameterNames = null;
            if (method != null)
            {
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

        private string CompileSpecialMethod(MethodDesc method, SpecialMethodKind kind)
        {
            StringBuilder builder = new StringBuilder();
            switch (kind)
            {
                case SpecialMethodKind.PInvoke:
                case SpecialMethodKind.RuntimeImport:
                    {
                        EcmaMethod ecmaMethod = method as EcmaMethod;

                        string importName = kind == SpecialMethodKind.PInvoke ?
                            method.GetPInvokeMethodMetadata().Name : ecmaMethod.GetAttributeStringValue("System.Runtime", "RuntimeImportAttribute");

                        if (importName == null)
                            importName = method.Name;

                        MethodSignature methodSignature = method.Signature;
                        bool slotCastRequired = false;

                        MethodSignature externCSignature;
                        if (_externCSignatureMap.TryGetValue(importName, out externCSignature))
                        {
                            slotCastRequired = !externCSignature.Equals(method.Signature);
                        }
                        else
                        {
                            _externCSignatureMap.Add(importName, methodSignature);
                            externCSignature = methodSignature;
                        }

                        builder.AppendLine(GetCppMethodDeclaration(method, true));
                        builder.AppendLine("{");

                        if (slotCastRequired)
                        {
                            AppendSlotTypeDef(builder, method);
                        }

                        if (!method.Signature.ReturnType.IsVoid)
                        {
                            builder.Append("return ");
                        }

                        if (slotCastRequired)
                            builder.Append("((__slot__" + GetCppMethodName(method) + ")");
                        builder.Append("::");
                        builder.Append(importName);
                        if (slotCastRequired)
                            builder.Append(")");

                        builder.Append("(");
                        builder.Append(GetCppMethodCallParamList(method));
                        builder.AppendLine(");");
                        builder.AppendLine("}");

                        return builder.ToString();
                    }

                default:
                    return GetCppMethodDeclaration(method, true) + " { throw 0xC000C000; }" + Environment.NewLine;
            }
        }

        public void CompileMethod(CppMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            _compilation.Log.WriteLine("Compiling " + method.ToString());

            SpecialMethodKind kind = method.DetectSpecialMethodKind();

            if (kind != SpecialMethodKind.Unknown)
            {
                string specialMethodCode = CompileSpecialMethod(method, kind);

                methodCodeNodeNeedingCode.SetCode(specialMethodCode, Array.Empty<Object>());
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

                IEnumerable<ILLocalVariable> localVariables = typeSystemContext.GetLocalVariableNamesForMethod(method);
                if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);

                IEnumerable<string> parameters = typeSystemContext.GetParameterNamesForMethod(method);
                if (parameters != null)
                    ilImporter.SetParameterNames(parameters);

                ilImporter.Compile(methodCodeNodeNeedingCode);
            }
            catch (Exception e)
            {
                _compilation.Log.WriteLine(e.Message + " (" + method + ")");

                methodCode = GetCppMethodDeclaration(method, true) + " { throw 0xC000C000; }" + Environment.NewLine;

                methodCodeNodeNeedingCode.SetCode(methodCode, Array.Empty<Object>());
            }
        }

        private TextWriter Out
        {
            get
            {
                return _out;
            }
        }

        private StreamWriter _out;

        private Dictionary<TypeDesc, List<MethodDesc>> _methodLists;

        private StringBuilder _statics;
        private StringBuilder _gcStatics;
        private StringBuilder _threadStatics;
        private StringBuilder _gcThreadStatics;

        // Base classes and valuetypes has to be emitted before they are used.
        private HashSet<TypeDesc> _emittedTypes;

        private TypeDesc GetFieldTypeOrPlaceholder(FieldDesc field)
        {
            try
            {
                return field.FieldType;
            }
            catch
            {
                // TODO: For now, catch errors due to missing dependencies
                return _compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Boolean);
            }
        }

        private void ExpandTypes()
        {
            _emittedTypes = new HashSet<TypeDesc>();
            foreach (var t in _cppSignatureNames.Keys.ToArray())
            {
                ExpandType(t);
            }
            _emittedTypes = null;
        }

        private void ExpandType(TypeDesc type)
        {
            if (_emittedTypes.Contains(type))
                return;
            _emittedTypes.Add(type);

            GetCppSignatureTypeName(type);
            var baseType = type.BaseType;
            if (baseType != null)
            {
                ExpandType(baseType);
            }

            foreach (var field in type.GetFields())
            {
                ExpandType(GetFieldTypeOrPlaceholder(field));
            }

            if (type.IsDelegate)
            {
                MethodDesc method = type.GetMethod("Invoke", null);

                var sig = method.Signature;
                ExpandType(sig.ReturnType);
                for (int i = 0; i < sig.Length; i++)
                    ExpandType(sig[i]);
            }

            if (type.IsArray)
            {
                ExpandType(((ArrayType)type).ElementType);
            }
        }

        private void OutputTypes(bool full)
        {
            if (full)
            {
                _statics = new StringBuilder();
                _gcStatics = new StringBuilder();
                _threadStatics = new StringBuilder();
                _gcThreadStatics = new StringBuilder();
            }

            _emittedTypes = new HashSet<TypeDesc>();
            foreach (var t in _cppSignatureNames.Keys)
            {
                if (t.IsByRef || t.IsPointer)
                    continue;

                // Base class types and valuetype instantance field types may be emitted out-of-order to make them 
                // appear before they are used.
                if (_emittedTypes.Contains(t))
                    continue;

                OutputType(t, full);
            }
            _emittedTypes = null;

            if (full)
            {
                Out.WriteLine();
                Out.WriteLine("struct {");
                Out.Write(_statics.ToString());
                Out.WriteLine("} __statics;");

                // TODO: Register GC statics with GC
                Out.WriteLine();
                Out.WriteLine("struct {");
                Out.Write(_gcStatics.ToString());
                Out.WriteLine("} __gcStatics;");

                Out.WriteLine();
                // @TODO_SDM: do for real - note: the 'extra' series are just testing the init syntax for 0-length arrays, they should be removed
                // TODO: preinitialized 0-length arrays are not supported in CLang
                Out.WriteLine("#ifdef _MSC_VER");
                Out.WriteLine("StaticGcDesc __gcStaticsDescs = { 1, { { sizeof(__gcStatics), 0 }, { 123, 456 }, { 789, 101112 } } };");
                Out.WriteLine("#else");
                Out.WriteLine("StaticGcDesc __gcStaticsDescs;");
                Out.WriteLine("#endif");

                Out.WriteLine();
                Out.WriteLine("SimpleModuleHeader __module = { &__gcStatics, &__gcStaticsDescs };");


                _statics = null;
                _gcStatics = null;
                _threadStatics = null;
                _gcThreadStatics = null;
            }
        }

        private void OutputType(TypeDesc t, bool full)
        {
            _emittedTypes.Add(t);

            if (full)
            {
                if (!t.IsValueType)
                {
                    var baseType = t.BaseType;
                    if (baseType != null)
                    {
                        if (!_emittedTypes.Contains(baseType))
                        {
                            OutputType(baseType, full);
                        }
                    }
                }

                foreach (var field in t.GetFields())
                {
                    var fieldType = GetFieldTypeOrPlaceholder(field);
                    if (fieldType.IsValueType && !fieldType.IsPrimitive && !field.IsStatic)
                    {
                        if (!_emittedTypes.Contains(fieldType))
                        {
                            OutputType(fieldType, full);
                        }
                    }
                }
            }

            string mangledName = GetCppTypeName(t);

            int nesting = 0;
            int current = 0;
            for (;;)
            {
                int sep = mangledName.IndexOf("::", current);
                if (sep < 0)
                    break;

                Out.Write("namespace " + mangledName.Substring(current, sep - current) + " { ");
                current = sep + 2;

                nesting++;
            }

            if (full)
            {
                Out.Write("class " + mangledName.Substring(current));
                if (!t.IsValueType)
                {
                    var baseType = t.BaseType;
                    if (baseType != null)
                    {
                        Out.Write(" : public " + GetCppTypeName(baseType));
                    }
                }
                Out.WriteLine(" { public:");

                // TODO: Enable once the dependencies are tracked for arrays
                // if (((DependencyNode)_compilation.NodeFactory.ConstructedTypeSymbol(t)).Marked)
                if (!t.IsPointer && !t.IsByRef)
                {
                    Out.WriteLine("static MethodTable * __getMethodTable();");
                }

                List<MethodDesc> virtualSlots;
                _compilation.NodeFactory.VirtualSlots.TryGetValue(t, out virtualSlots);
                if (virtualSlots != null)
                {
                    int baseSlots = 0;
                    var baseType = t.BaseType;
                    while (baseType != null)
                    {
                        List<MethodDesc> baseVirtualSlots;
                        _compilation.NodeFactory.VirtualSlots.TryGetValue(baseType, out baseVirtualSlots);
                        if (baseVirtualSlots != null)
                            baseSlots += baseVirtualSlots.Count;
                        baseType = baseType.BaseType;
                    }

                    for (int slot = 0; slot < virtualSlots.Count; slot++)
                    {
                        MethodDesc virtualMethod = virtualSlots[slot];
                        Out.WriteLine(GetCodeForVirtualMethod(virtualMethod, baseSlots + slot));
                    }
                }
                if (t.IsDelegate)
                {
                    Out.WriteLine(GetCodeForDelegate(t));
                }

                OutputTypeFields(t);

                if (t.HasStaticConstructor)
                {
                    _statics.AppendLine("bool __cctor_" + GetCppTypeName(t).Replace("::", "__") + ";");
                }

                List<MethodDesc> methodList;
                if (_methodLists.TryGetValue(t, out methodList))
                {
                    foreach (var m in methodList)
                    {
                        OutputMethod(m);
                    }
                }
                Out.Write("};");
            }
            else
            {
                Out.Write("class " + mangledName.Substring(current) + ";");
            }

            while (nesting > 0)
            {
                Out.Write(" };");
                nesting--;
            }
            Out.WriteLine();
        }

        private void OutputTypeFields(TypeDesc t)
        {
            bool explicitLayout = false;
            ClassLayoutMetadata classLayoutMetadata = default(ClassLayoutMetadata);

            if (t.IsValueType)
            {
                MetadataType metadataType = (MetadataType)t;
                if (metadataType.IsExplicitLayout)
                {
                    explicitLayout = true;
                    classLayoutMetadata = metadataType.GetClassLayout();
                }
            }

            int instanceFieldIndex = 0;

            if (explicitLayout)
                Out.WriteLine("union {");

            foreach (var field in t.GetFields())
            {
                if (field.IsStatic)
                {
                    if (field.IsLiteral)
                        continue;

                    TypeDesc fieldType = GetFieldTypeOrPlaceholder(field);
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
                    if (explicitLayout)
                    {
                        Out.WriteLine("struct {");
                        int offset = classLayoutMetadata.Offsets[instanceFieldIndex].Offset;
                        if (offset > 0)
                            Out.WriteLine("char __pad" + instanceFieldIndex + "[" + offset + "];");
                    }
                    Out.WriteLine(GetCppSignatureTypeName(GetFieldTypeOrPlaceholder(field)) + " " + GetCppFieldName(field) + ";");
                    if (explicitLayout)
                    {
                        Out.WriteLine("};");
                    }
                    instanceFieldIndex++;
                }
            }

            if (explicitLayout)
                Out.WriteLine("};");
        }

        private void OutputMethod(MethodDesc m)
        {
            Out.WriteLine(GetCppMethodDeclaration(m, false));
        }

        private void AppendSlotTypeDef(StringBuilder sb, MethodDesc method)
        {
            MethodSignature methodSignature = method.Signature;

            TypeDesc thisArgument = null;
            if (!methodSignature.IsStatic)
                thisArgument = method.OwningType;

            AppendSignatureTypeDef(sb, "__slot__" + GetCppMethodName(method), methodSignature, thisArgument);
        }

        internal void AppendSignatureTypeDef(StringBuilder sb, string name, MethodSignature methodSignature, TypeDesc thisArgument)
        {
            sb.Append("typedef ");
            sb.Append(GetCppSignatureTypeName(methodSignature.ReturnType));
            sb.Append("(*");
            sb.Append(name);
            sb.Append(")(");

            int argCount = methodSignature.Length;
            if (thisArgument != null)
                argCount++;
            for (int i = 0; i < argCount; i++)
            {
                if (thisArgument != null)
                {
                    if (i == 0)
                    {
                        sb.Append(GetCppSignatureTypeName(thisArgument));
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


        private String GetCodeForDelegate(TypeDesc delegateType)
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

        private String GetCodeForVirtualMethod(MethodDesc method, int slot)
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

        private void AppendVirtualSlots(StringBuilder sb, TypeDesc implType, TypeDesc declType)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                AppendVirtualSlots(sb, implType, baseType);

            List<MethodDesc> virtualSlots;
            _compilation.NodeFactory.VirtualSlots.TryGetValue(declType, out virtualSlots);
            if (virtualSlots != null)
            {
                for (int i = 0; i < virtualSlots.Count; i++)
                {
                    MethodDesc declMethod = virtualSlots[i];
                    MethodDesc implMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestMetadataType());

                    if (implMethod.IsAbstract)
                    {
                        sb.Append("NULL,");
                    }
                    else
                    {
                        sb.Append("(void*)&");
                        sb.Append(GetCppTypeName(implMethod.OwningType));
                        sb.Append("::");
                        sb.Append(GetCppMethodName(implMethod));
                        sb.Append(",");
                    }
                }
            }
        }

        private String GetCodeForType(TypeDesc type)
        {
            StringBuilder sb = new StringBuilder();

            int totalSlots = 0;

            TypeDesc t = type;
            while (t != null)
            {
                List<MethodDesc> virtualSlots;
                _compilation.NodeFactory.VirtualSlots.TryGetValue(t, out virtualSlots);
                if (virtualSlots != null)
                    totalSlots += virtualSlots.Count;
                t = t.BaseType;
            }

            UInt16 flags = 0;
            try
            {
                flags = EETypeBuilderHelpers.ComputeFlags(type);
            }
            catch
            {
                // TODO: Handling of missing dependencies
                flags = 0;
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
                sb.Append("{ sizeof(uint16_t), 0x");                            // EEType::_usComponentSize
                sb.Append(flags.ToString("x4", CultureInfo.InvariantCulture));  // EEType::_usFlags
                sb.Append(", 2 * sizeof(void*) + sizeof(int32_t) + 2, ");       // EEType::_uBaseSize
            }
            else
            if (type.IsArray && ((ArrayType)type).Rank == 1)
            {
                sb.Append("{ sizeof(");
                sb.Append(GetCppSignatureTypeName(((ArrayType)type).ElementType)); // EEType::_usComponentSize
                sb.Append("), 0x");
                sb.Append(flags.ToString("x4", CultureInfo.InvariantCulture));  // EEType::_usFlags
                sb.Append(", 3 * sizeof(void*), "); // EEType::_uBaseSize
            }
            else
            if (type.IsArray)
            {
                Debug.Assert(((ArrayType)type).Rank > 1);
                sb.Append("{ sizeof(");
                sb.Append(GetCppSignatureTypeName(((ArrayType)type).ElementType)); // EEType::_usComponentSize
                sb.Append("), 0x");
                sb.Append(flags.ToString("x4", CultureInfo.InvariantCulture));  // EEType::_usFlags
                sb.Append(", 3 * sizeof(void*) + ");                            // EEType::_uBaseSize
                sb.Append(((ArrayType)type).Rank.ToString());
                sb.Append("* sizeof(int32_t) * 2, ");
            }
            else
            {
                // sizeof(void*) == size of object header
                sb.Append("{ 0, 0x");                                           // EEType::_usComponentSize
                sb.Append(flags.ToString("x", CultureInfo.InvariantCulture));   // EEType::_usFlags
                sb.Append(", AlignBaseSize(sizeof(void*)+sizeof(");             // EEType::_uBaseSize
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
            if (((DependencyNode)_compilation.NodeFactory.ConstructedTypeSymbol(type)).Marked)
                AppendVirtualSlots(sb, type, type);

            sb.AppendLine("};");
            sb.AppendLine("return (MethodTable *)&mt.EEType;");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void BuildMethodLists(IEnumerable<DependencyNode> nodes)
        {
            _methodLists = new Dictionary<TypeDesc, List<MethodDesc>>();
            foreach (var node in nodes)
            {
                if (node is CppMethodCodeNode)
                {
                    CppMethodCodeNode methodCodeNode = (CppMethodCodeNode)node;

                    var method = methodCodeNode.Method;
                    var type = method.OwningType;

                    List<MethodDesc> methodList;
                    if (!_methodLists.TryGetValue(type, out methodList))
                    {
                        GetCppSignatureTypeName(type);

                        methodList = new List<MethodDesc>();
                        _methodLists.Add(type, methodList);
                    }

                    methodList.Add(method);
                }
                else
                if (node is EETypeNode)
                {
                    GetCppSignatureTypeName(((EETypeNode)node).Type);
                }
            }
        }

        public void OutputCode(IEnumerable<DependencyNode> nodes)
        {
            BuildMethodLists(nodes);

            ExpandTypes();

            Out.WriteLine("#include \"common.h\"");
            Out.WriteLine();

            OutputTypes(false);
            Out.WriteLine();
            OutputTypes(true);
            Out.WriteLine();

            foreach (var externC in _externCSignatureMap)
            {
                string importName = externC.Key;
                // TODO: hacky special-case
                if (importName != "memmove" && importName != "malloc") // some methods are already declared by the CRT headers
                {
                    Out.WriteLine(GetCppMethodDeclaration(null, false, importName, externC.Value));
                }
            }

            foreach (var t in _cppSignatureNames.Keys)
            {
                // TODO: Enable once the dependencies are tracked for arrays
                // if (((DependencyNode)_compilation.NodeFactory.ConstructedTypeSymbol(t)).Marked)
                if (!t.IsPointer && !t.IsByRef)
                {
                    Out.WriteLine(GetCodeForType(t));
                }

                List<MethodDesc> methodList;
                if (_methodLists.TryGetValue(t, out methodList))
                {
                    foreach (var m in methodList)
                    {
                        var methodCodeNode = (CppMethodCodeNode)_compilation.NodeFactory.MethodEntrypoint(m);
                        Out.WriteLine(methodCodeNode.CppCode);

                        var alternateName = _compilation.NodeFactory.GetSymbolAlternateName(methodCodeNode);
                        if (alternateName != null)
                        {
                            Out.WriteLine(GetCppMethodDeclaration(m, true, alternateName));
                            Out.WriteLine("{");
                            Out.Write("    ");
                            if (!m.Signature.ReturnType.IsVoid)
                            {
                                Out.Write("return ");
                            }
                            Out.Write(GetCppTypeName(m.OwningType));
                            Out.Write("::");
                            Out.Write(GetCppMethodName(m));
                            Out.Write("(");
                            Out.Write(GetCppMethodCallParamList(m));
                            Out.WriteLine(");");
                            Out.WriteLine("}");
                        }
                    }
                }
            }

            if (_compilation.StartupCodeMain != null)
            {
                var startupCodeMain = _compilation.StartupCodeMain;

                // Stub for main method
                if (_compilation.TypeSystemContext.Target.OperatingSystem == TargetOS.Windows)
                {
                    Out.WriteLine("int wmain(int argc, wchar_t * argv[]) { ");
                }
                else
                {
                    Out.WriteLine("int main(int argc, char * argv[]) { ");
                }


                Out.WriteLine("if (__initialize_runtime() != 0) return -1;");
                Out.WriteLine("__register_module(&__module);");
                Out.WriteLine("ReversePInvokeFrame frame; __reverse_pinvoke(&frame);");
                Out.WriteLine();

                Out.Write("int ret = ");
                Out.Write(GetCppTypeName(startupCodeMain.OwningType));
                Out.Write("::");
                Out.Write(GetCppMethodName(startupCodeMain));
                Out.WriteLine("(argc-1,(intptr_t)(argv+1));");
                Out.WriteLine();

                Out.WriteLine("__reverse_pinvoke_return(&frame);");
                Out.WriteLine("__shutdown_runtime();");

                Out.WriteLine("return ret;");
                Out.WriteLine("}");
            }

            Out.Dispose();
        }
    }
}
