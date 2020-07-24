using System.Collections.Generic;
using System.Linq;

namespace Carbon.Readability
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Returns the only one element in the sequence or default(T) if either the sequence doesn't contain any elements or it contains more than one element.
        /// </summary>
        public static T? SingleOrNone<T>(this IEnumerable<T> enumerable)
          where T : class
        {
            T firstElement = enumerable.FirstOrDefault();

            if (firstElement is null)
            {
                // no elements
                return null;
            }

            T secondElement = enumerable.Skip(1).FirstOrDefault();

            if (secondElement != null)
            {
                return null;
            }

            return firstElement;
        }
    }
}
