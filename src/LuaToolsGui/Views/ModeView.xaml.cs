using System.Windows.Controls;
using LuaToolsGui.ViewModels;

namespace LuaToolsGui.Views;

public partial class ModeView : UserControl
{
    public ModeView(ModeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        LuaToolsGui.Animations.Entrance.Enable(this);
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
