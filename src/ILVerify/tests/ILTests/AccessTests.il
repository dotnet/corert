// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern System.Runtime
{
}

.assembly AccessTests
{
}

.class private auto ansi beforefieldinit PrivateClass
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call        instance void [System.Runtime]System.Object::.ctor()
        ret
    }
}

.class public auto ansi beforefieldinit SimpleClass
        extends [System.Runtime]System.Object
{
    .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
    {
        ldarg.0
        call        instance void [System.Runtime]System.Object::.ctor()
        ret
    }

    .method private hidebysig static void PrivateMethod() cil managed 
    {
        ret
    }

    .method family hidebysig static void FamilyMethod() cil managed 
    {
        ret
    }

    .method assembly hidebysig static void AssemblyMethod() cil managed 
    {
        ret
    }

    .method famorassem hidebysig static void FamilyOrAssemblyMethod() cil managed 
    {
        ret
    }

    .method famandassem hidebysig static void FamilyAndAssemblyMethod() cil managed 
    {
        ret
    }

    .class nested public auto ansi beforefieldinit PublicNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }

    .class nested private auto ansi beforefieldinit PrivateNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }

    .class nested assembly auto ansi beforefieldinit AssemblyNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }

    .class nested famorassem auto ansi beforefieldinit AssemblyOrFamilyNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }

    .class nested famandassem auto ansi beforefieldinit AssemblyAndFamilyNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }

    .class nested family auto ansi beforefieldinit FamilyNestedClass
        extends [System.Runtime]System.Object
    {
        .method public hidebysig specialname rtspecialname instance void .ctor() cil managed 
        {
            ldarg.0
            call        instance void [System.Runtime]System.Object::.ctor()
            ret
        }
    }
}

.class public auto ansi beforefieldinit AccessTestsType
        extends [System.Runtime]System.Object
{
    .method public hidebysig instance void InstantiatePublicNested_Valid() cil managed
    {
        newobj      instance void SimpleClass/PublicNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void InstantiatePrivateNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void SimpleClass/PrivateNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void InstantiateAssemNested_Valid() cil managed
    {
        newobj      instance void SimpleClass/AssemblyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void InstantiateAssemOrFamNested_Valid() cil managed
    {
        newobj      instance void SimpleClass/AssemblyOrFamilyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void InstantiateAssemAndFamNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void SimpleClass/AssemblyAndFamilyNestedClass::.ctor()
        pop
        ret
    }

    .method public hidebysig instance void InstantiateFamilyNested_Invalid_MethodAccess() cil managed
    {
        newobj      instance void SimpleClass/FamilyNestedClass::.ctor()
        pop
        ret
    }
}

.class public auto ansi beforefieldinit DerivedType
        extends SimpleClass
{
    .method public hidebysig instance void Call.DerivedFamAndAssemblyMethod_Valid() cil managed
    {
        call      void SimpleClass::FamilyAndAssemblyMethod()
        ret
    }
}
