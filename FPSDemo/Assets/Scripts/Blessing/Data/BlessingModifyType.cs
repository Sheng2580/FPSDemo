namespace Blessing.Data
{
    /// <summary>
    /// 祝福数值修正方式
    /// </summary>
    public enum BlessingModifyType
    {
        // 固定加值
        Add,
        // 百分比加值
        PercentAdd,
        // 乘法倍率
        Multiply,
        // 覆盖
        Override
    }
}
