// Copyright (c) 2014 SharpLang - Virgile Bello
using System.Collections;
using System.Collections.Generic;

namespace System
{
    partial class Array
    {
        /*
         * These methods are used to implement the implicit generic interfaces 
         * implemented by arrays in NET 2.0.
         * Only make those methods generic which really need it, to avoid
         * creating useless instantiations.
         */
        internal int InternalArray__ICollection_get_Count()
        {
            return Length;
        }

        internal bool InternalArray__ICollection_get_IsReadOnly()
        {
            return true;
        }

        internal IEnumerator<T> InternalArray__IEnumerable_GetEnumerator<T>()
        {
            return new InternalEnumerator<T>(SharpLangHelper.UnsafeCast<T[]>(this));
        }

        internal void InternalArray__ICollection_Clear()
        {
            throw new NotSupportedException("Collection is read-only");
        }

        internal void InternalArray__ICollection_Add<T>(T item)
        {
            throw new NotSupportedException("Collection is of a fixed size");
        }

        internal bool InternalArray__ICollection_Remove<T>(T item)
        {
            throw new NotSupportedException("Collection is of a fixed size");
        }

        internal bool InternalArray__ICollection_Contains<T>(T item)
        {
            return Array.IndexOf<T>(SharpLangHelper.UnsafeCast<T[]>(this), item) != -1;
        }

        internal void InternalArray__ICollection_CopyTo<T>(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            // The order of these exception checks may look strange,
            // but that's how the microsoft runtime does it.
            if (this.Rank > 1)
                throw new RankException("Only single dimension arrays are supported.");
            if (index + this.GetLength(0) > array.GetLowerBound(0) + array.GetLength(0))
                throw new ArgumentException("Destination array was not long " +
                    "enough. Check destIndex and length, and the array's " +
                    "lower bounds.");
            if (array.Rank > 1)
                throw new RankException("Only single dimension arrays are supported.");
            if (index < 0)
                throw new ArgumentOutOfRangeException(
                    "index", "Value has to be >= 0.");

            Copy(this, this.GetLowerBound(0), array, index, this.GetLength(0));
        }

		internal T InternalArray__IReadOnlyList_get_Item<T> (int index)
		{
		    return SharpLangHelper.UnsafeCast<T[]>(this)[index];
		}

		internal int InternalArray__IReadOnlyCollection_get_Count ()
		{
			return Length;
		}

        internal void InternalArray__Insert<T>(int index, T item)
        {
            throw new NotSupportedException("Collection is of a fixed size");
        }

        internal void InternalArray__RemoveAt(int index)
        {
            throw new NotSupportedException("Collection is of a fixed size");
        }

        internal int InternalArray__IndexOf<T>(T item)
        {
            return Array.IndexOf<T>(SharpLangHelper.UnsafeCast<T[]>(this), item);
        }

        internal T InternalArray__get_Item<T>(int index)
        {
            return SharpLangHelper.UnsafeCast<T[]>(this)[index];
        }

        internal void InternalArray__set_Item<T>(int index, T item)
        {
            SharpLangHelper.UnsafeCast<T[]>(this)[index] = item;
        }

        internal struct InternalEnumerator<T> : IEnumerator<T>
        {
            const int NOT_STARTED = -2;

            // this MUST be -1, because we depend on it in move next.
            // we just decr the size, so, 0 - 1 == FINISHED
            const int FINISHED = -1;

            T[] array;
            int idx;

            internal InternalEnumerator(T[] array)
            {
                this.array = array;
                idx = NOT_STARTED;
            }

            public void Dispose()
            {
                idx = NOT_STARTED;
            }

            public bool MoveNext()
            {
                if (idx == NOT_STARTED)
                    idx = array.Length;

                return idx != FINISHED && --idx != FINISHED;
            }

            public T Current
            {
                get
                {
                    if (idx == NOT_STARTED)
                        throw new InvalidOperationException("Enumeration has not started. Call MoveNext");
                    if (idx == FINISHED)
                        throw new InvalidOperationException("Enumeration already finished");

                    return array[array.Length - 1 - idx];
                }
            }

            void IEnumerator.Reset()
            {
                idx = NOT_STARTED;
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }
        }
    }
}