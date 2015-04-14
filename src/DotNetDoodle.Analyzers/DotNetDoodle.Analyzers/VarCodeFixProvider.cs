using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetDoodle.Analyzers
{
    [ExportCodeFixProvider(VarDiagnosticAnalyzer.DiagnosticId, LanguageNames.CSharp)]
    public class VarCodeFixProvider : CodeFixProvider
    {
        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(VarDiagnosticAnalyzer.DiagnosticId); }
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            Diagnostic diagnostic = context.Diagnostics.First();
            TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            VariableDeclarationSyntax variableDeclaration = root
                .FindToken(diagnosticSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<VariableDeclarationSyntax>()
                .First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(CodeAction.Create(
                    "Replace 'var' with explicit Type",
                    cancellationToken => ReplaceVarWithExplicitType(context.Document, variableDeclaration, cancellationToken)
                ),
                diagnostic
            );
        }

        public async Task<Document> ReplaceVarWithExplicitType(Document document, VariableDeclarationSyntax variableDeclaration, CancellationToken cancellationToken)
        {
            TypeSyntax variableTypeName = variableDeclaration.Type;
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            // Retrieve the type inferred for var.
            ITypeSymbol type = semanticModel.GetTypeInfo(variableTypeName).ConvertedType;

            // Create a new TypeSyntax for the inferred type. Be careful
            // to keep any leading and trailing trivia from the var keyword.
            TypeSyntax typeName = SyntaxFactory.ParseTypeName(type.ToDisplayString())
                .WithLeadingTrivia(variableTypeName.GetLeadingTrivia())
                .WithTrailingTrivia(variableTypeName.GetTrailingTrivia());

            // Add an annotation to simplify the type name.
            TypeSyntax simplifiedTypeName = typeName.WithAdditionalAnnotations(Simplifier.Annotation);

            // Replace the type in the variable declaration with the siplified name and formatting.
            VariableDeclarationSyntax explicitVariableDeclaration = variableDeclaration
                .WithType(simplifiedTypeName)
                .WithAdditionalAnnotations(Formatter.Annotation);

            // Replace the old local declaration with the new local declaration.
            SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken);
            SyntaxNode newRoot = root.ReplaceNode(variableDeclaration, explicitVariableDeclaration);

            // Return document with transformed tree.
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
