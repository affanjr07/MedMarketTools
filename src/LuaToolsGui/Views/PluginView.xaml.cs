using System.Windows.Controls;
using LuaToolsGui.ViewModels;

namespace LuaToolsGui.Views;

public partial class PluginView : UserControl
{
    public PluginView(PluginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        LuaToolsGui.Animations.Entrance.Enable(this);
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
