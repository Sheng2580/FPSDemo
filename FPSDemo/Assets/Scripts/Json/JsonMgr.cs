using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 序列化和反序列化Json时  使用的是哪种方案    有两种  JsonUtility 不能直接序列化字典  ligJson可以序列化字典 
/// </summary>
public enum JsonType    
{
    JsonUtility,
    LitJson,
    Newtonsoft
}

/// <summary>
/// Json数据管理类 主要用于进行 Json的序列化存储到硬盘 和 反序列化从硬盘中读取到内存中
/// </summary>
public class JsonMgr:SingleTon<JsonMgr>
{
    public JsonMgr() { }

    //存储Json数据 序列化
    public void SaveData(object data, string fileName, string directPath = "", JsonType type = JsonType.Newtonsoft)
    {
        // 确定存储路径（自动处理路径斜杠问题）
        string directoryPath = Path.Combine(Application.persistentDataPath, directPath);
        string filePath = Path.Combine(directoryPath, fileName + ".json");

        // 序列化得到Json字符串
        string jsonStr = "";
        switch (type)
        {
            case JsonType.JsonUtility:
                jsonStr = JsonUtility.ToJson(data, prettyPrint: true); // 格式化输出，方便阅读
                break;

         
        }

        // 文件夹不存在就创建
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        //  写入文件
        File.WriteAllText(filePath, jsonStr);

        // 可选：在控制台打印保存路径，方便你找文件
        Debug.Log("保存成功：" + filePath);
    }

    //读取指定文件中的 Json数据 反序列化
    public T LoadData<T>(string fileName, JsonType type = JsonType.Newtonsoft) where T : new()
    {
        //数据对象
        T data = new T();
        //确定从哪个路径读取
        //首先先判断 默认数据文件夹中是否有我们想要的数据 如果有 就从中获取
        string path = Application.streamingAssetsPath + "/" + fileName + ".json";
        //先判断 是否存在这个文件
        //如果不存在默认文件 就从 读写文件夹中去寻找
        if(!File.Exists(path))
            path = Application.persistentDataPath + "/" + fileName + ".json";
        //如果读写文件夹中都还没有 那就返回一个默认对象
        if (!File.Exists(path))
            return data;
        //进行反序列化
        string jsonStr = File.ReadAllText(path);
        try
        {
            switch (type)
            {
                case JsonType.JsonUtility:
                    data = JsonUtility.FromJson<T>(jsonStr);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JsonMgr] Load json failed: {path}\n{e.Message}");
            return new T();
        }
        //把对象返回出去
        return data;
    }
    
    /// <summary>
    /// 获取指定文件夹下所有的 JSON 文件名（不带后缀）
    /// </summary>
    /// <param name="directPath">存档子文件夹名（留空则扫描根目录）</param>
    /// <returns>文件名列表（如 ["Save_01", "Save_02"]）</returns>
    public List<string> GetAllJsonFileNames(string directPath = "")
    {
        List<string> fileNames = new List<string>();

        // 拼接文件夹路径
        string directoryPath = Path.Combine(Application.persistentDataPath, directPath);

        // 如果文件夹不存在，直接返回空列表
        if (!Directory.Exists(directoryPath))
        {
            return fileNames;
        }

        //  获取文件夹下所有 .json 文件
        // SearchOption.TopDirectoryOnly: 只扫描当前文件夹，不扫描子文件夹
        string[] jsonFiles = Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly);

        // 4. 提取文件名（去掉路径和 .json 后缀）
        foreach (string file in jsonFiles)
        {
            // Path.GetFileNameWithoutExtension 可以直接得到 "文件名" 而不是 "文件名.json"
            string name = Path.GetFileNameWithoutExtension(file);
            fileNames.Add(name);
        }

        return fileNames;
    }

}
