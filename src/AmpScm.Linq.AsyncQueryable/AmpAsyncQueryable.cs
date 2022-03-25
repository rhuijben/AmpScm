using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AmpScm.Linq.AsyncQueryable;
using AmpScm.Linq.AsyncQueryable.Wrap;

namespace System.Linq
{
    /// <summary>
    /// Like <see cref="Queryable"/>, but then for lists that implement both <see cref="IQueryable{T}"/> and <see cref="IAsyncEnumerable{T}"/>,
    /// which makes both the <see cref="Queryable"/> and <see cref="AsyncEnumerable"/> apis apply, and give the compiler no way to choose.
    /// </summary>
    /// <remarks>Most work is delegated to <see cref="Queryable"/>, as that handles query building in the way we want it</remarks>
    public static class AmpAsyncQueryable
    {
        internal static MethodInfo GetMethod<T>(Expression<Action<T>> x)
            => ((MethodCallExpression)x.Body).Method.GetGenericMethodDefinition();

        /// <inheritdoc cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})" />
        public static IAsyncQueryable<TSource> Where<TSource>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return (IAsyncQueryable<TSource>)Queryable.Where(source, predicate);
        }

        /// <inheritdoc cref="Queryable.Where{TSource}(IQueryable{TSource}, Expression{Func{TSource, int, bool}})" />
        public static IAsyncQueryable<TSource> Where<TSource>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, int, bool>> predicate)
        {
            return (IAsyncQueryable<TSource>)Queryable.Where(source, predicate);
        }


        /// <inheritdoc cref="Queryable.Select{TSource, TResult}(IQueryable{TSource}, Expression{Func{TSource, TResult}})" />
        public static IAsyncQueryable<TResult> Select<TSource, TResult>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, TResult>> selector)
        {
            return (IAsyncQueryable<TResult>)Queryable.Select(source, selector);
        }

        /// <inheritdoc cref="Queryable.Select{TSource, TResult}(IQueryable{TSource}, Expression{Func{TSource, int, TResult}})" />
        public static IAsyncQueryable<TResult> Select<TSource, TResult>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, int, TResult>> selector)
        {
            return (IAsyncQueryable<TResult>)Queryable.Select(source, selector);
        }

        /// <inheritdoc cref="Queryable.OrderByDescending{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}})"/>
        public static IOrderedAsyncQueryable<TSource> OrderByDescending<TSource, TKey>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.OrderByDescending(source, keySelector);
        }

        /// <inheritdoc cref="Queryable.OrderByDescending{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}}, IComparer{TKey}?)"/>
        public static IOrderedAsyncQueryable<TSource> OrderByDescending<TSource, TKey>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, IComparer<TKey>? comparer)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.OrderByDescending(source, keySelector, comparer);
        }

        /// <inheritdoc cref="Queryable.OrderBy{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}})"/>
        public static IOrderedAsyncQueryable<TSource> OrderBy<TSource, TKey>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.OrderBy(source, keySelector);
        }

        /// <inheritdoc cref="Queryable.OrderBy{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}}, IComparer{TKey}?)"/>
        public static IOrderedQueryable<TSource> OrderBy<TSource, TKey>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, IComparer<TKey>? comparer)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.OrderBy(source, keySelector, comparer);
        }

        /// <inheritdoc cref="Queryable.ThenByDescending{TSource, TKey}(IOrderedQueryable{TSource}, Expression{Func{TSource, TKey}})"/>
        public static IOrderedAsyncQueryable<TSource> ThenByDescending<TSource, TKey>(this IOrderedAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.ThenByDescending(source, keySelector);
        }

        /// <inheritdoc cref="Queryable.ThenByDescending{TSource, TKey}(IOrderedQueryable{TSource}, Expression{Func{TSource, TKey}}, IComparer{TKey}?)"/>
        public static IOrderedAsyncQueryable<TSource> ThenByDescending<TSource, TKey>(this IOrderedAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, IComparer<TKey>? comparer)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.ThenByDescending(source, keySelector, comparer);
        }

        /// <inheritdoc cref="Queryable.ThenBy{TSource, TKey}(IOrderedQueryable{TSource}, Expression{Func{TSource, TKey}})"/>
        public static IOrderedAsyncQueryable<TSource> ThenBy<TSource, TKey>(this IOrderedAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.ThenBy(source, keySelector);
        }

        /// <inheritdoc cref="Queryable.ThenBy{TSource, TKey}(IOrderedQueryable{TSource}, Expression{Func{TSource, TKey}}, IComparer{TKey}?)"/>
        public static IOrderedAsyncQueryable<TSource> ThenBy<TSource, TKey>(this IOrderedAsyncQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector, IComparer<TKey>? comparer)
        {
            return (IOrderedAsyncQueryable<TSource>)Queryable.ThenBy(source, keySelector, comparer);
        }

        /// <inheritdoc cref="Queryable.TakeWhile{TSource}(IQueryable{TSource}, Expression{Func{TSource, bool}})"/>
        public static IAsyncQueryable<TSource> TakeWhile<TSource>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return (IAsyncQueryable<TSource>)Queryable.TakeWhile(source, predicate);
        }

        /// <inheritdoc cref="Queryable.TakeWhile{TSource}(IQueryable{TSource}, Expression{Func{TSource, int, bool}})"/>
        public static IAsyncQueryable<TSource> TakeWhile<TSource>(this IAsyncQueryable<TSource> source, Expression<Func<TSource, int, bool>> predicate)
        {
            return (IAsyncQueryable<TSource>)Queryable.TakeWhile(source, predicate);
        }

        /// <inheritdoc cref="Queryable.Take{TSource}(IQueryable{TSource}, int)"/>
        public static IAsyncQueryable<TSource> Take<TSource>(this IAsyncQueryable<TSource> source, int count)
        {
            return (IAsyncQueryable<TSource>)Queryable.Take(source, count);
        }


        /// <summary>
        /// Converts a generic <see cref="IEnumerable{T}"/> to a generic <see cref="IAsyncQueryable{T}"/>
        /// </summary>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static IAsyncQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is null)
                throw new ArgumentNullException(nameof(enumerable));
            if (enumerable is IAsyncQueryable<T> r)
                return r;
            else if (enumerable is IQueryable<T> q)
                return AsAsyncQueryable(q);

            return AsAsyncQueryable(enumerable.AsQueryable());
        }

        /// <summary>
        /// Converts a generic <see cref="IQueryable{T}"/> to a generic <see cref="IAsyncQueryable{T}"/>
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static IAsyncQueryable<T> AsAsyncQueryable<T>(this IQueryable<T> queryable)
        {
            if (queryable is IAsyncQueryable<T> r)
                return r;
            else
                return new AsyncQueryableWrapper<T>(queryable);
        }

        static MethodInfo _asAsyncQueryable = GetMethod<IQueryable<string>>(x => AmpAsyncQueryable.AsAsyncQueryable(x));
        /// <summary>
        /// Wraps an <see cref="IQueryable"/> as an <see cref="IAsyncQueryable"/>
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        public static IAsyncQueryable AsAsyncQueryable(this IQueryable queryable)
        {
            if (queryable is null)
                throw new ArgumentNullException(nameof(queryable));
            else if (queryable is IAsyncQueryable r)
                return r;
            else
            {
                return (IAsyncQueryable)_asAsyncQueryable.MakeGenericMethod(queryable.ElementType).Invoke(null, new[] { queryable })!;
            }
        }
    }
}
