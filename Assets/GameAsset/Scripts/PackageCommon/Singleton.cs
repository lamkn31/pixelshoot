using UnityEngine;

namespace Wayfu.Lamkn
{
	public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T _instance;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = GameObject.FindObjectOfType<T>();

					if (_instance == null)
					{
						Debug.Log($"On destroy, instance {typeof(T)} will be destroyed, can't be call!");
					}
				}

				return _instance;
			}
		}

		public static bool IsActive => _instance != null;

		protected virtual void Awake()
		{
			if (_instance == null)
			{
				_instance = this as T;
				DontDestroyOnLoad(gameObject);
				OnAwake();
			}
			else if (_instance != this)
			{
				Destroy(gameObject);
			}
		}

		protected virtual void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
			}
		}

		protected virtual void OnAwake()
		{
		}
	}
}