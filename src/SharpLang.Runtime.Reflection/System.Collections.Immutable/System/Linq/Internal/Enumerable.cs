// Copyright (c) 2014 SharpLang - Virgile Bello
// Extracted from mono's Enumerable.cs
//
// Enumerable.cs
//
// Authors:
//  Marek Safar (marek.safar@gmail.com)
//  Antonello Provenzano  <antonello@deveel.com>
//  Alejandro Serrano "Serras" (trupill@yahoo.es)
//  Jb Evain (jbevain@novell.com)
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System.Collections;
using System.Collections.Generic;

namespace System.Linq.Internal
{
    static class Enumerable
    {
        public static IEnumerable<TResult> Empty<TResult>()
        {
            return EmptyOf<TResult>.Instance;
        }

        public static IEnumerable<TResult> OfType<TResult>(this IEnumerable source)
        {
            Check.Source(source);

            return CreateOfTypeIterator<TResult>(source);
        }

        static IEnumerable<TResult> CreateOfTypeIterator<TResult>(IEnumerable source)
        {
            foreach (object element in source)
                if (element is TResult)
                    yield return (TResult)element;
        }

        public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            Check.SourceAndSelector(source, selector);

            return CreateSelectIterator(source, selector);
        }

        static IEnumerable<TResult> CreateSelectIterator<TSource, TResult>(IEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            foreach (var element in source)
                yield return selector(element);
        }

        public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            Check.SourceAndPredicate(source, predicate);

            // It cannot be IList<TSource> because it may break on user implementation
            var array = source as TSource[];
            if (array != null)
                return CreateWhereIterator(array, predicate);

            return CreateWhereIterator(source, predicate);
        }

        static IEnumerable<TSource> CreateWhereIterator<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            foreach (TSource element in source)
                if (predicate(element))
                    yield return element;
        }

        public static TSource SingleOrDefault<TSource>(this IEnumerable<TSource> source)
        {
            Check.Source(source);

            var found = false;
            var item = default(TSource);

            foreach (var element in source)
            {
                if (found)
                    throw new Exception();

                found = true;
                item = element;
            }

            return item;
        }

        public static TSource[] ToArray<TSource>(this IEnumerable<TSource> source)
        {
            Check.Source(source);

            TSource[] array;
            var collection = source as ICollection<TSource>;
            if (collection != null)
            {
                if (collection.Count == 0)
                    return EmptyOf<TSource>.Instance;

                array = new TSource[collection.Count];
                collection.CopyTo(array, 0);
                return array;
            }

            int pos = 0;
            array = EmptyOf<TSource>.Instance;
            foreach (var element in source)
            {
                if (pos == array.Length)
                {
                    if (pos == 0)
                        array = new TSource[4];
                    else
                        Array.Resize(ref array, pos * 2);
                }

                array[pos++] = element;
            }

            if (pos != array.Length)
                Array.Resize(ref array, pos);

            return array;
        }

        static class EmptyOf<T>
        {
            public static readonly T[] Instance = new T[0];
        }

        private static class Check
        {
            public static void Source(object source)
            {
                if (source == null)
                    throw new ArgumentNullException("source");
            }

            public static void SourceAndSelector(object source, object selector)
            {
                if (source == null)
                    throw new ArgumentNullException("source");
                if (selector == null)
                    throw new ArgumentNullException("selector");
            }

            public static void SourceAndPredicate(object source, object predicate)
            {
                if (source == null)
                    throw new ArgumentNullException("source");
                if (predicate == null)
                    throw new ArgumentNullException("predicate");
            }
        }
    }
}