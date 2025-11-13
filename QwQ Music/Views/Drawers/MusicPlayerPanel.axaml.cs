using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QwQ_Music.ViewModels;
using MusicCoverPageViewModel = QwQ_Music.ViewModels.Drawers.MusicCoverPageViewModel;

namespace QwQ_Music.Views.Drawers;

public partial class MusicPlayerPanel : Grid
{
    public MusicPlayerPanel()
    {
        InitializeComponent();
        DataContext = new MusicCoverPageViewModel();
        
        PointerMoved += OnPointerMoved;
        Unloaded += OnUnloaded;
        
        // 假设你有一个 AudioPlay 实例
        MusicPlayerViewModel.Default.AudioPlay.SpectrumDataUpdated += AudioPlayOnSpectrumDataUpdated;
    }

    private void AudioPlayOnSpectrumDataUpdated(object? sender, float[] e)
    {
        SpectrumVisualizer.SpectrumData = e;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        PointerMoved -= OnPointerMoved;
        MusicPlayerViewModel.Default.AudioPlay.SpectrumDataUpdated -= AudioPlayOnSpectrumDataUpdated;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // 获取鼠标相对于窗口的位置
        var mousePos = e.GetPosition(this);
        
        // 检查鼠标是否在按钮范围内
        if (IsMouseOverControl(ControlPanelGrid, mousePos))
        {
            ControlPanelGrid.Classes.Remove("Hide");
            SpectrumVisualizer.Classes.Add("Hide");
            return;
        }

        ControlPanelGrid.Classes.Add("Hide");
        SpectrumVisualizer.Classes.Remove("Hide");
    }

    private bool IsMouseOverControl(Control control, Point mousePosition)
    {
        // 将控件坐标转换为当前Grid的坐标系
        var transform = control.TransformToVisual(this);

        if (!transform.HasValue) 
            return false;

        // 获取控件在其父容器中的边界（包含位置和大小）
        var controlBounds = control.Bounds;
        
        // 将控件的边界矩形的四个角点转换到目标坐标系
        // 控件坐标系中的四个角点（相对于控件自身，从(0,0)开始）
        var topLeft = new Point(0, 0);
        var topRight = new Point(controlBounds.Width, 0);
        var bottomLeft = new Point(0, controlBounds.Height);
        var bottomRight = new Point(controlBounds.Width, controlBounds.Height);
        
        // 转换到目标坐标系
        var transformedTopLeft = topLeft.Transform(transform.Value);
        var transformedTopRight = topRight.Transform(transform.Value);
        var transformedBottomLeft = bottomLeft.Transform(transform.Value);
        var transformedBottomRight = bottomRight.Transform(transform.Value);
        
        // 找到转换后的边界矩形的最小和最大坐标（处理可能的旋转/缩放）
        double minX = Math.Min(Math.Min(transformedTopLeft.X, transformedTopRight.X), 
                           Math.Min(transformedBottomLeft.X, transformedBottomRight.X));
        double minY = Math.Min(Math.Min(transformedTopLeft.Y, transformedTopRight.Y), 
                           Math.Min(transformedBottomLeft.Y, transformedBottomRight.Y));
        double maxX = Math.Max(Math.Max(transformedTopLeft.X, transformedTopRight.X), 
                           Math.Max(transformedBottomLeft.X, transformedBottomRight.X));
        double maxY = Math.Max(Math.Max(transformedTopLeft.Y, transformedTopRight.Y), 
                           Math.Max(transformedBottomLeft.Y, transformedBottomRight.Y));
        
        // 构建转换后的边界矩形
        var transformedBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
            
        return transformedBounds.Contains(mousePosition);
    }
}
