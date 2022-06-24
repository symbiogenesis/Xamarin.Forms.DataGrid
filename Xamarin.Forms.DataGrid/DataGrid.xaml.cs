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
	public partial class DataGrid : Grid
	{
		#region Events
		public event EventHandler Refreshing;
		public event EventHandler<SelectedItemChangedEventArgs> ItemSelected;
		#endregion

		#region Fields
		private static readonly ColumnDefinition _starColumn = new() { Width = DataGridColumn.StarLength };
		private static readonly ColumnDefinition _autoColumn = new() { Width = DataGridColumn.AutoLength };
		private static readonly Lazy<ImageSource> _defaultSortIcon = new(() => ImageSource.FromResource("Xamarin.Forms.DataGrid.up.png", typeof(DataGrid).GetTypeInfo().Assembly));
        private static Lazy<Style> _defaultSortIconStyle;
        private readonly Dictionary<int, SortingOrder> _sortingOrders;
		private readonly ListView _listView;
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
			BindableProperty.Create(nameof(HeaderBackground), typeof(Color), typeof(DataGrid), Color.White,
				propertyChanged: (b, o, n) => {
					var self = (DataGrid)b;
					if (self._headerView != null && !self.HeaderBordersVisible)
						self._headerView.BackgroundColor = (Color)n;
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

					if (self.NoDataView != null)
					{
						if (self.ItemsSource == null || self.InternalItems.Count == 0)
							self._noDataView.IsVisible = true;
						else if (self._noDataView.IsVisible)
							self._noDataView.IsVisible = false;
					}
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
			BindableProperty.Create(nameof(RowHeight), typeof(int), typeof(DataGrid), 40,
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    self._listView.RowHeight = (int)n;
				});

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
					else
						return null;
				},
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    if (self._listView.SelectedItem != n)
						self._listView.SelectedItem = n;
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
						self._listView.IsPullToRefreshEnabled = false;
						self._listView.RefreshCommand = null;
					}
					else
					{
						self._listView.IsPullToRefreshEnabled = true;
						self._listView.RefreshCommand = (ICommand)n;
					}
				});

		public static readonly BindableProperty IsRefreshingProperty =
			BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(DataGrid), false, BindingMode.TwoWay,
				propertyChanged: (b, o, n) => ((DataGrid)b)._listView.IsRefreshing = (bool)n);

		public static readonly BindableProperty BorderThicknessProperty =
			BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(DataGrid), new Thickness(1),
				propertyChanged: (b, o, n) => {
                    var self = (DataGrid)b;
                    self._headerView.ColumnSpacing = ((Thickness)n).HorizontalThickness / 2;
                    self._headerView.Padding = ((Thickness)n).HorizontalThickness / 2;
				});

		public static readonly BindableProperty HeaderBordersVisibleProperty =
			BindableProperty.Create(nameof(HeaderBordersVisible), typeof(bool), typeof(DataGrid), true,
				propertyChanged: (b, o, n) =>
				{
                    var self = (DataGrid)b;
                    self._headerView.BackgroundColor = (bool)n ? self.BorderColor : self.HeaderBackground;
				});

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
                    if (o != n)
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
                    var style = ((Style)n).Setters.FirstOrDefault(x => x.Property == Image.SourceProperty);
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
						((DataGrid)b)._noDataView.Content = (View)n;
				});
		#endregion

		#region Properties
		public Color ActiveRowColor
		{
			get { return (Color)GetValue(ActiveRowColorProperty); }
			set { SetValue(ActiveRowColorProperty, value); }
		}

		public Color HeaderBackground
		{
			get { return (Color)GetValue(HeaderBackgroundProperty); }
			set { SetValue(HeaderBackgroundProperty, value); }
		}

		public LineBreakMode LineBreakMode
		{
			get => (LineBreakMode)GetValue(LineBreakModeProperty);
			set => SetValue(LineBreakModeProperty, value);
		}

		public Color BorderColor
		{
			get { return (Color)GetValue(BorderColorProperty); }
			set { SetValue(BorderColorProperty, value); }
		}

		public IColorProvider RowsBackgroundColorPalette
		{
			get { return (IColorProvider)GetValue(RowsBackgroundColorPaletteProperty); }
			set { SetValue(RowsBackgroundColorPaletteProperty, value); }
		}

		public IColorProvider RowsTextColorPalette
		{
			get { return (IColorProvider)GetValue(RowsTextColorPaletteProperty); }
			set { SetValue(RowsTextColorPaletteProperty, value); }
		}

		public IEnumerable ItemsSource
		{
			get { return (IEnumerable)GetValue(ItemsSourceProperty); }
			set { SetValue(ItemsSourceProperty, value); }
		}

		IList<object> _internalItems;

		internal IList<object> InternalItems
		{
			get { return _internalItems; }
			set
			{
				_internalItems = value;

				SortIfNeeded();
			}
		}

        public ColumnCollection Columns
		{
			get { return (ColumnCollection)GetValue(ColumnsProperty); }
			set { SetValue(ColumnsProperty, value); }
		}

		public double FontSize
		{
			get { return (double)GetValue(FontSizeProperty); }
			set { SetValue(FontSizeProperty, value); }
		}

		public string FontFamily
		{
			get { return (string)GetValue(FontFamilyProperty); }
			set { SetValue(FontFamilyProperty, value); }
		}

		public int RowHeight
		{
			get { return (int)GetValue(RowHeightProperty); }
			set { SetValue(RowHeightProperty, value); }
		}

		public int HeaderHeight
		{
			get { return (int)GetValue(HeaderHeightProperty); }
			set { SetValue(HeaderHeightProperty, value); }
		}

		public bool IsSortable
		{
			get { return (bool)GetValue(IsSortableProperty); }
			set { SetValue(IsSortableProperty, value); }
		}

		public bool SelectionEnabled
		{
			get { return (bool)GetValue(SelectionEnabledProperty); }
			set { SetValue(SelectionEnabledProperty, value); }
		}

		public object SelectedItem
		{
			get { return GetValue(SelectedItemProperty); }
			set { SetValue(SelectedItemProperty, value); }
		}

		public ICommand PullToRefreshCommand
		{
			get { return (ICommand)GetValue(PullToRefreshCommandProperty); }
			set { SetValue(PullToRefreshCommandProperty, value); }
		}

		public bool IsRefreshing
		{
			get { return (bool)GetValue(IsRefreshingProperty); }
			set { SetValue(IsRefreshingProperty, value); }
		}

		public Thickness BorderThickness
		{
			get { return (Thickness)GetValue(BorderThicknessProperty); }
			set { SetValue(BorderThicknessProperty, value); }
		}

		public bool HeaderBordersVisible
		{
			get { return (bool)GetValue(HeaderBordersVisibleProperty); }
			set { SetValue(HeaderBordersVisibleProperty, value); }
		}

		public SortData SortedColumnIndex
		{
			get { return (SortData)GetValue(SortedColumnIndexProperty); }
			set { SetValue(SortedColumnIndexProperty, value); }
		}

		public Style HeaderLabelStyle
		{
			get { return (Style)GetValue(HeaderLabelStyleProperty); }
			set { SetValue(HeaderLabelStyleProperty, value); }
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
			get { return (View)GetValue(NoDataViewProperty); }
			set { SetValue(NoDataViewProperty, value); }
		}
		#endregion

		#region ctor

		public DataGrid() : this(ListViewCachingStrategy.RecycleElement)
		{
		}

		public DataGrid(ListViewCachingStrategy cachingStrategy)
		{
			InitializeComponent();

			_defaultSortIconStyle = new(() => (Style)_headerView.Resources["SortIconStyle"]);

			_sortingOrders = new Dictionary<int, SortingOrder>();

			_listView = new ListView(cachingStrategy)
			{
				BackgroundColor = Color.Transparent,
				ItemTemplate = new DataGridRowTemplateSelector(),
				SeparatorVisibility = SeparatorVisibility.None,
			};

			_listView.ItemSelected += ListViewItemSelected;

			_listView.Refreshing += ListViewRefreshing;

			_listView.SetBinding(ListView.RowHeightProperty, new Binding("RowHeight", source: this));
			Grid.SetRow(_listView, 1);
			Children.Add(_listView);
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
				try
                {
					_listView.ItemSelected -= ListViewItemSelected;
					_listView.Refreshing -= ListViewRefreshing;

					foreach (Grid grid in _headerView.Children)
					{
						foreach (TapGestureRecognizer tgr in grid.GestureRecognizers)
						{
							tgr.Tapped -= SortTapped;
						}
					}
				}
				catch { }
            }
        }

		protected override void OnBindingContextChanged()
		{
			base.OnBindingContextChanged();
			SetColumnsBindingContext();
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
			column.HeaderLabel.Style = column.HeaderLabelStyle ?? this.HeaderLabelStyle ?? (Style)_headerView.Resources["HeaderDefaultStyle"];

			Grid grid = new() {
				ColumnSpacing = 0,
			};

			grid.ColumnDefinitions.Add(_starColumn);
			grid.ColumnDefinitions.Add(_autoColumn);

			if (IsSortable)
			{
				column.SortingIcon.Style = (Style)_headerView.Resources["ImageStyleBase"];

				grid.Children.Add(column.SortingIcon);
				Grid.SetColumn(column.SortingIcon, 1);

				TapGestureRecognizer tgr = new();
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
				_listView.ItemsSource = _internalItems;
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
			var column = Columns[colIndex];
			var sortOrder = sData.Order;

			if (!IsSortable)
				throw new InvalidOperationException("This DataGrid is not sortable");
			else if (column.PropertyName == null && column.SortingPropertyName == null)
				throw new InvalidOperationException("Please set the PropertyName or SortingPropertyName property of Column");

			IEnumerable<object> sorted;

			//Sort
			if (sortOrder == SortingOrder.Descendant)
            {
                sorted = _internalItems.OrderByDescending(x => ReflectionUtils.GetPropertyValue(x, colIndex));
			}
			else
            {
                sorted = _internalItems.OrderBy(x => ReflectionUtils.GetPropertyValue(x, colIndex));
			}

			_internalItems = sorted.ToList();

			//Support DescendingIcon property (if set)
			if (!column.SortingIcon.Style.Setters.Any(x => x.Property == Image.SourceProperty))
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

			_listView.ItemsSource = _internalItems;
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
			_listView.ScrollTo(item, position, animated);
        }

		private void ListViewItemSelected(object sender, SelectedItemChangedEventArgs e)
		{
			if (SelectionEnabled)
			{
				SelectedItem = _listView.SelectedItem;
				ItemSelected?.Invoke(this, e);
			}
			else
			{
				_listView.SelectedItem = null;
			}
		}

		private void ListViewRefreshing(object sender, EventArgs e)
		{
			Refreshing?.Invoke(this, e);
		}
		#endregion
	}
}
