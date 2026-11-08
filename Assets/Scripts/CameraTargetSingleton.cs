using UnityEngine;

public class CameraTargetSingleton : MonoBehaviour
{
    public static CameraTargetSingleton Instance;  //静态实例，用于全局访问

    public void Awake()
    {
        if(Instance != null)
        {
            Debug.LogWarning("发现多个CameraTargetSingleton实例，销毁当前对象以保持单例模式。");
            Destroy(gameObject);  //如果已经存在实例，销毁当前对象，确保单例模式
            return ;
        }
        Instance = this;  //将当前对象设置为单例实例
    }
}
