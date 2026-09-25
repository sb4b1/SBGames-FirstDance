public interface IIdentifier
{
    string UniqueId { get; }

    void GenerateID(){}
}