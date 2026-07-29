using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Certificates;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LauncherXWinUI.Controls.GridViewItems
{
    public sealed partial class GridViewTileGroup : UserControl
    {
        public GridViewTileGroup()
        {
            this.InitializeComponent();

            // Set the unique id to some guid
            this.UniqueId = System.Guid.NewGuid().ToString();

            // For some reason, StackPanel needs a background for events to work properly
            GroupPanel.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

            // Subscribe to the event to notify us when new items are added/removed to the GridViewTileGroup
            Items.CollectionChanged += Items_CollectionChanged;
            Items.CollectionChanged += (s, e) => UpdatePreview();

            // Subscribe to drag/drop for external items (Feature 1)
            GroupPanel.DragOver += GroupPanel_DragOver;
            GroupPanel.Drop += GroupPanel_Drop;
        }

        // Declare properties that this control will have

        /// <summary>
        /// A unique GUID to identify each item in the ItemsGridView
        /// </summary>
        public string UniqueId
        {
            get => (string)GetValue(UniqueIdProperty);
            set => SetValue(UniqueIdProperty, value);
        }

        DependencyProperty UniqueIdProperty = DependencyProperty.Register(
            nameof(UniqueId),
            typeof(string),
            typeof(GridViewTileGroup),
            new PropertyMetadata(default(string)));


        /// <summary>
        /// Size of the control
        /// </summary>
        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size),
            typeof(double),
            typeof(GridViewTileGroup),
            new PropertyMetadata(default(double), new PropertyChangedCallback(OnSizeChanged)));

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GridViewTileGroup gridViewTileGroup = d as GridViewTileGroup;
            double? newScale = e.NewValue as double?;
            if (newScale != null)
            {
                // Calculate new dimensions
                double newSize = Math.Sqrt(newScale.Value);
                double newWidth = 105 * newSize;
                double newHeight = 95 * newSize;

                // Update control dimensions
                gridViewTileGroup.ControlBorder.Width = newWidth;
                gridViewTileGroup.ControlBorder.Height = newHeight;
                gridViewTileGroup.ItemsPreviewGrid.Margin = new Thickness(0, newSize * 2.5, 0, 0);

                // Update image controls
                foreach (Image image in gridViewTileGroup.ItemsPreviewGrid.Children)
                {
                    image.Margin = new Thickness(newSize * 2.5);
                    image.Stretch = Stretch.Uniform;
                }

                // Update the font size of the textblock
                gridViewTileGroup.TileText.FontSize = newSize * 12;
            }
        }

        /// <summary>
        /// Text that is displayed below the ItemsPreviewGridView
        /// </summary>
        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        DependencyProperty DisplayTextProperty = DependencyProperty.Register(
            nameof(DisplayText),
            typeof(string),
            typeof(GridViewTileGroup),
            new PropertyMetadata(default(string), new PropertyChangedCallback(OnDisplayTextChanged)));

        private static void OnDisplayTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GridViewTileGroup gridViewTileGroup = d as GridViewTileGroup;
            string newDisplayText = e.NewValue as string;

            if (newDisplayText != null)
            {
                // Update textblock
                gridViewTileGroup.TileText.Text = newDisplayText;

                // Update tooltip
                ToolTipService.SetToolTip(gridViewTileGroup.GroupPanel, newDisplayText);
            }
        }

        /// <summary>
        /// List of GridViewTile objects in this GridViewTileGroup 
        /// </summary>
        /// Name the property "Items" for parity with GridView.Items and ListView.Items
        public ObservableCollection<GridViewTile> Items
        {
            get => (ObservableCollection<GridViewTile>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items),
            typeof(ObservableCollection<GridViewTile>),
            typeof(GridViewTileGroup),
            new PropertyMetadata(new ObservableCollection<GridViewTile>()));

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        // Methods

        /// <summary>
        /// Updates the preview of items shown in the GridViewTileGroup
        /// </summary>
        public void UpdatePreview()
        {
            ItemsPreviewGrid.Children.Clear();

            // Create new image objects to display the first 4 items in the items collection
            for (int i = 0; i < Items.Count; i++)
            {
                if (i >= 4)
                {
                    break;
                }

                double ControlSize = Math.Sqrt(Size);
                Image image = new Image();
                image.Source = Items[i].ImageSource;
                image.Margin = new Thickness(ControlSize * 2.5);
                image.Stretch = Stretch.Uniform;
                image.HorizontalAlignment = HorizontalAlignment.Center;
                image.VerticalAlignment = VerticalAlignment.Center;
                image.SetValue(Grid.RowProperty, Math.Floor((double)i / 2));
                image.SetValue(Grid.ColumnProperty, i % 2);
                ItemsPreviewGrid.Children.Add(image);
            }
        }

        /// <summary>
        /// Check if we should allow clicking interaction with this item, based on the selection mode of the parent GridView
        /// </summary>
        /// <returns>true if GridView is single select</returns>
        private bool IsInteractionEnabled()
        {
            GridView parentGridView = this.Parent as GridView;
            if (parentGridView.SelectionMode == ListViewSelectionMode.Single)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Highlights the control by drawing a border around it
        /// </summary>
        public void HighlightControl()
        {
            ControlBorder.BorderBrush = Application.Current.Resources["AccentFillColorDefaultBrush"] as SolidColorBrush;
        }

        /// <summary>
        /// Unhighlights the control by removing the border around it
        /// </summary>
        public void UnhighlightControl()
        {
            ControlBorder.BorderBrush = null;
        }

        /// <summary>
        /// Show the flyout to indicate that a item can be added to this group
        /// </summary>
        public void ShowAddItemIndicator()
        {
            MenuFlyout flyoutBase = (MenuFlyout)FlyoutBase.GetAttachedFlyout(GridViewTileGroupControl);
            flyoutBase.ShowAt(GridViewTileGroupControl);
            HighlightControl();
        }

        /// <summary>
        /// Hide the flyout that indicates that a item can be added to this group
        /// </summary>
        public void HideAddItemIndicator()
        {
            MenuFlyout flyoutBase = (MenuFlyout)FlyoutBase.GetAttachedFlyout(GridViewTileGroupControl);
            flyoutBase.Hide();
            UnhighlightControl();
        }

        /// <summary>
        /// Removes this group from the parent GridView
        /// </summary>
        public void RemoveFromGridView()
        {
            // Remove this group
            GridView parentGridView = this.Parent as GridView;
            if (parentGridView != null)
            {
                parentGridView.Items.Remove(this);
            }
        }

        /// <summary>
        /// Handles dragging external items over the group (Feature 1).
        /// </summary>
        private void GroupPanel_DragOver(object sender, DragEventArgs e)
        {
            // Accept files, folders, and URLs from external sources
            if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.WebLink))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                HighlightControl();
            }
        }

        /// <summary>
        /// Handles dropping external items onto the group (Feature 1).
        /// </summary>
        private async void GroupPanel_Drop(object sender, DragEventArgs e)
        {
            UnhighlightControl();

            if (e.DataView.Contains(StandardDataFormats.WebLink))
            {
                Uri uri = await e.DataView.GetWebLinkAsync();
                string url = uri.ToString();
                GridViewTile tile = new GridViewTile();
                tile.ExecutingPath = url;
                tile.DisplayText = url;
                tile.ImageSource = LauncherXWinUI.Classes.IconHelpers.GetWebsiteIcon(url);
                tile.Size = Items.Count > 0 ? Items[0].Size : 1.0;
                Items.Add(tile);
            }
            else if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();

                foreach (var storageFolder in items.OfType<Windows.Storage.StorageFolder>())
                {
                    var bitmapImage = await LauncherXWinUI.Classes.IconHelpers.GetFolderIcon(storageFolder.Path);
                    GridViewTile tile = new GridViewTile();
                    tile.ExecutingPath = storageFolder.Path;
                    tile.DisplayText = storageFolder.Name;
                    tile.ImageSource = bitmapImage;
                    tile.Size = Items.Count > 0 ? Items[0].Size : 1.0;
                    Items.Add(tile);
                }

                foreach (var storageFile in items.OfType<Windows.Storage.StorageFile>())
                {
                    // Support .txt/.rtf notes (Feature 4)
                    var bitmapImage = await LauncherXWinUI.Classes.IconHelpers.GetFileIcon(storageFile.Path);
                    GridViewTile tile = new GridViewTile();
                    tile.ExecutingPath = storageFile.Path;
                    tile.DisplayText = storageFile.Name;
                    tile.ImageSource = bitmapImage;
                    tile.Size = Items.Count > 0 ? Items[0].Size : 1.0;
                    Items.Add(tile);
                }
            }
        }

        // Event Handlers
        // Similar to GridViewTile.
        // For event handlers relating to left/right clicking the GridViewTile,
        // we only enable them if the parent GridView has "Single" selection mode,
        // as if we are in multiselect, we want the users to be able to select multiple items
        private void GroupPanel_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsInteractionEnabled())
            {
                HighlightControl();
            }
        }

        private void GroupPanel_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsInteractionEnabled())
            {
                UnhighlightControl();
            }
        }

        private async void GroupPanel_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (IsInteractionEnabled() == false)
            {
                return;
            }

            // Show the ContentDialog to display the items in this Group
            ItemsGridView.Items.Clear();
            foreach (GridViewTile gridViewTile in Items)
            {
                ItemsGridView.Items.Add(gridViewTile);
            }

            ItemsGridView.Items.VectorChanged += ItemsGridViewItems_VectorChanged;

            // When the dialog is closed, do 3 things:
            // 1. Unsubscribe from the necessary events
            // 2. Clear the items in the ItemsGridView, so that when GridViewTiles from this control are added to a new GridViewTileGroup control to add more GridViewTiles (see MainWindow.xaml.cs),
            // there won't be a case where a GridViewTile has 2 parents
            // 3. Unselect this control in the ItemsGridView in MainWindow
            GroupDialog.Closing += (s, e) =>
            {
                GroupDialogTitleTextBox.TextChanged -= GroupDialogTitleTextBox_TextChanged;
                ItemsGridView.Items.VectorChanged -= ItemsGridViewItems_VectorChanged;
                ItemsGridView.Items.Clear();

                // Unselect this item
                GridView parentGridView = this.Parent as GridView;
                if (parentGridView != null)
                {
                    parentGridView.SelectedItem = null;
                }
            };

            // Configure the title of the GroupDialog
            GroupDialogTitleTextBox.Text = DisplayText;
            GroupDialogTitleTextBox.TextChanged += GroupDialogTitleTextBox_TextChanged;

            // Configure the size of the GroupDialog
            GroupDialogContent.Width = App.MainWindow.Width * 0.8;
            GroupDialogContent.Height = App.MainWindow.Height * 0.6;
            App.MainWindow.SizeChanged += (s, e) =>
            {
                GroupDialogContent.Width = App.MainWindow.Width * 0.8;
                GroupDialogContent.Height = App.MainWindow.Height * 0.6;
            };

            var result = await GroupDialog.ShowAsync();
            UnhighlightControl();
        }

        // Only fires when the GroupDialog is open
        private void GroupDialogTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            DisplayText = GroupDialogTitleTextBox.Text;
        }

        // Only fires when the GroupDialog is open
        private void ItemsGridViewItems_VectorChanged(Windows.Foundation.Collections.IObservableVector<object> sender, Windows.Foundation.Collections.IVectorChangedEventArgs @event)
        {
            // Update the ItemsProperty based on the items in the ItemsGridView
            Items.Clear();
            foreach (GridViewTile gridViewTile in ItemsGridView.Items)
            {
                Items.Add(gridViewTile);
            }
        }

        private void ItemsGridView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            // Unhighlight all controls, just in case
            foreach (UserControl gridViewItem in ItemsGridView.Items)
            {
                if (gridViewItem is GridViewTile)
                {
                    GridViewTile gridViewTile = gridViewItem as GridViewTile;
                    gridViewTile.UnhighlightControl();
                }
            }
        }

        private void GroupPanel_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (IsInteractionEnabled())
            {
                // Show normal right click menu options 
                MenuFlyout flyoutBase = (MenuFlyout)FlyoutBase.GetAttachedFlyout(GroupPanel);
                flyoutBase.ShowAt(GroupPanel, e.GetPosition(GroupPanel));
            }
        }

        private void MenuRemoveOption_Click(object sender, RoutedEventArgs e)
        {
            RemoveFromGridView();
        }

        // ---- Feature 2: Multi-select and add-item handlers ----

        private void MultiSelectGroupToggle_Checked(object sender, RoutedEventArgs e)
        {
            ItemsGridView.SelectionMode = ListViewSelectionMode.Multiple;
            MultiSelectGroupToggle.Content = "Deselect all";
            ItemsGridView.SelectAll();
        }

        private void MultiSelectGroupToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ItemsGridView.SelectionMode = ListViewSelectionMode.Single;
            MultiSelectGroupToggle.Content = "Select all";
            ItemsGridView.SelectedItems.Clear();
        }

        private async void AddFileToGroup_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.DereferenceLinks = false;
            // WinUI 3 FileOpenPicker needs InitializeWithWindow
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeFilter.Add("*");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    GridViewTile tile = new GridViewTile();
                    tile.ExecutingPath = file.Path;
                    tile.DisplayText = file.Name;
                    tile.ImageSource = await IconHelpers.GetFileIcon(file.Path);
                    tile.Size = Items.Count > 0 ? Items[0].Size : 1.0;
                    Items.Add(tile);
                }
            }
        }

        private async void AddFolderToGroup_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                GridViewTile tile = new GridViewTile();
                tile.ExecutingPath = folder.Path;
                tile.DisplayText = folder.Name;
                tile.ImageSource = await IconHelpers.GetFolderIcon(folder.Path);
                tile.Size = Items.Count > 0 ? Items[0].Size : 1.0;
                Items.Add(tile);
            }
        }
    }
}
