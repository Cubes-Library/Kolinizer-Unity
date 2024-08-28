using System;

namespace Kub.Util
{
    public class Singleton<T> //: ISingleton<T>
        where T : new()
    {
        protected static T _inst = default;
        public static T Inst
        {
            get { return _inst ??= SyncOrSwim; }
        }
        private static T SyncOrSwim
        {
            // Can lock(sync){} around the return for thread safe if needed later. -chuck
            get { return _inst ??= new T(); }
        }
        public Singleton()
        {
            if (_inst != null) { throw new ApplicationException($"Singleton violation - mutiple objects created for {typeof(T).Name}"); }
        }
    }

    //public interface ISingleton<T> where T : new()
    //{
    //    static T Inst { get; }
    //}
}