using Godot;

public interface IHoverPopupItem
{
    Control GetPopupContent();
}

public class DelegateHoverPopupItem : IHoverPopupItem
{
    private System.Func<Control> _func;
    public DelegateHoverPopupItem(System.Func<Control> func)
    {
        _func = func;
    }
    public Control GetPopupContent()
    {
        return _func?.Invoke();
    }
}
