// Copyright (c) 2014 SharpLang - Virgile Bello

using System.Collections;
using System.Collections.Generic;

namespace SharpLang.CompilerServices
{
    /// <summary>
    /// Represents the stack of a function being codegen.
    /// </summary>
    class FunctionStack : IList<StackValue>
    {
        private readonly List<StackValue> storage;

        public FunctionStack()
        {
            storage = new List<StackValue>();
        }

        public FunctionStack(int capacity)
        {
            storage = new List<StackValue>(capacity);
        }

        public void Add(StackValue stackValue)
        {
            storage.Add(stackValue);
        }

        public void AddRange(IEnumerable<StackValue> stackValues)
        {
            storage.AddRange(stackValues);
        }

        public void Clear()
        {
            storage.Clear();
        }

        public bool Contains(StackValue item)
        {
            return storage.Contains(item);
        }

        public void CopyTo(StackValue[] array, int arrayIndex)
        {
            storage.CopyTo(array, arrayIndex);
        }

        public bool Remove(StackValue item)
        {
            return storage.Remove(item);
        }

        public int Count
        {
            get { return storage.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public IEnumerator<StackValue> GetEnumerator()
        {
            return storage.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)storage).GetEnumerator();
        }

        public int IndexOf(StackValue item)
        {
            return storage.IndexOf(item);
        }

        public void Insert(int index, StackValue item)
        {
            storage.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            storage.RemoveAt(index);
        }

        public void RemoveRange(int index, int count)
        {
            storage.RemoveRange(index, count);
        }

        public StackValue this[int index]
        {
            get { return storage[index]; }
            set { storage[index] = value; }
        }
    }
}