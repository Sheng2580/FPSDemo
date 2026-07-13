using System.Collections.Generic;
using Blessing.Data;
using UnityEngine;

/// <summary>
/// 祝福选择界面本局状态
/// </summary>
public sealed class BlessingSelectionRuntime
{
    private readonly Dictionary<int, int> _stacks = new Dictionary<int, int>();

    public int OwnedBuffCount { get; private set; }

    public int GetStack(int blessingId)
    {
        return blessingId > 0 && _stacks.TryGetValue(blessingId, out int stack)
            ? Mathf.Max(0, stack)
            : 0;
    }

    public int AddBlessing(int blessingId)
    {
        if (blessingId <= 0)
        {
            return 0;
        }

        int nextStack = GetStack(blessingId) + 1;
        _stacks[blessingId] = nextStack;
        OwnedBuffCount++;
        return nextStack;
    }

    public BlessingStackSnapshot[] CreateStackSnapshots()
    {
        BlessingStackSnapshot[] snapshots = new BlessingStackSnapshot[_stacks.Count];
        int index = 0;
        foreach (KeyValuePair<int, int> pair in _stacks)
        {
            snapshots[index] = new BlessingStackSnapshot(pair.Key, pair.Value);
            index++;
        }

        return snapshots;
    }
}
