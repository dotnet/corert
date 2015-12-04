# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
This script generates much of the implementation of  that provides a
"""

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import TypeContainer
from CsNativeFormatGen2 import PublishWellKnownTypes
from odict import odict

#==========================================================================================================
def AsHandle(rec):
    if isinstance(rec, sd.RecordDef):
        return AsHandle(rec.name)
    elif isinstance(rec, sd.MemberDef):
        return AsHandle(rec.typeName)
    elif type(rec) == tuple:
        return 'Handle'
    else:
        return rec + 'Handle'

#==========================================================================================================
class Walker(object):
    #------------------------------------------------------------------------------------------------------
    def __init__(self):
        self.csClass = None
        self.Ty = TypeContainer()
        PublishWellKnownTypes(self.Ty)
        
    #------------------------------------------------------------------------------------------------------
    def GenerateHandleVisitWorker(self):
        code = 'switch (handle.GetHandleType(_reader))\n{'
        for rec in sorted(sd.recordDefs.itervalues()):
            code += '\ncase HandleType.{0}:\n    Visit(handle.To{0}Handle(_reader), recurse);\n    break;'.format(rec.name)
        code += '\ndefault:\n    throw new ArgumentException();\n}'
        
        self.csClass.members.add(MethodDef(
            'Visit',
            flags = MemberFlags.Protected,
            sig = [self.Ty.void, [(self.Ty.Handle, 'handle'), (self.Ty.bool, 'recurse')]],
            body = code));
            
    #------------------------------------------------------------------------------------------------------
    def GenerateHandleVisitFunctions(self):
        for rec in sorted(sd.recordDefs.itervalues()):
            self.csClass.members.add(MethodDef(
                'Visit',
                flags = MemberFlags.Protected | MemberFlags.Virtual,
                sig = [self.Ty.void, [(self.Ty[AsHandle(rec)], 'handle'), (self.Ty.bool, 'recurse')]],
                body = '''
_visiting.Push(handle);
_visited.Add(handle);
Visit(handle.Get{0}(_reader), recurse);
_visiting.Pop();'''.format(rec.name)))

            self.csClass.members.add(MethodDef(
                'Visit',
                flags = MemberFlags.Protected | MemberFlags.Virtual,
                sig = [self.Ty.void, [(self.Ty[TypeInst(self.Ty.IEnumerableT, AsHandle(rec))], 'handles'), (self.Ty.bool, 'recurse')]],
                body = 'foreach (var handle in handles) Visit(handle, recurse);'));

    #------------------------------------------------------------------------------------------------------
    def GenerateRecordVisitFunctions(self):
        for rec in sorted(sd.recordDefs.itervalues()):
            stmts = list()
            stmts.append('''
if ({0}Event != null)
{{
    {0}Event(record);
}}'''.format(rec.name))

            # Don't do any recursion or queue any pending handles if 'recurse' is set to false
            stmts.append('if (!recurse) return;')

            for mem in sorted(rec.members, key = lambda m: '0' + m.name if m.name == 'Name' else m.name):
                if mem.flags.IsRef():
                    if mem.flags.IsChild():
                        stmts.append('Visit(record.{0}, recurse);'.format(mem.name))
                    else:
                        if mem.flags.IsSequence():
                            stmts.append('''
foreach (var handle in record.{0})
{{
    //Debug.Assert(((Handle)handle).GetHandleType(_reader) != HandleType.TypeDefinition);
    if (!_visited.Contains(handle))
        _pending.Add(handle);
}}'''.format(mem.name))
                        else:
                            stmts.append('''
if (!record.{0}.IsNull(_reader) && !_visited.Contains(record.{0}))
{{
    //Debug.Assert(((Handle)record.{0}).GetHandleType(_reader) != HandleType.TypeDefinition);
    _pending.Add(record.{0});
}}'''.format(mem.name))

            self.csClass.members.add(MethodDef(
                'Visit',
                flags = MemberFlags.Protected | MemberFlags.Virtual,
                sig = [self.Ty.void, [(self.Ty[rec.name], 'record'), (self.Ty.bool, 'recurse')]],
                body = '\n'.join(stmts)));

    #------------------------------------------------------------------------------------------------------
    def GenerateRecordVisitEvents(self):
        for rec in sorted(sd.recordDefs.itervalues()):
            # self.csClass.members.add(EventDef(
                # '{}BeginEvent'.format(rec),
                # self.Ty['VisitBeginHandler<{}>'.format(rec)],
                # flags = AccessFlags.Public))

            self.csClass.members.add(EventDef(
                '{}Event'.format(rec),
                self.Ty['VisitHandler<{}>'.format(rec)],
                flags = AccessFlags.Public))

            # self.csClass.members.add(EventDef(
                # '{}EndEvent'.format(rec),
                # self.Ty['VisitEndHandler<{}>'.format(rec)],
                # flags = AccessFlags.Public))

    #------------------------------------------------------------------------------------------------------
    def GenerateHandleDisplayWorker(self):
        code = 'switch (handle.GetHandleType(_reader))\n{'
        for rec in sorted(sd.recordDefs.itervalues()):
            code += '\ncase HandleType.{0}:\n    Display(handle.To{0}Handle(_reader).Get{0}(_reader));\n    break;'.format(rec.name)
        code += '\ndefault:\n    throw new ArgumentException();\n}'
        
        self.csClass.members.add(MethodDef(
            'Display',
            flags = MemberFlags.Public,
            sig = [self.Ty.void, [(self.Ty.Handle, 'handle')]],
            body = code));
            
    #------------------------------------------------------------------------------------------------------
    def GenerateHandleDisplayFunctions(self):
        for rec in sorted(sd.recordDefs.itervalues()):
            self.csClass.members.add(MethodDef(
                'Display',
                flags = MemberFlags.Public | MemberFlags.Virtual,
                sig = [self.Ty.void, [(self.Ty[AsHandle(rec)], 'handle')]],
                # body = 'Display(handle.Get{0}(_reader));'.format(rec)));
                body = '''
if (!_visited.Contains(handle))
{{
    _visited.Add(handle);
    Display(handle.Get{0}(_reader));
}}'''.format(rec.name)))

            self.csClass.members.add(MethodDef(
                'Display',
                flags = MemberFlags.Public | MemberFlags.Virtual,
                sig = [self.Ty.void, [(self.Ty[TypeInst(self.Ty.IEnumerableT, AsHandle(rec))], 'handles')]],
                body = 'foreach (var handle in handles) Display(handle);'));

    #------------------------------------------------------------------------------------------------------
    def GenerateRecordDisplayFunctions(self):
        for rec in sorted(sd.recordDefs.itervalues()):
            stmts = ['var sb = new System.Text.StringBuilder();']
            for mem in sorted(rec.members, key = lambda m: '0' + m.name if m.name == 'Name' else m.name):
                if mem.flags.IsRef():
                    pass
                # if mem.flags.IsNotPersisted():
                    # pass
                # elif mem.flags.IsSequence():
                    # if mem.flags.IsRef() and (mem.flags.IsChild() or mem.typeName == 'ConstantStringValue'):
                        # stmts.append('_sw.WriteLine("    {0} :\\n    [");\nforeach (var handle in record.{0}) Display(handle);\n_sw.WriteLine("\\n    ]");'.format(mem.name))
                    # else:
                        # stmts.append('_sw.WriteLine("    {0} :\\n    [");\nforeach (var handle in record.{0}) _sw.WriteLine("        {{0}}", handle.ToString());\n_sw.WriteLine("\\n    ]");'.format(mem.name))
                # elif mem.flags.IsRef() and (mem.flags.IsChild() or mem.typeName == 'ConstantStringValue'):
                    # stmts.append('_sw.Write("    {0} : ");\nDisplay(record.{0});'.format(mem.name))
                else:
                    stmts.append('sb.AppendFormat("    {0} : {{0}},\\n", record.{0}.ToString());'.format(mem.name, mem.typeName))
                    
            stmts.append('return sb.ToString();')
                    
            self.csClass.members.add(MethodDef(
                'Display',
                flags = MemberFlags.Public | MemberFlags.Virtual,
                sig = [self.Ty.string, [(self.Ty[str(rec)], 'record')]],
                body = '\n'.join(stmts)));
                
    #------------------------------------------------------------------------------------------------------
    def GenerateCsClass(self):
        if self.csClass == None:
            self.csClass = ClassDef(
                'Walker',
                flags = TypeFlags.Public | TypeFlags.Partial)
                

            self.GenerateHandleVisitWorker()
            self.GenerateHandleVisitFunctions()
            self.GenerateRecordVisitFunctions()
            self.GenerateRecordVisitEvents()

            # self.GenerateHandleDisplayWorker()
            # self.GenerateHandleDisplayFunctions()
            # self.GenerateRecordDisplayFunctions()
                
            self.Ty[self.csClass] = self.csClass

        return self.csClass

    #------------------------------------------------------------------------------------------------------
    def CsEmitSource(self):
        ns = NamespaceDef('MdWalker')
        ns.members.add(Walker().GenerateCsClass())
        
        with open(r'..\..\..\PnToolChain\Metadata\MdWalker\MdWalkerGen.cs', 'w') as output:
            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('using System;')
            iprint('using System.Linq;')
            iprint('using System.IO;')
            iprint('using System.Collections.Generic;')
            iprint('using System.Reflection;')
            iprint('using Internal.Metadata.NativeFormat;')
            iprint('using Debug = System.Diagnostics.Debug;')
            iprint()

            ns.CsDefine(iprint)

#==========================================================================================================
if __name__ == '__main__':
    Walker().CsEmitSource()
