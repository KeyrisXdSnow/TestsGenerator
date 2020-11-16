using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGeneratorLib
{
    public class DeclarationContainer
    {
        
        public List<FieldDeclarationSyntax> FieldDeclarations { get; }
        public MethodDeclarationSyntax MethodDeclarations { get; }
        public DeclarationContainer(List<FieldDeclarationSyntax> fieldDeclarations, MethodDeclarationSyntax methodDeclarations)
        {
            FieldDeclarations = fieldDeclarations;
            MethodDeclarations = methodDeclarations;
        }


    }
}