using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotNetDoodle.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class VarDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "TU0001";
        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Avoid Using Implicit Type",
            messageFormat: "Variable declation has implicit type 'var' instead of explicit type",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(Rule);
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.VariableDeclaration);
        }

        // privates

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            VariableDeclarationSyntax variableDeclaration = (VariableDeclarationSyntax)context.Node;
            if (IsLegitVarUsage(variableDeclaration, context.SemanticModel))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, variableDeclaration.Type.GetLocation()));
            }
        }

        private bool IsLegitVarUsage(VariableDeclarationSyntax variableDeclaration, SemanticModel semanticModel)
        {
            bool result;
            TypeSyntax variableTypeName = variableDeclaration.Type;
            if (variableTypeName.IsVar)
            {
                // Implicitly-typed variables cannot have multiple declarators. Short circuit if it does.
                bool hasMultipleVariables = variableDeclaration.Variables.Skip(1).Any();
                if(hasMultipleVariables == false)
                {
                    // Special case: Ensure that 'var' isn't actually an alias to another type. (e.g. using var = System.String).
                    IAliasSymbol aliasInfo = semanticModel.GetAliasInfo(variableTypeName);
                    if (aliasInfo == null)
                    {
                        // Retrieve the type inferred for var.
                        ITypeSymbol type = semanticModel.GetTypeInfo(variableTypeName).ConvertedType;

                        // Special case: Ensure that 'var' isn't actually a type named 'var'.
                        if (type.Name.Equals("var", StringComparison.Ordinal) == false)
                        {
                            // Special case: Ensure that the type is not an anonymous type.
                            if (type.IsAnonymousType == false)
                            {
                                // Special case: Ensure that it's not a 'new' expression.
                                if (variableDeclaration.Variables.First().Initializer.Value.IsKind(SyntaxKind.ObjectCreationExpression))
                                {
                                    result = false;
                                }
                                else
                                {
                                    result = true;
                                }
                            }
                            else
                            {
                                result = false;
                            }
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
                else
                {
                    result = false;
                }
            }
            else
            {
                result = false;
            }

            return result;
        }
    }
}
