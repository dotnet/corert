# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
This is a script that I wrote while trying to automatically generate the equivalent of MetaInfo
for desktop CLR. It did not work out very well, and is largely replaced by CsWalkerGen3.py, which
simply generates an event-based visitor pattern for walking the metadata. The manually authored
portion for the MetaInfo equivalent functionality is located in fx/src/tools/metadata/MdWalker.
"""

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
# from CsNativeFormatGen2 import *
from odict import odict

customWalkers = set(
    ['MetadataString', 'TypeSpecification', 'Method', 'Field', 'Property', 'TypeVariableSignature', 'MethodTypeVariableSignature', 'TypeSpecification'] + \
    filter(lambda rName: rName.endswith('Signature'), sd.recordSchema.iterkeys()))

#==========================================================================================================
class AttrDict(odict):
    @staticmethod
    def RaiseEntryNotFound(name):
        raise Exception("AttrDict: entry '{}' not found.".format(name))

    def __init__(self, default = lambda x: TypeRef(x), *args, **kv):
        super(AttrDict, self).__init__(*args, **kv)
        self.__default = default
        
    def get(self, name, default = None):
        value = super(AttrDict, self).get(name)
        if value:
            return value

        if type(name) != str:
            default = default or name
            name = str(name)

        if type(name) != str:
            raise Exception('unexpected argument type {0} ({1})'.format(type(name), name))

        return self.setdefault(name, default or self.__default(name))
        
    def __getitem__(self, name):
        return super(AttrDict, self).get(name, None) or self.get(name)
        
    def __setitem__(self, name, value):
        super(AttrDict, self).__setitem__(name, value)

    def __getattr__(self, name):
        if name in self:
            return super(AttrDict, self).get(name)
        elif name in self.__dict__:
            return self.__dict__[name]
        else:
            return self.get(name)

    def __setattr__(self, name, value):
        if isinstance(value, TypeDefOrRef):
            self.__setitem__(name, value)
        else:
            self.__dict__[name] = value

#==========================================================================================================
class BaseWalker(TypeDef):
    def __init__(self, name, types, flags = TypeFlags.Class, *args, **kv):
        super(BaseWalker, self).__init__(name, flags = flags, *args, **kv)
        self.Ty = types
        if (self.flags & TypeFlags.TypeMask) == 0:
            self.flags = self.flags | TypeFlags.Class
        
        # self.members.add(MethodDef(
            # 'Indent',
            # flags = AccessFlags.Private | MemberFlags.Static,
            # sig = ['void', [('int', 'indent')]],
            # body = 'for (int i = 0; i < indent * 4; ++i) Console.Write(" ");'))
        
    def CsTypeName(self):
        return str(self) + 'Extensions'
        
    def CsDefineMembers(self, iprint):
        super(BaseWalker, self).CsDefineMembers(iprint)
        

#==========================================================================================================
class RecordWalker(BaseWalker):
    def __init__(self, name, *args, **kv):
        super(RecordWalker, self).__init__(name, flags = AccessFlags.Public | TypeFlags.Static | TypeFlags.Partial, *args, **kv)
        self.handle = HandleWalker(self, *args, **kv)
        self.record = self

#==========================================================================================================
class HandleWalker(BaseWalker):
    def __init__(self, rec, *args, **kv):
        super(HandleWalker, self).__init__(str(rec) + 'Handle', flags = AccessFlags.Public | TypeFlags.Static | TypeFlags.Partial, *args, **kv)
        self.record = rec
        self.handle = self

#==========================================================================================================
class WalkerField(FieldDef):
    def __init__(self, name, fieldType, flags, parent, *args, **kv):
        super(WalkerField, self).__init__(name, fieldType, *args, **kv)
        self.schemaFlags = flags
        self.parent = parent
        
    def CsDefine(self, iprint):
        pass
        
#==========================================================================================================
class RecordWalkerMethod(MethodDef):
    def FindIdentityField(self):
        for field in filter(lambda m: isinstance(m, FieldDef), self.parentScope.members):
            if field.schemaFlags.IsIdentity():
                return field
        for field in filter(lambda m: isinstance(m, FieldDef), self.parentScope.members):
            if str(field) == 'Name':
                return field

    def CsDefineBody(self, iprint):
        indent = 'MdWalkerHelpers.Indent(indent); '
        iprint('\n{')
        identField = self.FindIdentityField()
        if identField:
            iprint(1, r'Console.Write("\"{{0}}\" (0x{{2}}){{1}}", record.{}.GetString(reader), (deep || newline ? "\n" : ""), record.Handle.ToString());'.format(identField))
        else:
            iprint(1, 'Console.Write("{{0}} ({{1}}){{2}}", Enum.GetName(typeof(HandleType), record.Handle.ToHandle(reader).GetHandleType(reader)), record.Handle, (deep || newline ? "\\n" : ""));'.format())
        if not str(self.parentScope).startswith('Constant'):
            iprint(1, 'if (!deep) return;')
        iprint(1, '{}Console.WriteLine("{{");'.format(indent))
        iprint(1, '++indent;')
        for field in filter(lambda m: isinstance(m, FieldDef), self.parentScope.members):
            if field == identField:
                continue
            if field.schemaFlags.IsRef():
                if field.schemaFlags.IsCollection():
                    iprint(1, 'if (record.{}.Count() != 0)'.format(field))
                else:
                    iprint(1, 'if (!record.{}.IsNull(reader))'.format(field))
            iprint(1, '{')
            if str(field.fieldType) == 'Handle':
                iprint(2, 'var handleType = Enum.GetName(typeof(HandleType), record.{}.GetHandleType(reader));'.format(field))
                iprint(2, '{1}Console.Write("{{0}}Handle {0} = ", handleType);'.format(field, indent))
            else:
                iprint(2, '{2}Console.Write("{0} {1} = ");'.format(field.fieldType, field, indent))
            deep = 'true && deep' if field.schemaFlags.IsChild() else 'false && deep'
            if field.schemaFlags.IsRef():
                if field.schemaFlags.IsCollection():
                    iprint(2, '''Console.WriteLine();
MdWalkerHelpers.Indent(indent); Console.WriteLine("{{");
foreach (var entry in record.{0})
{{
    MdWalkerHelpers.Indent(indent + 1);
    entry.Walk(reader, indent + 1, {1}, newline);
}}
MdWalkerHelpers.Indent(indent); Console.WriteLine("}}");'''.format(field, deep))
                else:
                    iprint(2, 'record.{}.Walk(reader, indent, {}, newline);'.format(field, deep))
            else:
                iprint(2, 'Console.WriteLine("{{0}}", record.{1});'.format(field.fieldType, field, indent))
            iprint(1, '}')
        iprint(1, '\n--indent;')
        iprint(1, '{}Console.WriteLine("}}");'.format(indent))
        iprint('}} // {}'.format(self))

#==========================================================================================================
class HandleWalkerMethod(MethodDef):
    def CsDefineBody(self, iprint):
        iprint('\n{')
        iprint(1, 'if (handle.IsNull(reader)) { Console.Write("<null>" + (newline ? "\\n" : "")); return; }')
        iprint(1, 'handle.Get{}(reader).Walk(reader, indent, deep, newline);'.format(self.parentScope.record))
        iprint('}} // {}'.format(self))

#==========================================================================================================
class WalkerGen(object):
    #------------------------------------------------------------------------------------------------------
    def PublishWellKnownTypes(self):
        # Intrinsics
        for tName,tValueTypeName in sd.primitiveTypes.iteritems():
            t = TypeRef(tName)
            t.valueTypeName = tValueTypeName
            self.Ty[tName] = t

        # Flags
        for (eName, eType) in sd.enumTypes.iteritems():
            self.Ty[eName] = EnumDef(eName, underlyingType = self.Ty[eType])
            
        # Some standard .NET types
        self.Ty.IEnumerable = InterfaceDef('IEnumerable')
        self.Ty.IEnumerable.parentScope = 'System.Collections'
        self.Ty.IEnumerator = InterfaceDef('IEnumerator', parentScope = 'System.Collections')
        self.Ty.IEnumerator.parentScope = 'System.Collections'
        self.Ty.IEnumerableT = InterfaceDef('IEnumerable', typeParams = ['T'], interfaces = [self.Ty.IEnumerable])
        self.Ty.IEnumeratorT = InterfaceDef('IEnumerator', typeParams = ['T'], interfaces = [self.Ty.IEnumerator])
        self.Ty.Dictionary = ClassDef('Dictionary', typeParams = ['K', 'V'])
        self.Ty.List = ClassDef('List', typeParams = ['T'])

    #------------------------------------------------------------------------------------------------------
    def __init__(self):
        self.nsReader = 'Internal.Metadata.NativeFormat'

        self.Ty = AttrDict()
        self.PublishWellKnownTypes()
        
        self.Ty.Handle = HandleWalker('', self.Ty)
        self.Ty.Record = ClassDef('Record')
        self.Ty.Record.handle = self.Ty.Handle
        self.Ty.Record.record = self.Ty.Record
        self.Ty.Handle.record = self.Ty.Record
        self.Ty.Handle.handle = self.Ty.Handle
        
        self.Ty.Handle.Walker = MethodDef(
            'Walk',
            flags = AccessFlags.Public | MemberFlags.Extension,
            sig = [self.Ty.void, [(self.Ty.Handle, 'handle'), (self.Ty.MetadataReader, 'reader'), (self.Ty.int, 'indent'), (self.Ty.bool, 'deep'), (self.Ty.bool, 'newline = true')]],
            body = '''
switch (handle.GetHandleType(reader))
{
case HandleType.Null:
    Console.Write("<null>" + (newline ? "\\n" : ""));
    break;''')
        self.Ty.Handle.members.add(self.Ty.Handle.Walker)
            
        for (rName, rMembers) in sd.recordSchema.iteritems():
            rec = RecordWalker(rName, self.Ty)
            hnd = rec.handle
            self.Ty[str(rec)] = rec
            self.Ty[str(hnd)] = hnd
            self.Ty.Handle.Walker.body += '\ncase HandleType.{}:\n    handle.To{}(reader).Walk(reader, indent, deep, newline);\n    break;'.format(rec, hnd)
            
        self.Ty.Handle.Walker.body += '''
default:
    throw new ArgumentException();
} '''

        for (rName, rMembers) in sd.recordSchema.iteritems():
            rec = self.Ty[rName]
            rec.members.add(RecordWalkerMethod(
                'Walk' if str(rec) not in customWalkers else 'ReferenceWalk',
                flags = AccessFlags.Public | MemberFlags.Static | MemberFlags.Extension,
                sig = [self.Ty.void, [(rec, 'record'), (self.Ty.MetadataReader, 'reader'), (self.Ty.int, 'indent'), (self.Ty.bool, 'deep'), (self.Ty.bool, 'newline = true')]]
            ))
            
            if str(rec.handle) not in customWalkers:
                rec.handle.members.add(HandleWalkerMethod(
                    'Walk',
                    flags = AccessFlags.Public | MemberFlags.Static | MemberFlags.Extension,
                    sig = [self.Ty.void, [(rec.handle, 'handle'), (self.Ty.MetadataReader, 'reader'), (self.Ty.int, 'indent'), (self.Ty.bool, 'deep'), (self.Ty.bool, 'newline = true')]]
                ))

            for (mName, mType, mFlags) in rMembers:
                if type(mType) == tuple:
                    mType = 'Record'
                fieldType = self.Ty[mType]
                if mFlags.IsRef():
                    fieldType = fieldType.handle
                if mFlags.IsCollection():
                    fieldType = TypeInst(self.Ty.IEnumerableT, fieldType)
                rec.members.add(WalkerField(mName, fieldType, mFlags, rec))

    #------------------------------------------------------------------------------------------------------
    def CsEmitSource(self):
        ns = NamespaceDef('System.Reflection.Metadata.Test')
        
        for walker in filter(lambda m: isinstance(m, BaseWalker), self.Ty.itervalues()):
            ns.members.add(walker)

        with open(r'..\..\..\PnToolChain\Metadata\MdWalker\MdWalkerGen.cs', 'w') as output :
            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('#pragma warning disable 649')
            iprint()
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
    WalkerGen().CsEmitSource()
