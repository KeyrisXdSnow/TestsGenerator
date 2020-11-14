using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestsGeneratorLib
{
    public class DeclarationHolder
    {
        
        public List<FieldDeclarationSyntax> FieldDeclarations { get; }
        public SyntaxList<MemberDeclarationSyntax> MethodDeclarations { get; }
        public DeclarationHolder(List<FieldDeclarationSyntax> fieldDeclarations, SyntaxList<MemberDeclarationSyntax> methodDeclarations)
        {
            FieldDeclarations = fieldDeclarations;
            MethodDeclarations = methodDeclarations;
        }


    }
}