namespace CheeseProtocol
{
    public enum QualityCurve
    {
        Linear, //후원 금액에 비례해 균등하게 증가
        EaseOut2, //소액 후원 효과가 좋고, 고액은 완만하게 증가
        EaseOut3, //소액 후원 보상이 매우 큼, 고액 차이는 적음
        Sqrt //아주 적은 후원도 빠르게 체감됨
    }
}