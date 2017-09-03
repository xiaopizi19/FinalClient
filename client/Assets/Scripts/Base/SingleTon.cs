namespace App.Base
{
    public abstract class SingleTon<T> where T : class,new()
    {
        private static T _instance;
        public static void CreateInstance()
        {
            if (_instance == null)
            {
                _instance = new T();
            }
        }
        public static void ReleaseInstance()
        {
            if (_instance != null)
            {
                _instance = null;
            }
        }
        public static T Instance
        {
            get
            {
                return _instance;
            }
        }
    }
}

