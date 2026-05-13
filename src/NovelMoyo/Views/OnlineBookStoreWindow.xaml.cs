using System.Windows;
using System.Windows.Input;
using NovelMoyo.ViewModels;

namespace NovelMoyo.Views;

public partial class OnlineBookStoreWindow : Window
{
    public OnlineBookStoreWindow(OnlineBookStoreViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var vm = (OnlineBookStoreViewModel)DataContext;
            vm.SearchCommand.Execute(null);
        }
    }

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var vm = (OnlineBookStoreViewModel)DataContext;
            vm.SearchByUrlCommand.Execute(null);
        }
    }
}
