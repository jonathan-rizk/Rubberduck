﻿using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.VBA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rubberduck.Inspections.Concrete
{
    public class SelectExpressionContextVisitor : IParseTreeVisitor<string> //ContextValueVisitor
    {
        private readonly RubberduckParserState _state;
        private ParseTreeValue _parseTreeValue;
        private string _typeNameResult;

        public SelectExpressionContextVisitor(RubberduckParserState state) //: base(state)
        {
            _state = state;
            _parseTreeValue = new ParseTreeValue(string.Empty);
            _typeNameResult = string.Empty;
        }

        public string SelectCaseEvaluationType => _typeNameResult;

        public string Visit(IParseTree tree)
        {
            return string.Empty;
        }

        public string VisitChildren(IRuleNode node)
        {
            if (node is VBAParser.SelectCaseStmtContext selectStmt)
            {
                var theTypeName = string.Empty;
                var prCtxt = selectStmt.selectExpression() as ParserRuleContext;
                if (prCtxt.children.Any(child => IsLogicalContext(child) || IsTrueFalseLiteral(child)))
                {
                    _typeNameResult = Tokens.Boolean;
                    return _typeNameResult;
                }

                var smplName = prCtxt.GetDescendent<VBAParser.SimpleNameExprContext>();
                if (SymbolList.TypeHintToTypeName.TryGetValue(smplName.GetText().Last().ToString(), out theTypeName))
                {
                    _typeNameResult = theTypeName;
                    return _typeNameResult;
                }

                var visitor = new ContextValueVisitor(_state);
                Visit(prCtxt);
                prCtxt.Accept(visitor);

                if (visitor.UnresolvedContexts.Any())
                {
                    var unresolvedContextTypeNames = visitor.UnresolvedContexts.Values.Where(val => val.HasDeclaredTypeName).Select(val => val.DeclaredTypeName);
                    if (TryDetermineEvaluationTypeFromTypes(unresolvedContextTypeNames, out theTypeName))
                    {
                        _typeNameResult = theTypeName;
                        return _typeNameResult;
                    }
                }
                else
                {
                    var resolvedContextTypeNames = visitor.ValueResolvedContexts.Values.Where(val => val.HasDeclaredTypeName).Select(val => val.DeclaredTypeName);

                    if (TryDetermineEvaluationTypeFromTypes(resolvedContextTypeNames, out theTypeName))
                    {
                        _typeNameResult = theTypeName;
                        return _typeNameResult;
                    }
                }

                var rgClauses = selectStmt.caseClause().SelectMany(cc => cc.rangeClause());

                var exprValues = new List<ParseTreeValue>();
                var exprs = rgClauses.SelectMany(rg => rg.GetDescendents())
                    .Where(expr => expr is VBAParser.LExprContext || expr is VBAParser.LiteralExprContext);
                foreach (var expr in exprs)
                {
                    visitor = new ContextValueVisitor(_state);
                    expr.Accept(visitor);
                    exprValues.AddRange(visitor.UnresolvedContexts.Values);
                    exprValues.AddRange(visitor.ValueResolvedContexts.Values);
                }

                var typeNames = exprValues.Where(expr => expr.DeclaredTypeName != Tokens.Variant).Select(expr => expr.UseageTypeName);
                if (TryDetermineEvaluationTypeFromTypes(typeNames, out string typeName))
                {
                    return typeName;
                }

                //If Strings are in the mix and prevent resolution to a type, we remove them
                //here and see if a resolution becomes possible.  The strings will be converted to the
                //final type during subsequent unreachable analysis.  If they cannot be converted to
                //the "Evaluation Type", they will be flagged as mismatching e.g., "45" converts to a number
                //but "foo" will not.
                var modifiedNames = typeNames.ToList();
                modifiedNames.RemoveAll(tn => tn.Equals(Tokens.String));
                if (TryDetermineEvaluationTypeFromTypes(modifiedNames, out typeName))
                {
                    _typeNameResult =  typeName;
                    return _typeNameResult;
                }
            }

            return string.Empty;
        }

        private bool TryDetermineEvaluationTypeFromTypes(IEnumerable<string> typeNames, out string typeName)
        {
            typeName = string.Empty;
            var typeList = typeNames.ToList();
            typeList.Remove(Tokens.Variant);
            if (!typeList.Any())
            {
                return false;
            }
            //To select "String" or "Currency", all types in the typelist must match
            if (typeList.All(tn => tn.Equals(typeList.First())))
            {
                typeName = typeList.First();
                return true;
            }

            var nextType = new string[] { Tokens.Long, Tokens.LongLong, Tokens.Integer, Tokens.Byte };
            var result = typeList.All(tn => nextType.Contains(tn));
            if (result)
            {
                typeName = Tokens.Long;
                return true;
            }

            nextType = new string[] { Tokens.Long, Tokens.LongLong, Tokens.Integer, Tokens.Byte, Tokens.Single, Tokens.Double };
            result = typeList.All(tn => nextType.Contains(tn));
            if (result)
            {
                typeName = Tokens.Double;
                return true;
            }
            return false;
        }

        //public string GetSelectCaseEvaluationType(IEnumerable<VBAParser.RangeClauseContext> rgClauses, string selectCaseVariable = "")
        //{
        //    var exprValues = new List<ParseTreeValue>();
        //    var exprs = rgClauses.SelectMany(rg => rg.GetDescendents())
        //        .Where(expr => expr is VBAParser.LExprContext || expr is VBAParser.LiteralExprContext);
        //    foreach (var expr in exprs)
        //    {
        //        var visitor = new ContextValueVisitor(_state);
        //        expr.Accept(visitor);
        //        exprValues.AddRange(visitor.UnresolvedContexts.Values);
        //        exprValues.AddRange(visitor.ValueResolvedContexts.Values);
        //    }

        //    var typeNames = exprValues.Where(expr => expr.DeclaredTypeName != Tokens.Variant).Select(expr => expr.UseageTypeName);
        //    if (TryDetermineEvaluationTypeFromTypes(typeNames, out string typeName))
        //    {
        //        return typeName;
        //    }

        //    //If Strings are in the mix and prevent resolution to a type, we remove them
        //    //here and see if a resolution becomes possible.  The strings will be converted to the
        //    //final type during subsequent unreachable analysis.  If they cannot be converted to
        //    //the "Evaluation Type", they will be flagged as mismatching e.g., "45" converts to a number
        //    //but "foo" will not.
        //    var modifiedNames = typeNames.ToList();
        //    modifiedNames.RemoveAll(tn => tn.Equals(Tokens.String));
        //    if (TryDetermineEvaluationTypeFromTypes(modifiedNames, out typeName))
        //    {
        //        return typeName;
        //    }
        //    return string.Empty;
        //}

        public string VisitErrorNode(IErrorNode node)
        {
            return string.Empty;
        }

        public string VisitTerminal(ITerminalNode node)
        {
            return string.Empty;
        }

        private bool IsLogicalContext<T>(T child)
        {
            return child is VBAParser.RelationalOpContext
                || child is VBAParser.LogicalXorOpContext
                || child is VBAParser.LogicalAndOpContext
                || child is VBAParser.LogicalOrOpContext
                || child is VBAParser.LogicalEqvOpContext
                || child is VBAParser.LogicalNotOpContext;
        }

        private bool IsTrueFalseLiteral<T>(T child)
        {
            if (child is VBAParser.LiteralExprContext)
            {
                var litExpr = child as VBAParser.LiteralExprContext;
                return litExpr.GetText().Equals(Tokens.True) || litExpr.GetText().Equals(Tokens.False);
            }
            return false;
        }
    }
}
