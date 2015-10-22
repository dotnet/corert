// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILToNative
{
    public partial class Compilation
    {
        void OutputCode()
        {
            Out.WriteLine(".text");

            foreach (var t in _registeredTypes.Values)
            {
                if (t.Methods != null)
                {
                    foreach (var m in t.Methods)
                    {
                        if (m.MethodCode != null)
                            OutputMethodCode(m);
                    }
                }
            }

            Out.WriteLine("//" + new string('-', 80));
            Out.WriteLine();

            OutputReadyToHelpers();
            OutputEETypes();

            Out.WriteLine();
            Out.WriteLine(".data");

            OutputStrings();

            Out.WriteLine();
            Out.WriteLine("// Non-GC statics");
            OutputNonGCStatics();

            Out.WriteLine();
            Out.WriteLine("// GC statics");
            OutputGCStatics();

            Out.Dispose();
        }

        void OutputMethodCode(RegisteredMethod m)
        {
            string mangledName = NameMangler.GetMangledMethodName(m.Method);

            Out.Write(".global ");
            Out.WriteLine(mangledName);

            Out.Write(mangledName);
            Out.WriteLine(":");

            var methodCode = (MethodCode)m.MethodCode;

            if (methodCode.Code == null)
            {
                Out.WriteLine("call DebugBreak");
                Out.WriteLine("ret");
                return;
            }

            Relocation[] relocs = methodCode.Relocs;
            int currentRelocIndex = 0;
            int nextRelocOffset = -1;

            byte[] code = methodCode.Code;

            if (relocs != null)
            {
                nextRelocOffset = relocs[currentRelocIndex].Offset;
                if (relocs[currentRelocIndex].RelocType == 0x10) // IMAGE_REL_BASED_REL32
                    nextRelocOffset--;
            }

            int lineLength = 0;
            for (int i = 0; i < code.Length; i++)
            {
                if (i == nextRelocOffset)
                {
                    if (lineLength > 0)
                    {
                        Out.WriteLine();
                        lineLength = 0;
                    }

                    Relocation reloc = relocs[currentRelocIndex];

                    Object target = reloc.Target;
                    string targetName;
                    if (target is MethodDesc)
                    {
                        targetName = NameMangler.GetMangledMethodName((MethodDesc)target);
                    }
                    else
                    if (target is ReadyToRunHelper)
                    {
                        targetName = ((ReadyToRunHelper)target).MangledName;
                    }
                    else
                    if (target is JitHelper)
                    {
                        targetName = ((JitHelper)target).MangledName;
                    }
                    else
                    if (target is string)
                    {
                        int id = AddToStringTable((string)target);
                        targetName = "__str" + id;
                    }
                    else
                    {
                        // TODO:
                        throw new NotImplementedException();
                    }

                    switch (reloc.RelocType)
                    {
                        case 0x0A: // IMAGE_REL_BASED_DIR64
                            Out.Write(".quad ");
                            Out.WriteLine(targetName);
                            i += 7;
                            break;

                        // REVIEW: I believe the JIT is emitting 0x3 instead of 0xA
                        // for x64, because emitter from x86 is ported for RyuJIT.
                        // I will consult with Bruce and if he agrees, I will delete
                        // this "case" duplicated by IMAGE_REL_BASED_DIR64.
                        case 0x03: // IMAGE_REL_BASED_HIGHLOW
                            Out.Write(".quad ");
                            Out.WriteLine(targetName);
                            i += 7;
                            break;

                        case 0x10:
                            if (code[i] != 0xE8) // call
                                throw new NotImplementedException();
                            Out.Write("call ");
                            Out.WriteLine(targetName);
                            i += 4;
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    currentRelocIndex++;
                    nextRelocOffset = -1;
                    if (currentRelocIndex < relocs.Length)
                    {
                        nextRelocOffset = relocs[currentRelocIndex].Offset;
                        if (relocs[currentRelocIndex].RelocType == 0x10) // IMAGE_REL_BASED_REL32
                            nextRelocOffset--;
                    }

                    continue;
                }

                if (lineLength == 0)
                {
                    Out.Write(".byte ");
                }
                else
                {
                    Out.Write(",");
                }

                Out.Write(code[i]);

                if (lineLength++ > 15)
                {
                    Out.WriteLine();
                    lineLength = 0;
                }
            }

            // TODO: ColdCode
            if (methodCode.ColdCode != null)
                throw new NotImplementedException();

            // TODO: ROData
            if (methodCode.ROData != null)
                throw new NotImplementedException();

            if (lineLength > 0)
                Out.WriteLine();
            Out.WriteLine();
        }

        void OutputReadyToHelpers()
        {
            foreach (var helper in _readyToRunHelpers.Values)
            {
                Out.Write(helper.MangledName);
                Out.WriteLine(":");

                switch (helper.Id)
                {
                    case ReadyToRunHelperId.NewHelper:
                        Out.Write("leaq __EEType_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rcx");

                        Out.WriteLine("jmp __allocate_object");
                        break;

                    case ReadyToRunHelperId.VirtualCall:
                        Out.WriteLine("movq (%rcx), %rax");
                        Out.Write("jmp *");

                        // TODO: More efficient lookup of the slot
                        {
                            MethodDesc method = (MethodDesc)helper.Target;
                            TypeDesc owningType = method.OwningType;

                            int baseSlots = 0;
                            var baseType = owningType.BaseType;

                            while (baseType != null)
                            {
                                var baseReg = GetRegisteredType(baseType);
                                if (baseReg.VirtualSlots != null)
                                    baseSlots += baseReg.VirtualSlots.Count;
                                baseType = baseType.BaseType;
                            }

                            var t = GetRegisteredType(owningType);
                            int methodSlot = -1;
                            for (int slot = 0; slot < t.VirtualSlots.Count; slot++)
                            {
                                if (t.VirtualSlots[slot] == method)
                                {
                                    methodSlot = slot;
                                    break;
                                }
                            }

                            Debug.Assert(methodSlot != -1);
                            Out.Write(16 /* sizeof(EEType */ + (baseSlots + methodSlot) * _typeSystemContext.Target.PointerSize);
                        }

                        Out.WriteLine("(%rax)");
                        break;

                    case ReadyToRunHelperId.IsInstanceOf:
                        Out.Write("leaq __EEType_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rdx");

                        Out.WriteLine("jmp __isinst_class");
                        break;

                    case ReadyToRunHelperId.CastClass:
                        Out.Write("leaq __EEType_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rdx");

                        Out.WriteLine("jmp __castclass_class");
                        break;

                    case ReadyToRunHelperId.NewArr1:
                        Out.Write("leaq __EEType_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rdx");

                        Out.WriteLine("jmp __allocate_array");
                        break;

                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        Out.Write("leaq __NonGCStaticBase_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rax");
                        Out.WriteLine("ret");
                        break;

                    case ReadyToRunHelperId.GetGCStaticBase:
                        Out.Write("leaq __GCStaticBase_");
                        Out.Write(NameMangler.GetMangledTypeName((TypeDesc)helper.Target));
                        Out.WriteLine("(%rip), %rax");
                        Out.WriteLine("mov (%rax), %rax");
                        Out.WriteLine("mov (%rax), %rax"); // RAX is now a GC pointer
                        Out.WriteLine("ret");
                        break;

                    default:
                        throw new NotImplementedException();
                }
                Out.WriteLine();
            }
        }

        void OutputEETypes()
        {
            foreach (RegisteredType t in _registeredTypes.Values)
            {
                if (!t.IncludedInCompilation)
                    continue;
                OutputEEType(t.Type, t.Constructed);
            }
        }

        void OutputEEType(TypeDesc type, bool isConstructed)
        {
            Out.WriteLine(".align 16");
            Out.Write("__EEType_");
            Out.Write(NameMangler.GetMangledTypeName(type));
            Out.WriteLine(":");

            if (type.IsArray && ((ArrayType)type).Rank == 1)
            {
                Out.Write(".word ");
                Out.WriteLine(type.GetElementSize());   // m_ComponentSize
                Out.WriteLine(".word 4");               // m_flags: IsArray(0x4)
                Out.WriteLine(".int 24");
            }
            else
            {
                Out.WriteLine(".int 0, 24");
            }

            if (type.BaseType != null)
            {
                Out.Write(".quad __EEType_");
                Out.WriteLine(NameMangler.GetMangledTypeName(type.BaseType));
            }
            else
            {
                Out.WriteLine(".quad 0");
            }

            if (isConstructed)
                OutputVirtualSlots(type, type);

            Out.WriteLine();
        }


        void OutputStrings()
        {
            var strType = TypeSystemContext.GetWellKnownType(WellKnownType.String);

            Out.Write(".global __EEType_");
            Out.WriteLine(NameMangler.GetMangledTypeName(strType));

            Out.WriteLine(".global __str_fixup");
            Out.WriteLine(".global __str_fixup_end");
            Out.WriteLine("__str_fixup:");
            foreach (var se in _stringTable)
            {
                Out.Write("__str");
                Out.Write(se.Value);
                Out.Write(": .quad __str_table_entry");
                Out.WriteLine(se.Value);
            }
            Out.WriteLine("__str_fixup_end:");

            Action<byte> byteWriter = (byte b) => { Out.WriteLine(".byte " + b); };
            AsmStringWriter nf = (_stringTable.Count > 0) ? new AsmStringWriter(byteWriter) : null;
            foreach (var se in _stringTable)
            {
                Out.Write("__str_table_entry");
                Out.Write(se.Value);
                Out.WriteLine(":");
                nf.WriteString(se.Key);               
            }
        }

        void OutputNonGCStatics()
        {
            foreach (var t in _registeredTypes.Values)
            {
                if (!t.IncludedInCompilation)
                    continue;

                var type = t.Type as MetadataType;
                if (type == null)
                    continue;

                if (type.NonGCStaticFieldSize == 0 && !type.HasStaticConstructor)
                    continue;

                // If the type has a class constructor, the GC statics section is prepended
                // by System.Runtime.CompilerServices.StaticClassConstructionContext.

                bool isAligned = false;

                if (type.HasStaticConstructor && _registeredMethods.ContainsKey(type.GetStaticConstructor()))
                {
                    int pointerSize = type.Context.Target.PointerSize;

                    // TODO: Assert that StaticClassConstructionContext type has the expected size and alignment
                    //       (need to make it a well known type?)
                    int alignmentRequired = Math.Max(type.NonGCStaticFieldAlignment, pointerSize);
                    int classConstructionContextSize = 2 * pointerSize;

                    Out.Write(".align ");
                    Out.WriteLine(alignmentRequired);

                    // Prepend the context to the existing fields without messing up the alignment of those fields.
                    int classConstructorContextStorageSize = AlignmentHelper.AlignUp(classConstructionContextSize, alignmentRequired);

                    // Add padding before the context if alignment forces us to do so
                    if (classConstructorContextStorageSize - classConstructionContextSize > 0)
                    {
                        Out.Write(".rept ");
                        Out.WriteLine(classConstructorContextStorageSize - classConstructionContextSize);
                    }

                    isAligned = true;

                    var cctorMethod = type.GetStaticConstructor();

                    Out.Write(".quad ");
                    Out.WriteLine(NameMangler.GetMangledMethodName(cctorMethod));
                    Out.WriteLine(".quad 0");
                }

                Out.Write("__NonGCStaticBase_");
                Out.Write(NameMangler.GetMangledTypeName(type));
                Out.WriteLine(":");

                if (type.NonGCStaticFieldSize > 0)
                {
                    if (!isAligned)
                    {
                        Out.Write(".align ");
                        Out.WriteLine(type.NonGCStaticFieldAlignment);
                    }
                    Out.Write(".rept ");
                    Out.WriteLine(type.NonGCStaticFieldSize);
                    Out.WriteLine(".byte 0");
                    Out.WriteLine(".endr");
                }

                Out.WriteLine();
            }
        }

        void OutputGCStatics()
        {
            // Emit an array of GCHandle-sized elements for each type with GC statics
            // Each element will be initially pointing at the pseudo EEType for the static.
            // At runtime, it will be replaced by a GC handle to the GC-heap allocated object.

            Out.WriteLine(".global __GCStaticRegionStart");
            Out.WriteLine("__GCStaticRegionStart:");

            foreach (var t in _registeredTypes.Values)
            {
                if (!t.IncludedInCompilation)
                    continue;

                var type = t.Type as MetadataType;
                if (type == null)
                    continue;

                if (type.GCStaticFieldSize > 0)
                {
                    Out.Write("__GCStaticBase_");
                    Out.Write(NameMangler.GetMangledTypeName(type));
                    Out.WriteLine(":");
                    Out.Write(".quad ");
                    Out.Write("__GCStaticEEType_");
                    Out.Write(NameMangler.GetMangledTypeName(type));
                    Out.WriteLine();
                }
            }        

            Out.WriteLine(".global __GCStaticRegionEnd");
            Out.WriteLine("__GCStaticRegionEnd:");

            // Next emit a GCDesc followed by the size of the region described by the GCDesc
            // for each type with GC statics.

            // It should be possible to intern these at some point.

            foreach (var t in _registeredTypes.Values)
            {
                if (!t.IncludedInCompilation)
                    continue;

                var type = t.Type as MetadataType;
                if (type == null)
                    continue;

                if (type.GCStaticFieldSize > 0)
                {
                    // numSeries
                    Out.WriteLine(".quad 0");

                    Out.Write("__GCStaticEEType_");
                    Out.Write(NameMangler.GetMangledTypeName(type));
                    Out.WriteLine(":");
                    Out.WriteLine(".int 0");
                    Out.Write(".int ");

                    // GC requires a minimum object size
                    int minimumObjectSize = type.Context.Target.PointerSize * 3;
                    int gcStaticSize = Math.Max(type.GCStaticFieldSize, minimumObjectSize);

                    Out.WriteLine(gcStaticSize);
                }
            }
        }

        void OutputVirtualSlots(TypeDesc implType, TypeDesc declType)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(implType, baseType);

            var reg = GetRegisteredType(declType);
            if (reg.VirtualSlots != null)
            {
                for (int i = 0; i < reg.VirtualSlots.Count; i++)
                {
                    MethodDesc declMethod = reg.VirtualSlots[i];

                    MethodDesc implMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestDefType());

                    Out.Write(".quad ");
                    Out.WriteLine(NameMangler.GetMangledMethodName(implMethod));
                }
            }
        }
    }
}
