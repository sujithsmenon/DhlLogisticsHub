namespace DhlLogistics.Web.Components.Common.ModalDialog
{
    public sealed class BsModalOptions : IModalOptions
    {
        public string ModalSize { get; set; } = "modal-xl";

        public Dictionary<string, object> ControlParameters { get; } = new Dictionary<string, object>();

        public Dictionary<string, object> OptionsList { get; } = new Dictionary<string, object>();

        public object Data { get; set; } = new();
    }
}
