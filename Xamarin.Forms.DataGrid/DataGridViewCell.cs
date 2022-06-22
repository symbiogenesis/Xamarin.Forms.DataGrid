namespace Xamarin.Forms.DataGrid
{
	internal sealed class DataGridViewCell : ViewCell
	{
		#region Fields
		Grid _mainLayout;
		Color _bgColor;
		Color _textColor;
		bool _hasSelected;
		#endregion

		#region Properties
		public DataGrid DataGrid
		{
			get { return (DataGrid)GetValue(DataGridProperty); }
			set { SetValue(DataGridProperty, value); }
		}

		public int Index
		{
			get { return (int)GetValue(IndexProperty); }
			set { SetValue(IndexProperty, value); }
		}

		public object RowContext
		{
			get { return GetValue(RowContextProperty); }
			set { SetValue(RowContextProperty, value); }
		}
		#endregion

		#region Bindable Properties
		public static readonly BindableProperty DataGridProperty =
			BindableProperty.Create(nameof(DataGrid), typeof(DataGrid), typeof(DataGridViewCell), null,
				propertyChanged: (b, o, n) => ((DataGridViewCell)b).CreateView());

		public static readonly BindableProperty IndexProperty =
			BindableProperty.Create(nameof(Index), typeof(int), typeof(DataGridViewCell), 0,
				propertyChanged: (b, o, n) => ((DataGridViewCell)b).UpdateBackgroundColor());

		public static readonly BindableProperty RowContextProperty =
			BindableProperty.Create(nameof(RowContext), typeof(object), typeof(DataGridViewCell),
				propertyChanged: (b, o, n) => ((DataGridViewCell)b).UpdateBackgroundColor());
		#endregion

		#region Methods
		private void CreateView()
		{
			UpdateBackgroundColor();

			_mainLayout = new Grid()
			{
				BackgroundColor = DataGrid.BorderColor,
				RowSpacing = 0,
				ColumnSpacing = DataGrid.BorderThickness.HorizontalThickness / 2,
				Padding = new Thickness(DataGrid.BorderThickness.HorizontalThickness / 2,
										DataGrid.BorderThickness.VerticalThickness / 2),
			};

			foreach (var col in DataGrid.Columns)
			{
				_mainLayout.ColumnDefinitions.Add(new ColumnDefinition() { Width = col.Width });
				View cell;

				if (col.CellTemplate != null)
				{
					cell = new ContentView() { Content = (View)col.CellTemplate.CreateContent() };
					if (col.PropertyName != null)
					{
						cell.SetBinding(BindingContextProperty,
							new Binding(col.PropertyName, source: RowContext));
					}
				}
				else
				{
                    cell = new Label
					{
                        BackgroundColor = _bgColor,
                        TextColor = _textColor,
						HorizontalTextAlignment = ConvertLayoutAlignmentToTextAlignment(col.HorizontalContentAlignment.Alignment),
						VerticalTextAlignment = ConvertLayoutAlignmentToTextAlignment(col.VerticalContentAlignment.Alignment),
						LineBreakMode = LineBreakMode.TailTruncation
					};
                    cell.SetBinding(Label.TextProperty, new Binding(col.PropertyName, BindingMode.Default, stringFormat: col.StringFormat));
                    cell.SetBinding(Label.FontSizeProperty, new Binding(DataGrid.FontSizeProperty.PropertyName, BindingMode.Default, source: DataGrid));
                    cell.SetBinding(Label.FontFamilyProperty, new Binding(DataGrid.FontFamilyProperty.PropertyName, BindingMode.Default, source: DataGrid));
				}

				_mainLayout.Children.Add(cell);
				Grid.SetColumn(cell, DataGrid.Columns.IndexOf(col));
			}

			View = _mainLayout;
		}

		private static TextAlignment ConvertLayoutAlignmentToTextAlignment(LayoutAlignment layoutAlignment)
		{
            switch (layoutAlignment)
            {
                case LayoutAlignment.Start:
                    return TextAlignment.Start;
                case LayoutAlignment.End:
                    return TextAlignment.End;
                default:
                    return TextAlignment.Center;
            }
        }

		private void UpdateBackgroundColor()
		{
			_hasSelected = DataGrid.SelectedItem == RowContext;
			int actualIndex = DataGrid?.InternalItems?.IndexOf(BindingContext) ?? -1;
			if (actualIndex > -1)
			{
				_bgColor = (DataGrid.SelectionEnabled && DataGrid.SelectedItem != null && DataGrid.SelectedItem == RowContext) ?
					DataGrid.ActiveRowColor : DataGrid.RowsBackgroundColorPalette.GetColor(actualIndex, BindingContext);
				_textColor = DataGrid.RowsTextColorPalette.GetColor(actualIndex, BindingContext);

				ChangeColor(_bgColor);
			}
		}

		private void ChangeColor(Color color)
		{
			foreach (var v in _mainLayout.Children)
			{
				v.BackgroundColor = color;
                if (v is Label label)
                    label.TextColor = _textColor;
            }
		}

		protected override void OnBindingContextChanged()
		{
			base.OnBindingContextChanged();
			UpdateBackgroundColor();
		}

		protected override void OnParentSet()
		{
			base.OnParentSet();
			if (Parent != null)
				DataGrid.ItemSelected += DataGrid_ItemSelected;
			else
				DataGrid.ItemSelected -= DataGrid_ItemSelected;
		}

		private void DataGrid_ItemSelected(object sender, SelectedItemChangedEventArgs e)
		{
			if (DataGrid.SelectionEnabled && (e.SelectedItem == RowContext || _hasSelected))
			{
				UpdateBackgroundColor();
			}
		}
		#endregion
	}
}
