﻿using System;
using System.Collections.Generic;
using Mono.Cecil;
using Telerik.JustDecompiler.Ast;
using Telerik.JustDecompiler.Decompiler;
using Telerik.JustDecompiler.Ast.Statements;
using Telerik.JustDecompiler.Steps.CodePatterns;
using Telerik.JustDecompiler.Ast.Expressions;

namespace Telerik.JustDecompiler.Steps
{
	class CodePatternsStep : BaseCodeTransformer, IDecompilationStep
	{
        private readonly bool isAggressive;

		private DecompilationContext context;
        private TypeSystem typeSystem;
        private CodePatternsContext patternsContext;

        public CodePatternsStep(bool isAggressive)
        {
            this.isAggressive = isAggressive;
        }

		public BlockStatement Process(DecompilationContext context, BlockStatement body)
		{
			this.context = context;
            this.typeSystem = context.MethodContext.Method.Module.TypeSystem;
            this.patternsContext = new CodePatternsContext(body);
			body = (BlockStatement)Visit(body);
			return body;
		}

		public override ICodeNode VisitBlockStatement(BlockStatement node)
		{
			node = (BlockStatement)base.VisitBlockStatement(node);

			if (node == null)
			{
				return node;
			}

            ICodePattern[] patternArray = new ICodePattern[]
												{
                                                    new NullCoalescingPattern(patternsContext, context.MethodContext),
                                                    GetTernaryPattern(patternsContext),
                                                    new ArrayInitialisationPattern(patternsContext, typeSystem),
                                                    new ObjectInitialisationPattern(patternsContext, typeSystem),
                                                    new CollectionInitializationPattern(patternsContext, typeSystem),
                                                    GetVariableInliningPattern(patternsContext),
                                                };

			bool matched = false;
			do
			{
				matched = false;
				Statement resultedStatement;
				int replacedStatementsCount;
				int startIndex;
				foreach (ICodePattern pattern in patternArray)
				{
					if(pattern.TryMatch(node.Statements, out startIndex, out resultedStatement, out replacedStatementsCount))
					{
						matched = true;
						if (resultedStatement != null)
						{
							RemoveRangeAndInsert(node, startIndex, replacedStatementsCount, resultedStatement);
						}
						else
						{
							RemoveRange(node.Statements, startIndex, replacedStatementsCount);
						}
						break;
					}
				}
			}while(matched);
			return node;
		}

        private VariableInliningPattern GetVariableInliningPattern(CodePatternsContext patternsContext)
        {
            return isAggressive ? new VariableInliningPatternAggressive(patternsContext, context.MethodContext) : new VariableInliningPattern(patternsContext, context.MethodContext);
        }

        private TernaryConditionPattern GetTernaryPattern(CodePatternsContext patternsContext)
        {
            return isAggressive ? new TernaryConditionPatternAgressive(patternsContext, typeSystem) : new TernaryConditionPattern(patternsContext, typeSystem);
        }

		private void RemoveRangeAndInsert(BlockStatement block, int startIndex, int length, Statement newStatement)
		{
			string label = block.Statements[startIndex].Label;
			RemoveRange(block.Statements, startIndex, length);
			newStatement.Label = label;
			if (!string.IsNullOrEmpty(label))
			{
				context.MethodContext.GotoLabels[label] = newStatement;
			}
			block.AddStatementAt(startIndex, newStatement);
		}

		private void RemoveRange(StatementCollection statements, int startIndex, int length)
		{
			if (length == 0)
			{
				return;
			}
			int count = statements.Count;
			for (int i = startIndex; i + length < count; i++)
			{
				statements[i] = statements[i + length];
			}

			while (length > 0)
			{
				statements.RemoveAt(--count);
				length--;
			}
		}
	
	}
}
