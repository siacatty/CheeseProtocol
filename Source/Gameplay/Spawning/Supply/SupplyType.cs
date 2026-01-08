namespace CheeseProtocol
{
    /// <summary>
    /// High-level supply category. Used for candidate filtering, settings, UI/letter.
    /// </summary>
    public enum SupplyType
    {
        Undefined,
        Food,       // 식량 보급
        Medicine,   // 의약품 보급
        Drug,       // 기호품 / 드럭
        Weapon      // 무기 보급
    }
}