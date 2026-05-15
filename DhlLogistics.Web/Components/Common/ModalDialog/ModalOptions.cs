namespace DhlLogistics.Web.Components.Common.ModalDialog
{
    public class ModalOptions : IModalOptions
    {
        public Dictionary<string, object> ControlParameters { get; } = new Dictionary<string, object>();

        public Dictionary<string, object> OptionsList { get; } = new Dictionary<string, object>();

        public object Data { get; set; } = new();
    }
}
