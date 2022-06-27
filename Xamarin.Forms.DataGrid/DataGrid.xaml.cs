using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Xamarin.Forms.DataGrid.Utils;

[assembly: InternalsVisibleTo("Xamarin.Forms.DataGrid.UnitTest")]
namespace Xamarin.Forms.DataGrid
{
	[Xaml.XamlCompilation(Xaml.XamlCompilationOptions.Compile)]
	public partial class DataGrid : IDisposable
	{
        #region Events
        public event EventHandler Refreshing;
        public event EventHandler<SelectionChangedEventArgs> ItemSelected;
        #endregion

		#region Fields
		private static readonly ColumnDefinition _starColumn = new() { Width = DataGridColumn.StarLength };
		private static readonly ColumnDefinition _autoColumn = new() { Width = DataGridColumn.AutoLength };
		private static readonly Lazy<ImageSource> _defaultSortIcon = new(() => ImageSource.FromResource("Xamarin.Forms.DataGrid.up.png", typeof(DataGrid).GetTypeInfo().Assembly));
		private static Lazy<Style> _defaultSortIconStyle;
        private readonly Dictionary<int, SortingOrder> _sortingOrders;
        #endregion

        #region Bindable properties
        public static readonly BindableProperty ActiveRowColorProperty =
			BindableProperty.Create(nameof(ActiveRowColor), typeof(Color), typeof(DataGrid), Color.FromRgb(128, 144, 160),
				coerceValue: (b, v) => {
					if (!((DataGrid)b).SelectionEnabled)
						throw new InvalidOperationException("Datagrid must be SelectionEnabled=true to set ActiveRowColor");
					return v;
				});

		public static readonly BindableProperty HeaderBackgroundProperty =
			BindableProperty.Create(nameof(HeaderBackground), typeof(Color), typeof(DataGrid), Color.FromHex("#555"),
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					if (self._headerView != null && !self.HeaderBordersVisible)
						self._headerView.BackgroundColor = (Color)n;
				});

        public static readonly BindableProperty HeaderForegroundProperty =
			BindableProperty.Create(nameof(HeaderForeground), typeof(Color), typeof(DataGrid), Color.White,
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					if (self._headerView != null && !self.HeaderBordersVisible)
					{
                        foreach (var child in self._headerView.Children)
						{
							if (child is Label label)
							{
								label.TextColor = (Color)n;
                            }
						}
                    }
                });

        public static readonly BindableProperty LineBreakModeProperty =
			BindableProperty.Create(nameof(LineBreakMode), typeof(LineBreakMode), typeof(DataGrid), LineBreakMode.TailTruncation,
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					if (self._headerView != null && !self.HeaderBordersVisible)
                    {
						foreach (var child in self._headerView.Children)
                        {
							if (child is Label label)
                            {
								label.LineBreakMode = (LineBreakMode)n;
							}
                        }
					}
				});

		public static readonly BindableProperty BorderColorProperty =
			BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(DataGrid), Color.Black,
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    if (self.HeaderBordersVisible)
						self._headerView.BackgroundColor = (Color)n;

					if (self.Columns != null && self.ItemsSource != null)
						self.Reload();
				});

		public static readonly BindableProperty RowsBackgroundColorPaletteProperty =
			BindableProperty.Create(nameof(RowsBackgroundColorPalette), typeof(IColorProvider), typeof(DataGrid), new PaletteCollection { default },
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    if (self.Columns != null && self.ItemsSource != null)
						self.Reload();
				});

		public static readonly BindableProperty RowsTextColorPaletteProperty =
			BindableProperty.Create(nameof(RowsTextColorPalette), typeof(IColorProvider), typeof(DataGrid), new PaletteCollection { Color.Black },
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    if (self.Columns != null && self.ItemsSource != null)
						self.Reload();
				});

		public static readonly BindableProperty ColumnsProperty =
			BindableProperty.Create(nameof(Columns), typeof(ColumnCollection), typeof(DataGrid),
				propertyChanged: (b, o, n) => ((DataGrid)b).InitHeaderView(),
				defaultValueCreator: b => new ColumnCollection()
			);

		public static readonly BindableProperty ItemsSourceProperty =
			BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(DataGrid), null,
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    //ObservableCollection Tracking 
                    if (o != null && o is INotifyCollectionChanged oChanged)
						oChanged.CollectionChanged -= self.HandleItemsSourceCollectionChanged;

					if (n != null)
					{
						self.InternalItems = ((IEnumerable)n).Cast<object>().ToList();

						if (n is INotifyCollectionChanged nChanged)
							nChanged.CollectionChanged += self.HandleItemsSourceCollectionChanged;
					}

					if (self.SelectedItem != null && !self.InternalItems.Contains(self.SelectedItem))
						self.SelectedItem = null;

					//if (self._collectionView.EmptyView != null)
					//{
					//	// TODO
					//}
                });

		void HandleItemsSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (var item in e.NewItems)
						_internalItems.Add(item);
					break;

				case NotifyCollectionChangedAction.Move:
					// Not tracking order
					break;

					case NotifyCollectionChangedAction.Remove:
					foreach (var item in e.OldItems)
						_internalItems.Remove(item);
					break;

				case NotifyCollectionChangedAction.Replace:
					foreach (var item in e.OldItems)
						_internalItems.Remove(item);
					foreach (var item in e.NewItems)
						_internalItems.Add(item);
					break;

				case NotifyCollectionChangedAction.Reset:
					_internalItems.Clear();
					break;
			}

			SortIfNeeded();

			if (SelectedItem != null && !InternalItems.Contains(SelectedItem))
				SelectedItem = null;
		}

		public static readonly BindableProperty RowHeightProperty =
			BindableProperty.Create(nameof(RowHeight), typeof(int), typeof(DataGrid), 40);

		public static readonly BindableProperty HeaderHeightProperty =
			BindableProperty.Create(nameof(HeaderHeight), typeof(int), typeof(DataGrid), 40,
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					self._headerView.HeightRequest = (int)n;
				});

		public static readonly BindableProperty IsSortableProperty =
			BindableProperty.Create(nameof(IsSortable), typeof(bool), typeof(DataGrid), true);

		public static readonly BindableProperty FontSizeProperty =
			BindableProperty.Create(nameof(FontSize), typeof(double), typeof(DataGrid), 13.0);

		public static readonly BindableProperty FontFamilyProperty =
			BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(DataGrid), Font.Default.FontFamily);

		public static readonly BindableProperty SelectedItemProperty =
			BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(DataGrid), null, BindingMode.TwoWay,
				coerceValue: (b, v) => {
                    var self = (DataGrid)b;
                    if (!self.SelectionEnabled && v != null)
						throw new InvalidOperationException("Datagrid must be SelectionEnabled=true to set SelectedItem");
					if (self.InternalItems?.Contains(v) == true)
						return v;
					return null;
				},
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					if (self._collectionView.SelectedItem != n)
						self._collectionView.SelectedItem = n;
				}
			);

		public static readonly BindableProperty SelectionEnabledProperty =
			BindableProperty.Create(nameof(SelectionEnabled), typeof(bool), typeof(DataGrid), true,
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
					if (!self.SelectionEnabled && self.SelectedItem != null)
					{
						self.SelectedItem = null;
					}
				});

		public static readonly BindableProperty PullToRefreshCommandProperty =
			BindableProperty.Create(nameof(PullToRefreshCommand), typeof(ICommand), typeof(DataGrid), null,
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    if (n == null)
					{
                        self._refreshView.IsEnabled = false;
						self._refreshView.Command = null;
					}
					else
					{
                        self._refreshView.IsEnabled = true;
						self._refreshView.Command = (ICommand)n;
					}
				});

		public static readonly BindableProperty IsRefreshingProperty =
			BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(DataGrid), false, BindingMode.TwoWay,
				propertyChanged: (b, o, n) => ((DataGrid) b)._refreshView.IsRefreshing = (bool)n);

		public static readonly BindableProperty BorderThicknessProperty =
			BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(DataGrid), new Thickness(1),
				propertyChanged: (b, o, n) => {
					((DataGrid) b)._headerView.ColumnSpacing = ((Thickness)n).HorizontalThickness / 2;
					((DataGrid) b)._headerView.Padding = ((Thickness)n).HorizontalThickness / 2;
				});

		public static readonly BindableProperty HeaderBordersVisibleProperty =
			BindableProperty.Create(nameof(HeaderBordersVisible), typeof(bool), typeof(DataGrid), true,
				propertyChanged: (b, o, n) => ((DataGrid) b)._headerView.BackgroundColor = (bool)n ? ((DataGrid) b).BorderColor : ((DataGrid) b).HeaderBackground);

		public static readonly BindableProperty SortedColumnIndexProperty =
			BindableProperty.Create(nameof(SortedColumnIndex), typeof(SortData), typeof(DataGrid), null, BindingMode.TwoWay,
				validateValue: (b, v) => {
					var self = (DataGrid)b;
					var sData = (SortData)v;

					return
						sData == null || //setted to null
						self.Columns == null || // Columns binded but not setted
						self.Columns.Count == 0 || //columns not setted yet
						(sData.Index < self.Columns.Count && self.Columns.ElementAt(sData.Index).SortingEnabled);
				},
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					self.SortItems((SortData)n);
				});

		public static readonly BindableProperty HeaderLabelStyleProperty =
			BindableProperty.Create(nameof(HeaderLabelStyle), typeof(Style), typeof(DataGrid));

		public static readonly BindableProperty SortIconProperty =
			BindableProperty.Create(nameof(SortIcon), typeof(ImageSource), typeof(DataGrid), DefaultSortIcon);

		public static readonly BindableProperty SortIconStyleProperty =
			BindableProperty.Create(nameof(SortIconStyle), typeof(Style), typeof(DataGrid), null,

				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					var style = ((Style) n).Setters.FirstOrDefault(x => x.Property == Image.SourceProperty);
					if (style != null)
					{
						if (style.Value is string vs)
							self.SortIcon = ImageSource.FromFile(vs);
						else
							self.SortIcon = (ImageSource)style.Value;
					}
				});

		public static readonly BindableProperty NoDataViewProperty =
			BindableProperty.Create(nameof(NoDataView), typeof(View), typeof(DataGrid),
				propertyChanged: (b, o, n) => {
					if (o != n)
						((DataGrid) b)._collectionView.EmptyView = n as View;
				});
		#endregion

		#region Properties
		public Color ActiveRowColor
		{
			get => (Color)GetValue(ActiveRowColorProperty);
			set => SetValue(ActiveRowColorProperty, value);
		}

		public Color HeaderBackground
		{
			get => (Color)GetValue(HeaderBackgroundProperty);
			set => SetValue(HeaderBackgroundProperty, value);
		}

        public Color HeaderForeground
        {
            get => (Color)GetValue(HeaderForegroundProperty);
            set => SetValue(HeaderForegroundProperty, value);
        }

        public LineBreakMode LineBreakMode
		{
			get => (LineBreakMode)GetValue(LineBreakModeProperty);
			set => SetValue(LineBreakModeProperty, value);
		}

		public Color BorderColor
		{
			get => (Color)GetValue(BorderColorProperty);
			set => SetValue(BorderColorProperty, value);
		}

		public IColorProvider RowsBackgroundColorPalette
		{
			get => (IColorProvider)GetValue(RowsBackgroundColorPaletteProperty);
			set => SetValue(RowsBackgroundColorPaletteProperty, value);
		}

		public IColorProvider RowsTextColorPalette
		{
			get => (IColorProvider)GetValue(RowsTextColorPaletteProperty);
			set => SetValue(RowsTextColorPaletteProperty, value);
		}

		public IEnumerable ItemsSource
		{
			get => (IEnumerable)GetValue(ItemsSourceProperty);
			set => SetValue(ItemsSourceProperty, value);
		}

		IList<object> _internalItems;

		internal IList<object> InternalItems
		{
			get => _internalItems;
			set
			{
				_internalItems = value;

				if (IsSortable && SortedColumnIndex != null)
					SortItems(SortedColumnIndex);
				else
					_collectionView.ItemsSource = _internalItems;
			}
		}

        public ColumnCollection Columns
		{
			get => (ColumnCollection)GetValue(ColumnsProperty);
			set => SetValue(ColumnsProperty, value);
		}

		public double FontSize
		{
			get => (double)GetValue(FontSizeProperty);
			set => SetValue(FontSizeProperty, value);
		}

		public string FontFamily
		{
			get => (string)GetValue(FontFamilyProperty);
			set => SetValue(FontFamilyProperty, value);
		}

		public int RowHeight
		{
			get => (int)GetValue(RowHeightProperty);
			set => SetValue(RowHeightProperty, value);
		}

		public int HeaderHeight
		{
			get => (int)GetValue(HeaderHeightProperty);
			set => SetValue(HeaderHeightProperty, value);
		}

		public bool IsSortable
		{
			get => (bool)GetValue(IsSortableProperty);
			set => SetValue(IsSortableProperty, value);
		}

		public bool SelectionEnabled
		{
			get => (bool) GetValue(SelectionEnabledProperty);
			set => SetValue(SelectionEnabledProperty, value);
		}

		public object SelectedItem
		{
			get => GetValue(SelectedItemProperty);
			set => SetValue(SelectedItemProperty, value);
		}

		public ICommand PullToRefreshCommand
		{
			get => (ICommand) GetValue(PullToRefreshCommandProperty);
			set => SetValue(PullToRefreshCommandProperty, value);
		}

		public bool IsRefreshing
		{
			get => (bool) GetValue(IsRefreshingProperty);
			set => SetValue(IsRefreshingProperty, value);
		}

		public Thickness BorderThickness
		{
			get => (Thickness) GetValue(BorderThicknessProperty);
			set => SetValue(BorderThicknessProperty, value);
		}

		public bool HeaderBordersVisible
		{
			get => (bool) GetValue(HeaderBordersVisibleProperty);
			set => SetValue(HeaderBordersVisibleProperty, value);
		}

		public SortData SortedColumnIndex
		{
			get => (SortData) GetValue(SortedColumnIndexProperty);
			set => SetValue(SortedColumnIndexProperty, value);
		}

		public Style HeaderLabelStyle
		{
			get => (Style) GetValue(HeaderLabelStyleProperty);
			set => SetValue(HeaderLabelStyleProperty, value);
		}

		public ImageSource SortIcon
		{
			get => (ImageSource) GetValue(SortIconProperty);
			set => SetValue(SortIconProperty, value);
		}

		public Style SortIconStyle
		{
			get => (Style) GetValue(SortIconStyleProperty);
			set => SetValue(SortIconStyleProperty, value);
		}

		public static Style DefaultSortIconStyle => _defaultSortIconStyle.Value;
        public static ImageSource DefaultSortIcon => _defaultSortIcon.Value;

        public View NoDataView
		{
			get => (View) GetValue(NoDataViewProperty);
			set => SetValue(NoDataViewProperty, value);
		}

		#endregion

		#region ctor

		public DataGrid()
		{
			InitializeComponent();

			if (_defaultSortIconStyle == null)
				_defaultSortIconStyle = new(() => (Style)_headerView.Resources["SortIconStyle"]);

			_sortingOrders = new Dictionary<int, SortingOrder>();

			_collectionView.SelectionChanged += CollectionViewSelectionChanged;

			_refreshView.Refreshing += RefreshViewRefreshing;
		}

		#endregion

		#region UI Methods
		protected override void OnParentSet()
		{
			base.OnParentSet();

            if (Parent != null)
            {
                InitHeaderView();
            }
            else
            {
				Dispose();
            }
        }

        protected override void OnBindingContextChanged()
		{
			base.OnBindingContextChanged();
			SetColumnsBindingContext();
		}

		private void DisposeGestures()
		{
			var gestureRecognizers = _headerView.Children.SelectMany(v => v.GestureRecognizers);

            foreach (var tgr in gestureRecognizers.Cast<TapGestureRecognizer>())
			{
				tgr.Tapped -= SortTapped;
			}
		}

		private void Reload()
		{
			InternalItems = new List<object>(_internalItems);
			//Maybe just use OnPropertyChanged(nameof(ItemsSource));
		}
		#endregion

		#region Header Creation Methods

		private View GetHeaderViewForColumn(DataGridColumn column)
		{
			column.HeaderLabel.Style = column.HeaderLabelStyle ?? HeaderLabelStyle ?? (Style)_headerView.Resources["HeaderDefaultStyle"];

			var grid = new Grid {
				ColumnSpacing = 0,
			};

			grid.ColumnDefinitions.Add(_starColumn);
			grid.ColumnDefinitions.Add(_autoColumn);

			if (IsSortable)
			{
				column.SortingIcon.Style = (Style)_headerView.Resources["ImageStyleBase"];

				grid.Children.Add(column.SortingIcon);
				Grid.SetColumn(column.SortingIcon, 1);

				var tgr = new TapGestureRecognizer();
				tgr.Tapped += SortTapped;
				grid.GestureRecognizers.Add(tgr);
			}

			grid.Children.Add(column.HeaderLabel);

			return grid;
		}

		private void SortTapped(object sender, EventArgs e)
		{
			int index = Grid.GetColumn((BindableObject)sender);
			SortingOrder order = _sortingOrders[index] == SortingOrder.Ascendant ? SortingOrder.Descendant : SortingOrder.Ascendant;

			if (Columns.ElementAt(index).SortingEnabled)
				SortedColumnIndex = new SortData(index, order);
		}

		private void InitHeaderView()
		{
			SetColumnsBindingContext();
			DisposeGestures();
			_headerView.Children.Clear();
			_headerView.ColumnDefinitions.Clear();
			_sortingOrders.Clear();

			_headerView.Padding = new Thickness(BorderThickness.Left, BorderThickness.Top, BorderThickness.Right, 0);
			_headerView.ColumnSpacing = BorderThickness.HorizontalThickness;

			if (Columns != null)
			{
				foreach (var col in Columns)
				{
					_headerView.ColumnDefinitions.Add(new ColumnDefinition { Width = col.Width });

					var cell = GetHeaderViewForColumn(col);

					_headerView.Children.Add(cell);
					Grid.SetColumn(cell, Columns.IndexOf(col));

					_sortingOrders.Add(Columns.IndexOf(col), SortingOrder.None);
				}
			}
		}

		private void SortIfNeeded()
		{
			if (IsSortable && SortedColumnIndex != null)
				SortItems(SortedColumnIndex);
			else
				_collectionView.ItemsSource = _internalItems;
		}

		private void SetColumnsBindingContext()
		{
			if (Columns == null)
			{
				return;
			}

			foreach (var c in Columns)
			{
				c.BindingContext = BindingContext;
			}
		}
		#endregion

		#region Sorting methods
		internal void SortItems(SortData sData)
		{
			if (InternalItems == null || sData.Index >= Columns.Count || !Columns[sData.Index].SortingEnabled)
				return;

			var colIndex = sData.Index;
			var column = Columns[sData.Index];
			var sortOrder = sData.Order;

			if (!IsSortable)
				throw new InvalidOperationException("This DataGrid is not sortable");
			else if (column.PropertyName == null && column.SortingPropertyName == null)
				throw new InvalidOperationException("Please set the PropertyName or SortingPropertyName property of Column");

			var sorted = new List<object>();

            //Sort
            if (column.SortingPropertyName != null) {  // SortingProperty is defined
                if (sortOrder == SortingOrder.Descendant)
					sorted = _internalItems.OrderByDescending(x => ReflectionUtils.GetPropertyValue(x, colIndex)).ToList();
                else
					sorted = _internalItems.OrderBy(x => ReflectionUtils.GetPropertyValue(x, colIndex)).ToList();
            } else { // SortingProperty is not defined, so go the default route, using PropertyName
			    if (sortOrder == SortingOrder.Descendant)
					sorted = _internalItems.OrderByDescending(x => ReflectionUtils.GetPropertyValue(x, colIndex)).ToList();
			    else
					sorted = _internalItems.OrderBy(x => ReflectionUtils.GetPropertyValue(x, colIndex)).ToList();
            }

			_internalItems = sorted.ToList();

			//Support DescendingIcon property (if setted)
			if (column.SortingIcon.Style.Setters.All(x => x.Property != Image.SourceProperty))
			{
				if (SortIconProperty.DefaultValue != SortIcon)
					column.SortingIcon.Source = SortIcon;
			}

            for (int i = 0; i < Columns.Count; i++)
            {
                if (i != colIndex)
                {
                    if (Columns[i].SortingIcon.Source != null)
                        Columns[i].SortingIcon.IsVisible = false;
                    _sortingOrders[i] = SortingOrder.None;
                }
            }

			ToggleSortIcon(column, sortOrder);

			_sortingOrders[colIndex] = sortOrder;
			SortedColumnIndex = sData;

			_collectionView.ItemsSource = _internalItems;
		}

        private void ToggleSortIcon(DataGridColumn column, SortingOrder sortOrder)
        {
			column.SortingIcon.Style = SortIconStyle ?? DefaultSortIconStyle;
			column.SortingIcon.RotateXTo(sortOrder == SortingOrder.Descendant ? 180 : 0);
			column.SortingIcon.IsVisible = true;
		}
        #endregion

        #region Methods
        public void ScrollTo(object item, ScrollToPosition position, bool animated = true)
        {
			_collectionView.ScrollTo(item, position:position, animate:animated);
        }

		private void CollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (SelectionEnabled)
				SelectedItem = _collectionView.SelectedItem;
			else
				_collectionView.SelectedItem = null;

			ItemSelected?.Invoke(this, e);
		}

		private void RefreshViewRefreshing(object sender, EventArgs e)
		{
            if (PullToRefreshCommand == null && Refreshing == null)
            {
				((RefreshView)sender).IsRefreshing = false;
            }

            Refreshing?.Invoke(this, e);
        }

		public void Dispose()
		{
			IsEnabled = false;
            IsVisible = false;
			_collectionView.ItemsSource = null;
            _collectionView.SelectionChanged -= CollectionViewSelectionChanged;
            _refreshView.Refreshing -= RefreshViewRefreshing;
            DisposeGestures();
            Children.Clear();
            _internalItems.Clear();
            _internalItems = null;
        }
        #endregion
    }
}
