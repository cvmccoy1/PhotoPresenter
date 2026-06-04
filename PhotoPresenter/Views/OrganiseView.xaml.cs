using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PhotoPresenter.Services;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class OrganiseView : UserControl
{
    private Point _dragStartPoint;
    private InsertionAdorner? _adorner;

    public OrganiseView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        FolderSplitter.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(OnSplitterDragCompleted));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var pos = UserSettings.Load().SplitterPosition;
        if (pos.HasValue && pos.Value >= FolderColumn.MinWidth)
            FolderColumn.Width = new GridLength(pos.Value);
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var settings = UserSettings.Load();
        settings.SplitterPosition = FolderColumn.ActualWidth;
        settings.Save();
    }

    private OrganiseViewModel? Vm => DataContext as OrganiseViewModel;

    // ── Folder list ────────────────────────────────────────────────────────────

    private void FolderList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Vm != null)
        {
            foreach (var folder in FolderList.SelectedItems.OfType<PhotoFolderViewModel>()
                .Where(f => !f.IsRemoved).ToList())
                Vm.RemoveFolder(folder);
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
            var dragList = FolderList.SelectedItems.Contains(item) && FolderList.SelectedItems.Count > 1
                ? FolderList.SelectedItems.OfType<PhotoFolderViewModel>()
                    .OrderBy(f => Vm!.Folders.IndexOf(f))
                    .ToList()
                : new List<PhotoFolderViewModel> { item };

            FolderList.QueryContinueDrag += CancelDragOnEscape;
            DragDrop.DoDragDrop(FolderList, dragList, DragDropEffects.Move);
            FolderList.QueryContinueDrag -= CancelDragOnEscape;
            RemoveAdorner();
            _dragStartPoint = Mouse.GetPosition(null);
        }
    }

    private void FolderList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(List<PhotoFolderViewModel>)))
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
        if (!e.Data.GetDataPresent(typeof(List<PhotoFolderViewModel>))) return;
        var dragging = (List<PhotoFolderViewModel>)e.Data.GetData(typeof(List<PhotoFolderViewModel>));
        if (Vm == null || dragging.Count == 0) return;

        // Do nothing if the cursor is over one of the items being dragged.
        var itemUnderCursor = HitTestItem<PhotoFolderViewModel>(FolderList, e.GetPosition(FolderList));
        if (itemUnderCursor != null && dragging.Contains(itemUnderCursor)) return;

        int slot = GetDropSlot(FolderList, e.GetPosition(FolderList));
        Vm.ReorderFolders(dragging, slot);
    }

    private void FolderRemove_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        foreach (var f in SelectedTargets<PhotoFolderViewModel>(FolderList, sender).Where(f => !f.IsRemoved))
            Vm.RemoveFolder(f);
    }

    private void FolderRestore_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        foreach (var f in SelectedTargets<PhotoFolderViewModel>(FolderList, sender).Where(f => f.IsRemoved))
            Vm.RestoreFolder(f);
    }

    private void FolderTile_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.ContextMenu is not ContextMenu cm) return;

        foreach (var mi in cm.Items.OfType<MenuItem>())
            mi.ClearValue(VisibilityProperty);

        var clicked = fe.DataContext as PhotoFolderViewModel;
        var selected = FolderList.SelectedItems.OfType<PhotoFolderViewModel>().ToList();
        if (clicked == null || !selected.Contains(clicked) || selected.Count <= 1) return;

        bool anyVisible = selected.Any(f => !f.IsRemoved);
        bool anyHidden  = selected.Any(f =>  f.IsRemoved);
        SetMenuItemVisibility(cm, "FolderRemove",  anyVisible ? Visibility.Visible : Visibility.Collapsed);
        SetMenuItemVisibility(cm, "FolderRestore", anyHidden  ? Visibility.Visible : Visibility.Collapsed);
    }

    // ── Photo list ─────────────────────────────────────────────────────────────

    private void PhotoList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Vm != null)
        {
            foreach (var photo in PhotoList.SelectedItems.OfType<PhotoItemViewModel>()
                .Where(p => !p.IsRemoved).ToList())
                Vm.RemovePhoto(photo);
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
            var photos = Vm?.Photos;
            var dragList = (photos != null && PhotoList.SelectedItems.Contains(item) && PhotoList.SelectedItems.Count > 1)
                ? PhotoList.SelectedItems.OfType<PhotoItemViewModel>()
                    .Where(p => photos.Contains(p))
                    .OrderBy(p => photos.IndexOf(p))
                    .ToList()
                : new List<PhotoItemViewModel> { item };

            PhotoList.QueryContinueDrag += CancelDragOnEscape;
            DragDrop.DoDragDrop(PhotoList, dragList, DragDropEffects.Move);
            PhotoList.QueryContinueDrag -= CancelDragOnEscape;
            RemoveAdorner();
            _dragStartPoint = Mouse.GetPosition(null);
        }
    }

    private void PhotoList_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(List<PhotoItemViewModel>)))
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
        if (!e.Data.GetDataPresent(typeof(List<PhotoItemViewModel>))) return;
        var dragging = (List<PhotoItemViewModel>)e.Data.GetData(typeof(List<PhotoItemViewModel>));
        if (Vm == null || dragging.Count == 0) return;

        var target = HitTestItem<PhotoItemViewModel>(PhotoList, e.GetPosition(PhotoList));
        if (target == null) return;

        // Do nothing if the cursor is over one of the items being dragged.
        if (dragging.Contains(target)) return;

        var photos = Vm.Photos;
        if (photos == null) return;
        int targetIndex = photos.IndexOf(target);
        if (targetIndex < 0) return;

        var container = PhotoList.ItemContainerGenerator.ContainerFromIndex(targetIndex) as ListBoxItem;
        bool insertBefore = container == null || e.GetPosition(container).X < container.ActualWidth / 2;
        int slot = insertBefore ? targetIndex : targetIndex + 1;

        Vm.ReorderPhotos(dragging, slot);
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
        if (Vm == null) return;
        foreach (var p in SelectedTargets<PhotoItemViewModel>(PhotoList, sender).Where(p => !p.IsRemoved))
            Vm.RemovePhoto(p);
    }

    private void PhotoRestore_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        foreach (var p in SelectedTargets<PhotoItemViewModel>(PhotoList, sender).Where(p => p.IsRemoved))
            Vm.RestorePhoto(p);
    }

    private void PhotoTile_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.ContextMenu is not ContextMenu cm) return;

        foreach (var mi in cm.Items.OfType<MenuItem>())
        {
            mi.ClearValue(VisibilityProperty);
            mi.ClearValue(IsEnabledProperty);
        }

        var clicked = fe.DataContext as PhotoItemViewModel;
        var selected = PhotoList.SelectedItems.OfType<PhotoItemViewModel>().ToList();
        if (clicked == null || !selected.Contains(clicked) || selected.Count <= 1) return;

        // Open / Open With — disabled for multi-selection.
        SetMenuItemEnabled(cm, "Open",     false);
        SetMenuItemEnabled(cm, "OpenWith", false);

        // Remove / Restore — based on whether any are visible or hidden.
        bool anyVisible = selected.Any(p => !p.IsRemoved);
        bool anyHidden  = selected.Any(p =>  p.IsRemoved);
        SetMenuItemVisibility(cm, "Remove",  anyVisible ? Visibility.Visible : Visibility.Collapsed);
        SetMenuItemVisibility(cm, "Restore", anyHidden  ? Visibility.Visible : Visibility.Collapsed);

        // Caption — replace Add/Edit with a single "Set Caption"; show Delete if any have one.
        SetMenuItemVisibility(cm, "SetCaption",   Visibility.Visible);
        SetMenuItemVisibility(cm, "AddCaption",   Visibility.Collapsed);
        SetMenuItemVisibility(cm, "EditCaption",  Visibility.Collapsed);
        bool anyHasCaption = selected.Any(p => p.HasCaption);
        SetMenuItemVisibility(cm, "DeleteCaption", anyHasCaption ? Visibility.Visible : Visibility.Collapsed);
    }

    private void PhotoCaptionSet_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var selected = PhotoList.SelectedItems.OfType<PhotoItemViewModel>().ToList();
        if (selected.Count == 0) return;
        var existing = selected.FirstOrDefault(p => p.HasCaption)?.Caption ?? "";
        var dlg = new CaptionDialog(existing) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            foreach (var photo in selected)
                Vm.SetCaption(photo, dlg.Caption);
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

    // If the right-clicked item is in the current selection, return all selected items of type T.
    // Otherwise return just the right-clicked item, so single-item right-click always works.
    private static List<T> SelectedTargets<T>(ListBox listBox, object menuItemSender) where T : class
    {
        var target = ContextMenuTarget<T>(menuItemSender);
        if (target == null) return new();
        return listBox.SelectedItems.Contains(target)
            ? listBox.SelectedItems.OfType<T>().ToList()
            : new List<T> { target };
    }

    private static MenuItem? FindMenuItem(ContextMenu cm, string tag) =>
        cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Tag as string == tag);

    private static void SetMenuItemVisibility(ContextMenu cm, string tag, Visibility v)
    {
        var mi = FindMenuItem(cm, tag);
        if (mi != null) mi.Visibility = v;
    }

    private static void SetMenuItemEnabled(ContextMenu cm, string tag, bool enabled)
    {
        var mi = FindMenuItem(cm, tag);
        if (mi != null) mi.IsEnabled = enabled;
    }
}
