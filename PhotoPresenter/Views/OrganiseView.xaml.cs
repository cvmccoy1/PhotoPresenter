using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class OrganiseView : UserControl
{
    private Point _dragStartPoint;
    private InsertionAdorner? _adorner;

    public OrganiseView() => InitializeComponent();

    private OrganiseViewModel? Vm => DataContext as OrganiseViewModel;

    // ── Folder list ────────────────────────────────────────────────────────────

    private void FolderList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Vm?.SelectedFolder != null)
        {
            Vm.RemoveFolder(Vm.SelectedFolder);
            e.Handled = true;
        }
    }

    private void FolderList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void FolderList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (Vm?.ShowAllFolders == true) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (!ExceedsThreshold(e.GetPosition(null))) return;

        var item = HitTestItem<PhotoFolderViewModel>(FolderList, e.GetPosition(FolderList));
        if (item != null)
        {
            FolderList.QueryContinueDrag += CancelDragOnEscape;
            DragDrop.DoDragDrop(FolderList, item, DragDropEffects.Move);
            FolderList.QueryContinueDrag -= CancelDragOnEscape;
            RemoveAdorner();
            // Reset start point so a still-held button doesn't immediately re-trigger a drag.
            _dragStartPoint = Mouse.GetPosition(null);
        }
    }

    private void FolderList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(PhotoFolderViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = DragDropEffects.Move;

        var container = HitTestContainer(FolderList, e.GetPosition(FolderList));
        if (container != null)
        {
            var pos = e.GetPosition(container);
            bool insertBefore = pos.Y < container.ActualHeight / 2;
            SetAdorner(container, insertBefore, vertical: false);
        }
        e.Handled = true;
    }

    private void FolderList_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void FolderList_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(typeof(PhotoFolderViewModel))) return;
        var dragging = (PhotoFolderViewModel)e.Data.GetData(typeof(PhotoFolderViewModel));

        int from = Vm?.Folders.IndexOf(dragging) ?? -1;
        if (from < 0 || Vm == null) return;

        // GetDropSlot returns an insertion slot (0..Count). Convert to a Move index:
        // moving down shifts earlier indices after removal, so subtract 1 when slot > from.
        int slot = GetDropSlot(FolderList, e.GetPosition(FolderList));
        int to   = slot > from ? slot - 1 : slot;
        if (from != to)
            Vm.ReorderFolder(from, to);
    }

    private void FolderRemove_Click(object sender, RoutedEventArgs e)
    {
        var folder = ContextMenuTarget<PhotoFolderViewModel>(sender);
        if (folder != null) Vm?.RemoveFolder(folder);
    }

    private void FolderRestore_Click(object sender, RoutedEventArgs e)
    {
        var folder = ContextMenuTarget<PhotoFolderViewModel>(sender);
        if (folder != null) Vm?.RestoreFolder(folder);
    }

    // ── Photo list ─────────────────────────────────────────────────────────────

    private void PhotoList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && PhotoList.SelectedItem is PhotoItemViewModel photo)
        {
            Vm?.RemovePhoto(photo);
            e.Handled = true;
        }
    }

    private void PhotoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void PhotoList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (Vm?.ShowAllPhotos == true) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (!ExceedsThreshold(e.GetPosition(null))) return;

        var item = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (item != null)
        {
            PhotoList.QueryContinueDrag += CancelDragOnEscape;
            DragDrop.DoDragDrop(PhotoList, item, DragDropEffects.Move);
            PhotoList.QueryContinueDrag -= CancelDragOnEscape;
            RemoveAdorner();
            _dragStartPoint = Mouse.GetPosition(null);
        }
    }

    private void PhotoList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(PhotoItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = DragDropEffects.Move;

        var container = HitTestContainer(PhotoList, e.GetPosition(PhotoList));
        if (container != null)
        {
            var pos = e.GetPosition(container);
            bool insertBefore = pos.X < container.ActualWidth / 2;
            SetAdorner(container, insertBefore, vertical: true);
        }
        e.Handled = true;
    }

    private void PhotoList_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void PhotoList_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(typeof(PhotoItemViewModel))) return;
        var dragging = (PhotoItemViewModel)e.Data.GetData(typeof(PhotoItemViewModel));
        var target = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (target == null || Vm == null) return;

        var photos = Vm.Photos;
        if (photos == null) return;
        int from = photos.IndexOf(dragging);
        int targetIndex = photos.IndexOf(target);
        if (from < 0 || targetIndex < 0) return;

        // Left half of target = insert before it; right half = insert after it.
        var container = PhotoList.ItemContainerGenerator.ContainerFromIndex(targetIndex) as ListBoxItem;
        bool insertBefore = container == null || e.GetPosition(container).X < container.ActualWidth / 2;
        int slot = insertBefore ? targetIndex : targetIndex + 1;

        int to = slot > from ? slot - 1 : slot;
        if (from != to)
            Vm.ReorderPhoto(from, to);
    }

    private async void SortByDate_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedFolder == null) return;
        var result = MessageBox.Show(
            Window.GetWindow(this),
            $"Reorder all items in \"{Vm.SelectedFolder.Name}\" by date (oldest first)?\n\nThe saved order will be updated.",
            "Sort by Date",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
            await Vm.SortPhotosByDateAsync();
    }

    private void PhotoRemove_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) Vm?.RemovePhoto(photo);
    }

    private void PhotoRestore_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) Vm?.RestorePhoto(photo);
    }

    private void PhotoTile_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PhotoItemViewModel photo })
            photo.EnsureToolTipLoaded();
    }

    private void PhotoCaptionAdd_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) ShowCaptionDialog(photo);
    }

    private void PhotoCaptionEdit_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) ShowCaptionDialog(photo);
    }

    private void PhotoCaptionDelete_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) Vm?.SetCaption(photo, "");
    }

    private void ShowCaptionDialog(PhotoItemViewModel photo)
    {
        var dlg = new CaptionDialog(photo.Caption) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            Vm?.SetCaption(photo, dlg.Caption);
    }

    // ── Open / Open With ──────────────────────────────────────────────────────

    private void PhotoOpen_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) OpenFile(photo.FullPath);
    }

    private void PhotoOpenWith_Click(object sender, RoutedEventArgs e)
    {
        var photo = ContextMenuTarget<PhotoItemViewModel>(sender);
        if (photo != null) ShowOpenWithDialog(photo.FullPath);
    }

    private void PhotoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var photo = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (photo != null) OpenFile(photo.FullPath);
    }

    private static void OpenFile(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }
    }

    private static void ShowOpenWithDialog(string filePath)
    {
        try
        {
            var openWith = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "openwith.exe");
            Process.Start(new ProcessStartInfo
            {
                FileName        = openWith,
                Arguments       = $"\"{filePath}\"",
                UseShellExecute = false
            });
        }
        catch { }
    }

    // ── Insertion adorner ──────────────────────────────────────────────────────

    private void SetAdorner(ListBoxItem container, bool insertBefore, bool vertical = false)
    {
        if (_adorner?.AdornedElement == container &&
            _adorner.InsertBefore == insertBefore &&
            _adorner.Vertical == vertical) return;
        RemoveAdorner();
        var layer = AdornerLayer.GetAdornerLayer(container);
        if (layer == null) return;
        _adorner = new InsertionAdorner(container, insertBefore, vertical);
        layer.Add(_adorner);
    }

    private void RemoveAdorner()
    {
        if (_adorner == null) return;
        AdornerLayer.GetAdornerLayer(_adorner.AdornedElement)?.Remove(_adorner);
        _adorner = null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private bool ExceedsThreshold(Point pos) =>
        Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

    private static T? HitTestItem<T>(ListBox listBox, Point position) where T : class
    {
        var container = HitTestContainer(listBox, position);
        return container?.DataContext as T;
    }

    private static ListBoxItem? HitTestContainer(ListBox listBox, Point position)
    {
        var element = listBox.InputHitTest(position) as DependencyObject;
        while (element != null && element != listBox)
        {
            if (element is ListBoxItem lbi) return lbi;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    // Returns insertion slot 0..Count: the index before which the dragged item should appear.
    private static int GetDropSlot(ListBox listBox, Point position)
    {
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            var container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;
            var topLeft = container.TranslatePoint(new Point(0, 0), listBox);
            if (position.Y < topLeft.Y + container.ActualHeight / 2)
                return i;
        }
        return listBox.Items.Count; // after last item
    }

    private void CancelDragOnEscape(object sender, QueryContinueDragEventArgs e)
    {
        if (e.EscapePressed)
        {
            e.Action  = DragAction.Cancel;
            e.Handled = true;
            RemoveAdorner();
        }
    }

    private static T? ContextMenuTarget<T>(object menuItemSender) where T : class
    {
        // ContextMenu is on the DataTemplate's StackPanel, so PlacementTarget is that StackPanel
        if (menuItemSender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement fe } })
            return fe.DataContext as T;
        return null;
    }
}
