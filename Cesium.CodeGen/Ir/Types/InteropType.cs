using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Extensions;
using Cesium.CodeGen.Ir.Expressions;
using Cesium.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Cesium.CodeGen.Ir.Types;

/// <summary>
/// Type that was imported from CLI for Cesium/CLI interop, most likely via <code>__cli_import</code>.
/// </summary>
internal record InteropType(TypeReference UnderlyingType) : IType
{
    public TypeReference Resolve(TranslationUnitContext context) => UnderlyingType;

    public int? GetSizeInBytes(TargetArchitectureSet arch)
    {
        switch (UnderlyingType)
        {
            case { FullName: TypeSystemEx.VoidPtrFullTypeName }:
                return PointerType.SizeInBytes(arch);
            case { IsGenericInstance: true }:
            {
                var parent = UnderlyingType.GetElementType();

                if (parent.FullName is TypeSystemEx.CPtrFullTypeName or TypeSystemEx.FuncPtrFullTypeName)
                    return PointerType.SizeInBytes(arch);
                break;
            }
        }

        throw new AssertException(
            $"{nameof(InteropType)} doesn't know how to get size of an underlying {UnderlyingType}.");
    }

    public void EmitConversion(IEmitScope scope, IExpression expression)
    {
        void EmitExprAndGetPtr()
        {
            expression.EmitTo(scope);
            scope.AddInstruction(OpCodes.Conv_I); // TODO: Should only emit if required.
        }

        var assemblyContext = scope.AssemblyContext;
        if (UnderlyingType.FullName == TypeSystemEx.VoidPtrFullTypeName)
        {
            EmitExprAndGetPtr();
            scope.AddInstruction(OpCodes.Call, assemblyContext.VoidPtrConverter);
            return;
        }

        if (UnderlyingType is GenericInstanceType typeInstance)
        {
            var parent = typeInstance.GetElementType();
            switch (parent.FullName)
            {
                case TypeSystemEx.CPtrFullTypeName:
                    EmitExprAndGetPtr();
                    scope.AddInstruction(
                        OpCodes.Call,
                        assemblyContext.CPtrConverter(typeInstance.GenericArguments.Single()));
                    break;
                case TypeSystemEx.FuncPtrFullTypeName:
                    var funcPtrVariable = new VariableDefinition(UnderlyingType);
                    scope.Method.Body.Variables.Add(funcPtrVariable);
                    scope.AddInstruction(OpCodes.Ldloca, funcPtrVariable); // TODO: Use common mechanism to efficiently address local variables, use ldloca.s when necessary
                    EmitExprAndGetPtr();
                    Instruction.Create(
                        OpCodes.Call,
                        assemblyContext.FuncPtrConstructor(typeInstance.GenericArguments.Single()));
                    break;
                default:
                    throw new AssertException($"No conversion available for interop type {parent}.");
            }
            return;
        }

        throw new AssertException(
            $"{nameof(InteropType)} doesn't know how to get a converter call for an underlying {UnderlyingType}.");
    }
}
