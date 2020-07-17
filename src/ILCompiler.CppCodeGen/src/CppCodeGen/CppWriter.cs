// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ILCompiler.Compiler.CppCodeGen;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.CppCodeGen
{
    internal class CppWriter
    {
        private CppCodegenCompilation _compilation;

        private void SetWellKnownTypeSignatureName(WellKnownType wellKnownType, string mangledSignatureName)
        {
            var type = _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
            var typeNode = _compilation.NodeFactory.ConstructedTypeSymbol(type);

            _cppSignatureNames.Add(type, mangledSignatureName);
        }

        public CppWriter(CppCodegenCompilation compilation, string outputFilePath)
        {
            _compilation = compilation;

            _out = new StreamWriter(new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, false), Encoding.UTF8);


            // Unify this list with the one in CppCodegenNodeFactory
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

        // Mangled type names referenced by the generated code
        private Dictionary<TypeDesc, string> _mangledNames = new Dictionary<TypeDesc, string>();

        private string GetMangledTypeName(TypeDesc type)
        {
            string mangledName;
            if (_mangledNames.TryGetValue(type, out mangledName))
                return mangledName;

            mangledName = _compilation.NameMangler.GetMangledTypeName(type);

            _mangledNames.Add(type, mangledName);

            return mangledName;
        }

        private Dictionary<TypeDesc, string> _cppSignatureNames = new Dictionary<TypeDesc, string>();

        public string GetCppSignatureTypeName(TypeDesc type)
        {
            type = ConvertToCanonFormIfNecessary(type, CanonicalFormKind.Specific);

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
                if (nodeAlias.Key is CppMethodCodeNode methodNode)
                {
                    _externCSignatureMap.Add(nodeAlias.Value, methodNode.Method.Signature);
                }
            }
        }

        private IList<string> GetParameterNamesForMethod(MethodDesc method)
        {
            // TODO: The uses of this method need revision. The right way to get to this info is from
            //       a MethodIL. For declarations, we don't need names.

            method = method.GetTypicalMethodDefinition();
            var ecmaMethod = method as EcmaMethod;
            if (ecmaMethod != null && ecmaMethod.Module.PdbReader != null)
            {
                List<string> parameterNames = new List<string>(new EcmaMethodDebugInformation(ecmaMethod).GetParameterNames());

                // Return the parameter names only if they match the method signature
                if (parameterNames.Count != 0)
                {
                    var methodSignature = method.Signature;
                    int argCount = methodSignature.Length;
                    if (!methodSignature.IsStatic)
                        argCount++;

                    if (parameterNames.Count == argCount)
                        return parameterNames;
                }
            }

            return null;
        }

        public string GetCppHiddenParam()
        {
            return "___hidden";
        }

        public void AppendCppMethodDeclaration(CppGenerationBuffer sb, MethodDesc method, bool implementation, string externalMethodName = null,
            MethodSignature methodSignature = null, string cppMethodName = null, bool isUnboxingStub = false)
        {
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
                    sb.Append(GetCppMethodDeclarationName(method.OwningType, cppMethodName ?? GetCppMethodName(method)));
                }
                else
                {
                    sb.Append(cppMethodName ?? GetCppMethodName(method));
                }
            }
            sb.Append("(");

            bool hasThis = !methodSignature.IsStatic;
            bool hasHiddenParam = false;
            int argCount = methodSignature.Length;

            int hiddenArgIdx = -1;
            int thisArgIdx = -1;
            int signatureArgOffset = 0;
            int paramListOffset = 0;

            if (hasThis)
            {
                argCount++;
                thisArgIdx++;
                signatureArgOffset++;
            }

            IList<string> parameterNames = null;
            if (method != null)
            {
                if (isUnboxingStub)
                    hasHiddenParam = method.IsSharedByGenericInstantiations &&
                        (method.HasInstantiation || method.Signature.IsStatic);
                else
                    hasHiddenParam = method.RequiresInstArg();
                parameterNames = GetParameterNamesForMethod(method);
            }

            if (hasHiddenParam)
            {
                argCount++;
                hiddenArgIdx++;
                if (hasThis)
                    hiddenArgIdx++;
                signatureArgOffset++;
                paramListOffset++;
            }

            for (int i = 0; i < argCount; i++)
            {
                if (i == thisArgIdx)
                {
                    var thisType = method.OwningType;
                    if (thisType.IsValueType)
                        thisType = thisType.MakeByRefType();
                    sb.Append(GetCppSignatureTypeName(thisType));
                }
                else if (i == hiddenArgIdx)
                {
                    sb.Append("void*");
                }
                else
                {
                    sb.Append(GetCppSignatureTypeName(methodSignature[i - signatureArgOffset]));
                }

                if (implementation)
                {
                    sb.Append(" ");

                    if (i == hiddenArgIdx)
                    {
                        sb.Append(" ");
                        sb.Append(GetCppHiddenParam());
                    }
                    else
                    {
                        int idx;
                        if (i == thisArgIdx)
                            idx = i;
                        else
                            idx = i - paramListOffset;

                        if (parameterNames != null)
                        {
                            sb.Append(SanitizeCppVarName(parameterNames[idx]));
                        }
                        else
                        {
                            sb.Append("_a");
                            sb.Append(idx.ToStringInvariant());
                        }
                    }
                }
                if (i != argCount - 1)
                    sb.Append(", ");
            }

            sb.Append(")");
            if (!implementation)
                sb.Append(";");
        }

        public void AppendCppMethodCallParamList(CppGenerationBuffer sb, MethodDesc method, bool unbox = false)
        {
            var methodSignature = method.Signature;

            bool hasThis = !methodSignature.IsStatic;
            bool hasHiddenParam = method.RequiresInstArg();
            int argCount = methodSignature.Length;

            int hiddenArgIdx = -1;
            int thisArgIdx = -1;
            int paramListOffset = 0;

            if (hasThis)
            {
                argCount++;
                thisArgIdx++;
            }

            IList<string> parameterNames = GetParameterNamesForMethod(method);

            if (hasHiddenParam)
            {
                argCount++;
                hiddenArgIdx++;
                if (hasThis)
                    hiddenArgIdx++;
                paramListOffset++;
            }

            for (int i = 0; i < argCount; i++)
            {
                if (i == thisArgIdx && unbox)
                {
                    // Unboxing stubs only valid for non-static methods on value types
                    System.Diagnostics.Debug.Assert(hasThis);
                    System.Diagnostics.Debug.Assert(method.OwningType.IsValueType);

                    var thisType = method.OwningType.MakeByRefType();

                    sb.Append("(");
                    sb.Append(GetCppSignatureTypeName(thisType));
                    sb.Append(")((uint8_t*)(");

                    if (parameterNames != null)
                    {
                        sb.Append(SanitizeCppVarName(parameterNames[i]));
                    }
                    else
                    {
                        sb.Append("_a");
                        sb.Append(i.ToStringInvariant());
                    }

                    sb.Append(")+sizeof(void*))");
                }
                else if (i == hiddenArgIdx)
                {
                    bool unboxStubHasHiddenArg = method.IsSharedByGenericInstantiations &&
                            (method.HasInstantiation || method.Signature.IsStatic);

                    if (unbox && !unboxStubHasHiddenArg)
                    {
                        sb.Append("*(void**)");
                        if (parameterNames != null)
                            sb.Append(SanitizeCppVarName(parameterNames[thisArgIdx]));
                        else
                            sb.Append("_a" + thisArgIdx);
                    }
                    else
                    {
                        sb.Append(GetCppHiddenParam());
                    }
                }
                else
                {
                    int idx = i - paramListOffset;

                    if (parameterNames != null)
                    {
                        sb.Append(SanitizeCppVarName(parameterNames[idx]));
                    }
                    else
                    {
                        sb.Append("_a");
                        sb.Append(idx.ToStringInvariant());
                    }
                }

                if (i != argCount - 1)
                    sb.Append(", ");
            }
        }

        public string GetCppTypeName(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.ByRef:
                case TypeFlags.Pointer:
                    return GetCppSignatureTypeName(((ParameterizedType)type).ParameterType) + "*";
                default:
                    return GetMangledTypeName(type);
            }
        }

        /// <summary>
        /// Compute a proper declaration for <param name="methodName"/> defined in <param name="owningType"/>.
        /// Usually the C++ name for a type is prefixed by "::" but this is not a valid way to declare a method,
        /// so we need to strip it if present.
        /// </summary>
        /// <param name="owningType">Type where <param name="methodName"/> belongs.</param>
        /// <param name="methodName">Name of method from <param name="owningType"/>.</param>
        /// <returns>C++ declaration name for <param name="methodName"/>.</returns>
        public string GetCppMethodDeclarationName(TypeDesc owningType, string methodName, bool isDeclaration = true)
        {
            var s = GetMangledTypeName(owningType);
            if (isDeclaration && s.StartsWith("::"))
            {
                // For a Method declaration we do not need the starting ::
                s = s.Substring(2, s.Length - 2);
            }
            return string.Concat(s, "::", methodName);
        }

        public string GetCppMethodName(MethodDesc method)
        {
            return _compilation.NameMangler.GetMangledMethodName(method).ToString();
        }

        public string GetCppReadyToRunGenericHelperNodeName(NodeFactory factory, ReadyToRunGenericHelperNode node)
        {
            if (node.DictionaryOwner is MethodDesc)
            {
                Utf8StringBuilder sb = new Utf8StringBuilder();
                MethodDesc method = (MethodDesc)node.DictionaryOwner;

                if (node is ReadyToRunGenericLookupFromDictionaryNode)
                    sb.Append("__GenericLookupFromDict_");
                else
                    sb.Append("__GenericLookupFromType_");

                sb.Append(factory.NameMangler.GetMangledTypeName(method.OwningType));
                sb.Append("_");
                sb.Append(factory.NameMangler.GetMangledMethodName(method));
                sb.Append("_");

                if (node.Id != ReadyToRunHelperId.DelegateCtor)
                    node.LookupSignature.AppendMangledName(factory.NameMangler, sb);
                else
                    ((DelegateCreationInfo)node.Target).AppendMangledName(factory.NameMangler, sb);

                return sb.ToString().Replace("::", "_");
            }

            return node.GetMangledName(factory.NameMangler).Replace("::", "_");
        }

        public string GetCppRuntimeMethodHandleName(RuntimeMethodHandleNode node)
        {
            MethodDesc method = node.Method;
            StringBuilder sb = new StringBuilder();

            sb.Append("__RuntimeMethodHandle_");
            sb.Append(GetMangledTypeName(method.OwningType));
            sb.Append("_");
            sb.Append(GetCppMethodName(method));

            return sb.ToString().Replace("::", "_");
        }

        private string GetCppNativeLayoutSignatureName(NodeFactory factory, NativeLayoutSignatureNode node)
        {
            if (node.Identity is MethodDesc)
            {
                MethodDesc method = node.Identity as MethodDesc;
                StringBuilder sb = new StringBuilder();

                sb.Append("__RMHSignature_");
                sb.Append(GetMangledTypeName(method.OwningType));
                sb.Append("_");
                sb.Append(GetCppMethodName(method));

                return sb.ToString().Replace("::", "_");
            }

            return node.GetMangledName(factory.NameMangler).Replace("::", "_");;
        }

        private string GetCppFatFunctionPointerNameForMethod(MethodDesc method,
            bool isUnboxingStub = false)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(isUnboxingStub ? "__fatunboxpointer_" : "__fatpointer_");
            sb.Append(GetMangledTypeName(method.OwningType));
            sb.Append("_");
            sb.Append(GetCppMethodName(method));

            return sb.ToString().Replace("::", "_");
        }

        private string GetCppFatFunctionPointerName(FatFunctionPointerNode node)
        {
            return GetCppFatFunctionPointerNameForMethod(node.Method, node.IsUnboxingStub);
        }

        public string GetCppSymbolNodeName(NodeFactory factory, ISymbolNode node)
        {
            if (node is RuntimeMethodHandleNode r)
            {
                return GetCppRuntimeMethodHandleName(r);
            }
            else if (node is NativeLayoutSignatureNode n)
            {
                return GetCppNativeLayoutSignatureName(factory, n);
            }
            else if (node is FatFunctionPointerNode f)
            {
                return GetCppFatFunctionPointerName(f);
            }
            else
            {
                return factory.GetSymbolAlternateName(node) ?? node.GetMangledName(factory.NameMangler);
            }
        }

        public string GetCppFieldName(FieldDesc field)
        {
            string name = _compilation.NameMangler.GetMangledFieldName(field).ToString();

            // TODO: name mangling robustness
            if (name == "register")
                name = "_" + name + "_";

            return name;
        }

        public string GetCppStaticsName(TypeDesc type, bool isGCStatic = false, bool isThreadStatic = false, bool dataNameNeeded = false)
        {
            string name;
            if (isThreadStatic)
            {
                name = "threadStatics";
            }
            else if (isGCStatic)
            {
                name = "gcStatics";
            }
            else
            {
                name = "statics";
            }

            string typeName = GetCppTypeName(type);
            string res = typeName.Replace("::", "_") + "_" + name;

            if (isGCStatic && !isThreadStatic && dataNameNeeded)
                res += "__data";

            return res;
        }

        public string GetCppStaticsTypeName(TypeDesc type, bool isGCStatic = false, bool isThreadStatic = false)
        {
            string name;
            if (isThreadStatic)
            {
                name = "ThreadStatics";
            }
            else if (isGCStatic)
            {
                name = "GCStatics";
            }
            else
            {
                name = "Statics";
            }

            string typeName = GetCppTypeName(type);
            return typeName.Replace("::", "_") + "_" + name;
        }

        public string SanitizeCppVarName(string varName)
        {
            // TODO: name mangling robustness
            if (varName == "errno" || varName == "environ" || varName == "template" || varName == "typename" || varName == "register") // some names collide with CRT headers
                return "_" + varName + "_";

            return _compilation.NameMangler.SanitizeName(varName);
        }

        private void CompileExternMethod(CppMethodCodeNode methodCodeNodeNeedingCode, string importName)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;
            MethodSignature methodSignature = method.Signature;

            bool slotCastRequired = false;

            MethodSignature externCSignature;
            if (_externCSignatureMap.TryGetValue(importName, out externCSignature))
            {
                slotCastRequired = !externCSignature.Equals(methodSignature);
            }
            else
            {
                _externCSignatureMap.Add(importName, methodSignature);
                externCSignature = methodSignature;
            }

            var sb = new CppGenerationBuffer();

            sb.AppendLine();
            AppendCppMethodDeclaration(sb, method, true);
            sb.AppendLine();

            // TODO: workaround unreachable globalization methods
            string moduleName = method.GetPInvokeMethodMetadata().Module;
            if (moduleName == (_compilation.TypeSystemContext.Target.IsWindows ? "libSystem.Globalization.Native" : "kernel32.dll"))
            {
                sb.Append("{ throw 0; }");
                methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
                return;
            }

            sb.Append("{");
            sb.Indent();

            if (slotCastRequired)
            {
                AppendSlotTypeDef(sb, method);
            }

            sb.AppendLine();
            if (!method.Signature.ReturnType.IsVoid)
            {
                sb.Append("return ");
            }

            if (slotCastRequired)
                sb.Append("((__slot__" + GetCppMethodName(method) + ")");
            sb.Append("::");
            sb.Append(importName);
            if (slotCastRequired)
                sb.Append(")");

            sb.Append("(");
            AppendCppMethodCallParamList(sb, method);
            sb.Append(");");
            sb.Exdent();
            sb.AppendLine();
            sb.Append("}");

            methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
        }

        public void CompileMethod(CppMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            if (_compilation.Logger.IsVerbose)
            {
                string methodName = method.ToString();
                _compilation.Logger.Writer.WriteLine("Compiling " + methodName);
            }

            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                CompileExternMethod(methodCodeNodeNeedingCode, ((EcmaMethod)method).GetRuntimeImportName());
                return;
            }

            if (method.IsRawPInvoke())
            {
                CompileExternMethod(methodCodeNodeNeedingCode, method.GetPInvokeMethodMetadata().Name ?? method.Name);
                return;
            }

            var methodIL = _compilation.GetMethodIL(method);
            if (methodIL == null)
                return;

            if (HardwareIntrinsicHelpers.IsHardwareIntrinsic(method))
                methodIL = HardwareIntrinsicHelpers.GetUnsupportedImplementationIL(method);

            try
            {
                var ilImporter = new ILImporter(_compilation, this, method, methodIL);

                CompilerTypeSystemContext typeSystemContext = _compilation.TypeSystemContext;

                MethodDebugInformation debugInfo = _compilation.GetDebugInfo(methodIL);

                if (!_compilation.Options.HasOption(CppCodegenConfigProvider.NoLineNumbersString))
                {
                    IEnumerable<ILSequencePoint> sequencePoints = debugInfo.GetSequencePoints();
                    if (sequencePoints != null)
                        ilImporter.SetSequencePoints(sequencePoints);
                }

                IEnumerable<ILLocalVariable> localVariables = debugInfo.GetLocalVariables();
                if (localVariables != null)
                    ilImporter.SetLocalVariables(localVariables);

                IEnumerable<string> parameters = GetParameterNamesForMethod(method);
                if (parameters != null)
                    ilImporter.SetParameterNames(parameters);

                ilImporter.Compile(methodCodeNodeNeedingCode);
            }
            catch (Exception e)
            {
                _compilation.Logger.Writer.WriteLine(e.Message + " (" + method + ")");

                var sb = new CppGenerationBuffer();
                sb.AppendLine();
                AppendCppMethodDeclaration(sb, method, true);
                sb.AppendLine();
                sb.Append("{");
                sb.Indent();
                sb.AppendLine();
                sb.Append("throw 0xC000C000;");
                sb.Exdent();
                sb.AppendLine();
                sb.Append("}");

                methodCodeNodeNeedingCode.SetCode(sb.ToString(), Array.Empty<Object>());
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
        private HashSet<TypeDesc> _typesWithCctor;

        private Dictionary<TypeDesc, CppGenerationBuffer> _statics;
        private Dictionary<TypeDesc, CppGenerationBuffer> _gcStatics;
        private Dictionary<TypeDesc, CppGenerationBuffer> _threadStatics;

        private Dictionary<Tuple<string, MethodSignature>, int> _methodNameAndSignatures =
            new Dictionary<Tuple<string, MethodSignature>, int>();

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

        private void OutputTypeFields(CppGenerationBuffer sb, TypeDesc t)
        {
            bool explicitLayout = false;
            bool hasSize = false;
            ClassLayoutMetadata classLayoutMetadata = default(ClassLayoutMetadata);

            if (t.IsValueType)
            {
                MetadataType metadataType = (MetadataType)t;
                classLayoutMetadata = metadataType.GetClassLayout();
                hasSize = classLayoutMetadata.Size > 0;
                if (metadataType.IsExplicitLayout)
                {
                    explicitLayout = true;
                }
            }

            int instanceFieldIndex = 0;

            if (explicitLayout || hasSize)
            {
                sb.AppendLine();
                sb.Append("union {");
                sb.Indent();
                if (!explicitLayout)
                {
                    sb.Append("struct {");
                    sb.Indent();
                }
            }

            foreach (var field in t.GetFields())
            {
                if (field.IsStatic)
                {
                    if (field.IsLiteral)
                        continue;

                    TypeDesc fieldType = GetFieldTypeOrPlaceholder(field);
                    CppGenerationBuffer builder;

                    if (field.IsThreadStatic)
                    {
                        if (!_threadStatics.TryGetValue(t, out builder))
                        {
                            builder = new CppGenerationBuffer();
                            builder.Indent();
                            _threadStatics[t] = builder;
                        }
                    }
                    else if (field.HasGCStaticBase)
                    {
                        if (!_gcStatics.TryGetValue(t, out builder))
                        {
                            builder = new CppGenerationBuffer();
                            builder.Indent();
                            _gcStatics[t] = builder;
                        }
                    }
                    else
                    {
                        if (!_statics.TryGetValue(t, out builder))
                        {
                            // TODO: Valuetype statics with GC references
                            builder = new CppGenerationBuffer();
                            builder.Indent();
                            _statics[t] = builder;
                        }
                    }
                    builder.AppendLine();
                    builder.Append(GetCppSignatureTypeName(fieldType));
                    builder.Append(" ");
                    builder.Append(GetCppFieldName(field) + ";");
                }
                else
                {
                    if (explicitLayout)
                    {
                        sb.AppendLine();
                        sb.Append("struct {");
                        sb.Indent();
                        int offset = classLayoutMetadata.Offsets[instanceFieldIndex].Offset.AsInt;
                        if (offset > 0)
                        {
                            sb.AppendLine();
                            sb.Append("char __pad" + instanceFieldIndex + "[" + offset + "];");
                        }
                    }
                    sb.AppendLine();
                    sb.Append(GetCppSignatureTypeName(GetFieldTypeOrPlaceholder(field)) + " " + GetCppFieldName(field) + ";");
                    if (explicitLayout)
                    {
                        sb.Exdent();
                        sb.AppendLine();
                        sb.Append("};");
                    }
                    instanceFieldIndex++;
                }
            }

            if (explicitLayout || hasSize)
            {
                if (!explicitLayout)
                {
                    sb.Exdent();
                    sb.AppendLine();
                    sb.Append("};");
                }

                if (classLayoutMetadata.Size > 0)
                {
                    sb.AppendLine();
                    sb.Append("struct { char __sizePadding[" + classLayoutMetadata.Size + "]; };");
                }

                sb.Exdent();
                sb.AppendLine();
                sb.Append("};");
            }
        }

        private void AppendSlotTypeDef(CppGenerationBuffer sb, MethodDesc method)
        {
            MethodSignature methodSignature = method.Signature;

            TypeDesc thisArgument = null;
            if (!methodSignature.IsStatic)
                thisArgument = method.OwningType;

            AppendSignatureTypeDef(sb, "__slot__" + GetCppMethodName(method), methodSignature, thisArgument);
        }

        internal void AppendSignatureTypeDef(CppGenerationBuffer sb, string name, MethodSignature methodSignature, TypeDesc thisArgument, bool needsHiddenArg = false)
        {
            sb.AppendLine();
            sb.Append("typedef ");
            sb.Append(GetCppSignatureTypeName(methodSignature.ReturnType));
            sb.Append("(*");
            sb.Append(name);
            sb.Append(")(");

            int argCount = methodSignature.Length;

            int hiddenArgIdx = -1;
            int thisArgIdx = -1;
            int signatureArgOffset = 0;

            if (thisArgument != null)
            {
                argCount++;
                signatureArgOffset++;
                thisArgIdx++;
            }

            if (needsHiddenArg)
            {
                argCount++;
                hiddenArgIdx++;
                if (thisArgument != null)
                    hiddenArgIdx++;
                signatureArgOffset++;
            }

            for (int i = 0; i < argCount; i++)
            {
                if (i == thisArgIdx)
                {
                    sb.Append(GetCppSignatureTypeName(thisArgument));
                }
                else if (i == hiddenArgIdx)
                {
                    sb.Append("void*");
                }
                else
                {
                    sb.Append(GetCppSignatureTypeName(methodSignature[i - signatureArgOffset]));
                }

                if (i != argCount - 1)
                    sb.Append(", ");
            }
            sb.Append(");");
        }

        private String GetCodeForDelegate(TypeDesc delegateType, bool generateTypeDef)
        {
            var sb = new CppGenerationBuffer();

            MethodDesc method = delegateType.GetKnownMethod("Invoke", null);

            if (generateTypeDef)
            {
                AppendSlotTypeDef(sb, method);
            }

            sb.AppendLine();
            sb.Append("static __slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(" __invoke__");
            sb.Append(GetCppMethodName(method));
            sb.Append("(void * pThis)");
            sb.AppendLine();
            sb.Append("{");
            sb.Indent();
            sb.AppendLine();
            sb.Append("return (__slot__");
            sb.Append(GetCppMethodName(method));
            sb.Append(")(((");
            sb.Append(GetCppSignatureTypeName(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.MulticastDelegate)));
            sb.Append(")pThis)->m_functionPointer);");
            sb.Exdent();
            sb.AppendLine();
            sb.Append("};");

            return sb.ToString();
        }

        private String GetCodeForVirtualMethod(MethodDesc method, int slot)
        {
            var sb = new CppGenerationBuffer();
            sb.Indent();

            if (method.OwningType.IsInterface)
            {

                AppendSlotTypeDef(sb, method);
                sb.Indent();
                sb.AppendLine();
                sb.Append("static uint16_t");
                sb.Append(" __getslot__");
                sb.Append(GetCppMethodName(method));
                sb.Append("(void * pThis)");
                sb.AppendLine();
                sb.Append("{");
                sb.Indent();
                sb.AppendLine();
                sb.Append("return ");
                sb.Append(slot);
                sb.Append(";");
                sb.AppendLine();
            }
            else
            {
                AppendSlotTypeDef(sb, method);
                sb.Indent();
                sb.AppendLine();
                sb.Append("static __slot__");
                sb.Append(GetCppMethodName(method));
                sb.Append(" __getslot__");
                sb.Append(GetCppMethodName(method));
                sb.Append("(void * pThis)");
                sb.AppendLine();
                sb.Append("{");
                sb.Indent();
                sb.AppendLine();
                sb.Append(" return (__slot__");
                sb.Append(GetCppMethodName(method));
                sb.Append(")*((void **)(*((RawEEType **)pThis) + 1) + ");
                sb.Append(slot.ToStringInvariant());
                sb.Append(");");

            }
            sb.Exdent();
            sb.AppendLine();
            sb.Append("};");
            sb.Exdent();
            return sb.ToString();
        }

        private String GetCodeForObjectNode(ObjectNode node, NodeFactory factory, bool generateMethod = true, string structType = null)
        {
            // virtual slots
            var nodeData = node.GetData(factory, false);

            CppGenerationBuffer nodeCode = new CppGenerationBuffer();

            /* Create list of byte data. Used to divide contents between reloc and byte data
          * First val - isReloc
          * Second val - size of byte data if first value of tuple is false
          */

            List<NodeDataSection> nodeDataSections = new List<NodeDataSection>();
            byte[] actualData = new byte[nodeData.Data.Length];
            Relocation[] relocs = nodeData.Relocs;
            int[] relocOffsets = new int[relocs.Length];

            int nextRelocOffset = -1;
            int nextRelocIndex = -1;
            int lastByteIndex = 0;

            if (relocs.Length > 0)
            {
                nextRelocOffset = relocs[0].Offset;
                nextRelocIndex = 0;
            }

            int i = 0;
            int offset = 0;
            CppGenerationBuffer nodeDataDecl = new CppGenerationBuffer();

            if (node is ISymbolDefinitionNode)
            {
                offset = (node as ISymbolDefinitionNode).Offset;
            }
            while (i < nodeData.Data.Length)
            {
                if (i == nextRelocOffset)
                {
                    Relocation reloc = relocs[nextRelocIndex];

                    int size = _compilation.TypeSystemContext.Target.PointerSize;
                    // Make sure we've gotten the correct size for the reloc
                    System.Diagnostics.Debug.Assert(reloc.RelocType == (size == 8 ? RelocType.IMAGE_REL_BASED_DIR64 : RelocType.IMAGE_REL_BASED_HIGHLOW));

                    if (size == 8)
                    {
                        relocOffsets[nextRelocIndex] = (int)BitConverter.ToInt64(nodeData.Data, i);
                    }
                    else
                    {
                        relocOffsets[nextRelocIndex] = BitConverter.ToInt32(nodeData.Data, i);
                    }

                    // Update nextRelocIndex/Offset
                    if (++nextRelocIndex < relocs.Length)
                    {
                        nextRelocOffset = relocs[nextRelocIndex].Offset;
                    }
                    nodeDataSections.Add(new NodeDataSection(NodeDataSectionType.Relocation, size));
                    i += size;
                    lastByteIndex = i;
                }
                else
                {
                    if (i + 1 == nextRelocOffset || i + 1 == nodeData.Data.Length)
                    {
                        nodeDataSections.Add(new NodeDataSection(NodeDataSectionType.ByteData, (i + 1) - lastByteIndex));
                    }
                    i++;
                }
            }

            generateMethod = generateMethod && !(node is BlobNode);

            string retType;

            if (node is EETypeNode)
                retType = "MethodTable * ";
            else if (node is RuntimeMethodHandleNode)
                retType = "::System_Private_CoreLib::System::RuntimeMethodHandle ";
            else
                retType = "void * ";

            string mangledName = GetCppSymbolNodeName(factory, (ISymbolNode)node);

            if (generateMethod)
            {
                nodeCode.Append(retType);
                if (node is EETypeNode)
                {
                    nodeCode.Append(GetCppMethodDeclarationName((node as EETypeNode).Type, "__getMethodTable"));
                }
                else
                {
                    // Rename nodes to avoid name clash with types
                    bool shouldReplaceNamespaceQualifier = node is GenericCompositionNode || node is EETypeOptionalFieldsNode ||
                        node is SealedVTableNode || node is TypeGenericDictionaryNode || node is IndirectionNode || node is MethodGenericDictionaryNode;

                    nodeCode.Append(shouldReplaceNamespaceQualifier ? mangledName.Replace("::", "_") : mangledName);
                }
                nodeCode.Append("()");
                nodeCode.AppendLine();
                nodeCode.Append("{");
                nodeCode.Indent();
                nodeCode.AppendLine();
                nodeCode.Append("static ");
            }
            else
            {
                if (node is BlobNode)
                    nodeCode.Append("extern \"C\" ");
            }
            nodeCode.Append("struct ");

            if (structType != null)
            {
                nodeCode.Append(structType);
                nodeCode.Append(" ");
            }

            nodeCode.Append("{");

            nodeCode.AppendLine();
            nodeCode.Append(GetCodeForNodeStruct(nodeDataSections, node));

            nodeCode.AppendLine();

            if (generateMethod)
                nodeCode.Append("} mt = {");
            else
                nodeCode.Append(" } " + mangledName.Replace("::", "_") + " = {");

            nodeCode.Append(GetCodeForNodeData(nodeDataSections, relocs, relocOffsets, nodeData.Data, node, 0, factory));

            nodeCode.Append("};");

            if (generateMethod)
            {
                nodeCode.AppendLine();

                if (node is RuntimeMethodHandleNode)
                {
                    nodeCode.Append(retType);
                    nodeCode.Append(" r = {(intptr_t)&mt};");
                    nodeCode.AppendLine();
                    nodeCode.Append("return r;");
                }
                else
                {
                    nodeCode.Append("return ( ");
                    nodeCode.Append(retType);
                    nodeCode.Append(")((char*)&mt + ");
                    nodeCode.Append(offset.ToString());
                    nodeCode.Append(");");
                }

                nodeCode.Exdent();
                nodeCode.AppendLine();
                nodeCode.Append("}");
            }
            nodeCode.AppendLine();
            return nodeCode.ToString();
        }

        private String GetCodeForNodeData(List<NodeDataSection> nodeDataSections, Relocation[] relocs, int[] relocOffsets, byte[] byteData, DependencyNode node, int offset, NodeFactory factory)
        {
            CppGenerationBuffer nodeDataDecl = new CppGenerationBuffer();
            int relocCounter = 0;
            int divisionStartIndex = offset;
            nodeDataDecl.Indent();
            nodeDataDecl.Indent();
            nodeDataDecl.AppendLine();

            for (int i = 0; i < nodeDataSections.Count; i++)
            {
                if (nodeDataSections[i].SectionType == NodeDataSectionType.Relocation)
                {
                    Relocation reloc = relocs[relocCounter];
                    nodeDataDecl.Append("(char*)(");
                    nodeDataDecl.Append(GetCodeForReloc(reloc, node, factory));
                    nodeDataDecl.Append(") + ");
                    nodeDataDecl.Append(relocOffsets[relocCounter].ToString());
                    nodeDataDecl.Append(",");
                    relocCounter++;
                }
                else
                {
                    AppendFormattedByteArray(nodeDataDecl, byteData, divisionStartIndex, divisionStartIndex + nodeDataSections[i].SectionSize);
                    nodeDataDecl.Append(",");
                }
                divisionStartIndex += nodeDataSections[i].SectionSize;
                nodeDataDecl.AppendLine();
            }
            return nodeDataDecl.ToString();
        }

        private String GetCodeForReloc(Relocation reloc, DependencyNode node, NodeFactory factory)
        {
            CppGenerationBuffer relocCode = new CppGenerationBuffer();
            if (reloc.Target is CppMethodCodeNode)
            {
                var method = reloc.Target as CppMethodCodeNode;

                relocCode.Append("(void*)&");
                relocCode.Append(GetCppMethodDeclarationName(method.Method.OwningType, GetCppMethodName(method.Method), false));
            }
            else if (reloc.Target is EETypeNode)
            {
                var type = (reloc.Target as EETypeNode).Type;

                relocCode.Append(GetCppMethodDeclarationName(type, "__getMethodTable", false));
                relocCode.Append("()");
            }
            else if (reloc.Target is ObjectAndOffsetSymbolNode &&
                (reloc.Target as ObjectAndOffsetSymbolNode).Target is ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode>)
            {
                relocCode.Append("dispatchMapModule");
            }
            else if (reloc.Target is ObjectAndOffsetSymbolNode)
            {
                ObjectAndOffsetSymbolNode symbolNode = reloc.Target as ObjectAndOffsetSymbolNode;

                bool isEagerCctorTable = symbolNode.Target is ArrayOfEmbeddedPointersNode<IMethodNode>;

                if ((!(symbolNode.Target is EmbeddedDataContainerNode) &&
                    !(symbolNode.Target is StackTraceMethodMappingNode) ||
                    isEagerCctorTable
                    ) && !(symbolNode.Target as ObjectNode).ShouldSkipEmittingObjectNode(factory))
                {
                    string symbolName = isEagerCctorTable ? "eagerCctorTable" :
                        (symbolNode.Target as ISymbolNode).GetMangledName(factory.NameMangler);

                    relocCode.Append("((char *)");
                    relocCode.Append(symbolName);

                    if (!isEagerCctorTable)
                        relocCode.Append("()");

                    relocCode.Append(") + ");

                    relocCode.Append((symbolNode as ISymbolDefinitionNode).Offset.ToString());
                }
                else
                {
                    relocCode.Append("NULL");
                }
            }
            else if (reloc.Target is CppUnboxingStubNode)
            {
                var method = reloc.Target as CppUnboxingStubNode;

                relocCode.Append("(void*)&");
                relocCode.Append(GetCppMethodDeclarationName(method.Method.OwningType, method.GetMangledName(factory.NameMangler), false));
            }
            else if (reloc.Target is GCStaticsNode)
            {
                var gcStaticNode = reloc.Target as GCStaticsNode;

                relocCode.Append("(void*)&");
                relocCode.Append(GetCppStaticsName(gcStaticNode.Type, true));
            }
            else if (reloc.Target is NonGCStaticsNode)
            {
                var nonGcStaticNode = reloc.Target as NonGCStaticsNode;

                relocCode.Append("(char*)&");
                relocCode.Append(GetCppStaticsName(nonGcStaticNode.Type));

                if (_compilation.HasLazyStaticConstructor(nonGcStaticNode.Type))
                    relocCode.Append(" + sizeof(StaticClassConstructionContext)");
            }
            else if (reloc.Target is TypeThreadStaticIndexNode)
            {
                var threadStaticIndexNode = reloc.Target as TypeThreadStaticIndexNode;

                relocCode.Append("(void*)&");
                relocCode.Append(GetCppStaticsName(threadStaticIndexNode.Type, true, true));
            }
            else if (!(reloc.Target is ReadyToRunHeaderNode) &&
                     !(reloc.Target is StackTraceMethodMappingNode) &&
                     !(reloc.Target as ObjectNode).ShouldSkipEmittingObjectNode(factory))
            {
                string mangledTargetName = GetCppSymbolNodeName(factory, reloc.Target);

                bool shouldReplaceNamespaceQualifier = reloc.Target is GenericCompositionNode || reloc.Target is EETypeOptionalFieldsNode ||
                    reloc.Target is SealedVTableNode || reloc.Target is TypeGenericDictionaryNode || reloc.Target is IndirectionNode ||
                    reloc.Target is MethodGenericDictionaryNode;

                bool shouldUsePointer = reloc.Target is GenericCompositionNode || reloc.Target is TypeGenericDictionaryNode ||
                    reloc.Target is MethodGenericDictionaryNode;

                bool isRuntimeMethodHandle = reloc.Target is RuntimeMethodHandleNode;
                bool isMethodGenericDictionary = reloc.Target is MethodGenericDictionaryNode;

                if (shouldUsePointer)
                    relocCode.Append("(char *)&");
                else if (isRuntimeMethodHandle)
                    relocCode.Append("(void *)(");

                relocCode.Append(shouldReplaceNamespaceQualifier ? mangledTargetName.Replace("::", "_") : mangledTargetName);

                if (isMethodGenericDictionary)
                    relocCode.Append(" + sizeof(void*)");

                if (!shouldUsePointer)
                    relocCode.Append("()");

                if (isRuntimeMethodHandle)
                    relocCode.Append("._value)");
            }
            else
            {
                relocCode.Append("NULL");
            }
            return relocCode.ToString();
        }

        private String GetCodeForNodeStruct(List<NodeDataSection> nodeDataDivs, DependencyNode node)
        {
            CppGenerationBuffer nodeStructDecl = new CppGenerationBuffer();
            int relocCounter = 1;
            int i = 0;
            nodeStructDecl.Indent();

            for (i = 0; i < nodeDataDivs.Count; i++)
            {
                NodeDataSection section = nodeDataDivs[i];
                if (section.SectionType == NodeDataSectionType.Relocation)
                {
                    nodeStructDecl.Append("void* reloc");
                    nodeStructDecl.Append(relocCounter);
                    nodeStructDecl.Append(";");
                    relocCounter++;
                }
                else
                {
                    nodeStructDecl.Append("unsigned char data");
                    nodeStructDecl.Append((i + 1) - relocCounter);
                    nodeStructDecl.Append("[");
                    nodeStructDecl.Append(section.SectionSize);
                    nodeStructDecl.Append("];");
                }
                nodeStructDecl.AppendLine();
            }
            nodeStructDecl.Exdent();

            return nodeStructDecl.ToString();
        }

        private static void AppendFormattedByteArray(CppGenerationBuffer sb, byte[] array, int startIndex, int endIndex)
        {
            sb.Append("{");
            sb.Append("0x");
            sb.Append(BitConverter.ToString(array, startIndex, endIndex - startIndex).Replace("-", ",0x"));
            sb.Append("}");
        }

        private void BuildMethodLists(IEnumerable<DependencyNode> nodes)
        {
            _methodLists = new Dictionary<TypeDesc, List<MethodDesc>>();
            _typesWithCctor = new HashSet<TypeDesc>();
            foreach (var node in nodes)
            {
                if (node is CppMethodCodeNode)
                {
                    CppMethodCodeNode methodCodeNode = (CppMethodCodeNode)node;

                    var method = methodCodeNode.Method;
                    var type = method.OwningType;

                    if (_compilation.HasLazyStaticConstructor(type) && method.Equals(type.GetStaticConstructor()))
                        _typesWithCctor.Add(type);

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
                if (node is IEETypeNode)
                {
                    IEETypeNode eeTypeNode = (IEETypeNode)node;

                    if (eeTypeNode.Type.IsGenericDefinition)
                    {
                        // TODO: CppWriter can't handle generic type definition EETypes
                    }
                    else
                        GetCppSignatureTypeName(eeTypeNode.Type);
                }
            }
        }

        /// <summary>
        /// Output C++ code via a given dependency graph
        /// </summary>
        /// <param name="nodes">A set of dependency nodes</param>
        /// <param name="entrypoint">Code entrypoint</param>
        /// <param name="factory">Associated NodeFactory instance</param>
        /// <param name="definitions">Text buffer in which the type and method definitions will be written</param>
        /// <param name="implementation">Text buffer in which the method implementations will be written</param>
        public void OutputNodes(IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            CppGenerationBuffer dispatchPointers = new CppGenerationBuffer();
            CppGenerationBuffer eagerCctorPointers = new CppGenerationBuffer();
            CppGenerationBuffer forwardDefinitions = new CppGenerationBuffer();
            CppGenerationBuffer typeDefinitions = new CppGenerationBuffer();
            CppGenerationBuffer methodTables = new CppGenerationBuffer();
            CppGenerationBuffer additionalNodes = new CppGenerationBuffer();
            CppGenerationBuffer indirectionNodes = new CppGenerationBuffer();
            DependencyNodeIterator nodeIterator = new DependencyNodeIterator(nodes, factory);

            // Number of InterfaceDispatchMapNodes needs to be declared explicitly for Ubuntu and OSX
            int dispatchMapCount = 0;
            dispatchPointers.AppendLine();
            dispatchPointers.Indent();

            int eagerCctorCount = 0;
            eagerCctorPointers.AppendLine();
            eagerCctorPointers.Indent();

            //RTR header needs to be declared after all modules have already been output
            ReadyToRunHeaderNode rtrHeaderNode = null;
            string rtrHeader = string.Empty;

            // Iterate through nodes
            foreach (var node in nodeIterator.GetNodes())
            {
                if (node is EETypeNode)
                    OutputTypeNode(node as EETypeNode, factory, typeDefinitions, methodTables);
                else if (node is ArrayOfEmbeddedPointersNode<InterfaceDispatchMapNode> dispatchMap)
                {
                    var dispatchMapData = dispatchMap.GetData(factory, false);
                    Debug.Assert(dispatchMapData.Relocs.Length == dispatchMapData.Data.Length / factory.Target.PointerSize);
                    foreach (Relocation reloc in dispatchMapData.Relocs)
                    {
                        dispatchPointers.Append("(void *)");
                        dispatchPointers.Append(reloc.Target.GetMangledName(factory.NameMangler));
                        dispatchPointers.Append("(),");
                        dispatchPointers.AppendLine();
                        dispatchMapCount++;
                        additionalNodes.Append(GetCodeForObjectNode(reloc.Target as ObjectNode, factory));
                    }
                }
                else if (node is ArrayOfEmbeddedPointersNode<IMethodNode> eagerCctorTable)
                {
                    var eagerCctorTableData = eagerCctorTable.GetData(factory, false);
                    Debug.Assert(eagerCctorTableData.Relocs.Length == eagerCctorTableData.Data.Length / factory.Target.PointerSize);
                    foreach (Relocation reloc in eagerCctorTableData.Relocs)
                    {
                        var method = reloc.Target as CppMethodCodeNode;

                        eagerCctorPointers.Append("(void *)&");
                        eagerCctorPointers.Append(GetCppMethodDeclarationName(method.Method.OwningType, GetCppMethodName(method.Method), false));
                        eagerCctorPointers.Append(",");
                        eagerCctorPointers.AppendLine();
                        eagerCctorCount++;
                    }
                }
                else if (node is ReadyToRunHeaderNode)
                    rtrHeaderNode = node as ReadyToRunHeaderNode;
                else if (node is ReadyToRunGenericHelperNode)
                    additionalNodes.Append(GetCodeForReadyToRunGenericHelper(node as ReadyToRunGenericHelperNode, factory));
                else if (node is ObjectNode &&
                        !(node is EmbeddedDataContainerNode) &&
                        !(node is StackTraceMethodMappingNode) &&
                        !(node is GCStaticsNode) &&
                        !(node is NonGCStaticsNode) &&
                        !(node is TypeThreadStaticIndexNode) &&
                        !(node is InterfaceDispatchMapNode) &&
                        !(node as ObjectNode).ShouldSkipEmittingObjectNode(factory))
                {
                    if (node is IndirectionNode)
                    {
                        indirectionNodes.Append(GetCodeForObjectNode(node as ObjectNode, factory));
                    }
                    else
                    {
                        bool shouldUsePointer = node is GenericCompositionNode || node is TypeGenericDictionaryNode ||
                            node is MethodGenericDictionaryNode;

                        if (shouldUsePointer)
                        {
                            string varName = GetCppSymbolNodeName(factory, (ISymbolNode)node).Replace("::", "_");
                            string structType = "T_" + varName;

                            additionalNodes.Append(GetCodeForObjectNode(node as ObjectNode, factory, false, structType));

                            forwardDefinitions.AppendLine();
                            forwardDefinitions.Append("extern struct ");
                            forwardDefinitions.Append(structType);
                            forwardDefinitions.Append(" ");
                            forwardDefinitions.Append(varName);
                            forwardDefinitions.Append(";");
                        }
                        else
                        {
                            additionalNodes.Append(GetCodeForObjectNode(node as ObjectNode, factory));
                        }
                    }

                    if (node is NativeLayoutSignatureNode ||
                        node is RuntimeMethodHandleNode ||
                        node is FatFunctionPointerNode ||
                        node is GCStaticEETypeNode)
                    {
                        forwardDefinitions.AppendLine();
                        if (node is RuntimeMethodHandleNode)
                            forwardDefinitions.Append("::System_Private_CoreLib::System::RuntimeMethodHandle ");
                        else
                            forwardDefinitions.Append("void * ");
                        forwardDefinitions.Append(GetCppSymbolNodeName(factory, (ISymbolNode)node));
                        forwardDefinitions.Append("();");
                    }
                }
            }

            rtrHeader = GetCodeForReadyToRunHeader(rtrHeaderNode, factory);

            dispatchPointers.AppendLine();
            dispatchPointers.Exdent();

            WriteForwardDefinitions();

            Out.Write(forwardDefinitions.ToString());

            Out.Write(typeDefinitions.ToString());

            OutputStaticsCode(factory, _statics);

            OutputStaticsCode(factory, _gcStatics, true);

            OutputStaticsCode(factory, _threadStatics, true, true);

            Out.Write(indirectionNodes.ToString());

            Out.Write(additionalNodes.ToString());

            Out.Write(methodTables.ToString());

            // Emit pointers to dispatch map nodes, to be used in interface dispatch
            if (dispatchMapCount > 0)
            {
                Out.Write("void * dispatchMapModule[");
                Out.Write(dispatchMapCount);
                Out.Write("] = {");
                Out.Write(dispatchPointers.ToString());
                Out.Write("};");
            }

            // Emit pointers to eager cctor table nodes
            if (eagerCctorCount > 0)
            {
                Out.Write("void * eagerCctorTable[");
                Out.Write(eagerCctorCount);
                Out.Write("] = {");
                Out.Write(eagerCctorPointers.ToString());
                Out.Write("};");
            }

            Out.Write(rtrHeader);
        }

        /// <summary>
        /// Output C++ code for a given codeNode
        /// </summary>
        /// <param name="methodCodeNode">The code node to be output</param>
        /// <param name="methodImplementations">The buffer in which to write out the C++ code</param>
        private void OutputMethodNode(CppMethodCodeNode methodCodeNode)
        {
            Out.WriteLine();
            Out.Write(methodCodeNode.CppCode);

            var alternateName = _compilation.NodeFactory.GetSymbolAlternateName(methodCodeNode);
            if (alternateName != null)
            {
                CppGenerationBuffer sb = new CppGenerationBuffer();
                sb.AppendLine();
                AppendCppMethodDeclaration(sb, methodCodeNode.Method, true, alternateName);
                sb.AppendLine();
                sb.Append("{");
                sb.Indent();
                sb.AppendLine();
                if (!methodCodeNode.Method.Signature.ReturnType.IsVoid)
                {
                    sb.Append("return ");
                }
                sb.Append(GetCppMethodDeclarationName(methodCodeNode.Method.OwningType, GetCppMethodName(methodCodeNode.Method)));
                sb.Append("(");
                AppendCppMethodCallParamList(sb, methodCodeNode.Method);
                sb.Append(");");
                sb.Exdent();
                sb.AppendLine();
                sb.Append("}");

                Out.Write(sb.ToString());
            }
        }

        /// <summary>
        /// Write forward definitions for all mangled type names referenced by the generated C++ code. This set is tracked separately from
        /// the types that need EEType because of the type mangled names are often needed to just get the code to compile but type node is not
        /// actually required for it.
        /// </summary>
        private void WriteForwardDefinitions()
        {
            CppGenerationBuffer forwardDefinitions = new CppGenerationBuffer();

            string[] mangledNames = _mangledNames.Values.ToArray();
            Array.Sort(mangledNames);
            foreach (string mangledName in mangledNames)
            {
                int nesting = 0;
                int current = 0;

                for (; ; )
                {
                    int sep = mangledName.IndexOf("::", current);

                    if (sep < 0)
                        break;

                    if (sep != 0)
                    {
                        // Case of a name not starting with ::
                        forwardDefinitions.Append("namespace " + mangledName.Substring(current, sep - current) + " { ");
                        nesting++;
                    }
                    current = sep + 2;
                }

                forwardDefinitions.Append("class " + mangledName.Substring(current) + ";");

                while (nesting > 0)
                {
                    forwardDefinitions.Append(" }");
                    nesting--;
                }

                forwardDefinitions.AppendLine();
            }

            Out.Write(forwardDefinitions.ToString());
        }

        private void OutputTypeNode(IEETypeNode typeNode, NodeFactory factory, CppGenerationBuffer typeDefinitions, CppGenerationBuffer methodTable)
        {
            if (_emittedTypes == null)
            {
                _emittedTypes = new HashSet<TypeDesc>();
            }

            TypeDesc nodeType = typeNode.Type;
            if (_emittedTypes.Contains(nodeType))
                return;
            _emittedTypes.Add(nodeType);

            // Create Namespaces
            string mangledName = GetMangledTypeName(nodeType);

            int nesting = 0;
            int current = 0;

            for (;;)
            {
                int sep = mangledName.IndexOf("::", current);

                if (sep < 0)
                    break;

                if (sep != 0)
                {
                    // Case of a name not starting with ::
                    typeDefinitions.Append("namespace " + mangledName.Substring(current, sep - current) + " { ");
                    typeDefinitions.Indent();
                    nesting++;
                }
                current = sep + 2;
            }

            // type definition
            typeDefinitions.Append("class " + mangledName.Substring(current));
            if (!nodeType.IsValueType)
            {
                if (nodeType.BaseType != null)
                {
                    TypeDesc baseType = nodeType.BaseType;

                    if (!nodeType.IsGenericDefinition && baseType.IsCanonicalSubtype(CanonicalFormKind.Any))
                        baseType = baseType.ConvertToCanonForm(CanonicalFormKind.Specific);

                    // Don't emit inheritance if base type has not been marked for emission
                    if (_emittedTypes.Contains(baseType))
                    {
                        typeDefinitions.Append(" : public " + GetCppTypeName(baseType));
                    }
                }
            }
            typeDefinitions.Append(" {");
            typeDefinitions.AppendLine();
            typeDefinitions.Append("public:");
            typeDefinitions.Indent();

            if (typeNode.Marked)
            {
                typeDefinitions.AppendLine();
                typeDefinitions.Append("static MethodTable * __getMethodTable();");
            }

            if (nodeType.IsDefType && !nodeType.IsGenericDefinition)
            {
                if (_compilation.HasLazyStaticConstructor(nodeType))
                {
                    MethodDesc cctor = nodeType.GetStaticConstructor().GetCanonMethodTarget(CanonicalFormKind.Specific);

                    if (_typesWithCctor.Contains(cctor.OwningType))
                    {
                        CppGenerationBuffer staticsBuffer;

                        if (!_statics.TryGetValue(nodeType, out staticsBuffer))
                        {
                            staticsBuffer = new CppGenerationBuffer();
                            staticsBuffer.Indent();
                            _statics[nodeType] = staticsBuffer;
                        }

                        staticsBuffer.Append("void *__cctorMethodAddress;");
                        staticsBuffer.AppendLine();
                        staticsBuffer.Append("int __initialized;");
                        staticsBuffer.AppendLine();

                        if (_compilation.TypeSystemContext.Target.PointerSize == 8)
                        {
                            staticsBuffer.Append("int __pad;");
                            staticsBuffer.AppendLine();
                        }
                    }
                }

                OutputTypeFields(typeDefinitions, nodeType);
            }

            if ((typeNode is ConstructedEETypeNode || typeNode is CanonicalEETypeNode ||
                typeNode is NecessaryCanonicalEETypeNode) && nodeType is DefType)
            {
                DefType closestDefType = nodeType.GetClosestDefType();

                IReadOnlyList<MethodDesc> virtualSlots = _compilation.NodeFactory.VTable(closestDefType).Slots;

                MethodDesc delegateInvoke = nodeType.IsDelegate ? nodeType.GetKnownMethod("Invoke", null) : null;
                bool generateTypeDef = true;
                foreach (MethodDesc slot in virtualSlots)
                {
                    generateTypeDef = generateTypeDef && (slot != delegateInvoke);
                    typeDefinitions.AppendLine();
                    int slotNumber = VirtualMethodSlotHelper.GetVirtualMethodSlot(_compilation.NodeFactory, slot, closestDefType);
                    typeDefinitions.Append(GetCodeForVirtualMethod(slot, slotNumber));
                }

                if (nodeType.IsDelegate)
                {
                    typeDefinitions.AppendLine();
                    typeDefinitions.Append(GetCodeForDelegate(nodeType, generateTypeDef));
                }
            }

            List<MethodDesc> methodList;
            if (_methodLists.TryGetValue(nodeType, out methodList))
            {
                foreach (var m in methodList)
                {
                    typeDefinitions.AppendLine();
                    AppendCppMethodDeclaration(typeDefinitions, m, false);
                    typeDefinitions.AppendLine();
                    AppendCppMethodDeclaration(typeDefinitions, m, false, null, null, CppUnboxingStubNode.GetMangledName(factory.NameMangler, m), true);
                }
            }

            typeDefinitions.AppendEmptyLine();
            typeDefinitions.Append("};");
            typeDefinitions.AppendEmptyLine();
            typeDefinitions.Exdent();

            while (nesting > 0)
            {
                typeDefinitions.Append("};");
                typeDefinitions.Exdent();
                nesting--;
            }
            typeDefinitions.AppendEmptyLine();

            // declare method table
            if (typeNode.Marked)
            {
                methodTable.Append(GetCodeForObjectNode(typeNode as ObjectNode, factory));
            }
            methodTable.AppendEmptyLine();
        }

        private String GetCodeForReadyToRunHeader(ReadyToRunHeaderNode headerNode, NodeFactory factory)
        {
            CppGenerationBuffer rtrHeader = new CppGenerationBuffer();
            int pointerSize = _compilation.TypeSystemContext.Target.PointerSize;

            rtrHeader.Append(GetCodeForObjectNode(headerNode, factory));
            rtrHeader.AppendLine();
            rtrHeader.Append("extern \"C\" void* RtRHeaderWrapper() {");
            rtrHeader.Indent();
            rtrHeader.AppendLine();
            rtrHeader.Append("static struct {");
            rtrHeader.AppendLine();
            if (pointerSize == 8)
                rtrHeader.Append("unsigned char leftPadding[8];");
            else
                rtrHeader.Append("unsigned char leftPadding[4];");
            rtrHeader.AppendLine();
            rtrHeader.Append("void* rtrHeader;");
            rtrHeader.AppendLine();
            if (pointerSize == 8)
                rtrHeader.Append("unsigned char rightPadding[8];");
            else
                rtrHeader.Append("unsigned char rightPadding[4];");
            rtrHeader.AppendLine();
            rtrHeader.Append("} rtrHeaderWrapper = {");
            rtrHeader.Indent();
            rtrHeader.AppendLine();
            if (pointerSize == 8)
                rtrHeader.Append("{ 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 },");
            else
                rtrHeader.Append("{ 0x00,0x00,0x00,0x00 },");
            rtrHeader.AppendLine();
            rtrHeader.Append("(void*)");
            rtrHeader.Append(headerNode.GetMangledName(factory.NameMangler));
            rtrHeader.Append("(),");
            rtrHeader.AppendLine();
            if (pointerSize == 8)
                rtrHeader.Append("{ 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 }");
            else
                rtrHeader.Append("{ 0x00,0x00,0x00,0x00 },");
            rtrHeader.AppendLine();
            rtrHeader.Append("};");
            rtrHeader.Exdent();
            rtrHeader.AppendLine();
            rtrHeader.Append("return (void *)&rtrHeaderWrapper;");
            rtrHeader.Exdent();
            rtrHeader.AppendLine();
            rtrHeader.Append("}");
            rtrHeader.AppendLine();

            return rtrHeader.ToString();
        }

        private void OutputCodeForTriggerCctor(CppGenerationBuffer sb, NodeFactory factory,
            TypeDesc type, string staticsBaseVarName)
        {
            type = type.ConvertToCanonForm(CanonicalFormKind.Specific);
            MethodDesc cctor = type.GetStaticConstructor();
            IMethodNode helperNode = (IMethodNode)factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);

            sb.Append(GetCppMethodDeclarationName(helperNode.Method.OwningType, GetCppMethodName(helperNode.Method), false));
            sb.Append("((::System_Private_CoreLib::System::Runtime::CompilerServices::StaticClassConstructionContext*)((char*)");
            sb.Append(staticsBaseVarName);
            sb.Append(" - sizeof(StaticClassConstructionContext)), (intptr_t)");
            sb.Append(staticsBaseVarName);
            sb.Append(");");

            sb.AppendLine();
        }

        private void OutputCodeForDictionaryLookup(CppGenerationBuffer sb, NodeFactory factory,
                 ReadyToRunGenericHelperNode node, GenericLookupResult lookup, string ctxVarName,
                 string resVarName)
        {
            // Find the generic dictionary slot
            int dictionarySlot = factory.GenericDictionaryLayout(node.DictionaryOwner).GetSlotForEntry(lookup);

            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            sb.Append(resVarName);
            sb.Append(" = *(void **)((intptr_t)");
            sb.Append(ctxVarName);
            sb.Append(" + ");
            sb.Append(offset);
            sb.Append(");");
            sb.AppendLine();

            switch (lookup.LookupResultReferenceType(factory))
            {
                case GenericLookupResultReferenceType.Indirect:
                    {
                        sb.Append(resVarName);
                        sb.Append(" = *(void **)");
                        sb.Append(resVarName);
                        sb.Append(";");
                        sb.AppendLine();
                    }
                    break;

                case GenericLookupResultReferenceType.ConditionalIndirect:
                    throw new NotImplementedException();

                default:
                    break;
            }
        }

        private string GetCodeForReadyToRunGenericHelper(ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            CppGenerationBuffer sb = new CppGenerationBuffer();

            string mangledName = GetCppReadyToRunGenericHelperNodeName(factory, node);
            List<string> argNames = new List<string>(new string[] {"arg"});
            string ctxVarName = "ctx";
            string resVarName = "res";
            string retVarName = "ret";

            string retType;
            switch (node.Id)
            {
                case ReadyToRunHelperId.MethodHandle:
                    retType = "::System_Private_CoreLib::System::RuntimeMethodHandle";
                    break;
                case ReadyToRunHelperId.DelegateCtor:
                    retType = "void";
                    break;
                default:
                    retType = "void*";
                    break;
            }

            sb.AppendLine();
            sb.Append(retType);
            sb.Append(" ");
            sb.Append(mangledName);
            sb.Append("(");
            sb.Append("void *");
            sb.Append(argNames[0]);

            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                sb.Append(", ");

                DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                MethodDesc constructor = target.Constructor.Method;

                bool isStatic = constructor.Signature.IsStatic;

                int argCount = constructor.Signature.Length;
                if (!isStatic)
                    argCount++;

                int startIdx = argNames.Count;
                for (int i = 0; i < argCount; i++)
                {
                    string argName = $"arg{i + startIdx}";
                    argNames.Add(argName);

                    TypeDesc argType;
                    if (i == 0 && !isStatic)
                    {
                        argType = constructor.OwningType;
                    }
                    else
                    {
                        argType = constructor.Signature[i - (isStatic ? 0 : 1)];
                    }

                    sb.Append(GetCppSignatureTypeName(argType));
                    sb.Append(" ");
                    sb.Append(argName);

                    if (i != argCount - 1)
                        sb.Append(", ");
                }
            }

            sb.Append(")");

            sb.AppendLine();
            sb.Append("{");
            sb.Indent();
            sb.AppendLine();

            sb.Append("void *");
            sb.Append(ctxVarName);
            sb.Append(";");
            sb.AppendLine();

            sb.Append("void *");
            sb.Append(resVarName);
            sb.Append(";");
            sb.AppendLine();

            if (node is ReadyToRunGenericLookupFromTypeNode)
            {
                // Locate the VTable slot that points to the dictionary
                int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)node.DictionaryOwner);

                int pointerSize = factory.Target.PointerSize;
                int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);

                // Load the dictionary pointer from the VTable
                sb.Append(ctxVarName);
                sb.Append(" = *(void **)((intptr_t)");
                sb.Append(argNames[0]);
                sb.Append(" + ");
                sb.Append(slotOffset.ToString());
                sb.Append(");");
                sb.AppendLine();
            }
            else
            {
                sb.Append(ctxVarName);
                sb.Append(" = ");
                sb.Append(argNames[0]);
                sb.Append(";");
                sb.AppendLine();
            }

            OutputCodeForDictionaryLookup(sb, factory, node, node.LookupSignature, ctxVarName, resVarName);

            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            OutputCodeForTriggerCctor(sb, factory, target, resVarName);

                            sb.Append(resVarName);
                            sb.Append(" = ");
                            sb.Append("(char*)");
                            sb.Append(resVarName);
                            sb.Append(" - sizeof(StaticClassConstructionContext);");
                            sb.AppendLine();
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        sb.Append(resVarName);
                        sb.Append(" = **(void ***)");
                        sb.Append(resVarName);
                        sb.Append(";");
                        sb.AppendLine();

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            string nonGcStaticsBase = "nonGcBase";

                            sb.Append("void *");
                            sb.Append(nonGcStaticsBase);
                            sb.Append(";");
                            sb.AppendLine();

                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);

                            OutputCodeForDictionaryLookup(sb, factory, node, nonGcRegionLookup, ctxVarName, nonGcStaticsBase);

                            OutputCodeForTriggerCctor(sb, factory, target, nonGcStaticsBase);
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            string nonGcStaticsBase = "nonGcBase";

                            sb.Append("void *");
                            sb.Append(nonGcStaticsBase);
                            sb.Append(";");
                            sb.AppendLine();

                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);

                            OutputCodeForDictionaryLookup(sb, factory, node, nonGcRegionLookup, ctxVarName, nonGcStaticsBase);

                            OutputCodeForTriggerCctor(sb, factory, target, nonGcStaticsBase);
                        }
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                        MethodDesc constructor = target.Constructor.Method;

                        sb.Append(argNames[3]);
                        sb.Append(" = ((intptr_t)");
                        sb.Append(resVarName);
                        sb.Append(") + ");
                        sb.Append(constructor.Context.Target.FatFunctionPointerOffset.ToString());
                        sb.Append(";");
                        sb.AppendLine();

                        sb.Append("::");
                        sb.Append(GetCppMethodDeclarationName(constructor.OwningType, GetCppMethodName(constructor)));
                        sb.Append("(");

                        for (int i = 1; i < argNames.Count; i++)
                        {
                            sb.Append(argNames[i]);

                            if (i != argNames.Count - 1)
                                sb.Append(", ");
                        }

                        sb.Append(");");
                        sb.AppendLine();
                    }
                    break;

                // These are all simple: just get the thing from the dictionary and we're done
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.MethodHandle:
                case ReadyToRunHelperId.FieldHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.DefaultConstructor:
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (node.Id != ReadyToRunHelperId.DelegateCtor)
            {
                sb.Append(retType);
                sb.Append(" ");
                sb.Append(retVarName);
                sb.Append(" = ");

                if (node.Id == ReadyToRunHelperId.MethodHandle)
                {
                    sb.Append("{");
                    sb.Append("(intptr_t)");
                    sb.Append(resVarName);
                    sb.Append("};");
                }
                else
                {
                    sb.Append(resVarName);
                    sb.Append(";");
                }

                sb.AppendLine();

                sb.Append("return ");
                sb.Append(retVarName);
                sb.Append(";");
            }

            sb.Exdent();
            sb.AppendLine();
            sb.Append("}");
            sb.AppendLine();

            return sb.ToString();
        }

        private int GetNameAndSignatureId(MethodDesc method)
        {
            var nameAndSignature = new Tuple<string, MethodSignature>(method.Name, method.Signature);

            int uniqueId;

            if (!_methodNameAndSignatures.TryGetValue(nameAndSignature, out uniqueId))
            {
                uniqueId = _methodNameAndSignatures.Count;

                _methodNameAndSignatures[nameAndSignature] = uniqueId;
            }

            return uniqueId;
        }

        private void OutputExternCSignatures()
        {
            var sb = new CppGenerationBuffer();
            foreach (var externC in _externCSignatureMap)
            {
                string importName = externC.Key;
                // TODO: hacky special-case
                if (importName != "memmove" && importName != "memset" && importName != "malloc") // some methods are already declared by the CRT headers
                {
                    sb.AppendLine();
                    AppendCppMethodDeclaration(sb, null, false, importName, externC.Value);
                }
            }

            Out.Write(sb.ToString());
        }

        /// <summary>
        /// Output C++ code for a given unboxingStubNode
        /// </summary>
        /// <param name="unboxingStubNode">The unboxing stub node to be output</param>
        /// <param name="methodImplementations">The buffer in which to write out the C++ code</param>
        private void OutputUnboxingStubNode(CppUnboxingStubNode unboxingStubNode)
        {
            Out.WriteLine();

            CppGenerationBuffer sb = new CppGenerationBuffer();
            sb.AppendLine();
            AppendCppMethodDeclaration(sb, unboxingStubNode.Method, true, null, null, unboxingStubNode.GetMangledName(_compilation.NameMangler), true);
            sb.AppendLine();
            sb.Append("{");
            sb.Indent();
            sb.AppendLine();
            if (!unboxingStubNode.Method.Signature.ReturnType.IsVoid)
            {
                sb.Append("return ");
            }
            sb.Append("::");
            sb.Append(GetCppMethodDeclarationName(unboxingStubNode.Method.OwningType, GetCppMethodName(unboxingStubNode.Method)));
            sb.Append("(");
            AppendCppMethodCallParamList(sb, unboxingStubNode.Method, true);
            sb.Append(");");
            sb.Exdent();
            sb.AppendLine();
            sb.Append("}");

            Out.Write(sb.ToString());
        }

        private void OutputStaticsCode(NodeFactory factory, Dictionary<TypeDesc, CppGenerationBuffer> statics,
            bool isGCStatic = false, bool isThreadStatic = false)
        {
            CppGenerationBuffer sb = new CppGenerationBuffer();

            foreach (var entry in statics)
            {
                TypeDesc t = entry.Key;

                sb.Append("struct ");
                sb.Append(GetCppStaticsTypeName(t, isGCStatic, isThreadStatic));
                sb.Append(" {");
                sb.Indent();
                sb.AppendLine();

                if (isGCStatic)
                {
                    // GC statics start with a pointer to the "EEType" that signals the size and GCDesc to the GC
                    sb.Append("void * __pad;");
                    sb.AppendLine();
                }

                sb.Append(entry.Value.ToString());
                sb.Exdent();
                sb.AppendLine();
                sb.Append("};");
                sb.AppendLine();

                if (t.IsCanonicalSubtype(CanonicalFormKind.Any))
                    continue;

                if (isThreadStatic)
                    sb.Append("CORERT_THREAD ");

                sb.Append(GetCppStaticsTypeName(t, isGCStatic, isThreadStatic));
                sb.Append(" ");
                sb.Append(GetCppStaticsName(t, isGCStatic, isThreadStatic, isGCStatic && !isThreadStatic));

                if (!isGCStatic && _compilation.HasLazyStaticConstructor(t))
                {
                    MethodDesc cctor = t.GetStaticConstructor();
                    MethodDesc canonCctor = cctor.GetCanonMethodTarget(CanonicalFormKind.Specific);

                    if (_typesWithCctor.Contains(canonCctor.OwningType))
                    {
                        sb.Append(" = {");
                        sb.Indent();
                        sb.AppendLine();

                        if (canonCctor.RequiresInstArg())
                        {
                            sb.Append("(char *)");
                            sb.Append(GetCppFatFunctionPointerNameForMethod(cctor));
                            sb.Append("() + ");
                            sb.Append(cctor.Context.Target.FatFunctionPointerOffset.ToString());
                        }
                        else
                        {
                            sb.Append("(void*)&");
                            sb.Append(GetCppMethodDeclarationName(canonCctor.OwningType, GetCppMethodName(canonCctor)));
                        }

                        sb.Exdent();
                        sb.AppendLine();
                        sb.Append("}");
                    }
                }

                sb.Append(";");
                sb.AppendLine();

                if (isGCStatic && !isThreadStatic)
                {
                    sb.Append(GetCppStaticsTypeName(t, isGCStatic, isThreadStatic));
                    sb.Append(" *");
                    sb.Append(GetCppStaticsName(t, isGCStatic, isThreadStatic, true));
                    sb.Append("__ptr = &");
                    sb.Append(GetCppStaticsName(t, isGCStatic, isThreadStatic, true));
                    sb.Append(";");
                    sb.AppendLine();

                    sb.Append(GetCppStaticsTypeName(t, isGCStatic, isThreadStatic));
                    sb.Append(" **");
                    sb.Append(GetCppStaticsName(t, isGCStatic, isThreadStatic));
                    sb.Append(" = &");
                    sb.Append(GetCppStaticsName(t, isGCStatic, isThreadStatic, true));
                    sb.Append("__ptr;");
                    sb.AppendLine();
                }
            }

            Out.Write(sb.ToString());
        }

        public TypeDesc ConvertToCanonFormIfNecessary(TypeDesc type, CanonicalFormKind policy)
        {
            if (!type.IsCanonicalSubtype(CanonicalFormKind.Any))
                return type;

            if (type.IsPointer || type.IsByRef)
            {
                ParameterizedType parameterizedType = (ParameterizedType)type;
                TypeDesc paramTypeConverted = ConvertToCanonFormIfNecessary(parameterizedType.ParameterType, policy);
                if (paramTypeConverted == parameterizedType.ParameterType)
                    return type;

                if (type.IsPointer)
                    return _compilation.TypeSystemContext.GetPointerType(paramTypeConverted);

                if (type.IsByRef)
                    return _compilation.TypeSystemContext.GetByRefType(paramTypeConverted);
            }

            return type.ConvertToCanonForm(policy);
        }

        public void OutputCode(IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            BuildMethodLists(nodes);

            Out.WriteLine("#include \"common.h\"");
            Out.WriteLine("#include \"CppCodeGen.h\"");
            Out.WriteLine();

            _statics = new Dictionary<TypeDesc, CppGenerationBuffer>();
            _gcStatics = new Dictionary<TypeDesc, CppGenerationBuffer>();
            _threadStatics = new Dictionary<TypeDesc, CppGenerationBuffer>();

            OutputNodes(nodes, factory);

            OutputExternCSignatures();

            foreach (var node in nodes)
            {
                if (node is CppMethodCodeNode)
                    OutputMethodNode(node as CppMethodCodeNode);
                else if (node is CppUnboxingStubNode)
                    OutputUnboxingStubNode(node as CppUnboxingStubNode);
            }

            Out.Dispose();
        }
    }
}
