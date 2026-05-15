using Syncfusion.Blazor.Notifications;

namespace DhlLogistics.Web.Components.Common.Toast
{
    public interface IToastService
    {
        event Action<ToastModel> ToastInstance;
        void Show(ToastModel model);
    }

    public class ToastService : IToastService
    {
        public event Action<ToastModel>? ToastInstance;

        public void Show(ToastModel model)
        {
            ToastInstance?.Invoke(model);
        }
    }
}
