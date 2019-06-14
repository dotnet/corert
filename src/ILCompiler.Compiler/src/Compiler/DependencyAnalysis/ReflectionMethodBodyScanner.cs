// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.IL;
using Internal.TypeSystem;

using AssemblyName = System.Reflection.AssemblyName;
using StringBuilder = System.Text.StringBuilder;
using DependencyList = ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.DependencyList;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Simple scanner that scans method bodies for common reflection patterns
    /// and adds the necessary dependencies to the graph.
    /// </summary>
    internal static class ReflectionMethodBodyScanner
    {
        public static void Scan(ref DependencyList list, NodeFactory factory, MethodIL methodIL)
        {
            ILReader reader = new ILReader(methodIL.GetILBytes());

            Tracker tracker = new Tracker(methodIL);

            // The algorithm here is really primitive: we scan the IL forward in a single pass, remembering
            // the last type/string/token we saw.
            //
            // We then intrinsically recognize a couple methods that consume this information.
            //
            // This has obvious problems since we don't have exact knowledge of the parameters passed
            // (something being in front of a call doesn't mean it's a parameter to the call). But since
            // this is a heuristic, it's okay. We want this to be as fast as possible.
            //
            // The main purposes of this scanner is to make following patterns work:
            //
            // * Enum.GetValues(typeof(Foo)) - this is very common and we need to make sure Foo[] is compiled.
            // * Type.GetType("Foo, Bar").GetMethod("Blah") - framework uses this to work around layering problems.
            // * typeof(Foo<>).MakeGenericType(arg).GetMethod("Blah") - used in e.g. LINQ expressions implementation

            while (reader.HasNext)
            {
                ILOpcode opcode = reader.ReadILOpcode();
                switch (opcode)
                {
                    case ILOpcode.ldstr:
                        tracker.TrackStringToken(reader.ReadILToken());
                        break;

                    case ILOpcode.ldtoken:
                        tracker.TrackLdTokenToken(reader.ReadILToken());
                        break;

                    case ILOpcode.call:
                    case ILOpcode.callvirt:
                        var method = methodIL.GetObject(reader.ReadILToken()) as MethodDesc;
                        if (method != null)
                        {
                            HandleCall(ref list, factory, methodIL, method, ref tracker);
                        }
                        break;

                    default:
                        reader.Skip(opcode);
                        break;
                }
            }
        }

        private static void HandleCall(ref DependencyList list, NodeFactory factory, MethodIL methodIL, MethodDesc methodCalled, ref Tracker tracker)
        {
            switch (methodCalled.Name)
            {
                // Enum.GetValues(Type) needs array of that type
                case "GetValues" when methodCalled.OwningType == factory.TypeSystemContext.GetWellKnownType(WellKnownType.Enum):
                    {
                        TypeDesc type = tracker.GetLastType();
                        if (type != null && type.IsEnum && !type.IsGenericDefinition /* generic enums! */)
                        {
                            list = list ?? new DependencyList();
                            list.Add(factory.ConstructedTypeSymbol(type.MakeArrayType()), "Enum.GetValues");
                        }
                    }
                    break;

                // Type.GetType(string...) needs the type with the given name
                case "GetType" when methodCalled.OwningType.IsSystemType() && methodCalled.Signature.Length > 0:
                    {
                        string name = tracker.GetLastString();
                        if (name != null
                            && methodIL.OwningMethod.OwningType is MetadataType mdType
                            && ResolveType(name, mdType.Module, out TypeDesc type, out ModuleDesc referenceModule)
                            && !factory.MetadataManager.IsReflectionBlocked(type))
                        {
                            const string reason = "Type.GetType";
                            list = list ?? new DependencyList();
                            list.Add(factory.MaximallyConstructableType(type), reason);

                            // Also add module metadata in case this reference was through a type forward
                            if (factory.MetadataManager.CanGenerateMetadata(referenceModule.GetGlobalModuleType()))
                                list.Add(factory.ModuleMetadata(referenceModule), reason);

                            // Opportunistically remember the type so that it flows to Type.GetMethod if needed.
                            tracker.TrackType(type);
                        }
                    }
                    break;

                // Type.GetMethod(string...)
                case "GetMethod" when methodCalled.OwningType.IsSystemType():
                    {
                        string name = tracker.GetLastString();
                        TypeDesc type = tracker.GetLastType();
                        if (name != null
                            && type != null
                            && !factory.MetadataManager.IsReflectionBlocked(type))
                        {
                            if (type.IsGenericDefinition)
                            {
                                Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(type.Instantiation, allowCanon: false);
                                if (inst.IsNull)
                                    break;
                                type = ((MetadataType)type).MakeInstantiatedType(inst);
                                list = list ?? new DependencyList();
                                list.Add(factory.MaximallyConstructableType(type), "Type.GetMethod");
                            }

                            MethodDesc reflectedMethod = type.GetMethod(name, null);
                            if (reflectedMethod != null
                                && !factory.MetadataManager.IsReflectionBlocked(reflectedMethod))
                            {
                                if (reflectedMethod.HasInstantiation)
                                {
                                    // Don't want to accidentally get Foo<__Canon>.Bar<object>()
                                    if (reflectedMethod.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any))
                                        break;

                                    Instantiation inst = TypeExtensions.GetInstantiationThatMeetsConstraints(reflectedMethod.Instantiation, allowCanon: false);
                                    if (inst.IsNull)
                                        break;
                                    reflectedMethod = reflectedMethod.MakeInstantiatedMethod(inst);
                                }

                                const string reason = "Type.GetMethod";
                                list = list ?? new DependencyList();
                                if (reflectedMethod.IsVirtual)
                                    RootVirtualMethodForReflection(ref list, factory, reflectedMethod, reason);

                                if (!reflectedMethod.IsAbstract)
                                {
                                    list.Add(factory.CanonicalEntrypoint(reflectedMethod), reason);
                                    if (reflectedMethod.HasInstantiation
                                        && reflectedMethod != reflectedMethod.GetCanonMethodTarget(CanonicalFormKind.Specific))
                                        list.Add(factory.MethodGenericDictionary(reflectedMethod), reason);
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private static bool ResolveType(string name, ModuleDesc callingModule, out TypeDesc type, out ModuleDesc referenceModule)
        {
            // This can do enough resolution to resolve "Foo" or "Foo, Assembly, PublicKeyToken=...".
            // The reflection resolution rules are complicated. This is only needed for a heuristic,
            // not for correctness, so this shortcut is okay.

            type = null;
            referenceModule = null;

            int i = 0;

            // Consume type name part
            StringBuilder typeName = new StringBuilder();
            StringBuilder typeNamespace = new StringBuilder();
            while (i < name.Length && (char.IsLetterOrDigit(name[i]) || name[i] == '.' || name[i] == '`'))
            {
                if (name[i] == '.')
                {
                    if (typeNamespace.Length > 0)
                        typeNamespace.Append('.');
                    typeNamespace.Append(typeName);
                    typeName.Clear();
                }
                else
                {
                    typeName.Append(name[i]);
                }
                i++;
            }

            // Consume any comma or white space
            while (i < name.Length && (name[i] == ' ' || name[i] == ','))
            {
                i++;
            }

            // Consume assembly name
            StringBuilder assemblyName = new StringBuilder();
            while (i < name.Length && (char.IsLetterOrDigit(name[i]) || name[i] == '.'))
            {
                assemblyName.Append(name[i]);
                i++;
            }

            TypeSystemContext context = callingModule.Context;

            // If the name was assembly-qualified, resolve the assembly
            // If it wasn't qualified, we resolve in the calling assembly

            referenceModule = callingModule;
            if (assemblyName.Length > 0)
            {
                referenceModule = context.ResolveAssembly(new AssemblyName(assemblyName.ToString()), false);
                if (referenceModule == null)
                    return false;
            }

            // Resolve type in the assembly
            type = referenceModule.GetType(typeNamespace.ToString(), typeName.ToString(), false);
            
            // If it didn't resolve and wasn't assembly-qualified, we also try mscorlib
            if (type == null && assemblyName.Length == 0)
            {
                referenceModule = context.ResolveAssembly(new AssemblyName("mscorlib"), false);
                type = referenceModule.GetType(typeNamespace.ToString(), typeName.ToString(), false);
            }
            
            return type != null;
        }

        private static void RootVirtualMethodForReflection(ref DependencyList list, NodeFactory factory, MethodDesc method, string reason)
        {
            if (method.HasInstantiation)
            {
                list.Add(factory.GVMDependencies(method), reason);
            }
            else
            {
                // Virtual method use is tracked on the slot defining method only.
                MethodDesc slotDefiningMethod = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method);
                if (!factory.VTable(slotDefiningMethod.OwningType).HasFixedSlots)
                    list.Add(factory.VirtualMethodUse(slotDefiningMethod), reason);
            }

            if (method.IsAbstract)
            {
                list.Add(factory.ReflectableMethod(method), reason);
            }
        }

        private static bool IsSystemType(this TypeDesc type)
        {
            return type is MetadataType mdType &&
                mdType.Name == "Type" &&
                mdType.Namespace == "System" &&
                mdType.Module == type.Context.SystemModule;
        }

        private struct Tracker
        {
            public readonly MethodIL _methodIL;

            private int _lastStringToken;
            private int _lastLdTokenToken;
            private TypeDesc _lastType;

            public Tracker(MethodIL methodIL)
            {
                _methodIL = methodIL;

                _lastStringToken = 0;
                _lastLdTokenToken = 0;
                _lastType = null;
            }

            public void TrackStringToken(int value)
            {
                _lastStringToken = value;
            }

            public void TrackLdTokenToken(int value)
            {
                _lastLdTokenToken = value;
                _lastType = null;
            }

            public void TrackType(TypeDesc value)
            {
                _lastType = value;
            }

            public TypeDesc GetLastType()
            {
                TypeDesc result = _lastType;
                if (result == null)
                {
                    if (_lastLdTokenToken != 0)
                    {
                        result = _methodIL.GetObject(_lastLdTokenToken) as TypeDesc;
                        _lastLdTokenToken = 0;
                    }
                }
                else
                {
                    _lastLdTokenToken = 0;
                    _lastType = null;
                }

                return result;
            }

            public string GetLastString()
            {
                string result = _lastStringToken == 0 ? null : _methodIL.GetObject(_lastStringToken) as string;
                _lastStringToken = 0;
                return result;
            }
        }

    }
}
