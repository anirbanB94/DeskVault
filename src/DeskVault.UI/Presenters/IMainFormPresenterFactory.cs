using DeskVault.UI.Views;

namespace DeskVault.UI.Presenters;

public interface IMainFormPresenterFactory
{
    MainFormPresenter Create(
        IMainFormView view);
}
