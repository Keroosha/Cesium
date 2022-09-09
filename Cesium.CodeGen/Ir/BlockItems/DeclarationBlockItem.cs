using Cesium.Ast;
using Cesium.CodeGen.Contexts;
using Cesium.CodeGen.Extensions;
using Cesium.CodeGen.Ir.Declarations;
using Cesium.CodeGen.Ir.Expressions;
using Cesium.CodeGen.Ir.Types;
using Cesium.Core;

namespace Cesium.CodeGen.Ir.BlockItems;

internal class DeclarationBlockItem : IBlockItem
{
    private readonly IScopedDeclarationInfo _declaration;
    private DeclarationBlockItem(IScopedDeclarationInfo declaration)
    {
        _declaration = declaration;
    }

    public DeclarationBlockItem(Declaration declaration)
        : this(IScopedDeclarationInfo.Of(declaration))
    {
    }

    public IBlockItem Lower(IDeclarationScope scope)
    {
        switch (_declaration)
        {
            case ScopedIdentifierDeclaration scopedDeclaration:
            {
                scopedDeclaration.Deconstruct(out var items);
                List<InitializableDeclarationInfo> newItems = new List<InitializableDeclarationInfo>();
                foreach (var (declaration, initializer) in items)
                {
                    var (type, identifier, cliImportMemberName) = declaration;

                    // TODO[#91]: A place to register whether {type} is const or not.

                    if (identifier == null)
                        throw new CompilationException("An anonymous local declaration isn't supported.");

                    if (cliImportMemberName != null)
                        throw new CompilationException(
                            $"Local declaration with a CLI import member name {cliImportMemberName} isn't supported.");

                    scope.AddVariable(identifier, type);

                    var initializerExpression = initializer;
                    if (initializerExpression != null)
                    {
                        var initializerType = initializerExpression.Lower(scope).GetExpressionType(scope);
                        if (scope.CTypeSystem.IsConversionAvailable(initializerType, type)
                            && !initializerType.Equals(type))
                        {
                            initializerExpression = new TypeCastExpression(type, initializerExpression);
                        }
                    }

                    newItems.Add(new InitializableDeclarationInfo(declaration, initializerExpression?.Lower(scope)));
                }

                return new DeclarationBlockItem(new ScopedIdentifierDeclaration(newItems));
            }
            case TypeDefDeclaration: return this;
            default: throw new WipException(212, $"Unknown kind of declaration: {_declaration}.");
        }
    }


    public void EmitTo(IEmitScope scope)
    {
        switch (_declaration)
        {
            case ScopedIdentifierDeclaration declaration:
                EmitScopedIdentifier(scope, declaration);
                break;
            case TypeDefDeclaration declaration:
                EmitTypeDef(declaration);
                break;
            default:
                throw new WipException(212, $"Unknown kind of declaration: {_declaration}.");
        }
    }

    private static void EmitScopedIdentifier(IEmitScope scope, ScopedIdentifierDeclaration scopedDeclaration)
    {
        scopedDeclaration.Deconstruct(out var declarations);
        foreach (var (declaration, initializer) in declarations)
        {
            var (type, identifier, _) = declaration;
            switch (initializer)
            {
                case null when type is not InPlaceArrayType:
                    continue;
                case null when type is InPlaceArrayType arrayType:
                    arrayType.EmitInitializer(scope);
                    break;
                default:
                    initializer?.EmitTo(scope);
                    break;
            }

            var variable = scope.ResolveVariable(identifier!);
            scope.StLoc(variable);
        }
    }

    private static void EmitTypeDef(TypeDefDeclaration declaration) =>
        throw new WipException(214, $"typedef is not supported at block level, yet: {declaration}.");
}
