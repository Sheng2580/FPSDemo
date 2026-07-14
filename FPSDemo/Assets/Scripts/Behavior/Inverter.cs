using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inverter : Decorator
{
    protected override EStatus OnUpdate()
    {
        child.Tick();
        if(child.IsFailure)
            return EStatus.Success;
        if(child.IsSuccess)
            return EStatus.Failure;
        return EStatus.Running;
    }
}