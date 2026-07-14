using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugNode : Behavior
{
    private string word;
    public DebugNode(string word)
    {
        this.word = word;
    }
    protected override EStatus OnUpdate()
    {
        Debug.Log(word);
        return EStatus.Success;
    }
}

public partial class BehaviorTreeBuilder
{
    public BehaviorTreeBuilder DebugNode(string word)
    {
        var node = new DebugNode(word);
        AddBehavior(node);
        return this;
    }
}