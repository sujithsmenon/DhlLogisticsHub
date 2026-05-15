using Microsoft.AspNetCore.Components;

namespace DhlLogistics.Web.Components.Common.ModalDialog
{
    public abstract class ModalDialogBase : ComponentBase
    {
        public readonly IModalDialogContext Context = new ModalDialogContext();

        public override Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            this.Context.NotifyRenderRequired = this.OnRenderRequested;

            return base.SetParametersAsync(ParameterView.Empty);
        }

        private void OnRenderRequested()
            => StateHasChanged();
    }
}
