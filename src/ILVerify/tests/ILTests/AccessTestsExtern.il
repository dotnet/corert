// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly extern AccessTests
{
}

.assembly AccessTestsExternal
{
}

.class public auto ansi beforefieldinit AccessTestsType
        extends [System.Runtime]System.Object
{
    .method public hidebysig instance void Instantiate.PublicClass_Valid() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.PrivateClass_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]PrivateClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.PublicNested_Valid() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/PublicNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.PrivateNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/PrivateNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.AssemNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/AssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.AssemOrFamNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyOrAssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.AssemAndFamNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyAndAssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.FamilyNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Call.PrivateMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::PrivateMethod()
        ret
    }

    .method public hidebysig instance void Call.FamilyMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyMethod()
        ret
    }

    .method public hidebysig instance void Call.AssemblyMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::AssemblyMethod()
        ret
    }

    .method public hidebysig instance void Call.FamOrAssemMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyOrAssemblyMethod()
        ret
    }

    .method public hidebysig instance void Call.FamAndAssemMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyAndAssemblyMethod()
        ret
    }
}

.class public auto ansi beforefieldinit DerivedType
        extends [AccessTests]SimpleClass
{
    .method public hidebysig instance void Call.DerivedFamMethod_Valid() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyMethod()
        ret
    }

    .method public hidebysig instance void Call.DerivedFamOrAssemblyMethod_Valid() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyOrAssemblyMethod()
        ret
    }

    .method public hidebysig instance void Call.DerivedFamAndAssemblyMethod_Invalid_MethodAccess() cil managed
    {
        call      void [AccessTests]SimpleClass::FamilyAndAssemblyMethod()
        ret
    }
}

.class public auto ansi beforefieldinit DerivedClass
        extends [AccessTests]SimpleClass
{
    .method public hidebysig instance void Instantiate.FamAndAssemblyNestedClassFromDerived_Invalid_MethodAccess() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyAndAssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.FamOrAssemblyNestedClassFromDerived_Valid() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyOrAssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void Instantiate.FamNestedClassFromDerived_Valid() cil managed
    {
        newobj      instance void [AccessTests]SimpleClass/FamilyNestedClass::.ctor()
        pop
        ret
    }
}
