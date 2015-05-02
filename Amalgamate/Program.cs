using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Amalgamate {
    class Program {
        static void Main (string[] args) {
            var usingMap = new Dictionary<string, UsingDirectiveSyntax>();
            var namespaceMap = new Dictionary<string, List<NamespaceDeclarationSyntax>>();

            // read and process all files in directory
            foreach (var file in Directory.EnumerateFiles(args[0], "*.cs")) {
                // parse the syntax tree
                var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
                var root = syntaxTree.GetCompilationUnitRoot();

                foreach (var child in root.ChildNodes()) {
                    // merge usings and namespaces
                    switch (child.Kind()) {
                        case SyntaxKind.UsingDirective:
                            usingMap[child.ToString()] = (UsingDirectiveSyntax)child;
                            break;

                        case SyntaxKind.NamespaceDeclaration:
                            List<NamespaceDeclarationSyntax> list;
                            var ns = (NamespaceDeclarationSyntax)child;
                            var name = ns.Name.ToString();
                            if (namespaceMap.TryGetValue(name, out list))
                                list.Add(ns);
                            else
                                namespaceMap.Add(name, new List<NamespaceDeclarationSyntax> { ns });
                            break;

                        default:
                            throw new InvalidOperationException($"We don't yet handle top-level nodes of type '{child.Kind()}'");
                    }
                }
            }

            // build the output tree
            var output = CompilationUnit()
                .WithUsings(List(
                    from pair in usingMap
                    orderby pair.Key.TrimEnd(';')   // sort usings by name
                    select pair.Value
                ))
                .WithMembers(List<MemberDeclarationSyntax>(
                    from pair in namespaceMap
                    orderby pair.Key    // sort namespaces by name
                    select NamespaceDeclaration(ParseName(pair.Key))
                        .WithNamespaceKeyword(Token(TriviaList(CarriageReturnLineFeed), SyntaxKind.NamespaceKeyword, TriviaList(Space)))    // namespace Foo
                        .WithOpenBraceToken(Token(TriviaList(Space), SyntaxKind.OpenBraceToken, TriviaList(CarriageReturnLineFeed)))        // K&R brace style
                        .WithMembers(NormalizedList(
                            from namespaceDecl in pair.Value
                            from member in namespaceDecl.Members
                            orderby MemberSortKey(member)   // sort members by accessibility, type, name
                            select member
                        ))
                ));

            File.WriteAllText(args[1], output.ToString());
        }

        static string MemberSortKey (MemberDeclarationSyntax member) {
            var typeDecl = member as BaseTypeDeclarationSyntax;
            if (typeDecl == null) {
                var delegateDecl = member as DelegateDeclarationSyntax;
                if (delegateDecl == null)
                    throw new InvalidOperationException("Unknown member declaration type.");

                return SortDecl(delegateDecl.Modifiers, SyntaxKind.DelegateDeclaration, delegateDecl.Identifier);
            }

            return SortDecl(typeDecl.Modifiers, typeDecl.Kind(), typeDecl.Identifier);
        }

        static string SortDecl (SyntaxTokenList modifiers, SyntaxKind kind, SyntaxToken identifier) {
            var m = modifiers.Any(SyntaxKind.PublicKeyword) ? "A" :
                    modifiers.Any(SyntaxKind.InternalKeyword) ? "B" : "C";

            return $"{m}{KindSortKeys[kind]}{identifier}";
        }

        static SyntaxList<MemberDeclarationSyntax> NormalizedList (IEnumerable<MemberDeclarationSyntax> members) {
            // add newlines between all members
            var last = members.LastOrDefault();
            var result = new List<MemberDeclarationSyntax>();
            foreach (var member in members) {
                var leadingTrivia = member.GetLeadingTrivia();
                int count = 0;
                foreach (var trivia in leadingTrivia) {
                    if (trivia.Kind() != SyntaxKind.EndOfLineTrivia)
                        break;
                    count++;
                }

                var trailingTrivia = member.GetTrailingTrivia();
                if (member != last)
                    trailingTrivia = trailingTrivia.Add(CarriageReturnLineFeed);

                result.Add(member.WithLeadingTrivia(leadingTrivia.Skip(count)).WithTrailingTrivia(trailingTrivia));
            }

            return List(result);
        }

        static readonly Dictionary<SyntaxKind, string> KindSortKeys = new Dictionary<SyntaxKind, string> {
            { SyntaxKind.InterfaceDeclaration, "A" },
            { SyntaxKind.ClassDeclaration, "B" },
            { SyntaxKind.StructDeclaration, "C" },
            { SyntaxKind.EnumDeclaration, "D" },
            { SyntaxKind.DelegateDeclaration, "E" }
        };
    }
}
