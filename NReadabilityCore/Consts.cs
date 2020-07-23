using System.Reflection;

namespace NReadability
{
    public static class Consts
    {
        private static readonly string _nReadabilityFullName;

        static Consts()
        {
            _nReadabilityFullName = string.Format("NReadability {0}", Assembly.GetExecutingAssembly().GetName().Version);
        }

        public static string NReadabilityFullName => _nReadabilityFullName;
    }
}
