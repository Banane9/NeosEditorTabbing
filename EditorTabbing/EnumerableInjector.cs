using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EditorTabbing
{
    internal class EnumerableInjector<T> : IEnumerable<T>
    {
        public Action Postfix = nothing;
        public Action<T, bool> PostItem = nothing;
        public Action Prefix = nothing;
        public Func<T, bool> PreItem = yes;
        public Func<T, T> TransformItem = nothing;

        private readonly IEnumerator<T> enumerator;

        public EnumerableInjector(IEnumerable<T> enumerable)
            : this(enumerable.GetEnumerator())
        { }

        public EnumerableInjector(IEnumerator<T> enumerator)
        {
            this.enumerator = enumerator;
        }

        public IEnumerator<T> GetEnumerator()
        {
            Prefix();

            while (enumerator.MoveNext())
            {
                var item = enumerator.Current;
                var returnItem = PreItem(item);

                if (returnItem)
                    yield return TransformItem(item);

                PostItem(item, returnItem);
            }

            Postfix();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private static void nothing()
        { }

        private static void nothing(T _, bool __)
        { }

        private static T nothing(T item)
        {
            return item;
        }

        private static bool yes(T _)
        {
            return true;
        }
    }
}