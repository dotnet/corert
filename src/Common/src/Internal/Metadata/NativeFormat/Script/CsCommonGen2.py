# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
Defines classes that can be used to build up a set of symbols similar to those of C#
(namespace, type, method, property, etc.) that may be manipulated and subsequently
emitted as C# source code.
"""

import sys
import re
import copy
import itertools
from odict import odict

#==========================================================================================================
def classinit(cls, realcls = None):
    """
    Automatically initialize a class by calling __clsinit__ if it exists on cls and its base types.
    Currently used to implement class static initialization, and in particular the Flags functionality.
    """
    for base in cls.__bases__:
        classinit(base, cls)
    f = getattr(cls, '__clsinit__', None)
    if f:
        f.__func__(realcls or cls)
    return cls

#==========================================================================================================
class Flags(int):
    """
    Base class for easily defining sets of flags. Values should be added within a __clsinit__ method.
    """
    
    def __apply_op(self, op, other):
        if isinstance(other, type(self)):
            return type(other)(op(int(self), int(other)))
        elif isinstance(self, type(other)):
            return type(self)(op(int(self), int(other)))
        else:
            raise Exception('Incompatible flag types.')

    def __init__(self, value = 0):
        """ Used to construct an instance object with value 'value' """
        super(Flags, self).__init__(int(value))
        
    def __or__(self, other):
        return self.__apply_op(int.__or__, other)

    def __xor__(self, other):
        return self.__apply_op(int.__xor__, other)

    def __and__(self, other):
        return self.__apply_op(int.__and__, other)

    def __invert__(self):
        return type(self)(~(int(self)))

    @classmethod
    def __clsinit__(cls):
        """ Creates internal structures to track flag values. """
        cls.__values = list()
        cls.__masks = list()
        cls.__next_value = 0x1
        
    @classmethod
    def AddFlag(cls, name):
        """ Add a flag value. Adds attribute '<name>' instance method 'Is<name>'. """
        value = cls.__next_value
        cls.__next_value = cls.__next_value << 1
        cls.__values.append((name, value))
        setattr(cls, name, cls(value))
        def isFn(self):
            return (self & getattr(cls, name)) != 0
        setattr(cls, 'Is' + name, isFn)
        
    @classmethod
    def AddMask(cls, name, *args):
        """ Create a mask from a set of flag names. Adds attribute '<name>Mask' and function 'Is<name>'. """
        value = 0
        for arg in args:
            value = value | getattr(cls, arg)
        cls.__masks.append((name + 'Mask', value))
        setattr(cls, name + 'Mask', cls(value))
        def isFn(self):
            return (self & getattr(cls, name + 'Mask')) != 0
        setattr(cls, 'Is' + name, isFn)
        
    def __str__(self):
        """ Returns a textual representation of the set flag values. """
        return '(' + '|'.join(map(lambda (n,f): n, filter(lambda (n,f): (self & f), self.__values))) + ')'
        
    @classmethod
    def iterflags(cls):
        return cls.__values.__iter__()

    @classmethod
    def itermasks(cls):
        return cls.__masks.__iter__()

#==========================================================================================================
class StrictlyType(object):
    """Returns a strictly typed function"""
    def __init__(self,*args):
        self.args = args
    def __call__(self, f):
        def func(*args, **kv):
            for a in zip(args[1:min(len(args), len(self.args))] if len(args) == len(self.args) + 1 else args[min(len(args), len(self.args)):], self.args):
                if a[1] != None and not isinstance(a[0], a[1]):
                    raise TypeError, 'Expected %s, got %s (%s)' % (a[1], type(a[0]), a[0])
            v = f(*args, **kv)
            return v
        func.func_name = f.func_name
        return func

#==========================================================================================================
class IPrint:
    def __init__(self, *args):
        self.output = None
        self.cur_indent = 0
        self.parent = None
        self.addWhitespace = False
        self.pendingWhitespace = False

        for arg in args:
            if isinstance(arg, int):
                self.cur_indent += arg
            elif isinstance(arg, IPrint):
                self.parent = arg
                self.output = arg.output
                self.cur_indent += arg.cur_indent + 1
                self.addWhitespace = False
                self.pendingWhitespace = False
            elif isinstance(arg, file):
                self.output = arg

        if not self.output:
            self.output = sys.stdout

    def __write(self, *args, **kv):
        indent = 0

        if len(args) == 0: return ''
        elif len(args) == 1: (arg,) = args
        elif len(args) == 2: (indent, arg) = args
        elif len(args) > 2: raise Exception('Invalid arguments')

        indent += self.cur_indent

        isFirst = True
        outstr = '\n' if self.pendingWhitespace and self.addWhitespace else ''
        self.pendingWhitespace = False
        for line in map(lambda a: a.rstrip(), arg.split('\n')):
            if not isFirst: outstr += '\n'
            if (not isFirst) or ('indentFirstLine' not in kv) or (kv['indentFirstLine']):
                outstr += ' ' * (indent * 4)
            outstr += line.rstrip()
            isFirst = False
        outstr = outstr.rstrip()
        if arg.endswith('\n'):
            outstr += '\n'
        self.addWhitespace = True
        return outstr

    def append(self, *args, **kv):
        self.output.write(self.__write(indentFirstLine = False, *args, **kv))

    def write(self, *args, **kv):
        self.output.write(self.__write(*args, **kv))

    def writeline(self, *args, **kv):
        self.output.write(self.__write(*args, **kv))
        self.output.write('\n')

    def __call__(self, *args):
        return self.writeline(*args)

    def indent(self):
        return IPrint(self)

    def outdent(self):
        return self.parent

    def AddWhitespace(self):
        self.pendingWhitespace = True
        p = self.parent
        while p:
            p.addWhitespace = True
            p = p.parent


#==========================================================================================================
# @classinit
class AccessFlags(Flags):
    def __init__(self, value):
        super(AccessFlags, self).__init__(value)

    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('Public')
        cls.AddFlag('Internal')
        cls.AddFlag('Protected')
        cls.AddFlag('Private')
        cls.AddMask('Visibility', 'Public', 'Internal', 'Protected', 'Private')
        cls.All = cls.VisibilityMask
        cls.AddFlag('CsExclude')
classinit(AccessFlags)

#==========================================================================================================
# @classinit
class MemberFlags(AccessFlags):
    def __init__(self, value):
        super(MemberFlags, self).__init__(value)

    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('Static')
        cls.AddFlag('Method')
        cls.AddFlag('Property')
        cls.AddMask('Type', 'Method', 'Property')
        cls.AddFlag('Override')
        cls.AddFlag('Serialize')
        cls.AddFlag('Explicit')
        cls.AddFlag('Implicit')
        cls.AddFlag('Operator')
        cls.AddFlag('Abstract')
        cls.AddFlag('Partial')
        cls.AddFlag('Virtual')
        cls.AddFlag('Extension')
        cls.AddFlag('New')
        cls.AddFlag('DebugOnly')
        cls.AddFlag('Sealed')
classinit(MemberFlags)


#==========================================================================================================
# @classinit
class TypeFlags(AccessFlags):
    def __init__(self, value):
        super(TypeFlags, self).__init__(value)

    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('Class')
        cls.AddFlag('Struct')
        cls.AddFlag('Interface')
        cls.AddFlag('Enum')
        cls.AddMask('Type', 'Class', 'Struct', 'Interface', 'Enum')
        cls.AddFlag('Abstract')
        cls.AddFlag('Partial')
        cls.AddFlag('Static')
        cls.AddFlag('Array')
classinit(TypeFlags)

#==========================================================================================================
# @classinit
class EnumFlags(TypeFlags):
    def __init__(self, value):
        super(EnumFlags, self).__init__(value)
        
    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('HasFlagValues')
classinit(EnumFlags)

#==========================================================================================================
class CsScopeBlock(object):
    def __init__(self, iprint):
        self.iprint = iprint

    def __enter__(self):
        self.iprint('{')
        self.iprint.cur_indent += 1

    def __exit__(self, exc_type, exc_value, traceback, comment = None):
        self.iprint.cur_indent -= 1
        if comment:
            self.iprint('}} // {0}'.format(comment))
        else:
            self.iprint('}')

#==========================================================================================================
class CsNamespace(CsScopeBlock):
    def __init__(self, iprint, name):
        super(CsNamespace, self).__init__(iprint)
        self.name = name

    def __enter__(self):
        self.iprint('\nnamespace {0}'.format(self))
        super(CsNamespace, self).__enter__()

    def __exit__(self, exc_type, exc_value, traceback):
        super(CsNamespace, self).__exit__(exc_type, exc_value, traceback, 'namespace ' + str(self))
        
    def __str__(self):
        return self.name

#==========================================================================================================
def CsMakePrivateName(name):
    name = str(name)
    if re.match(r'[A-Z][a-z0-9_]', name):
        return '_' + name[0].lower() + name[1:]
    else:
        return '_' + name

#==========================================================================================================
def CsMakeArgumentName(name):
    name = str(name)
    if re.match(r'I[A-Z][a-z0-9_]', name):
        return name[1].lower() + name[2:]
    else:
        return name[0].lower() + name[1:]

#==========================================================================================================
def CsAccessKeyword(flags):
    if flags & AccessFlags.Public:
        return 'public'
    elif flags & AccessFlags.Internal:
        return 'internal'
    elif flags & AccessFlags.Protected:
        return 'protected'
    elif flags & AccessFlags.Private:
        return 'private'
    else:
        return ''

#==========================================================================================================


#==========================================================================================================
def CsMemberDeclaration(m):
    out = []
    if m.flags & MemberFlags.Partial:
        return 'partial'
    if '.' not in str(m) and not m.parentScope.IsInterface():
        out.append(m.CsVisibility())
    if m.flags & MemberFlags.Static:
        out.append('static')
    if m.flags & MemberFlags.Explicit:
        out.append('explicit')
    if m.flags & MemberFlags.Implicit:
        out.append('implicit')
    if m.flags & MemberFlags.Virtual:
        out.append('virtual')
    if m.flags & MemberFlags.Operator:
        out.append('operator')
    if m.flags & MemberFlags.New:
        out.append('new')
    if not m.parentScope.IsInterface():
        if m.flags & MemberFlags.Override:
            out.append('override')
        if m.flags & MemberFlags.Abstract:
            out.append('abstract')
        if m.flags & MemberFlags.Sealed:
            out.append('sealed')
    return ' '.join(out)

#==========================================================================================================
def Singular(x):
    if x == 'MethodSemantics':
        return x
    elif x.endswith('ies'):
        return x[:-3] + 'y'
    elif x.endswith('ses'):
        return x[:-3]
    elif x.endswith('s'):
        return x[:-1]
    else:
        return x

#==========================================================================================================
def Plural(x):
    x = Singular(x)
    if x == 'MethodSemantics':
        return x
    elif x.endswith('y'):
        return x[:-1] + 'ies'
    elif x.endswith('s'):
        return x + 'ses'
    else:
        return x + 's'

#==========================================================================================================
def CsEmitFileHeader(iprint):
        iprint(
'''// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// NOTE: This is a generated file - do not manually edit!
''')

#==========================================================================================================
class MemberSet(object):
    def __init__(self, parentScope, *args, **kv):
        self.__dict = odict()
        self.parentScope = parentScope

    def add(self, item):
        if item == None:
            return
        item.parentScope = self.parentScope
        self.__dict.setdefault(str(item), list()).append(item)
        return item
        
    def clear(self):
        self.__dict.clear()
        
    def __iadd__(self, iter):
        for item in iter:
            self.add(item)
        return self
        
    def __iter__(self):
        return itertools.chain(*self.__dict.values())
        
    def __getitem__(self, name):
        val = self.__dict.get(name)
        if type(val) != str and len(val) == 1:
            return val[0]
        else:
            return val
            
    def __len__(self):
        return len(self.__dict)
        
#==========================================================================================================
class TypeDefOrRef(object):
    def __init__(self, name, comment = None, typeParams = None, **kv):
        if type(name) != str:
            raise Exception('Must provide name for type.')
        self.name = name
        self.comment = comment
        self.typeParams = typeParams or []
        for n,v in kv.iteritems():
            setattr(self, n, v)

    def UnderlyingType(self):
        return getattr(self, 'underlyingType', self)
        
    def ElementType(self):
        return getattr(self, 'elementType', self)
        
    def IsGeneric(self):
        return len(self.typeParams) != 0
        
    def Instantiate(self, *args):
        if not self.IsGeneric():
            raise Exception('Attempt to instantiate non-generic type.')
        return TypeInst(self, *args)

    def __str__(self):
        return self.name
        
#==========================================================================================================
class TypeRef(TypeDefOrRef):
    def __init__(self, *args, **kv):
        super(TypeRef, self).__init__(*args, **kv)
        
    def CsDefine(self, iprint):
        pass

    def CsComment(self, iprint):
        pass
        
#==========================================================================================================
class TypeDef(TypeDefOrRef):
    def __init__(self, name, baseType = None, flags = AccessFlags.Public,
                 typeParams = None, interfaces = None, members = None,
                 underlyingType = None, comment = None, **kv):
        super(TypeDef, self).__init__(name, typeParams = typeParams or [], **kv)
        if not (flags & AccessFlags.VisibilityMask):
            flags = flags | AccessFlags.Public
        self.flags = TypeFlags(flags)
        if type(baseType) == str:
            baseType = TypeRef(baseType)
        self.interfaces = interfaces or list()
        if self.IsInterface() and baseType:
            self.interfaces.append(baseType)
            baseType = None
        self.baseType = baseType
        self.members = MemberSet(self)
        if members:
            self.members += members
        self.parentScope = None
        if type(underlyingType) == str:
            underlyingType = TypeRef(underlyingType)
        self.underlyingType = underlyingType or self
        self.comment = comment
        
    def CsDefine(self, iprint):
        # If this is a struct and there is a base type specified, fold the base
        # type members into the child
        if self.flags.IsStruct() and self.baseType != None:
            self.members += getattr(self.baseType, 'members', [])
            self.baseType = None
        
        # self.CsComment(iprint)
        self.CsDefineHeader(iprint)
        self.CsDefineBegin(iprint)
        self.CsDefineMembers(iprint.indent())
        self.CsDefineEnd(iprint)
        
    def CsComment(self, iprint):
        if self.comment:
            iprint('\n/// ' + self.comment)
        
    def CsTypeName(self):
        return str(self)
    
    def CsDefineHeader(self, iprint):
        iprint(
'''/// <summary>
/// {}{}
/// </summary>'''.format(self, ' : ' + self.comment if self.comment else ''))

    def __CsTypeDeclaration(self):
        out = [CsAccessKeyword(self.flags)]
        if not self.IsInterface():
            if self.flags & TypeFlags.Abstract:
                out += ['abstract']
            if self.flags & TypeFlags.Static:
                out += ['static']
        if self.flags & TypeFlags.Partial:
            out += ['partial']
        if (self.flags & TypeFlags.TypeMask) == TypeFlags.Class:
            out += ['class']
        elif (self.flags & TypeFlags.TypeMask) == TypeFlags.Struct:
            out += ['struct']
        elif (self.flags & TypeFlags.TypeMask) == TypeFlags.Interface:
            out += ['interface']
        elif (self.flags & TypeFlags.TypeMask) == TypeFlags.Enum:
            out += ['enum']
        out += [self.CsTypeName()]
        return ' '.join(out)

    def CsDefineBegin(self, iprint):
        iprint.write(self.__CsTypeDeclaration())
        if self.flags.IsArray():
            iprint.append('[]')
        if self.typeParams:
            iprint.append('<{0}>'.format(', '.join(self.typeParams)))
        if self.baseType or self.interfaces:
            lst = map(lambda t: str(t), ([self.baseType] if self.baseType else []) + (self.interfaces or []))
            iprint.append(' : {0}'.format(', '.join(lst)))
            del(lst)
        iprint()
        iprint('{')

    def CsDefineMembers(self, iprint):
        members = list()

        if self.IsStruct() and self.baseType:
            members += filter(lambda t: not isinstance(t, CtorDef), getattr(self.baseType, 'members', []))

        members += self.members

        if self.IsInterface():
            members = filter(lambda m: not isinstance(m, FieldDef), members)

        for m in members:
            m.CsDefine(iprint)
            if not (isinstance(m, FieldDef) or (m.flags & MemberFlags.Abstract)):
                iprint.AddWhitespace()

    def CsDefineEnd(self, iprint):
        iprint('}} // {0}'.format(self))

    def FullName(self):
        if self.parentScope:
            return str(self.parentScope) + '.' + str(self)
        else:
            return str(self)
            
    def IsInterface(self):
        return self.flags & TypeFlags.Interface

    def IsStruct(self):
        return self.flags & TypeFlags.Struct

    def IsClass(self):
        return self.flags & TypeFlags.Class

    def IsEnum(self):
        return self.flags & TypeFlags.Enum
        
    def CsVisibility(self):
        return CsAccessKeyword(self.flags)
        
    def __str__(self):
        name = super(TypeDef, self).__str__()
        if self.IsInterface():
            if not name.startswith('I') or (name[1] != name[1].upper()):
                return 'I' + name
        return name

#==========================================================================================================
def ClassDef(*args, **kv):
    t = TypeDef(*args, **kv)
    t.flags = TypeFlags((t.flags & ~TypeFlags.TypeMask) | TypeFlags.Class)
    return t

#==========================================================================================================
def StructDef(*args, **kv):
    t = TypeDef(*args, **kv)
    t.flags = TypeFlags((t.flags & ~TypeFlags.TypeMask) | TypeFlags.Struct)
    return t

#==========================================================================================================
def InterfaceDef(*args, **kv):
    t = TypeDef(*args, **kv)
    t.flags = TypeFlags((t.flags & ~TypeFlags.TypeMask) | TypeFlags.Interface)
    return t

#==========================================================================================================
class TypeInst(TypeDefOrRef):
    def __init__(self, genericType, *typeArgs):
        if not typeArgs:
            raise Exception('Invalid type instantiation.')
        while isinstance(genericType, TypeInst):
            typeArgs = genericType.typeArgs + typeArgs
            genericType = genericType.genericType
        if not isinstance(genericType, TypeDef):
            raise Exception('Invalid argument: genericType')
        self.genericType = genericType
        self.typeArgs = typeArgs

    def __str__(self):
        if len(self.genericType.typeParams) != len(self.typeArgs):
            raise Exception('Incomplete type instantiation: {0}<{1}> / {0}<{2}>'.format(self.genericType, self.genericType.typeParams, map(lambda a: str(a), self.typeArgs)))
        return str(self.genericType) + '<{0}>'.format(', '.join(map(lambda a: str(a), self.typeArgs)))

#==========================================================================================================
class MemberDefBase(object):
    @StrictlyType(str, None)
    def __init__(self, name, flags, comment = None):
        self.name = name
        self.flags = flags
        self.comment = comment

    def __str__(self):
        return self.name

    def CsDefineHeader(self, iprint):
        iprint(
'''/// <summary>
/// {}
/// </summary>'''.format(self))

    def CsDefine(self, iprint):
        self.CsComment(iprint)
        self.CsDefineMember(iprint)
        
    def CsComment(self, iprint):
        if self.comment:
            iprint('\n/// ' + self.comment)

    def CsDefineMember(self, iprint):
        pass

    def CsVisibility(self):
        if '.' in str(self):
            return ''
        else:
            return CsAccessKeyword(self.flags)

#==========================================================================================================
class FieldDef(MemberDefBase):
    def __init__(self, name, fieldType, flags = AccessFlags.Public, autoInitialize = False, *args, **kv):
        if not isinstance(fieldType, TypeDefOrRef):
            raise Exception('Invalid field type: {0}'.format(type(fieldType)))
        super(FieldDef, self).__init__(name, flags, *args, **kv)
        if type(fieldType) == str:
            fieldType = TypeDef(fieldType)
        self.fieldType = fieldType
        self.autoInitialize = autoInitialize

    def CsDefineMember(self, iprint):
        decl = ' '.join([self.CsVisibility(), str(self.fieldType), str(self)])
        if self.autoInitialize:
            decl += ' = new {0}()'.format(self.fieldType)
        decl += ';'
        iprint(decl)

#==========================================================================================================
class EmptyArrayDef(MemberDefBase):
    def __init__(self, name, fieldType, flags = AccessFlags.Public, *args, **kv):
        super(EmptyArrayDef, self).__init__(name, flags, *args, **kv)
        self.fieldType = fieldType

    def CsDefineMember(self, iprint):
        decl = self.CsVisibility() + ' static ' + str(self.fieldType) + '[]' + ' ' + str(self)
        decl += ' = new ' + str(self.fieldType) + '[0];';
        iprint(decl)

#==========================================================================================================
def ShouldEmitMethodBody(m):
    if m.flags & MemberFlags.Abstract:
        return False
    elif m.flags & MemberFlags.Partial:
        return False
    # elif m.parentScope.flags & TypeFlags.Abstract:
        # return False
    elif m.parentScope.flags & TypeFlags.Interface:
        return False
    else:
        return True

#==========================================================================================================
class PropertyDef(MemberDefBase):
    def __init__(self, name, propertyType, flags = AccessFlags.Public, getter = None, setter = None, field = None, *args, **kv):
        super(PropertyDef, self).__init__(name, flags, *args, **kv)
        self.propertyType = propertyType
        if not getter and not setter:
            getter = PropertyGetter()
            setter = PropertySetter()
        self.getter = getter
        self.setter = setter
        self.field = field
        if self.field:
            if self.getter:
                self.getter.body = 'return {0};'.format(field)
            if self.setter:
                self.setter.body = '{0} = value;'.format(field)

    def CsDefineMember(self, iprint):
        iprint(' '.join([CsMemberDeclaration(self), str(self.propertyType), str(self)]).strip())
        iprint('{')
        for op in filter(lambda op: op != None, [self.getter, self.setter]):
            op.parentScope = self
            iprint.write(1, ' '.join([op.CsVisibility() if self.flags != op.flags else '', str(op)]).strip())
            if op.body and ShouldEmitMethodBody(self):
                iprint.append('\n')
                iprint(1, '{')
                iprint(2, op.body.strip())
                iprint(1, '}')
            else:
                iprint.append(';\n')
        iprint('}} // {}'.format(self))

#==========================================================================================================
class PropertyOp(object):
    def __init__(self, name, flags = AccessFlags.Public, body = None):
        self.name = name
        self.flags = flags
        self.body = body

    def __str__(self):
        return self.name

    def CsVisibility(self):
        if (self.parentScope.flags & AccessFlags.Public) == 0:
            return ''
        elif (self.flags & AccessFlags.All) != (self.parentScope.flags & AccessFlags.All):
            return CsAccessKeyword(self.flags)
        else:
            return ''

#==========================================================================================================
class PropertyGetter(PropertyOp):
    def __init__(self, flags = AccessFlags.Public, body = None):
        super(PropertyGetter, self).__init__('get', flags, body)

#==========================================================================================================
class PropertySetter(PropertyOp):
    def __init__(self, flags = AccessFlags.Public, body = None):
        super(PropertySetter, self).__init__('set', flags, body)

#==========================================================================================================
class MethodDef(MemberDefBase):
    def __init__(self, name, flags = AccessFlags.Public, sig = ['void', []],
                 body = 'throw new NotImplementedException();', typeParams = None,
                 constraints = None):
        super(MethodDef, self).__init__(name, flags)
        self.sig = sig
        self.body = body
        self.typeParams = typeParams
        self.constraints = constraints
        if flags & MemberFlags.Extension:
            self.flags = MemberFlags(flags | MemberFlags.Static)

    def __str__(self):
        if self.typeParams:
            return self.name + '<' + ', '.join(typeParams) + '>'
        else:
            return self.name

    def IsGeneric(self):
        return self.typeParams != None

    def ReturnType(self):
        return str(self.sig[0] or '')

    def Parameters(self):
        params = '('
        if self.flags & MemberFlags.Extension:
            params += 'this '
        params += ', '.join([' '.join(map(lambda a: str(a), a)) for a in self.sig[1]])
        params += ')'
        return params

    def TypeParameters(self):
        return '<' + ', '.join([str(x) for x in self.typeParams]) + '>'
        
    def CsDefineBody(self, iprint):
        if self.body != None and ShouldEmitMethodBody(self):
            iprint.append('\n')
            iprint('{')
            iprint(1, self.body.strip())
            iprint('}} // {}'.format(self))
        else:
            iprint.append(';\n')

    def CsDefineMember(self, iprint):
        if self.flags & MemberFlags.DebugOnly:
            iprint('[System.Diagnostics.Conditional("DEBUG")]')
        super(MethodDef, self).CsDefineMember(iprint)
        decl = [CsMemberDeclaration(self), self.ReturnType(), self]
        if self.IsGeneric():
            decl += [self.TypeParameters()]
        if self.constraints:
            for constraint in self.constraints:
                decl.append('where {0} : {1}'.format(*constraint))
        iprint.write(' '.join(map(lambda i: str(i).strip(), decl)).strip() + self.Parameters())
        self.CsDefineBody(iprint)

    def CsDefineHeader(self, iprint):
        iprint('''
/// <summary>
/// {}
/// </summary>'''.format(self))

        for pType,pName in self.sig[1]:
            iprint('/// <param name="{}"></param>'.format(pName))
        if self.sig[0] and str(self.sig[0]) != 'void':
            iprint('/// <returns></returns>')

#==========================================================================================================
class CtorDef(MethodDef):
    def __init__(self, flags = AccessFlags.Public, sig = [],
                 body = 'throw new NotImplementedException();', typeParams = None,
                 ctorDelegation = None):
        super(CtorDef, self).__init__('.ctor', flags, [None, sig], body)
        self.name = None
        self.ctorDelegation = ctorDelegation
        
    def __str__(self):
        return str(self.parentScope)
        
    def CsDefineMember(self, iprint):
        if (self.flags & AccessFlags.Public) and not self.body:
            return

        decl = [CsMemberDeclaration(self), self]
        if self.IsGeneric():
            decl += [self.TypeParameters()]
        ctorDel = ''
        if self.ctorDelegation:
            ctorDel = ' : this(' + ', '.join(self.ctorDelegation) + ')'
        iprint.write(' '.join(map(lambda i: str(i).strip(), decl)) + self.Parameters() + ctorDel)
        if ShouldEmitMethodBody(self):
            iprint.append('\n')
            iprint('{')
            iprint(1, self.body.strip())
            iprint('}')
        else:
            iprint.append(';\n')

#==========================================================================================================
class MethodInst(object):
    def __init__(self, methodDef, typeArgs):
        if typeArgs and type(typeArgs) != list:
            typeArgs = [typeArgs]
        if (not typeArgs) or (type(typeArgs) != list):
            raise Exception('Invalid type instantiation.')
        while isinstance(methodDef, MethodInst):
            methodDef = methodDef.methodDef
            typeArgs = methodDef.typeArgs + typeArgs
        self.methodDef = methodDef
        self.typeArgs = typeArgs
    def __str__(self):
        if len(self.methodDef.typeParams) != len(self.typeArgs):
            raise Exception('Incomplete type instantiation.')
        return str(self.methodDef) + '<{0}>'.format(', '.join(self.typeArgs))

#==========================================================================================================
class NamespaceDef(object):
    def __init__(self, name):
        self.parentScope = None
        self.members = MemberSet(self)
        if '.' in name:
            pname, name = '.'.join(name.split('.')[:-1]), name.split('.')[-1]
            self.name = name
            NamespaceDef(pname).members.add(self)
        else:
            self.name = name

    def __str__(self):
        if self.parentScope:
            return str(self.parentScope) + '.' + self.name
        else:
            return self.name

    def CsDefineBegin(self, iprint):
        iprint('namespace {0}\n{{'.format(str(self)))
        
    def CsDefineEnd(self, iprint):
        iprint('}} // {0}'.format(str(self)))

    def CsDefine(self, iprint):
        # Sort the member types in a predictable way.
        def typesortkey(t):
            name = str(t)
            # Remove 'I' prefix from interfaces for sorting.
            if t.IsInterface() and re.match(r'I[A-Z]', name):
                name = name[1:]
            if isinstance(t, EnumDef):
                return (1, name)
            else:
                return (2, name)

        # Sort members for consistent source generation, which enables easier diffing.
        typeMembers = sorted(filter(lambda m: not isinstance(m, NamespaceDef), self.members),lambda x,y: cmp(typesortkey(x), typesortkey(y)))
        
        if len(typeMembers) > 0:
            self.CsDefineBegin(iprint)
            iprint = iprint.indent()
            for m in typeMembers:
                m.CsDefine(iprint)
                iprint.AddWhitespace()
            iprint = iprint.outdent()
            self.CsDefineEnd(iprint)

        nsMembers = filter(lambda m: isinstance(m, NamespaceDef), self.members)
        for m in nsMembers:
            m.CsDefine(iprint)
            iprint.AddWhitespace()

#==========================================================================================================
class CodeBlockDef(object):
    def __init__(self, codeBlock):
        self.codeBlock = codeBlock

    def CsDefine(self, iprint):
        iprint(self.codeBlock)

#==========================================================================================================
class EnumDef(TypeDef):
    def __init__(self, *args, **kv):
        super(EnumDef, self).__init__(*args, **kv)
        self.flags = EnumFlags((self.flags & ~TypeFlags.TypeMask) | TypeFlags.Enum)
        
    def CsDefineHeader(self, iprint):
        super(EnumDef, self).CsDefineHeader(iprint)
        if self.flags.IsHasFlagValues():
            iprint('[Flags]')

    def CsDefineMembers(self, iprint):
        for m in self.members:
            if not isinstance(m, EnumValue):
                raise Exception('Expected enum value')
            if m.value == None:
                m.value = self.__FindValue()
            m.CsDefine(iprint)
            
    def CalculateValues(self):
        for m in self.members:
            if m.value == None:
                m.value = self.__FindValue()
        
    def __FindValue(self):
        value = 1 if self.flags.IsHasFlagValues() else 0
        while reduce(lambda x, y: x or y.value == value, self.members, False):
            if self.flags.IsHasFlagValues():
                value <<= 1
            else:
                value += 1
        return value
        
#==========================================================================================================
class EnumValue(object):
    def __init__(self, name, value = None, comment = None):
        self.name = name
        self.value = value
        self.flags = 0
        self.comment = comment
        
    def CsDefineHeader(self, iprint):
        if self.comment:
            iprint.AddWhitespace()
            iprint('/// {}'.format(self.comment))
        
    def CsDefine(self, iprint):
        self.CsDefineHeader(iprint)
        if self.value != None:
            if self.parentScope.flags & EnumFlags.HasFlagValues:
                iprint('{0} = {1},'.format(self.name, hex(self.value)))
            else:
                iprint('{0} = {1},'.format(self.name, hex(self.value)))
        else:
            iprint('{0},'.format(self.name))
            
    def __str__(self):
        if self.parentScope:
            return str(self.parentScope) + '.' + self.name
        else:
            return self.name
