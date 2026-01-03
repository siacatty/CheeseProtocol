namespace CheeseProtocol
{
    public interface IProtocol
    {
        string Id { get; }              // internal stable id: "colonist", "gift_small", ...
        string DisplayName { get; }     // user-facing name
        bool CanExecute(ProtocolContext ctx);
        void Execute(ProtocolContext ctx);
    }
}