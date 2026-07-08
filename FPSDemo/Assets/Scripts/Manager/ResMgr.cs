using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 资源加载管理器  
/// </summary>
public class ResMgr : SingleTon<ResMgr>
{
    /// <summary>
    /// 资源加载方法
    /// </summary>
    /// <param name="name"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T load<T>(string name,Transform father=null)where T:Object
    {
        T res = Resources.Load<T>(name);
        if (res is GameObject) //GameObject 
        {
            T obj = GameObject.Instantiate(res);
            if (father != null)
            {
                (obj as GameObject).transform.SetParent(father.transform);
                (obj as GameObject).transform.localPosition = Vector3.zero;
                (obj as GameObject).transform.localRotation = Quaternion.identity;
                obj.name = name;
            }

            return obj;
        }
        else//AudioClip TextAsset  
        {
            return res; 
        }
    }
}
