using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.PlayerLoop;

/// <summary>
/// 池子数据 
/// </summary>
public class PoolData //小池子 
{
    private string poolName;
    //池子中的父容器   
    public GameObject fatherObj;
    //池子中放置容器的列表 
    public List<GameObject> poolList ;

    public PoolData(GameObject obj,GameObject grandFatherObj)
    {
        poolName = obj.name;
        CreateFatherObj(grandFatherObj);
        poolList = new List<GameObject>();
        //添加新对象到池子容器列表中 并添加到fatherObj对象下面
        PushObj(obj, grandFatherObj);
    }
/// <summary>
/// 往池子中的放东西  游戏对象用完了
/// </summary>
/// <param name="obj"></param>
    public void PushObj(GameObject obj, GameObject grandFatherObj = null)
    {
        if (obj == null)
        {
            return;
        }

        if (fatherObj == null)
        {
            CreateFatherObj(grandFatherObj);
        }

        obj.SetActive(false);
        poolList.Add(obj);  
        obj.transform.parent = fatherObj.transform;
    }
/// <summary>
/// 从池子中拿对象 
/// </summary>
/// <returns></returns>
    public GameObject PopObj()
    {
        RemoveDestroyedObjects();
        if (poolList.Count == 0)
        {
            return null;
        }

        GameObject obj=poolList[0];
        poolList.RemoveAt(0);
        obj.transform.parent =null;
        obj.SetActive(true);
        return obj;
    }

    public bool HasAvailableObject()
    {
        RemoveDestroyedObjects();
        return poolList.Count > 0;
    }

    public void RemoveDestroyedObjects()
    {
        for (int i = poolList.Count - 1; i >= 0; i--)
        {
            if (poolList[i] == null)
            {
                poolList.RemoveAt(i);
            }
        }
    }

    private void CreateFatherObj(GameObject grandFatherObj)
    {
        fatherObj = new GameObject(poolName);
        if (grandFatherObj != null)
        {
            fatherObj.transform.parent = grandFatherObj.transform;
        }
    }
}

/// <summary>
/// 缓存池管理器
/// </summary>
public class PoolMgr : SingleTon<PoolMgr>
{
   //缓存池容器 
   public Dictionary<string, PoolData> poolDic = new Dictionary<string, PoolData>();
   private GameObject grandFatherObj;
   
   public GameObject getObj(string name)
   {
       //判断字典中是否有该对象对应的池子 并且池子列表长度大于0 这时候才可以从池子列表中获取池子对象
       if (poolDic.ContainsKey(name) && poolDic[name].HasAvailableObject())
       {
           return poolDic[name].PopObj();  
       }
       else
       {
           //如果池子中没有对象则自己从资源中加载生成一个对象 
           GameObject obj=ResMgr.Instance.load<GameObject>(name);
           if (obj == null) obj = new GameObject();
           obj.name = name;
           return obj;
           
       }
   }
   
   //从Ab包加载  //异步 //默认GameObject  //加载完成使用
   public void  GetObjForAB(string abName, string resName,UnityAction<GameObject> callback)
   {
       if (poolDic.ContainsKey(resName) && poolDic[resName].HasAvailableObject())
       {
           callback(poolDic[resName].PopObj());
       }
       else
       {
           ABManager.Instance.LoadResAsync(abName, resName,typeof (GameObject),(obj) =>
           {
               GameObject resobj = obj as GameObject;
               if (resobj == null)
               {
                   resobj = new GameObject();
               }
               resobj.name = resName;
               callback(resobj);
           });
       }
   }
   
   
/// <summary>
/// 将不用的对象还给池子  
/// </summary>
/// <param name="name"></param>
/// <param name="obj"></param>
   public void pushObj(string name, GameObject obj)
   {
       if (obj == null)
       {
           return;
       }

       EnsurePoolRoot();
       //判断是否有池子 
       if (poolDic.ContainsKey(name))
       {
           poolDic[name].PushObj(obj, grandFatherObj);  
       }
       else
       {
           poolDic.Add(name,new PoolData(obj,grandFatherObj));
       }
   }

/// <summary>
/// 清空池子容器
/// </summary>
   public void clear()
   {
       poolDic.Clear(); 
       if (grandFatherObj != null)
       {
           UnityEngine.Object.Destroy(grandFatherObj);
       }
       grandFatherObj = null;   
   }

   private void EnsurePoolRoot()
   {
       if (grandFatherObj != null)
       {
           return;
       }

       grandFatherObj = new GameObject("Pool");
       UnityEngine.Object.DontDestroyOnLoad(grandFatherObj);
   }
   
}
