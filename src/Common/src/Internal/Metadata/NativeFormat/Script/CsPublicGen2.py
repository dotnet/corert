# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
This script generates the common API declarations that any metadata reader must implement.
In general, this script consumes the metadata record schema defined in SchemaDef2.py and
generates two interfaces and two structs for each - one (interface, struct) pair corresponding
to the metadata record itself, and one (interface, struct) pair corresponding to the 'Handle'
used to reference the specific record type. The interfaces are used as a way of
enforcing that the structs implement all required public members, but are not publicly consumed
and are declared as internal. The use of structs instead of classes for each record was driven
by a requirement from the Roslyn team that a metadata reader must minimize as much as possible
the number of allocations made; thus, structs are allocated on the stack and exist only as
long as the declaring scope remains on the stack.

Each record interface simply declares as properties the members declared in the schema definition,
and each struct is declared as partial and as implmenting the interface, thus requiring all
interface properties to be supplied by the metadata reader implementation.

Each handle interface requires type-specific equality functionality by itself implementing
IEquatable<XXXHandle>, and the handle structs similarly declare this interface to require that
the implementation be supplied by the reader.

This script generates NativeFormatReaderCommonGen.cs.

This script also generates IMetadataReader, which defines what the reader class itself must
implement.
"""

import sys
import os
import copy
import itertools
import re

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from CsNativeFormatGen2 import *

#==========================================================================================================
system = NamespaceDef('System')
system.collections = system.members.add(NamespaceDef('Collections'))
system.collections.generic = system.collections.members.add(NamespaceDef('Generic'))

sys_coll_enumable = system.collections.members.add(InterfaceDef('IEnumerable'))
sys_coll_enumator = system.collections.members.add(InterfaceDef('IEnumerator'))
sys_coll_gen_enumableT = system.collections.generic.members.add(InterfaceDef('IEnumerable', typeParams = ['T'], interfaces = [sys_coll_enumable]))
sys_coll_gen_enumatorT = system.collections.generic.members.add(InterfaceDef('IEnumerator', typeParams = ['T'], interfaces = [sys_coll_enumator]))

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
def CreateCommonHandleMembers():
    members = list()

    # members.append(MethodDef(
        # 'Equals',
        # flags = AccessFlags.Public | MemberFlags.Abstract,
        # sig = ['bool', [('Object', 'obj')]]))

    members.append(MethodDef(
        'GetHashCode',
        flags = AccessFlags.Public | MemberFlags.Abstract,
        sig = ['int', []]))
        
    return members

#==========================================================================================================
def GetEnumerableForHandle(hnd):
    # return str(hnd) + 'Enumerable'
    return TypeInst(sys_coll_gen_enumableT, hnd)

#==========================================================================================================
def CreateRecordMembers(recordType, mName, mType, flags):
    members = list()
    mType = GetOrCreateType(mType, flags)
    field = FieldDef(CsMakePrivateName(mName), mType, AccessFlags.Internal | MemberFlags.Serialize)
    members.append(field)
    if flags.IsCollection():
        members.append(PropertyDef(mName, GetEnumerableForHandle(mType.ElementType()), getter = PropertyGetter()))
    else:
        members.append(PropertyDef(mName, mType, getter = PropertyGetter()))

    return members

#==========================================================================================================
def CreateRecord(name, handle, schemaMembers):
    irec = InterfaceDef(name, flags = AccessFlags.Internal)
    Ty[str(irec)] = irec
    rec = StructDef(name, flags = AccessFlags.Public | TypeFlags.Partial, interfaces = [irec])
    Ty[str(rec)] = rec
    rec.handle = handle
    handle.record = rec
    for m in schemaMembers:
        irec.members += CreateRecordMembers(rec, *m)
    irec.members.add(PropertyDef(
        'Handle',
        handle,
        getter = PropertyGetter()))
    return rec

#==========================================================================================================
def CreateHandleMembers(handleType):
    members = CreateCommonHandleMembers()

    # if handleType != Ty.Handle:
        # members.append(MethodDef(
            # 'Equals',
            # flags = AccessFlags.Public | MemberFlags.Abstract,
            # sig = ['bool', [(Ty.Handle, 'handle')]]))
            
    return members

#==========================================================================================================
def CreateHandle(name):
    ihnd = InterfaceDef(name + str(Ty.Handle), flags = AccessFlags.Internal)#, interfaces = [Ty.IHandle])
    Ty[str(ihnd)] = ihnd

    ihnd.members.add(MethodDef(
        'ToHandle',
        flags = AccessFlags.Public | MemberFlags.Abstract,
        sig = [Ty.Handle, [(Ty.MetadataReader, 'reader')]]))

    
    hnd = StructDef(name + str(Ty.Handle), flags = AccessFlags.Public | TypeFlags.Partial, interfaces = [ihnd])
    Ty[str(hnd)] = hnd
    
    ihnd.interfaces += [Ty.IEquatableT.Instantiate(hnd), Ty.IEquatableOfHandle, Ty.IEquatableOfObject]

    hnd.record = None
    hnd.enumType = 'HandleType'
    hnd.enumValue = '{0}'.format(name)
    
    return hnd

#==========================================================================================================
def CreateReaderRecordDecodeMethods(rec):
    methods = list()

    # Method 'GetXXX'
    params = [(str(rec.handle), 'handle')]
    methods.append(MethodDef(
        'Get%s' % rec,
        flags = AccessFlags.Public | MemberFlags.Abstract,
        sig = [str(rec), params]))

    return methods

#==========================================================================================================
def CreateReaderMembers(reader, recs, hnds):
    members = list()
    name = str(reader)

    def __GetRecordMember(name, type, flags = sd.MemberDefFlags(0)):
        return (name, type, flags)

    # Generate all of the enumerator-based GetXForY (eg GetCustomAttributesForTypeDefinition)
    for (rName,rMembers) in sd.recordSchema.iteritems():
        rec = recs[rName]
        # for (mName,mType,mFlags) in rMembers:
            # mType = GetOrCreateType(mType, mFlags)
            # if mFlags.IsCollection() and mFlags.IsChild():
                # members.append(MethodDef(
                    # 'Get{0}For{1}'.format(mName, str(rec)),
                    # flags = AccessFlags.Public | MemberFlags.Abstract,
                    # sig = [GetEnumerableForHandle(mType), [(str(rec), 'record')]]))
                # members.append(MethodDef(
                    # 'Get{0}For{1}'.format(mName, str(rec)),
                    # flags = AccessFlags.Public | MemberFlags.Abstract,
                    # sig = [GetEnumerableForHandle(mType), [(str(rec.handle), 'handle')]]))

    # Generate all of the record decoding methods GetX (eg GetTypeDefinition)
    for rec in recs.itervalues():
        recMembers = CreateReaderRecordDecodeMethods(rec)
        members += recMembers

    # Generate all of the ToHandle and ToXHandle methods (eg ToHandle(TypeDefinitionHandle) and ToTypeDefinitionHandle(Handle))
    # for hnd in hnds.itervalues():
        # members.append(MethodDef(
            # 'To{0}'.format(str(hnd)),
            # flags = AccessFlags.Public | MemberFlags.Abstract,
            # sig = [hnd, [(Ty.Handle, 'handle')]]))
        # members.append(MethodDef(
            # 'ToHandle'.format(str(hnd)),
            # flags = AccessFlags.Public | MemberFlags.Abstract,
            # sig = [Ty.Handle, [(str(hnd), 'handle')]]))
            
    # members.append(MethodDef(
        # 'GetHandleType',
        # flags = AccessFlags.Public | MemberFlags.Abstract,
        # sig = ['HandleType', [(Ty.Handle, 'handle')]]))
        
    members.append(PropertyDef(
        'ScopeDefinitions',
        Ty.get(TypeInst(sys_coll_gen_enumableT, hnds['ScopeDefinitionHandle'])),
        flags = AccessFlags.Public | MemberFlags.Abstract,
        getter = PropertyGetter()))
        
    members.append(PropertyDef(
        'NullHandle',
        str(Ty.Handle),
        flags = AccessFlags.Public | MemberFlags.Abstract,
        getter = PropertyGetter()))
        
    # members.append(MethodDef(
        # 'IsNull',
        # flags = AccessFlags.Public | MemberFlags.Abstract,
        # sig = ['bool', [(Ty.Handle, 'handle')]]))
        
    # for hnd in hnds.values():
        # members.append(MethodDef(
            # 'IsNull',
            # flags = AccessFlags.Public | MemberFlags.Abstract,
            # sig = ['bool', [(str(hnd), 'handle')]]))
            
    return members

#==========================================================================================================
def CreateReader(name, recs, hnds):
    reader = InterfaceDef(name, flags = AccessFlags.Public)
    reader.members += CreateReaderMembers(reader, recs, hnds)
    return reader

#==========================================================================================================
def AsInterface(td):
    i = InterfaceDef(
        str(td),
        flags = type(td.flags)((td.flags & ~(TypeFlags.Partial | AccessFlags.All)) | AccessFlags.Internal),
        # flags = type(td.flags)((td.flags & ~(TypeFlags.Partial | AccessFlags.All)) | AccessFlags.Public),
        interfaces = copy.copy(td.interfaces),
        members = copy.copy(td.members))
    return i

#==========================================================================================================
def CreateEnumFromSchema(item):
    enum = EnumDef(item.name, baseType = Ty.get(item.baseTypeName), comment = item.comment)
    if item.flags.IsFlags():
        enum.flags |= EnumFlags.HasFlagValues
        
    enum.members += [EnumValue(member.name, comment = member.comment, value = getattr(member, 'value', None)) for member in item.members]
    
    return enum

#==========================================================================================================
Ty = TypeContainer()

#==========================================================================================================
def CsEmitSource():
    ns = NamespaceDef('Internal.Metadata.NativeFormat')

    # ns.members.add(CreateIHandle())
    # Ty = TypeContainer()
    PublishWellKnownTypes(Ty)

    ns.members += [CreateEnumFromSchema(item) for item in sd.enumSchema]

    Ty.IHandle = InterfaceDef('Handle', flags = AccessFlags.Internal)
    Ty.Handle = StructDef('Handle', flags = AccessFlags.Public | TypeFlags.Partial, interfaces = [Ty.IHandle])
    Ty.IEquatableOfHandle = Ty.IEquatableT.Instantiate(Ty.Handle)
    Ty.IEquatableOfObject = Ty.IEquatableT.Instantiate(Ty.Object)
    Ty.IHandle.interfaces = [Ty.IEquatableOfHandle, Ty.IEquatableOfObject]
    Ty.IHandle.members += CreateHandleMembers(Ty.Handle)
    ns.members += [Ty.IHandle, Ty.Handle]

    hnds = dict([(h + 'Handle', CreateHandle(h)) for h in sd.handleSchema])
    recs = dict([(rName, CreateRecord(rName, hnds[rName + 'Handle'], rMembers)) for (rName,rMembers) in sd.recordSchema.iteritems()])

    # Add handles and records to namespace
    for hnd in hnds.itervalues():
        ns.members += hnd.interfaces + [hnd]
    for rec in recs.itervalues():
        ns.members += rec.interfaces + [rec]

    for hnd in hnds.itervalues():
        hnd.interfaces[0].members += CreateHandleMembers(hnd)
        
    Ty.HandleType = EnumDef('HandleType', baseType = Ty.byte)
    Ty.HandleType.members += [EnumValue('Null')]
    Ty.HandleType.members += [EnumValue(hnd) for hnd in sorted(map(lambda hnd: str(hnd.enumValue), hnds.itervalues()))]
    ns.members.add(Ty.HandleType)
    
    Ty.IMetadataReader = CreateReader('IMetadataReader', recs, hnds)
    Ty.MetadataReader = ClassDef('MetadataReader', flags = TypeFlags.Partial, interfaces = [Ty.IMetadataReader])
    Ty.IHandle.members.add(MethodDef('GetHandleType', sig = [Ty.HandleType, [(Ty.MetadataReader, 'reader')]]))
    ns.members.add(Ty.IMetadataReader)
    ns.members.add(Ty.MetadataReader)

    for hnd in hnds.itervalues():
        Ty.IHandle.members.add(MethodDef(
            'To{0}'.format(str(hnd)),
            flags = AccessFlags.Public | MemberFlags.Abstract,
            sig = [hnd, [(Ty.MetadataReader, 'reader')]]))

    # Source NativeFormatReaderCommonGen.cs
    with open(r'NativeFormatReaderCommonGen.cs', 'w') as output :
        iprint = IPrint(output)
        CsEmitFileHeader(iprint)
        iprint('using System;')
        iprint('using System.Reflection;')
        iprint('using System.Collections.Generic;')
        iprint()
        
        iprint("#pragma warning disable 108     // base type 'uint' is not CLS-compliant")
        iprint("#pragma warning disable 3009    // base type 'uint' is not CLS-compliant")
        iprint("#pragma warning disable 282     // There is no defined ordering between fields in multiple declarations of partial class or struct")
        iprint()

        ns.CsDefine(iprint)

#==========================================================================================================
if __name__ == '__main__':
    CsEmitSource()
