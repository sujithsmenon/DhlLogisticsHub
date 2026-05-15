namespace DhlLogistics.Web.Components.Common.ModalDialog
{
    public interface IModalOptions
    {
        public Dictionary<string, object> ControlParameters { get; }

        public Dictionary<string, object> OptionsList { get; }

        public object Data { get; }
    }
}
