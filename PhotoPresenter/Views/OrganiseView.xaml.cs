using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class OrganiseView : UserControl
{
    private Point _dragStartPoint;

    public OrganiseView() => InitializeComponent();

    private OrganiseViewModel? Vm => DataContext as OrganiseViewModel;

    // ── Folder list ────────────────────────────────────────────────────────────

    private void FolderList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void FolderList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (!ExceedsThreshold(e.GetPosition(null))) return;

        var item = HitTestItem<PhotoFolderViewModel>(FolderList, e.GetPosition(FolderList));
        if (item != null)
            DragDrop.DoDragDrop(FolderList, item, DragDropEffects.Move);
    }

    private void FolderList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(PhotoFolderViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void FolderList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(PhotoFolderViewModel))) return;
        var dragging = (PhotoFolderViewModel)e.Data.GetData(typeof(PhotoFolderViewModel));
        var target = HitTestItem<PhotoFolderViewModel>(FolderList, e.GetPosition(FolderList));
        if (target == null || ReferenceEquals(dragging, target) || Vm == null) return;

        int from = Vm.Folders.IndexOf(dragging);
        int to = Vm.Folders.IndexOf(target);
        if (from >= 0 && to >= 0)
            Vm.ReorderFolder(from, to);
    }

    // ── Photo list ─────────────────────────────────────────────────────────────

    private void PhotoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void PhotoList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (!ExceedsThreshold(e.GetPosition(null))) return;

        var item = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (item != null)
            DragDrop.DoDragDrop(PhotoList, item, DragDropEffects.Move);
    }

    private void PhotoList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(PhotoItemViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void PhotoList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(PhotoItemViewModel))) return;
        var dragging = (PhotoItemViewModel)e.Data.GetData(typeof(PhotoItemViewModel));
        var target = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (target == null || ReferenceEquals(dragging, target) || Vm == null) return;

        var photos = Vm.Photos;
        if (photos == null) return;
        int from = photos.IndexOf(dragging);
        int to = photos.IndexOf(target);
        if (from >= 0 && to >= 0)
            Vm.ReorderPhoto(from, to);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private bool ExceedsThreshold(Point pos) =>
        Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

    private static T? HitTestItem<T>(ListBox listBox, Point position) where T : class
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element != null && element != listBox)
        {
            if (element is ListBoxItem lbi && lbi.DataContext is T vm)
                return vm;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
