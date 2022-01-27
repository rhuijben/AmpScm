using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Amp.Git.Implementation
{
    internal class GitQueryVisitor : ExpressionVisitor
    {
        ConstantExpression? _defaultRoot;
 
        // Fold all root references to exxactly the same value
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (typeof(IGitQueryRoot).IsAssignableFrom(node.Type))
            {
                if (_defaultRoot != null)
                    return _defaultRoot;

                if (node.Value != null)
                {
                    return _defaultRoot = node;
                }
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];
                if (typeof(GitSet).IsAssignableFrom(arg.Type))
                {
                    var paramType = node.Method.GetParameters()[0].ParameterType;

                    if (IsSafeQueryableType(paramType, out var elementType))
                    {
                        if (_defaultRoot == null)
                            base.VisitMethodCall(node);

                        if (_defaultRoot == null)
                            throw new InvalidOperationException();

                        var newArguments = node.Arguments.ToArray();

                        newArguments[i] = Expression.Call(_defaultRoot, "GetAll", new[] { elementType! });

                        node = node.Update(node.Object!, newArguments);
                    }
                }
            }

            return base.VisitMethodCall(node);
        }

        private bool IsSafeQueryableType(Type paramType, out Type? elementType)
        {
            if (GitQueryProvider.GetElementType(paramType) is Type elType)
            {
                elementType = elType;
                Type queryableType = typeof(IQueryable<>).MakeGenericType(elType);

                return paramType.IsAssignableFrom(queryableType);
            }
            elementType = null;
            return false;
        }
    }
}
