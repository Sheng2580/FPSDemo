using UnityEngine;

public class UnitySingleTonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static bool isShuttingDown;
    private static bool wasAutoCreated;
    private static bool isApplicationQuitting;

    public static T Instance
    {
        get
        {
            if (isShuttingDown)
            {
                return null;
            }

            if (instance == null)
            {
                instance = FindObjectOfType<T>();
            }

            if (instance == null)
            {
                GameObject obj = new GameObject(typeof(T).Name);
                instance = obj.AddComponent<T>();
                wasAutoCreated = true;
            }

            return instance;
        }
    }

    public virtual void Awake()
    {
        DontDestroyOnLoad(gameObject);
        isApplicationQuitting = false;
        isShuttingDown = false;
        if (instance == null)
        {
            instance = this as T;
            wasAutoCreated = false;
            name = typeof(T).Name;
        }
        else if (instance != this)
        {
            if (wasAutoCreated && instance != null)
            {
                DestroyImmediate(instance.gameObject);
                instance = this as T;
                wasAutoCreated = false;
                name = typeof(T).Name;
            }
            else
            {
                DestroyImmediate(this);
            }
        }
    }

    protected virtual void OnApplicationQuit()
    {
        if (instance == this)
        {
            isApplicationQuitting = true;
            isShuttingDown = true;
            instance = null;
            wasAutoCreated = false;
        }
    }

    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            isShuttingDown = isApplicationQuitting;
            instance = null;
            wasAutoCreated = false;
        }
    }
}
