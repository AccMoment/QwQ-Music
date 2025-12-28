using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Services;
using QwQ_Music.Models;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels.Panels;

public partial class MusicListPanelViewModel : MusicItemsViewModelBase {
    
    public MusicListModel? MusicListModel {
        get;
        set {
            if (field == value)
                return;
            _ = value?.LoadCurrentAsync()
                     .ContinueWith(LoggerService.HandleException)
                     .ContinueWith(_ => SetAllItems(value.Musics!))
                     .ConfigureAwait(false);
            field?.DisposeCurrent();
            field = value;
        }
    }

    [ObservableProperty]
    public partial double DataGridHorizontalScrollValue { get; set; }


    [RelayCommand]
    private void ScrollToTop(DataGrid dataGrid) {
        // 滚动到第一行（第一行数据）
        dataGrid.ScrollIntoView(dataGrid.CollectionView.Cast<MusicItemModel>().FirstOrDefault(), null);
    }
}