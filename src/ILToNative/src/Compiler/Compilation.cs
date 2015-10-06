﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

using Internal.JitInterface;

namespace ILToNative
{
    public struct CompilationOptions
    {
        public bool Cpp;
    }

    public partial class Compilation
    {
        readonly TypeSystemContext _typeSystemContext;
        readonly CompilationOptions _options;

        Dictionary<TypeDesc, RegisteredType> _registeredTypes = new Dictionary<TypeDesc, RegisteredType>();
        Dictionary<MethodDesc, RegisteredMethod> _registeredMethods = new Dictionary<MethodDesc, RegisteredMethod>();
        Dictionary<FieldDesc, RegisteredField> _registeredFields = new Dictionary<FieldDesc, RegisteredField>();
        List<MethodDesc> _methodsThatNeedsCompilation = null;

        ILToNative.CppCodeGen.CppWriter _cppWriter = null;

        public Compilation(TypeSystemContext typeSystemContext, CompilationOptions options)
        {
            _typeSystemContext = typeSystemContext;
            _options = options;
        }

        public TypeSystemContext TypeSystemContext
        {
            get
            {
                return _typeSystemContext;
            }
        }

        public TextWriter Log
        {
            get;
            set;
        }

        public TextWriter Out
        {
            get;
            set;
        }

        MethodDesc _mainMethod;

        internal MethodDesc MainMethod
        {
            get
            {
                return _mainMethod;
            }
        }

        internal IEnumerable<RegisteredType> RegisteredTypes
        {
            get
            {
                return _registeredTypes.Values;
            }
        }

        internal RegisteredType GetRegisteredType(TypeDesc type)
        {
            RegisteredType existingRegistration;
            if (_registeredTypes.TryGetValue(type, out existingRegistration))
                return existingRegistration;

            RegisteredType registration = new RegisteredType() { Type = type };
            _registeredTypes.Add(type, registration);

            // Register all base types too
            var baseType = type.BaseType;
            if (baseType != null)
                GetRegisteredType(baseType);

            return registration;
        }

        internal RegisteredMethod GetRegisteredMethod(MethodDesc method)
        {
            RegisteredMethod existingRegistration;
            if (_registeredMethods.TryGetValue(method, out existingRegistration))
                return existingRegistration;

            RegisteredMethod registration = new RegisteredMethod() { Method = method };
            _registeredMethods.Add(method, registration);

            GetRegisteredType(method.OwningType);

            return registration;
        }

        internal RegisteredField GetRegisteredField(FieldDesc field)
        {
            RegisteredField existingRegistration;
            if (_registeredFields.TryGetValue(field, out existingRegistration))
                return existingRegistration;

            RegisteredField registration = new RegisteredField() { Field = field };
            _registeredFields.Add(field, registration);

            GetRegisteredType(field.OwningType);

            return registration;
        }

        enum SpecialMethodKind
        {
            Unknown,
            PInvoke,
            RuntimeImport,
            Intrinsic
        };

        SpecialMethodKind DetectSpecialMethodKind(MethodDesc method)
        {
            if (method is EcmaMethod)
            {
                if (((EcmaMethod)method).IsPInvoke())
                {
                    return SpecialMethodKind.PInvoke;
                }
                else if (((EcmaMethod)method).HasCustomAttribute("System.Runtime.RuntimeImportAttribute"))
                {
                    Log.WriteLine("RuntimeImport: " + method.ToString());
                    return SpecialMethodKind.RuntimeImport;
                }
                else if (((EcmaMethod)method).HasCustomAttribute("System.Runtime.CompilerServices.IntrinsicAttribute"))
                {
                    Log.WriteLine("Intrinsic: " + method.ToString());
                    return SpecialMethodKind.Intrinsic;
                }
                else if (((EcmaMethod)method).HasCustomAttribute("System.Runtime.InteropServices.NativeCallableAttribute"))
                {
                    Log.WriteLine("NativeCallable: " + method.ToString());
                    // TODO: add reverse pinvoke callout
                    throw new NotImplementedException();
                }
            }
            return SpecialMethodKind.Unknown;
        }

        ILProvider _ilProvider = new ILProvider();

        public MethodIL GetMethodIL(MethodDesc method)
        {
            return _ilProvider.GetMethodIL(method);
        }

        void CompileMethod(MethodDesc method)
        {
            string methodName = method.ToString();
            Log.WriteLine("Compiling " + methodName);

            SpecialMethodKind kind = DetectSpecialMethodKind(method);

            if (kind == SpecialMethodKind.Unknown || kind == SpecialMethodKind.Intrinsic)
            {
                var methodIL = _ilProvider.GetMethodIL(method);
                if (methodIL == null)
                    return;

                var methodCode = _corInfo.CompileMethod(method);

                GetRegisteredMethod(method).MethodCode = methodCode;
            }
        }

        void CompileMethods()
        {
            var pendingMethods = _methodsThatNeedsCompilation;
            _methodsThatNeedsCompilation = null;

            foreach (MethodDesc method in pendingMethods)
            {
                if (_options.Cpp)
                {
                    _cppWriter.CompileMethod(method);
                }
                else
                {
                    CompileMethod(method);
                }
           }
        }

        void ExpandVirtualMethods()
        {
            // Take a snapshot of _registeredTypes - new registered types can be added during the expansion
            foreach (var reg in _registeredTypes.Values.ToArray())
            {
                if (!reg.Constructed)
                    continue;

                TypeDesc declType = reg.Type;
                while (declType != null)
                {
                    var declReg = GetRegisteredType(declType);
                    if (declReg.VirtualSlots != null)
                    {
                        for (int i = 0; i < declReg.VirtualSlots.Count; i++)
                        {
                            MethodDesc declMethod = declReg.VirtualSlots[i];

                            AddMethod(ResolveVirtualMethod(reg.Type, declMethod));
                        }
                    }

                    declType = declType.BaseType;
                }
            }
        }

        CorInfoImpl _corInfo;

        public void CompileSingleFile(MethodDesc mainMethod)
        {
            if (_options.Cpp)
            {
                _cppWriter = new CppCodeGen.CppWriter(this);
            }
            else
            {
                _corInfo = new CorInfoImpl(this);
            }

            _mainMethod = mainMethod;
            AddMethod(mainMethod);

            while (_methodsThatNeedsCompilation != null)
            {
                CompileMethods();

                ExpandVirtualMethods();
            }

            if (_options.Cpp)
            {
                _cppWriter.OutputCode();
            }
            else
            {
                OutputCode();
            }
        }

        public void AddMethod(MethodDesc method)
        {
            RegisteredMethod reg = GetRegisteredMethod(method);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            if (_methodsThatNeedsCompilation == null)
                _methodsThatNeedsCompilation = new List<MethodDesc>();
            _methodsThatNeedsCompilation.Add(method);

            GetMangledMethodName(method);

            if (_options.Cpp)
            {
                // Precreate name to ensure that all types referenced by signatures are present
                GetMangledTypeName(method.OwningType);
                var signature = method.Signature;
                GetMangledTypeName(signature.ReturnType);
                for (int i = 0; i < signature.Length; i++)
                    GetMangledTypeName(signature[i]);
            }
        }

        public void AddVirtualSlot(MethodDesc method)
        {
            RegisteredType reg = GetRegisteredType(method.OwningType);

            if (reg.VirtualSlots == null)
                reg.VirtualSlots = new List<MethodDesc>();

            for (int i = 0; i < reg.VirtualSlots.Count; i++)
            {
                if (reg.VirtualSlots[i] == method)
                    return;
            }

            reg.VirtualSlots.Add(method);
        }

        public void MarkAsConstructed(TypeDesc type)
        {
            GetRegisteredType(type).Constructed = true;
        }

        public void AddType(TypeDesc type)
        {
            RegisteredType reg = GetRegisteredType(type);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            TypeDesc baseType = type.BaseType;
            if (baseType != null)
                AddType(baseType);
            if (type.IsArray)
                AddType(((ArrayType)type).ElementType);
        }

        public void AddField(FieldDesc field)
        {
            RegisteredField reg = GetRegisteredField(field);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            if (_options.Cpp)
            {
                // Precreate name to ensure that all types referenced by signatures are present
                GetMangledTypeName(field.OwningType);
                GetMangledTypeName(field.FieldType);
            }
        }

        internal MethodDesc ResolveVirtualMethod(TypeDesc implType, MethodDesc declMethod)
        {
            return VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestDefType());
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
            var reg = GetRegisteredType(type);

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
                    if (_options.Cpp)
                        mangledName = _cppWriter.GetCppSignatureTypeName(((ByRefType)type).ParameterType) + "*";
                    else
                        mangledName = GetMangledTypeName(((ByRefType)type).ParameterType) + "__ByRef";
                    break;
                case TypeFlags.Pointer:
                    if (_options.Cpp)
                        mangledName = _cppWriter.GetCppSignatureTypeName(((PointerType)type).ParameterType) + "*";
                    else
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

                    mangledName = mangledName.Replace(".", _options.Cpp ? "::" : "_");

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
            var reg = GetRegisteredMethod(method);

            string mangledName = reg.MangledName;
            if (mangledName != null)
                return mangledName;

            RegisteredType regType = GetRegisteredType(method.OwningType);

            mangledName = SanitizeName(method.Name);

            mangledName = mangledName.Replace(".", "_"); // To handle names like .ctor

            if (!_options.Cpp)
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

        struct ReadyToRunHelperKey : IEquatable<ReadyToRunHelperKey>
        {
            ReadyToRunHelperId _id;
            Object _obj;

            public ReadyToRunHelperKey(ReadyToRunHelperId id, Object obj)
            {
                _id = id;
                _obj = obj;
            }

            public bool Equals(ReadyToRunHelperKey other)
            {
                return (_id == other._id) && ReferenceEquals(_obj, other._obj);
            }

            public override int GetHashCode()
            {
                return _id.GetHashCode() ^ _obj.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (!(obj is ReadyToRunHelperKey))
                    return false;

                return Equals((ReadyToRunHelperKey)obj);
            }
        }

        Dictionary<ReadyToRunHelperKey, ReadyToRunHelper> _readyToRunHelpers = new Dictionary<ReadyToRunHelperKey, ReadyToRunHelper>();

        public Object GetReadyToRunHelper(ReadyToRunHelperId id, Object target)
        {
            ReadyToRunHelper helper;

            ReadyToRunHelperKey key = new ReadyToRunHelperKey(id, target);
            if (!_readyToRunHelpers.TryGetValue(key, out helper))
                _readyToRunHelpers.Add(key, helper = new ReadyToRunHelper(this, id, target));

            return helper;
        }
    }
}
