namespace CheeseProtocol
{
    public enum ParticipantPawnStatus
    {
        OkOnMap,        // bubble 가능
        Removed,        // record 제거됨
        Inactive,// dead/kidnapped bucket으로 이동
        Caravan, // 카라반
        NoBubble        // 그 외(맵 없음 등)
    }
}