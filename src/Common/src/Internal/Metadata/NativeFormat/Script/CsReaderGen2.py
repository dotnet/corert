# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
This script file generates most of the implementation of the MetadataReader for the ProjectN format,
ensuring that the contract defined by CsPublicGen2 is implemented. The generated file is
'NativeFormatReaderGen.cs', and any missing implementation is the supplied in the human-authored
source counterpart 'NativeFormatReader.cs'.
"""
import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import *
from odict import odict

Ty = None
handles = None
records = None

#==========================================================================================================
def GetOrCreateType(ty, flags):
    if isinstance(ty, TypeDefOrRef):
        return ty

    if type(ty) == tuple:
        ty = str(Ty.Handle)

    if flags.IsCollection():
        elementType = GetOrCreateType(ty, flags & (~sd.MemberDefFlags.CollectionMask))
        return Ty.get(str(elementType) + '[]', TypeRef(
            name = str(elementType) + '[]',
            flags = TypeFlags.Public | TypeFlags.Array,
            elementType = elementType))

    if flags.IsRef() and str(ty) != 'Handle':
        ty = ty + 'Handle'

    return Ty[ty]

#==========================================================================================================
def CreateRecordDecodeMethods(rec):
    methods = list()

    # Code snipped to calculate size of record
    sizeBody = 'uint recordSize =\n    '
    
    # Method 'GetXXX'
    decodeBodyGetPrefix = ''
    decodeBody = ''

    # Null handles for string types are automatically promoted to records with Value property set to null.
    if str(rec) in sd.stringRecordTypes:
        decodeBodyGetPrefix = '''
if (IsNull(handle))
    return new {}();'''.format(rec)

    decodeBody = '''
var record = new {0}() {{ _reader = this, _handle = handle }};
var offset = (uint)handle.Offset;'''.format(rec)

    for m in filter(lambda f: (f.flags & MemberFlags.Serialize) and isinstance(f, FieldDef), rec.members):
        decodeBody += '\noffset = _streamReader.Read(offset, out record.{0});'.format(m)

    params = [(rec.handle, 'handle')]
    methods.append(MethodDef(
        'Get{}'.format(rec),
        sig = [rec, params],
        body = decodeBodyGetPrefix + decodeBody + '\nreturn record;'))

    return methods

#==========================================================================================================
def GetEnumerableForType(ty):
    return TypeInst(Ty.IEnumerableT, ty)

#==========================================================================================================
def CreateRecordMembers(rType, mName, mType, flags):
    members = list()
    if flags.IsNotPersisted():
        return members

    comment = None
    if type(mType) == tuple:
        comment = 'One of: ' + ', '.join(mType)

    mType = GetOrCreateType(mType, flags)
    field = FieldDef(CsMakePrivateName(mName), mType, MemberFlags.Internal | MemberFlags.Serialize)

    if flags.IsCollection():
        members.append(PropertyDef(mName, GetEnumerableForType(mType.ElementType()),
            getter = PropertyGetter(body = 'return ({}){};'.format(GetEnumerableForType(mType.ElementType()), field)), comment = comment))
    else:
        members.append(PropertyDef(mName, mType, field = field, getter = PropertyGetter(), comment = comment))

    members.append(field)
        
    return members

#==========================================================================================================
def CreateRecord(name):
    if name in Ty:
        return Ty[name]
    recordDef = sd.recordDefs[name]
    baseType = Ty.get(recordDef.baseTypeName) if recordDef.baseTypeName else None
    rec = Ty.get(name, StructDef(name, baseType, AccessFlags.Public | TypeFlags.Partial | TypeFlags.Struct))
    rec.handle = None
    rec.members.add(FieldDef(CsMakePrivateName('Reader'), Ty.NativeFormatReader, AccessFlags.Internal))
    rec.decodeMethod = None

    return rec

#==========================================================================================================
def CreateHandleMembers(hnd):
    hnd.members.add(MethodDef(
        'Equals',
        MemberFlags.Public | MemberFlags.Override,
        ['bool', [('object', 'obj')]],
        body = '''\
if (obj is {0})
    return _value == (({0})obj)._value;
else if (obj is Handle)
    return _value == ((Handle)obj)._value;
else
    return false;'''.format(hnd)))

    hnd.members.add(MethodDef(
        'Equals',
        MemberFlags.Public,
        ['bool', [(hnd, 'handle')]],
        body = 'return _value == handle._value;'))

    hnd.members.add(MethodDef(
        'Equals',
        MemberFlags.Public,
        ['bool', [(Ty.Handle, 'handle')]],
        body = 'return _value == handle._value;'))

    hnd.members.add(MethodDef(
        'GetHashCode',
        MemberFlags.Public | MemberFlags.Override,
        [Ty.int, []],
        body = 'return (int)_value;'))
        
    hnd.members.add(FieldDef(CsMakePrivateName('Value'), Ty.int, AccessFlags.Internal))

    hnd.members.add(CtorDef(
        MemberFlags.Internal,
        sig = [(Ty.Handle, 'handle')],
        ctorDelegation = ['handle._value'],
        body = ''))

    hnd.members.add(CtorDef(
        flags = AccessFlags.Internal,
        sig = [(Ty.int, 'value')],
        body = '''
HandleType hType = (HandleType)(value >> 24);
if (!(hType == 0 || hType == {0} || hType == {1}))
    throw new ArgumentException();
_value = (value & 0x00FFFFFF) | (((int){0}) << 24);
_Validate();'''.format(hnd.enumValue, Ty.HandleType.members['HandleType.Null'])))

    hnd.members.add(MethodDef(
        str(Ty.Handle),
        AccessFlags.Public | MemberFlags.Static | MemberFlags.Implicit | MemberFlags.Operator,
        [None, [(hnd, 'handle')]],
        body ='return new Handle(handle._value);'.format()))

    hnd.members.add(PropertyDef(
        'Offset',
        Ty.int,
        flags = AccessFlags.Internal,
        getter = PropertyGetter(body = 'return (this._value & 0x00FFFFFF);')))

    if hnd.record:
        hnd.members.add(MethodDef(
            'Get{}'.format(hnd.record),
            flags = AccessFlags.Public,
            sig = [hnd.record, [(Ty.NativeFormatReader, 'reader')]],
            body = 'return reader.Get{}(this);'.format(hnd.record)))

        hnd.members.add(MethodDef(
            'IsNull',
            flags = AccessFlags.Public,
            sig = [Ty.bool, [(Ty.NativeFormatReader, 'reader')]],
            body = 'return reader.IsNull(this);'))

    hnd.members.add(MethodDef(
        'ToHandle',
        flags = AccessFlags.Public,
        sig = [Ty.Handle, [(Ty.NativeFormatReader, 'reader')]],
        body = 'return reader.ToHandle(this);'))
        
    hnd.members.add(MethodDef(
        '_Validate',
        flags = AccessFlags.Internal | MemberFlags.DebugOnly,
        sig = [Ty.void, []],
        body = '''
if ((HandleType)((_value & 0xFF000000) >> 24) != {})
    throw new ArgumentException();'''.format(hnd.enumValue)))

    hnd.members.add(MethodDef(
        'ToString',
        flags = AccessFlags.Public | MemberFlags.Override,
        sig = [Ty.String, []],
        body = 'return String.Format("{0:X8}", _value);'))

#==========================================================================================================
def CreateHandle(name):
    hName = name + str(Ty.Handle)
    hnd = Ty.get(hName, StructDef(hName, None, AccessFlags.Public | TypeFlags.Partial))
    hnd.record = None
    hnd.enumValueName = '{0}'.format(name)
    return hnd

#==========================================================================================================
def CreateBaseHandleEnumerator(hnds):
    eName = str(Ty.Handle) + 'Enumerator'
    eType = StructDef(
        eName,
        flags = AccessFlags.Public | TypeFlags.Partial,
        interfaces = [TypeInst(Ty.IEnumeratorT, Ty.Handle)])

    moveNext = MethodDef(
        'MoveNext',
        sig = [Ty.bool, []],
        body = '''
if (_curCount == _count || ++_curCount == _count)
    return false;

if (_curCount != 0)
{
    switch (_type)
    {
''')

    for hnd in hnds:
        moveNext.body += '''
    case {}:
        _curHandle = _reader.MoveNext(new {}(_curHandle)).ToHandle(_reader);
        break;'''.format(hnd.enumValue, hnd)

    moveNext.body += '''
    }
}
    
return true;'''

    eType.members.add(moveNext)
    setattr(Ty, eName, eType)
    return eType

#==========================================================================================================
def CreateBaseHandleConversions(recs):
    members = list()
    
    for rec in recs:
        members.append(MethodDef(
            'To{}'.format(rec.handle),
            sig = [rec.handle, [(Ty.MetadataReader, 'reader')]],
            body = 'return new {}(this);'.format(rec.handle)))

    return members
    
#==========================================================================================================
def CreateReaderMembers(reader, recs, hnds):
    members = list()
    name = str(reader)

    # Generate all of the enumerator-based GetXForY (eg GetCustomAttributesForTypeDefinition)
    for (rName,rMembers) in sd.recordSchema.iteritems():
        rec = recs[rName]
        for (mName,mType,mFlags) in rMembers:
            mType = GetOrCreateType(mType, mFlags)
            # if mFlags.IsCollection() and mFlags.IsChild():
                # if not mFlags.IsRef(): raise Exception('Unexpected collection element type')
                # members.append(MethodDef(
                    # 'Get{0}For{1}'.format(mName, rec),
                    # flags = MemberFlags.Internal,
                    # sig = [GetEnumerableForType(mType), [(rec, 'record')]],
                    # body = 'return new {0}(this, record.{1}, (int)record.{2});'.format(str(mType) + 'Enumerable', CsMakePrivateName(mName), CsMakePrivateName(Singular(mName) + 'Count'))))
                # members.append(MethodDef(
                    # 'Get{0}For{1}'.format(mName, rec),
                    # flags = MemberFlags.Internal,
                    # sig = [GetEnumerableForType(mType), [(rec.handle, 'handle')]],
                    # body = 'return Get{0}For{1}(Get{1}(handle));'.format(mName, rec)))

    # Generate all of the record decoding methods GetX (eg GetTypeDefinition)
    for rec in recs.itervalues():
        recMembers = CreateRecordDecodeMethods(rec)
        members += recMembers

    # Generate all of the ToHandle and ToXHandle methods (eg ToHandle(TypeDefinitionHandle) and ToTypeDefinitionHandle(Handle))
    for hnd in hnds.itervalues():
        members.append(MethodDef(
            'To{0}'.format(hnd),
            flags = AccessFlags.Internal,
            sig = [hnd, [(Ty.Handle, 'handle')]],
            body = 'return new {0}(handle._value);'.format(hnd)))

        members.append(MethodDef(
            'ToHandle'.format(hnd),
            flags = AccessFlags.Internal,
            sig = [Ty.Handle, [(hnd, 'handle')]],
            body = 'return new Handle(handle._value);'.format()))

    for hnd in hnds.itervalues():
        members.append(MethodDef(
            'IsNull',
            flags = AccessFlags.Internal,
            sig = ['bool', [(hnd, 'handle')]],
            body = 'return (handle._value & 0x00FFFFFF) == 0;'))

    return members

#==========================================================================================================
def CreateReader(name, baseType = None, interfaces = []):
    return ClassDef(
        name,
        flags = AccessFlags.Public | TypeFlags.Partial,
        baseType = baseType,
        interfaces = interfaces)

#==========================================================================================================
def CreateHandleEnumerator(hnd):
    e = ClassDef(
        str(hnd) + 'Enumerator',
        flags = AccessFlags.Public | TypeFlags.Partial,
        interfaces = [TypeInst(Ty.IEnumeratorT, hnd), Ty.IEnumerator.FullName()])

    e.members.add(FieldDef(
        CsMakePrivateName('Reader'),
        Ty.NativeFormatReader,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName(str(Ty.Handle)),
        hnd,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName('Count'),
        Ty.int,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName('Cur' + str(Ty.Handle)),
        hnd,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName('CurCount'),
        Ty.int,
        flags = AccessFlags.Internal))

    e.members.add(CtorDef(
        sig = [(Ty.NativeFormatReader, 'reader'), (hnd, 'handle'), (Ty.int, 'count')],
        body =
'''_reader = reader;
_handle = handle;
_count = count;
_curCount = -1;
_curHandle = handle;'''))

    e.members.add(MethodDef(
        'MoveNext',
        sig = [Ty.bool, []],
        body =
'''if (_curCount == _count || ++_curCount == _count)
    return false;

if (_curCount != 0)
    _curHandle = _reader.MoveNext(_curHandle);

return true;'''))

    e.members.add(PropertyDef(
        'System.Collections.IEnumerator.Current',
        Ty.object,
        getter = PropertyGetter(),
        field = e.members[CsMakePrivateName('CurHandle')]))

    e.members.add(PropertyDef(
        'Current',
        hnd,
        getter = PropertyGetter(),
        field = e.members[CsMakePrivateName('CurHandle')]))

    e.members.add(MethodDef(
        'Dispose',
        sig = [Ty.void, []],
        body ='_reader = null;'))

    e.members.add(MethodDef(
        'Reset',
        sig = [Ty.void, []],
        body ='_curCount = -1;\n_curHandle = _handle;'))

    hnd.enumerator = e
    return e

#==========================================================================================================
def CreateHandleEnumerable(hnd):
    e = ClassDef(
        str(hnd) + 'Enumerable',
        flags = AccessFlags.Public | TypeFlags.Partial,
        interfaces = [TypeInst(Ty.IEnumerableT, hnd), Ty.IEnumerable.FullName()])

    e.members.add(FieldDef(
        CsMakePrivateName('Reader'),
        Ty.NativeFormatReader,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName('Start'),
        hnd,
        flags = AccessFlags.Internal))

    e.members.add(FieldDef(
        CsMakePrivateName('Count'),
        Ty.int,
        flags = AccessFlags.Internal))

    e.members.add(CtorDef(
        sig = [(Ty.NativeFormatReader, 'reader'), (hnd, 'start'), ('int', 'count')],
        body =
'''_reader = reader;
_start = start;
_count = count;'''))

    e.members.add(MethodDef(
        'GetEnumerator',
        sig = [hnd.enumerator, []],
        body = 'return new {0}(_reader, _start, _count);'.format(hnd.enumerator)))

    e.members.add(MethodDef(
        '{0}.GetEnumerator'.format(TypeInst(Ty.IEnumerableT, hnd)),
        sig = [TypeInst(Ty.IEnumeratorT, hnd), []],
        body = 'return new {0}(_reader, _start, _count);'.format(hnd.enumerator)))

    e.members.add(MethodDef(
        '{0}.GetEnumerator'.format(Ty.IEnumerable.FullName()),
        sig = [Ty.IEnumerator.FullName(), []],
        body = 'return new {0}(_reader, _start, _count);'.format(hnd.enumerator.FullName())))

    hnd.enumerable = e
    return e

#==========================================================================================================
def CreateBinaryReaderExtensionMethod(tDecl, tReal = None, body = None):
    tReal = tReal or tDecl
    body = body or 'value = ({0})reader.Read{1}();'.format(tDecl, tReal)
    return MethodDef(
        'Read',
        sig = [Ty.void, [(Ty.MdBinaryReader, 'reader'), ('out ' + str(tDecl), 'value')]],
        flags = AccessFlags.Internal | MemberFlags.Extension,
        body = body)

#==========================================================================================================
def CreateBinaryReaderExtensionClass(hnds):
    cl = ClassDef(
        'BinaryReaderExtensions',
        flags = AccessFlags.Internal | TypeFlags.Static | TypeFlags.Partial)
    # for iName, iValueType in sd.primitiveTypes.iteritems():
        # cl.members.add(CreateBinaryReaderExtensionMethod(Ty[iName], Ty[iValueType]))
    for eName, eType in sd.enumTypes.iteritems():
        enum = Ty.get(eName, EnumDef(eName, underlyingType = Ty[eType]))
        cl.members.add(CreateBinaryReaderExtensionMethod(enum, enum.underlyingType.valueTypeName))
    for hnd in hnds.itervalues():
        cl.members.add(CreateBinaryReaderExtensionMethod(hnd, body = 'reader.UnencodedRead(out value._value);'))
    return cl

#==========================================================================================================
def CreateHandleTypeEnum(hnds):
    eName = str(Ty.Handle) + 'Type'
    eType = EnumDef(eName)
    eType.members.add(EnumValue('Null'))
    for hnd in hnds:
        eValue = EnumValue(hnd.enumValueName)
        eType.members.add(eValue)
        hnd.enumValue = eValue

    Ty.HandleType = eType

#==========================================================================================================
def CsEmitSource():
    globals()['Ty'] = TypeContainer()
    PublishWellKnownTypes(Ty)
        
    Ty.Handle = StructDef('Handle', flags = AccessFlags.Public | TypeFlags.Partial)
    Ty.IMetadataReader = InterfaceDef('IMetadataReader')
    Ty.MetadataReader = CreateReader('MetadataReader', interfaces = [Ty.IMetadataReader])
    Ty.NativeFormatReader = Ty.MetadataReader

    ns = NamespaceDef('Internal.Metadata.NativeFormat')
    ns.members.add(Ty.MetadataReader)
    
    globals()['handles'] = odict([(str(hnd), hnd) for hnd in map(lambda hName: CreateHandle(hName), sd.handleSchema)])
    globals()['records'] = odict([(str(rec), rec) for rec in map(lambda rName: CreateRecord(rName), sd.recordSchema.iterkeys())])
    
    CreateHandleTypeEnum(handles.values())

    # Associate records and handles
    for rec in records.itervalues():
        hnd = handles[str(rec) + str(Ty.Handle)]
        rec.handle = hnd
        hnd.record = rec

    # Create record members
    for rName,rMembers in sd.recordSchema.iteritems():
        rec = records[rName]
        rec.members.add(FieldDef(CsMakePrivateName('Handle'), rec.handle, flags = AccessFlags.Internal))
        rec.members.add(PropertyDef('Handle', rec.handle, getter = PropertyGetter(body = 'return _handle;')))
        for rMember in rMembers:
            rec.members += CreateRecordMembers(rec, *rMember)

    # Create handle members
    for hnd in handles.itervalues():
        CreateHandleMembers(hnd)

    # Add base handle helper functions
    baseHandle = StructDef(
        str(Ty.Handle),
        flags = AccessFlags.Public | TypeFlags.Partial)
        
    baseHandle.members += CreateBaseHandleConversions(records.itervalues())
    ns.members.add(baseHandle)

    # Add handles and records to namespace
    ns.members += handles.itervalues()
    ns.members += records.itervalues()

    Ty.MetadataReader.members += CreateReaderMembers(Ty.MetadataReader, records, handles)

    # Source NativeFormatReaderGen.cs
    with open(r'NativeFormatReaderGen.cs', 'w') as output :
        iprint = IPrint(output)
        CsEmitFileHeader(iprint)
        iprint('#pragma warning disable 649')
        iprint('#pragma warning disable 169')
        iprint('#pragma warning disable 282 // There is no defined ordering between fields in multiple declarations of partial class or struct')
        iprint()
        iprint('using System;')
        iprint('using System.Reflection;')
        iprint('using System.Collections.Generic;')
        iprint()

        ns.CsDefine(iprint)

#==========================================================================================================
# Ty = TypeContainer()
# PublishWellKnownTypes(Ty)
    
# Ty.Handle = StructDef('Handle', flags = AccessFlags.Public | TypeFlags.Partial)
# handles = odict([(str(hnd), hnd) for hnd in map(lambda hName: CreateHandle(hName), sd.handleSchema)])
# records = odict([(str(rec), rec) for rec in map(lambda rName: CreateRecord(rName), sd.recordSchema.iterkeys())])

def IsHandle(t):
    return isinstance(t, TypeDef) and (str(t) in handles or t == Ty.Handle)
    
def IsRecord(t):
    return isinstance(t, TypeDef) and str(t) in records

#==========================================================================================================
if __name__ == '__main__':
    CsEmitSource()
