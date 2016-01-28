# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""
Provides TypeContainer class and utility function PublishWellKnownTypes which can populate a
TypeContainer instance with standard .NET classes and primitive types.
"""

import sys
import os

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

import SchemaDef2 as sd
from CsCommonGen2 import *
from odict import odict

#==========================================================================================================
class TypeContainer(odict):
    def __init__(self, default = lambda tn: TypeRef(str(tn)), *args, **kv):
        super(TypeContainer, self).__init__(*args, **kv)
        self.__default = default
        
    def get(self, name, default = None):
        value = super(TypeContainer, self).get(name)
        if value:
            return value

        if isinstance(name, TypeDefOrRef):
            default = default or name
            name = str(name)

        if type(name) != str:
            raise Exception('unexpected argument type {0}'.format(type(name)))

        if default != None and not isinstance(default, TypeDefOrRef):
            raise Exception('unexpected argument {} (type: {})'.format(str(name), type(name)))
            
        return self.setdefault(name, default or self.__default(name))

    # Overriding __getitem__ and __setitem__ allows 'dot name' use of types that have not
    # yet been published, with the default behavior of inserting a type reference for the
    # provided name. I.e., 'Ty.int', where 'Ty' is an instance of TypeContainer that does
    # not contain a pre-existing 'int' member will automatically generate a type reference
    # named 'int' and return it.
    def __getitem__(self, name):
        return super(TypeContainer, self).get(name, None) or self.get(name)
        
    def __setitem__(self, name, value):
        super(TypeContainer, self).__setitem__(name, value)

    def __getattr__(self, name):
        if name in self:
            return super(TypeContainer, self).get(name)
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
def PublishWellKnownTypes(Ty):
    # Intrinsics
    for tName,tValueTypeName in sd.primitiveTypes.iteritems():
        t = TypeRef(tName)
        t.valueTypeName = tValueTypeName
        Ty[tName] = t

    for (eName, eType) in sd.enumTypes.iteritems():
        Ty[eName] = EnumDef(eName, underlyingType = Ty[eType])

    # Some standard .NET types. Extend as needed.
    Ty.IEnumerable = InterfaceDef('IEnumerable')
    Ty.IEnumerable.parentScope = 'System.Collections'
    Ty.IEnumerator = InterfaceDef('IEnumerator', parentScope = 'System.Collections')
    Ty.IEnumerator.parentScope = 'System.Collections'
    Ty.IEnumerableT = InterfaceDef('IEnumerable', typeParams = ['T'], interfaces = [Ty.IEnumerable])
    Ty.IEnumeratorT = InterfaceDef('IEnumerator', typeParams = ['T'], interfaces = [Ty.IEnumerator])
    Ty.IEquatableT = InterfaceDef('IEquatable', typeParams = ['T'])
    Ty.Dictionary = ClassDef('Dictionary', typeParams = ['K', 'V'])
    Ty.List = ClassDef('List', typeParams = ['T'])
    Ty.IListT = InterfaceDef('IList', typeParams = ['T'])
    # Add Tuple`2 through Tuple`5
    for i in range(2,5):
        Ty['Tuple`' + str(i)] = TypeRef('Tuple`', typeParams = ['T' + str(j) for j in range(2, i)])
    Ty.ThreadLocal = ClassDef('ThreadLocal', typeParams = ['T'])
