namespace DhlLogistics.Web.Components.Common.Spinner
{
    public class SpinnerService
    {
        public event Action? OnShow;
        public event Action? OnHide;

        public void Show()
        {
            OnShow?.Invoke();
        }

        public void Hide()
        {
            OnHide?.Invoke();
        }
    }
}
