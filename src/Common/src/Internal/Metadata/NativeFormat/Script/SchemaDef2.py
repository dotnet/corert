# Copyright (c) Microsoft. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

"""
This script defines the metadata schema that is consumed by all of the other scripts. It contains
the following main parts:
 - Declaration of pre-existing types (enums, primitives)
 - Types that encapsulate the schema definition (MemberDef, RecordDef). These are consumed by
   some scripts, but some others consume the schema definition directly (they were not updated
   to consume the encapsulating class instances).
 - Introduced enum definitions (attribute name: enumSchema)
 - 
"""

import os
import sys

if __name__ == '__main__':
    #Dynamically append current script path to PYTHONPATH
    sys.path.append(os.path.dirname(sys.argv[0]))

# from BaseGen import classinit
from copy import copy
from CsCommonGen2 import *
from odict import odict

#==========================================================================================================
enumTypes = odict(sorted([
    ('AssemblyFlags', 'uint'),
    ('AssemblyHashAlgorithm', 'uint'),
    ('CallingConventions', 'ushort'), # System.Reflection.CallingConventions
    ('EventAttributes', 'ushort'),
    ('FieldAttributes', 'ushort'),
    ('FixedArgumentAttributes', 'byte'),
    ('GenericParameterAttributes', 'ushort'),
    ('GenericParameterKind', 'byte'),
    ('MethodAttributes', 'ushort'),
    ('MethodImplAttributes', 'ushort'),
    ('MethodSemanticsAttributes', 'ushort'),
    ('NamedArgumentMemberKind', 'byte'),
    ('ParameterAttributes', 'ushort'),
    ('PInvokeAttributes', 'ushort'),
    ('PropertyAttributes', 'ushort'),
    ('TypeAttributes', 'uint'),
]))

#==========================================================================================================
primitiveTypes = odict(sorted([(
    ('bool', 'Boolean'),
    ('char', 'Char'),
    ('string', 'String'),
    ('byte', 'Byte'),
    ('sbyte', 'SByte'),
    ('short', 'Int16'),
    ('ushort', 'UInt16'),
    ('int', 'Int32'),
    ('uint', 'UInt32'),
    ('long', 'Int64'),
    ('ulong', 'UInt64'),
    ('float', 'Single'),
    ('double', 'Double'))]))

#==========================================================================================================
class RecordDefFlags(Flags):
    """
    Defines the set of flags that may be applied to a schema record definition.
    """
    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('Enum')             # Indicates that the record is actually an enum.
        cls.AddFlag('Flags')            # Indicates that [Flags] should be applied to enum definition.
        cls.AddFlag('CustomCompare')    # Indicates a specific set of members have been flagged for use in
                                        # implementing equality functionality; else all members are used.
        cls.AddFlag('ReentrantEquals')  # The generated Equals method is potentially reentrant on the same instance
                                        # and should have a fast exit path to protect from infinite recursion.
classinit(RecordDefFlags)

#==========================================================================================================
class MemberDefFlags(Flags):
    """
    Defines the set of flags that may be applied to the member definition of a schema record.
    """
    @classmethod
    def __clsinit__(cls):
        cls.AddFlag('Map')              # => Dictionary<string, RecordType> for MetadataWriter
                                        # => List<RecordType> for MetadataReader
        cls.AddFlag('List')             # => List<RecordType>
        cls.AddFlag('Array')            # => RecordType[]
        cls.AddMask('Collection', 'Map', 'List', 'Array')
        cls.AddMask('Sequence', 'List', 'Array')
        cls.AddFlag('RecordRef')        # => RecordTypeHandle
        cls.AddMask('Ref', 'RecordRef')
        cls.AddFlag('Child')            # Member instance is logically defined and owned by record;
                                        # otherwise instance may be shared (such as a TypeRef).
        cls.AddFlag('Name')             # May be used as the member's simple name for diagnostics.
        cls.AddFlag('NotPersisted')     # Indicates member is not written to or read from metadata.
        cls.AddFlag('Compare')          # Indicates member should be used for equality functionality.
        cls.AddFlag('EnumerateForHashCode') # Indicates that the collection is safe to be enumerated in GetHashCode
                                            # without causing reentrancy
classinit(MemberDefFlags)

#==========================================================================================================
class MemberDef(object):
    """
    Encapsulates definition of member in schema record definition.
    """
    def __init__(self, name, typeName = None, flags = MemberDefFlags(0), comment = None, **kv):
        self.name = name
        self.typeName = typeName
        self.flags = MemberDefFlags(int(flags))
        self.comment = comment
        # Add additional (name,value) argument pairs as attributes on this instance.
        for k,v in kv.iteritems():
            setattr(self, k, v)
        
    def __cmp__(self, other):
        return cmp(self.name, other.name)

#==========================================================================================================
class RecordDef(object):
    """
    Encapsulates definition of schema record.
    """
    def __init__(self, name, baseTypeName = None, flags = RecordDefFlags(0), members = [], comment = None, **kv):
        self.name = name
        self.baseTypeName = baseTypeName
        self.flags = RecordDefFlags(int(flags))
        self.comment = comment
        self.members = list()
        for member in members:
            self.members.append(MemberDef(**member))
        # Add additional (name,value) argument pairs as attributes on this instance.
        for k,v in kv.iteritems():
            setattr(self, k, v)
            
    def __str__(self):
        return self.name
        
    def __cmp__(self, other):
        return cmp(self.name, other.name)

#=== Defined Enums ========================================================================================
# Defined as a list of dictionaries of (name, value) pairs. These enums supplement those defined by
# System.Reflection.Primitives.

__enumSchema = [
    # AssemblyFlags - as defined in ECMA
    {   'name': 'AssemblyFlags',
        'baseTypeName': 'uint',
        'flags': RecordDefFlags.Enum | RecordDefFlags.Flags,
        'members': [
            { 'name': 'PublicKey', 'value': 0x0001, 'comment': 'The assembly reference holds the full (unhashed) public key.' },
            { 'name': 'Retargetable', 'value': 0x0100, 'comment': 'The implementation of this assembly used at runtime is not expected to match the version seen at compile time.' },
            { 'name': 'DisableJITcompileOptimizer', 'value': 0x4000, 'comment': 'Reserved.' },
            { 'name': 'EnableJITcompileTracking', 'value': 0x8000, 'comment': 'Reserved.' },
        ],
    },
    # AssemblyHashAlgorithm - as defined in ECMA
    {   'name': 'AssemblyHashAlgorithm',
        'baseTypeName': 'uint',
        'flags': RecordDefFlags.Enum,
        'members': [
            { 'name': 'None', 'value': 0x0000 },
            { 'name': 'Reserved', 'value': 0x8003 },
            { 'name': 'SHA1', 'value': 0x8004 },
        ],
    },
    # FixedArgumentAttributes - used to indicate if an argument for a custom attribute instantiation
    # should be boxed.
    {   'name': 'FixedArgumentAttributes',
        'baseTypeName': 'byte',
        'flags': RecordDefFlags.Enum | RecordDefFlags.Flags,
        'members': [
            { 'name': 'None', 'value': 0 },
            { 'name': 'Boxed', 'comment': 'Values should be boxed as Object' },
        ],
    },
    # NamedArgumentMemberKind - used to disambiguate the referenced members of the named
    # arguments to a custom attribute instance.
    {   'name': 'NamedArgumentMemberKind',
        'baseTypeName': 'byte',
        'flags': RecordDefFlags.Enum,
        'members': [
            { 'name': 'Property', 'comment': 'Specifies the name of a property' },
            { 'name': 'Field',    'comment': 'Specifies the name of a field' },
        ]
    },
    # GenericParameterKind - used to distinguish between generic type and generic method type parameters.
    {   'name': 'GenericParameterKind',
        'baseTypeName': 'byte',
        'flags': RecordDefFlags.Enum,
        'members': [
            { 'name': 'GenericTypeParameter', 'comment': 'Represents a type parameter for a generic type.' },
            { 'name': 'GenericMethodParameter', 'comment': 'Represents a type parameter from a generic method.' },
        ]
    },
]

# Create a list of RecordDef instances based on the contents of __enumSchema
enumSchema = [RecordDef(**enumDef) for enumDef in __enumSchema]

#=== Record schema definition =============================================================================

#--- ConstantXXXValue, ConstantXXXArray, and corresponding Handle types -----------------------------------
# Creates a set of record schema definitions (see format description in 'Metadata records' section below)
# that represent contant primitive type values. Adds concept of constant managed reference, which
# must always have a null value (thus the use of the NotPersisted flag).
__constantValueTypes = sorted([fName for (csName, fName) in primitiveTypes.iteritems()] + ['Reference', 'Handle'])
__constantValueRecordSchema = [('Constant' + fName + 'Value', None, RecordDefFlags(0), [('Value', csName, MemberDefFlags(0))]) \
    for (csName, fName) in primitiveTypes.iteritems()] + \
    [('ConstantReferenceValue', None, RecordDefFlags(0), [('Value', 'Object', MemberDefFlags.NotPersisted)])]

# Creates a set of record schema definitions (see format description in 'Metadata records' section below)
# that represent contant arrays primitive type values. Adds concept of a constant array of handle values (currently used to store
# an array TypeDefOrRefOrSpec handles corresponding to System.Type arguments to the instantiation of a custom attribute, or to store
# custom initialized object[] arrays in custom attributes).
__constantValueArrayTypes = sorted([fName + 'Array' for (csName, fName) in primitiveTypes.iteritems()] + ['HandleArray'])
__constantArrayRecordSchema = [('Constant' + fName + 'Array', None, RecordDefFlags(0), [('Value', csName, MemberDefFlags.Array)]) \
    for (csName, fName) in primitiveTypes.iteritems()] + \
    [('ConstantHandleArray', None, RecordDefFlags(0), [('Value', 'Handle', MemberDefFlags.RecordRef | MemberDefFlags.List)])]
    
# Combines constant value and array schema record definitions
__constantRecordSchema = sorted(__constantValueRecordSchema + __constantArrayRecordSchema, key = lambda x: x[0])

#--- Common tuple definitions -----------------------------------------------------------------------------
TypeDefOrRef = ('TypeDefinition', 'TypeReference')
TypeDefOrRefOrSpec = TypeDefOrRef + ('TypeSpecification',)
TypeSig = ('TypeInstantiationSignature', 'SZArraySignature', 'ArraySignature', 'PointerSignature', 'ByReferenceSignature', 'TypeVariableSignature', 'MethodTypeVariableSignature')
TypeDefOrRefOrSpecOrConstant = TypeDefOrRefOrSpec + tuple(ct[0] for ct in __constantRecordSchema)

#--- Metadata records -------------------------------------------------------------------------------------
# The record schema is defined as a list of tuples, one for each record type.
# Record tuple format: (name, base type (not currently used), flags, [members])
# Member tuple format: (name, type, flags)
# These are largely based on the definitions in ECMA335.

__recordSchema = [
    ('TypeDefinition', None, RecordDefFlags.CustomCompare, [
        ('Flags', 'TypeAttributes', MemberDefFlags(0)),
        ('BaseType', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('NamespaceDefinition', 'NamespaceDefinition', MemberDefFlags.RecordRef | MemberDefFlags.Compare),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Compare),
        ('Size', 'uint', MemberDefFlags(0)),
        ('PackingSize', 'uint', MemberDefFlags(0)),
        ('EnclosingType', 'TypeDefinition', MemberDefFlags.RecordRef | MemberDefFlags.Compare),
        ('NestedTypes', 'TypeDefinition', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Methods', 'Method', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Fields', 'Field', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Properties', 'Property', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Events', 'Event', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('GenericParameters', 'GenericParameter', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Interfaces', TypeDefOrRefOrSpec, MemberDefFlags.List | MemberDefFlags.RecordRef),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('TypeReference', None, RecordDefFlags(0), [
        ('ParentNamespaceOrType', ('NamespaceReference', 'TypeReference'), MemberDefFlags.RecordRef),
        ('TypeName', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Name),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('TypeSpecification', None, RecordDefFlags(0), [
        ('Signature', TypeDefOrRef + TypeSig, MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('ScopeDefinition', None, RecordDefFlags.CustomCompare, [
        ('Flags', 'AssemblyFlags', MemberDefFlags.Compare),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Compare),
        ('HashAlgorithm', 'AssemblyHashAlgorithm', MemberDefFlags.Compare),
        ('MajorVersion', 'ushort', MemberDefFlags.Compare),
        ('MinorVersion', 'ushort', MemberDefFlags.Compare),
        ('BuildNumber', 'ushort', MemberDefFlags.Compare),
        ('RevisionNumber', 'ushort', MemberDefFlags.Compare),
        ('PublicKey', 'byte', MemberDefFlags.Array | MemberDefFlags.Compare),
        ('Culture', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Compare),
        ('RootNamespaceDefinition', 'NamespaceDefinition', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('ScopeReference', None, RecordDefFlags(0), [
        ('Flags', 'AssemblyFlags', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('MajorVersion', 'ushort', MemberDefFlags(0)),
        ('MinorVersion', 'ushort', MemberDefFlags(0)),
        ('BuildNumber', 'ushort', MemberDefFlags(0)),
        ('RevisionNumber', 'ushort', MemberDefFlags(0)),
        ('PublicKeyOrToken', 'byte', MemberDefFlags.Array),
        ('Culture', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('NamespaceDefinition', None, RecordDefFlags.CustomCompare, [
        ('ParentScopeOrNamespace', ('NamespaceDefinition', 'ScopeDefinition'), MemberDefFlags.RecordRef | MemberDefFlags.Compare),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Compare),
        ('TypeDefinitions', 'TypeDefinition', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('TypeForwarders', 'TypeForwarder', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('NamespaceDefinitions', 'NamespaceDefinition', MemberDefFlags.Map | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('NamespaceReference', None, RecordDefFlags(0), [
        ('ParentScopeOrNamespace', ('NamespaceReference', 'ScopeReference'), MemberDefFlags.RecordRef),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('Method', None, RecordDefFlags(0), [
        ('RVA', 'uint', MemberDefFlags(0)),
        ('Flags', 'MethodAttributes', MemberDefFlags(0)),
        ('ImplFlags', 'MethodImplAttributes', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Signature', 'MethodSignature', MemberDefFlags.RecordRef | MemberDefFlags.Child ),
        ('Parameters', 'Parameter', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ('GenericParameters', 'GenericParameter', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ('MethodImpls', 'MethodImpl', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('MethodInstantiation', None, RecordDefFlags(0), [
        ('Method', ('Method', 'MemberReference'), MemberDefFlags.RecordRef),
        ('Instantiation', 'MethodSignature', MemberDefFlags.RecordRef),
        ]),
    ('MemberReference', None, RecordDefFlags(0), [
        ('Parent', ('Method',) + TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Signature', ('MethodSignature', 'FieldSignature'), MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('Field', None, RecordDefFlags(0), [
        ('Flags', 'FieldAttributes', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Signature', 'FieldSignature', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('DefaultValue', TypeDefOrRefOrSpecOrConstant, MemberDefFlags.RecordRef),
        ('Offset', 'uint', MemberDefFlags(0)),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('Property', None, RecordDefFlags(0), [
        ('Flags', 'PropertyAttributes', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Signature', 'PropertySignature', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('MethodSemantics', 'MethodSemantics', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ('DefaultValue', TypeDefOrRefOrSpecOrConstant, MemberDefFlags.RecordRef),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('Event', None, RecordDefFlags(0), [
        ('Flags', 'EventAttributes', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('MethodSemantics', 'MethodSemantics', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('CustomAttribute', None, RecordDefFlags.ReentrantEquals, [
        ('Type', TypeDefOrRef, MemberDefFlags.RecordRef),
        ('Constructor', ('Method', 'MemberReference'), MemberDefFlags.RecordRef),
        ('FixedArguments', 'FixedArgument', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ('NamedArguments', 'NamedArgument', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('FixedArgument', None, RecordDefFlags(0), [
        ('Flags', 'FixedArgumentAttributes', MemberDefFlags(0)),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('Value', TypeDefOrRefOrSpecOrConstant, MemberDefFlags.RecordRef),
        ]),
    ('NamedArgument', None, RecordDefFlags(0), [
        ('Flags', 'NamedArgumentMemberKind', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Value', 'FixedArgument', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('GenericParameter', None, RecordDefFlags(0), [
        ('Number', 'ushort', MemberDefFlags(0)),
        ('Flags', 'GenericParameterAttributes', MemberDefFlags(0)),
        ('Kind', 'GenericParameterKind', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('Constraints', TypeDefOrRefOrSpec, MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.EnumerateForHashCode),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('MethodImpl', None, RecordDefFlags(0), [
        ('MethodDeclaration', ('Method', 'MemberReference'), MemberDefFlags.RecordRef),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('Parameter', None, RecordDefFlags(0), [
        ('Flags', 'ParameterAttributes', MemberDefFlags(0)),
        ('Sequence', 'ushort', MemberDefFlags(0)),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('DefaultValue', TypeDefOrRefOrSpecOrConstant, MemberDefFlags.RecordRef),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('MethodSemantics', None, RecordDefFlags(0), [
        ('Attributes', 'MethodSemanticsAttributes', MemberDefFlags(0)),
        ('Method', 'Method', MemberDefFlags.RecordRef),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('TypeInstantiationSignature', None, RecordDefFlags(0), [
        ('GenericType', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('GenericTypeArguments', TypeDefOrRefOrSpec, MemberDefFlags.List | MemberDefFlags.RecordRef),
        ]),
    ('SZArraySignature', None, RecordDefFlags(0), [
        ('ElementType', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    ('ArraySignature', None, RecordDefFlags(0), [
        ('ElementType', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('Rank', 'int', MemberDefFlags(0)),
        ('Sizes', 'int', MemberDefFlags.Array),
        ('LowerBounds', 'int', MemberDefFlags.Array),
        ]),
    ('ByReferenceSignature', None, RecordDefFlags(0), [
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    ('PointerSignature', None, RecordDefFlags(0), [
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    ('TypeVariableSignature', None, RecordDefFlags(0), [
        ('Number', 'int', MemberDefFlags(0)),
        ]),
    ('MethodTypeVariableSignature', None, RecordDefFlags(0), [
        ('Number', 'int', MemberDefFlags(0)),
        ]),
    ('FieldSignature', None, RecordDefFlags(0), [
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('CustomModifiers', 'CustomModifier', MemberDefFlags.List | MemberDefFlags.RecordRef),
        ]),
    ('PropertySignature', None, RecordDefFlags(0), [
        ('CallingConvention', 'CallingConventions', MemberDefFlags(0)),
        ('CustomModifiers', 'CustomModifier', MemberDefFlags.List | MemberDefFlags.RecordRef),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ('Parameters', 'ParameterTypeSignature', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('MethodSignature', None, RecordDefFlags(0), [
        ('CallingConvention', 'CallingConventions', MemberDefFlags(0)),
        ('GenericParameterCount', 'int', MemberDefFlags(0)),
        ('ReturnType', 'ReturnTypeSignature', MemberDefFlags.RecordRef),
        ('Parameters', 'ParameterTypeSignature', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.EnumerateForHashCode),
        ('VarArgParameters', 'ParameterTypeSignature', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.EnumerateForHashCode),
        ]),
    ('ReturnTypeSignature', None, RecordDefFlags(0), [
        ('CustomModifiers', 'CustomModifier', MemberDefFlags.List | MemberDefFlags.RecordRef),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    ('ParameterTypeSignature', None, RecordDefFlags(0), [
        ('CustomModifiers', 'CustomModifier', MemberDefFlags.List | MemberDefFlags.RecordRef),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    ('TypeForwarder', None, RecordDefFlags(0), [
        ('Scope', 'ScopeReference', MemberDefFlags.RecordRef),
        ('Name', 'ConstantStringValue', MemberDefFlags.RecordRef | MemberDefFlags.Child | MemberDefFlags.Name),
        ('NestedTypes', 'TypeForwarder', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ('CustomAttributes', 'CustomAttribute', MemberDefFlags.List | MemberDefFlags.RecordRef | MemberDefFlags.Child),
        ]),
    ('CustomModifier', None, RecordDefFlags(0), [
        ('IsOptional', 'bool', MemberDefFlags(0)),
        ('Type', TypeDefOrRefOrSpec, MemberDefFlags.RecordRef),
        ]),
    # ConstantXXXValue provided in __constantRecordSchema and appened to end of this list.
] + __constantRecordSchema

# Ordered dictionary of schema records (as defined above)
recordSchema = odict([(rName, rMembers) for rName, rBase, rFlags, rMembers in __recordSchema])

# Ordered dictionary of RecordDef instances
recordDefs = odict([(rName, RecordDef(rName, rBase, rFlags,
                                        [ { 'name': mName, 'typeName': mType, 'flags': mFlags } for mName,mType,mFlags in rMembers])) for rName, rBase, rFlags, rMembers in __recordSchema])

# Used to enable some custom actions for string records.
stringRecordTypes = set(['ConstantStringValue'])
    
# Contains a list of records with corresponding Handle types (currently all of them).
handleSchema = [n for n in recordSchema.iterkeys()]

# Print out some diagnostic info if script is directly invoked.
if __name__ == '__main__':
    print '\nRecordFlags flag values:'
    for n,v in RecordDefFlags.iterflags():
        print '    {} : 0x{:x}'.format(n,v)

    print '\nRecordFlags mask values:'
    for n,v in RecordDefFlags.itermasks():
        print '    {} : 0x{:x}'.format(n,v)

    print '\nRecordMemberFlags flag values:'
    for n,v in MemberDefFlags.iterflags():
        print '    {} : 0x{:x}'.format(n,v)

    print '\nRecordMemberFlags mask values:'
    for n,v in MemberDefFlags.itermasks():
        print '    {} : 0x{:x}'.format(n,v)

    print '\nRecords:'
    for k in recordSchema.iterkeys():
        print '    ' + k

    print '\nHandles:'
    for k in handleSchema:
        print '    ' + k
