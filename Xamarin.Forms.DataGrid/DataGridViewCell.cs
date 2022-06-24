using System.Linq;

namespace Xamarin.Forms.DataGrid
{
	internal sealed class DataGridViewCell : Grid
	{
		#region Fields

		private bool _hasSelected;

		#endregion

		#region Properties
		public DataGrid DataGrid
		{
			get => (DataGrid) GetValue(DataGridProperty);
			set => SetValue(DataGridProperty, value);
		}

		public int Index
		{
			get => (int) GetValue(IndexProperty);
			set => SetValue(IndexProperty, value);
		}

		public object RowContext
		{
			get => GetValue(RowContextProperty);
			set => SetValue(RowContextProperty, value);
		}

		#endregion

		#region Bindable Properties

		public static readonly BindableProperty DataGridProperty =
			BindableProperty.Create(nameof(DataGrid), typeof(DataGrid), typeof(DataGridViewCell), null,
				propertyChanged: (b, o, n) => ((DataGridViewCell) b).CreateView());

		public static readonly BindableProperty IndexProperty =
			BindableProperty.Create(nameof(Index), typeof(int), typeof(DataGridViewCell), 0,
				propertyChanged: (b, o, n) => ((DataGridViewCell) b).UpdateBackgroundColor());

		public static readonly BindableProperty RowContextProperty =
			BindableProperty.Create(nameof(RowContext), typeof(object), typeof(DataGridViewCell),
				propertyChanged: (b, o, n) => ((DataGridViewCell) b).UpdateBackgroundColor());

		#endregion

		#region Methods

		private void CreateView()
		{
			UpdateBackgroundColor();
			BackgroundColor = DataGrid.BorderColor;
			ColumnSpacing = DataGrid.BorderThickness.HorizontalThickness / 2;
			Padding = new Thickness(DataGrid.BorderThickness.HorizontalThickness / 2,
				DataGrid.BorderThickness.VerticalThickness / 2);

			for (int i = 0; i < DataGrid.Columns.Count; i++)
			{
				DataGridColumn col = DataGrid.Columns[i];
				ColumnDefinitions.Add(new ColumnDefinition {Width = col.Width});
				View cell;

				if (col.CellTemplate != null)
				{
					cell = new ContentView {Content = col.CellTemplate.CreateContent() as View};
					if (col.PropertyName != null)
                    {
                        cell.SetBinding(BindingContextProperty, new Binding(col.PropertyName, source: RowContext));
                    }
                }
				else
				{
                    cell = new Label
					{
						HorizontalTextAlignment = ConvertLayoutAlignmentToTextAlignment(col.HorizontalContentAlignment.Alignment),
						VerticalTextAlignment = ConvertLayoutAlignmentToTextAlignment(col.VerticalContentAlignment.Alignment),
						LineBreakMode = LineBreakMode.TailTruncation
					};
					cell.SetBinding(Label.TextProperty, new Binding(col.PropertyName, BindingMode.Default, stringFormat: col.StringFormat));
					cell.SetBinding(Label.FontSizeProperty, new Binding(DataGrid.FontSizeProperty.PropertyName, BindingMode.Default, source: DataGrid));
					cell.SetBinding(Label.FontFamilyProperty, new Binding(DataGrid.FontFamilyProperty.PropertyName, BindingMode.Default, source: DataGrid));
				}

				Children.Add(cell);
				SetColumn(cell, i);
			}
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
			var actualIndex = DataGrid?.InternalItems?.IndexOf(BindingContext) ?? -1;
			if (actualIndex > -1)
			{
				var bgColor =
					DataGrid.SelectionEnabled && DataGrid.SelectedItem != null && DataGrid.SelectedItem == RowContext
						? DataGrid.ActiveRowColor
						: DataGrid.RowsBackgroundColorPalette.GetColor(actualIndex, BindingContext);
				var textColor = DataGrid.RowsTextColorPalette.GetColor(actualIndex, BindingContext);

				ChangeColor(bgColor, textColor);
			}
		}

		private void ChangeColor(Color bgColor, Color textColor)
		{
			foreach (var v in Children)
			{
				v.BackgroundColor = bgColor;
				var contentView = v as ContentView;
				if (v is Label label)
				{
					label.TextColor = textColor;
				}
				else if (contentView?.Content is Label cvLabel)
				{
					cvLabel.TextColor = textColor;
				}
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

		private void DataGrid_ItemSelected(object sender, SelectionChangedEventArgs e)
		{
			if (DataGrid.SelectionEnabled && (e.CurrentSelection.FirstOrDefault() == RowContext || _hasSelected))
				UpdateBackgroundColor();
		}

		#endregion
	}
}