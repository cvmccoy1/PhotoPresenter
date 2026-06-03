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
            DragDrop.DoDragDrop(FolderList, item, DragDropEffects.Move);
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
            SetFolderAdorner(container, insertBefore);
        }
        e.Handled = true;
    }

    private void FolderList_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void FolderList_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(typeof(PhotoFolderViewModel))) return;
        var dragging = (PhotoFolderViewModel)e.Data.GetData(typeof(PhotoFolderViewModel));

        int to = GetDropIndex(FolderList, e.GetPosition(FolderList));
        int from = Vm?.Folders.IndexOf(dragging) ?? -1;
        if (from >= 0 && to >= 0 && from != to)
            Vm!.ReorderFolder(from, to);
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
            DragDrop.DoDragDrop(PhotoList, item, DragDropEffects.Move);
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

        // For the wrap-panel list highlight the target item with a left-edge adorner
        var container = HitTestContainer(PhotoList, e.GetPosition(PhotoList));
        if (container != null)
            SetFolderAdorner(container, insertBefore: true);

        e.Handled = true;
    }

    private void PhotoList_DragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void PhotoList_Drop(object sender, DragEventArgs e)
    {
        RemoveAdorner();
        if (!e.Data.GetDataPresent(typeof(PhotoItemViewModel))) return;
        var dragging = (PhotoItemViewModel)e.Data.GetData(typeof(PhotoItemViewModel));
        var target = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (target == null || ReferenceEquals(dragging, target) || Vm == null) return;

        var photos = Vm.Photos;
        if (photos == null) return;
        int from = photos.IndexOf(dragging);
        int to   = photos.IndexOf(target);
        if (from >= 0 && to >= 0)
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

    // ── Insertion adorner ──────────────────────────────────────────────────────

    private void SetFolderAdorner(ListBoxItem container, bool insertBefore)
    {
        if (_adorner?.AdornedElement == container && _adorner.InsertBefore == insertBefore) return;
        RemoveAdorner();
        var layer = AdornerLayer.GetAdornerLayer(container);
        if (layer == null) return;
        _adorner = new InsertionAdorner(container, insertBefore);
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

    private static int GetDropIndex(ListBox listBox, Point position)
    {
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            var container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;
            var topLeft = container.TranslatePoint(new Point(0, 0), listBox);
            if (position.Y < topLeft.Y + container.ActualHeight / 2)
                return i;
        }
        return Math.Max(0, listBox.Items.Count - 1);
    }

    private static T? ContextMenuTarget<T>(object menuItemSender) where T : class
    {
        // ContextMenu is on the DataTemplate's StackPanel, so PlacementTarget is that StackPanel
        if (menuItemSender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement fe } })
            return fe.DataContext as T;
        return null;
    }
}
