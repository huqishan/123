namespace Module.Communication.Features.ProtocolConfig.ViewModels.PresentationModels
{
    public sealed class ProtocolOption<T>
    {
        public ProtocolOption(T value, string displayName, string description)
        {
            Value = value;
            DisplayName = displayName;
            Description = description;
        }

        public T Value { get; }

        public string DisplayName { get; }

        public string Description { get; }
    }
}
