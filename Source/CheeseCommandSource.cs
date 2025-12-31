namespace CheeseProtocol
{
    public enum CheeseCommandSource
    {
        Chat = 0,        // 채팅 + 도네 둘 다 허용 (금액 무시)
        Donation = 1     // 도네만 허용 (최소금액 적용)
    }
}