using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGeneratorLib
{
    public class DeclarationHolder
    {
        
        public List<FieldDeclarationSyntax> FieldDeclarations { get; }
        public MethodDeclarationSyntax MethodDeclarations { get; }
        public DeclarationHolder(List<FieldDeclarationSyntax> fieldDeclarations, MethodDeclarationSyntax methodDeclarations)
        {
            FieldDeclarations = fieldDeclarations;
            MethodDeclarations = methodDeclarations;
        }


    }
}