# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import *
from odict import odict

class WriterGen(object):
    #==========================================================================================================
    def __init__(self):
        self.nsReader = 'Internal.Metadata.NativeFormat'
        
        self.Ty = TypeContainer()
        PublishWellKnownTypes(self.Ty)
            
        self.Ty.MetadataRecord = StructDef('MetadataRecord', flags = AccessFlags.Public | TypeFlags.Partial)
        self.Ty.Handle = self.Ty.MetadataRecord

        self.Ty.NativeWriter = ClassDef('NativeWriter')

        self.records = odict([(rName, self.CreateRecord(rName, rMembers)) for (rName,rMembers) in sd.recordSchema.iteritems()])

    #==========================================================================================================
    def IsRecord(self, t):
        return isinstance(t, TypeDef) and str(t) in sd.recordSchema.iterkeys()

    #==========================================================================================================
    def GetOrCreateType(self, ty, flags):
        if type(ty) == tuple:
            if not flags.IsRef(): raise Exception('Unexpected ty type.')
            ty = self.Ty.Handle

        if flags.IsArray():
            return self.Ty.get(ty + '[]', TypeRef(
                name = ty + '[]',
                flags = TypeFlags.Public | TypeFlags.Array,
                underlyingType = self.GetOrCreateType(ty, flags ^ sd.MemberDefFlags.Array)))

        if type(ty) == str:
            if ty == 'Handle':
                ty = 'MetadataRecord'
            if ty == 'Handle[]':
                ty = 'MetadataRecord[]'
            ty = self.Ty[ty]
            
        if flags.IsCollection() and not flags.IsRef() and not flags.IsArray():
            raise Exception('Unexpected collection element type "{}" (flags = {}.'.format(type(ty), flags))

        if flags.IsMap():
            # return self.Ty[TypeInst(self.Ty.Dictionary, self.Ty.string, ty)]
            return self.Ty[TypeInst(self.Ty.List, ty)]
        elif flags.IsList():
            return self.Ty[TypeInst(self.Ty.List, ty)]
        else:
            return ty

    #==========================================================================================================
    def ProcessRecordMemberForVisitor(self, rec, mName, mType, flags):
        if flags.IsRef():
            if flags.IsMap():
                if flags.IsChild():
                    # rec.onVisit.body += '\n{0} = visitor.Visit(this, {0}.AsEnumerable());'.format(mName)
                    rec.onVisit.body += '\n{0} = visitor.Visit(this, {0}.AsEnumerable());'.format(mName)
                else:
                    # rec.onVisit.body += '\nforeach (var key in {0}.Keys)\n    {0}[key] = visitor.Visit(this, {0}[key]);'.format(mName)
                    rec.onVisit.body += '\n{0} = {0}.Select(value => visitor.Visit(this, value)).ToList();'.format(mName)
            elif flags.IsSequence():
                if flags.IsChild():
                    rec.onVisit.body += '\n{0} = visitor.Visit(this, {0}.AsEnumerable());'.format(mName)
                else:
                    rec.onVisit.body += '\n{0} = {0}.Select(value => visitor.Visit(this, value)).ToList();'.format(mName)
            else:
                if flags.IsChild():
                    rec.onVisit.body += '\n{0} = visitor.Visit(this, {0}.AsSingleEnumerable()).FirstOrDefault();'.format(mName)
                else:
                    rec.onVisit.body += '\n{0} = visitor.Visit(this, {0});'.format(mName)
                
    #==========================================================================================================
    def ProcessRecordMember(self, rec, mName, mType, flags):
        members = list()
        if flags.IsNotPersisted():
            return members

        mTypeSet = mType if type(mType) == tuple else (mType,)
        mType = self.GetOrCreateType(mType, flags)
        mType.schemaFlags = flags
        
        field = FieldDef(mName, mType, flags = AccessFlags.Public | MemberFlags.Serialize)
        field.schemaFlags = flags
        members.append(field)
        
        if flags.IsCollection() and not flags.IsArray():
            field.autoInitialize = True

        if (flags.IsCompare() or not rec.defFlags.IsCustomCompare()):
            if flags.IsCollection():
                rec.mEquals.body += '\nif (!{0}.SequenceEqual(other.{0})) return false;'.format(field)
            elif str(field.fieldType) in sd.primitiveTypes or str(field.fieldType) in sd.enumTypes:
                rec.mEquals.body += '\nif ({0} != other.{0}) return false;'.format(field)
            else:
                rec.mEquals.body += '\nif (!Object.Equals({0}, other.{0})) return false;'.format(field)

            # Being very selective here to prevent reentrancy in GetHashCode.
            if str(field.fieldType) in sd.stringRecordTypes:
                rec.mHashCode.body += '\nhash = ((hash << 13) - (hash >> 19)) ^ ({0} == null ? 0 : {0}.GetHashCode());'.format(field)
            elif str(field.fieldType) == 'string':
                rec.mHashCode.body += '\nhash = ((hash << 13) - (hash >> 19)) ^ ({0} == null ? 0 : {0}.GetHashCode());'.format(field)
            elif str(field.fieldType) in sd.primitiveTypes or str(field.fieldType) in sd.enumTypes:
                rec.mHashCode.body += '\nhash = ((hash << 13) - (hash >> 19)) ^ {0}.GetHashCode();'.format(field)
            elif flags.IsArray() and (str(field.fieldType.underlyingType) in sd.primitiveTypes):
                rec.mHashCode.body += '''
if ({0} != null)
{{
    for (int i = 0; i < {0}.Length; i++)
    {{
        hash = ((hash << 13) - (hash >> 19)) ^ {0}[i].GetHashCode();
    }}
}}'''.format(field)
            elif flags.IsList() and flags.IsEnumerateForHashCode():
                rec.mHashCode.body += '''
if ({0} != null)
{{
    for (int i = 0; i < {0}.Count; i++)
    {{
        hash = ((hash << 13) - (hash >> 19)) ^ ({0}[i] == null ? 0 : {0}[i].GetHashCode());
    }}
}}'''.format(field)
            elif not flags.IsCollection():
                rec.mHashCode.body += '\nhash = ((hash << 13) - (hash >> 19)) ^ ({0} == null ? 0 : {0}.GetHashCode());'.format(field)
        
        if flags.IsRef() and len(mTypeSet) > 1:
            valueName = str(field)
            rec.recordEmit.body += '\nDebug.Assert('
            if flags.IsSequence():
                rec.recordEmit.body += '{}.TrueForAll(handle => '.format(field)
                valueName = 'handle'
            rec.recordEmit.body += ' ||\n    '.join(['{} == null'.format(valueName)] + ['{}.HandleType == HandleType.{}'.format(valueName, ty) for ty in mTypeSet])
            if flags.IsSequence():
                rec.recordEmit.body += ')'
            rec.recordEmit.body += ');'
                
        if flags.IsRef() and flags.IsMap():
            # rec.recordEmit.body += '\nwriter.Write({}.Values);'.format(field)
            rec.recordEmit.body += '\nwriter.Write({0});'.format(field)
        else:
            rec.recordEmit.body += '\nwriter.Write({0});'.format(field)
            
        # if mName == "CustomAttributes":
            # rec.interfaces += [self.Ty.ICustomAttributeMetadataRecord]
            # members.append(MethodDef(
                # 'ICustomAttributeMetadataRecord.GetCustomAttributes',
                # flags = MemberFlags(0),
                # sig = [TypeInst(self.Ty.IListT, self.Ty.CustomAttribute), []],
                # body = 'return CustomAttributes;'))

        return members

    #==========================================================================================================
    def CreateRecord(self, name, members):
        flags = AccessFlags.Public | TypeFlags.Partial | TypeFlags.Struct
        recordDef = sd.recordDefs[name]
        rec = ClassDef(name, self.Ty.get(recordDef.baseTypeName or 'MetadataRecord'), flags)
        rec.enumType = self.Ty.HandleType
        rec.enumValue = str(rec)
        rec.handle = self.Ty['{}Handle'.format(rec)]
        rec.defFlags = recordDef.flags
        self.Ty[name] = rec
        
        rec.ctor = CtorDef(body = '')
        rec.members.add(rec.ctor)
        
        rec.members.add(PropertyDef(
            'HandleType',
            self.Ty.HandleType,
            flags = AccessFlags.Public | MemberFlags.Override,
            getter = PropertyGetter(
                body = 'return HandleType.{0};'.format(str(rec)))))

        rec.onVisit = rec.members.add(MethodDef(
            'Visit',
            flags = AccessFlags.Internal | MemberFlags.Override,
            sig = [self.Ty.void, [(self.Ty.IRecordVisitor, 'visitor')]],
            body = ''))

        rec.mEquals = rec.members.add(MethodDef(
            'Equals',
            flags = AccessFlags.Public | MemberFlags.Override | MemberFlags.Sealed,
            sig = [self.Ty.bool, [(self.Ty.Object, 'obj')]],
            body = '''
if (Object.ReferenceEquals(this, obj)) return true;
var other = obj as {0};
if (other == null) return false;'''.format(rec)))

        if rec.defFlags.IsReentrantEquals():
            rec.members.add(FieldDef(
                '_equalsReentrancyGuard',
                fieldType = self.Ty[TypeInst(self.Ty.ThreadLocal, self.Ty['ReentrancyGuardStack'])],
                flags = AccessFlags.Private,
                autoInitialize = False))
            rec.mEquals.body += '''
if (_equalsReentrancyGuard.Value.Contains(other))
    return true;
_equalsReentrancyGuard.Value.Push(other);
try
{'''
            rec.ctor.body += '_equalsReentrancyGuard = new ThreadLocal<ReentrancyGuardStack>(() => new ReentrancyGuardStack());'

        rec.mHashCode = rec.members.add(MethodDef(
            'GetHashCode',
            flags = AccessFlags.Public | MemberFlags.Override | MemberFlags.Sealed,
            sig = [self.Ty.int, []],
            body = '''
if (_hash != 0)
    return _hash;
EnterGetHashCode();
int hash = {};'''.format(hash(str(rec)))))
        
        rec.recordEmit = MethodDef(
            'Save',
            flags = AccessFlags.Internal | MemberFlags.Override,
            sig = [self.Ty.void, [(self.Ty.NativeWriter, 'writer')]],
            body = '''
''')
        rec.members.add(rec.recordEmit)
            
        rec.members.add(MethodDef(
            'AsHandle'.format(rec),
            sig = [rec.handle, [(rec, 'record')]],
            flags = AccessFlags.Internal | MemberFlags.Static,
            body = '''
if (record == null)
{{
    return new {0}(0);
}}
else
{{
    return record.Handle;
}}
'''.format(rec.handle, rec.enumType, rec.enumValue)))

        # String records with a null Value property are translated to null handle values so that
        # we can tell the difference between the empty and null string values.
        if str(rec) in sd.stringRecordTypes:
            rec.recordEmit.body = 'if (Value == null)\n    return;\n' + rec.recordEmit.body

            rec.members.add(PropertyDef(
                'Handle'.format(rec),
                '{}Handle'.format(rec),
                flags = AccessFlags.Internal | MemberFlags.New,
                getter = PropertyGetter(body = '''
if (Value == null)
    return new {0}Handle(0);
else
    return new {0}Handle(HandleOffset);
'''.format(rec, rec.enumType, rec.enumValue))))
        else:
            rec.members.add(PropertyDef(
                'Handle'.format(rec),
                '{}Handle'.format(rec),
                flags = AccessFlags.Internal | MemberFlags.New,
                getter = PropertyGetter(body = '''
return new {0}Handle(HandleOffset);
'''.format(rec, rec.enumType, rec.enumValue))))

        for m in members:
            rec.members += self.ProcessRecordMember(rec, *m)

        for m in sorted(members, lambda (m1,t1,f1),(m2,t2,f2): cmp(not f1.IsChild(), not f2.IsChild())):
            self.ProcessRecordMemberForVisitor(rec, *m)

        if rec.defFlags.IsReentrantEquals():
            rec.mEquals.body += '''
}
finally
{
    var popped = _equalsReentrancyGuard.Value.Pop();
    Debug.Assert(Object.ReferenceEquals(other, popped));
}'''
        rec.mEquals.body += '\nreturn true;'
        rec.mHashCode.body += '\nLeaveGetHashCode();\n_hash = hash;\nreturn _hash;'

        return rec

    #==========================================================================================================
    def CreateWriterMembers(self, reader, recs):
        members = list()
        
        # for rec in recs:
            # field = FieldDef(
                # CsMakePrivateName(Plural(str(rec))),
                # self.Ty[TypeInst(self.Ty.List, rec)],
                # flags = AccessFlags.Internal,
                # autoInitialize = True)
            # members.append(field)
     
            # members.append(MethodDef(
                # 'AddRecord',
                # flags = AccessFlags.Internal,
                # sig = [self.Ty.void, [(rec, 'record')]],
                # body = '{0}.Add(record);'.format(field)))

        return members
        
    #==========================================================================================================
    def CreateWriter(self, name, recs):
        writer = ClassDef(name, flags = AccessFlags.Public | TypeFlags.Partial)
        writer.members += self.CreateWriterMembers(writer, recs)
        return writer

    #==========================================================================================================
    def CreateBinaryWriterExtensionMethod(self, tDecl, tReal = None, body = None, argName = 'value'):
        tReal = tReal or tDecl
        body = body or 'writer.Write(({}){});'.format(tReal, argName)
        return MethodDef(
            'Write',
            sig = [self.Ty.void, [(self.Ty.MdBinaryWriter, 'writer'), (tDecl, argName)]],
            flags = AccessFlags.Internal | MemberFlags.Extension,
            body = body)

    #==========================================================================================================
    def CreateDictionaryExtensionClass(self, recs):
        cl = ClassDef(
            'DictionaryExtensions',
            flags = AccessFlags.Public | TypeFlags.Static | TypeFlags.Partial)
        for rec in recs.itervalues():
            if len(filter(lambda field: isinstance(field, FieldDef) and str(field) == 'Name', rec.members)) > 0:
                cl.members.add(MethodDef(
                    'Add',
                    flags = AccessFlags.Public | MemberFlags.Extension,
                    sig = [self.Ty.void, [(TypeInst(self.Ty.Dictionary, self.Ty.string, rec), 'dict'), (rec, 'record')]],
                    body = 'dict.Add(record.Name.Value ?? "<null>", record);'))
        return cl

    #==========================================================================================================
    # def CreateVisitorInterface(self):
        # itf = InterfaceDef(
            # 'IRecordVisitor',
            # flags = AccessFlags.Internal | TypeFlags.Partial)
            
        # for item in itertools.chain(
                # self.records.itervalues(),
                # map(lambda k: self.Ty[k], sd.primitiveTypes.iterkeys()),
                # map(lambda e: self.Ty[e], sd.enumTypes.iterkeys())):
            # itf.members.add(MethodDef(
                # 'Visit',
                # sig = [self.Ty.void, [(item, 'item')]]))
                
        # return itf
            
    #==========================================================================================================
    # def CreateVisitorEnumerate(self):
        # cls = ClassDef(
            # 'IRecordVisitor',
            # flags = AccessFlags.Internal | TypeFlags.Partial)
            
        # for item in itertools.chain(
                # self.records.itervalues(),
                # map(lambda k: self.Ty[k], sd.primitiveTypes.iterkeys()),
                # map(lambda e: self.Ty[e], sd.enumTypes.iterkeys())):
            # cls.members.add(MethodDef(
                # 'Visit',
                # sig = [self.Ty.void, [(item, 'item')]]))
                
        # return cls

    #==========================================================================================================
    def CsEmitSource(self):
        ns = NamespaceDef('Internal.Metadata.NativeFormat.Writer')

        ns.members += self.records.values()

        writer = self.CreateWriter('MetadataWriter', self.records.values())
        ns.members.add(writer)

        # ns.members.add(self.CreateDictionaryExtensionClass(self.records))
        
        # ns.members.add(self.CreateVisitorInterface())

        # for hnd in filter(lambda h: getattr(h, 'record', None), hnds.values()):
            # ns.members.add(CreateHandleEnumerator(reader, hnd))
            # ns.members.add(CreateHandleEnumerable(reader, hnd))

        # Source NativeFormatReaderGen.cs
        with open(r'..\..\..\..\..\ILCompiler.MetadataWriter\src\Internal\Metadata\NativeFormat\Writer\NativeFormatWriterGen.cs', 'w') as output :
            iprint = IPrint(output)
            CsEmitFileHeader(iprint)
            iprint('#pragma warning disable 649')
            iprint()
            iprint('using System;')
            iprint('using System.Linq;')
            iprint('using System.IO;')
            iprint('using System.Collections.Generic;')
            iprint('using System.Reflection;')
            iprint('using System.Threading;')
            iprint('using Internal.Metadata.NativeFormat.Writer;')
            iprint('using Internal.NativeFormat;')
            iprint('using HandleType = Internal.Metadata.NativeFormat.HandleType;')
            iprint('using Debug = System.Diagnostics.Debug;')
            iprint()

            ns.CsDefine(iprint)

#==========================================================================================================
if __name__ == '__main__':
    WriterGen().CsEmitSource()
