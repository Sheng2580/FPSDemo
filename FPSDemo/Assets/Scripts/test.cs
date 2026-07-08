using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        ABManager.Instance.LoadResAsync("gameobj","Cube", (obj) =>
        {
            GameObject go = obj as GameObject;
            go.transform.position = Vector3.zero;
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
        });
    }

  
}
