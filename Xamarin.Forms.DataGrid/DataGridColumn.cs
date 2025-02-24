﻿using System;

namespace Xamarin.Forms.DataGrid
{
	public class DataGridColumn : BindableObject, IDefinition
	{
		public static readonly GridLength AutoLength = new(1, GridUnitType.Auto);
		public static readonly GridLength StarLength = new(1, GridUnitType.Star);

		#region Bindable Properties
		public static readonly BindableProperty WidthProperty =
			BindableProperty.Create(nameof(Width), typeof(GridLength), typeof(DataGridColumn), StarLength,
				propertyChanged: (b, o, n) => { if (o != n) ((DataGridColumn) b).OnSizeChanged(); });

		public static readonly BindableProperty TitleProperty =
			BindableProperty.Create(nameof(Title), typeof(string), typeof(DataGridColumn), string.Empty,
				propertyChanged: (b, o, n) => ((DataGridColumn) b).HeaderLabel.Text = (string)n);

		public static readonly BindableProperty FormattedTitleProperty =
			BindableProperty.Create(nameof(FormattedTitle), typeof(FormattedString), typeof(DataGridColumn),
				propertyChanged: (b, o, n) => ((DataGridColumn) b).HeaderLabel.FormattedText = (FormattedString)n);

		public static readonly BindableProperty PropertyNameProperty =
			BindableProperty.Create(nameof(PropertyName), typeof(string), typeof(DataGridColumn));

        public static readonly BindableProperty IsVisibleProperty =
			BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(DataGridColumn), true);

        public static readonly BindableProperty SortingPropertyNameProperty =
            BindableProperty.Create(nameof(SortingPropertyName), typeof(string), typeof(DataGridColumn));

        public static readonly BindableProperty StringFormatProperty =
			BindableProperty.Create(nameof(StringFormat), typeof(string), typeof(DataGridColumn));

		public static readonly BindableProperty CellTemplateProperty =
			BindableProperty.Create(nameof(CellTemplate), typeof(DataTemplate), typeof(DataGridColumn));

		public static readonly BindableProperty HorizontalContentAlignmentProperty =
			BindableProperty.Create(nameof(HorizontalContentAlignment), typeof(LayoutOptions), typeof(DataGridColumn), LayoutOptions.Center);

		public static readonly BindableProperty VerticalContentAlignmentProperty =
			BindableProperty.Create(nameof(VerticalContentAlignment), typeof(LayoutOptions), typeof(DataGridColumn), LayoutOptions.Center);

		public static readonly BindableProperty SortingEnabledProperty =
			BindableProperty.Create(nameof(SortingEnabled), typeof(bool), typeof(DataGridColumn), true);

		public static readonly BindableProperty HeaderLabelStyleProperty =
			BindableProperty.Create(nameof(HeaderLabelStyle), typeof(Style), typeof(DataGridColumn),
				propertyChanged: (b, o, n) => {
					var column = (DataGridColumn)b;
                    if (column.HeaderLabel != null && (o != n))
						column.HeaderLabel.Style = (Style)n;
				});

		#endregion

		#region Properties

		public GridLength Width
		{
			get => (GridLength) GetValue(WidthProperty);
			set => SetValue(WidthProperty, value);
		}

		public string Title
		{
			get => (string) GetValue(TitleProperty);
			set => SetValue(TitleProperty, value);
		}

		public FormattedString FormattedTitle
		{
			get => (string) GetValue(FormattedTitleProperty);
			set => SetValue(FormattedTitleProperty, value);
		}

		public string PropertyName
		{
			get => (string) GetValue(PropertyNameProperty);
			set => SetValue(PropertyNameProperty, value);
		}

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public string SortingPropertyName
        {
            get { return (string)GetValue(SortingPropertyNameProperty); }
            set { SetValue(SortingPropertyNameProperty, value); }
        }

        public string StringFormat
		{
			get => (string) GetValue(StringFormatProperty);
			set => SetValue(StringFormatProperty, value);
		}

		public DataTemplate CellTemplate
		{
			get => (DataTemplate) GetValue(CellTemplateProperty);
			set => SetValue(CellTemplateProperty, value);
		}

		internal Image SortingIcon { get; set; }
		internal Label HeaderLabel { get; set; }

		public LayoutOptions HorizontalContentAlignment
		{
			get => (LayoutOptions) GetValue(HorizontalContentAlignmentProperty);
			set => SetValue(HorizontalContentAlignmentProperty, value);
		}

		public LayoutOptions VerticalContentAlignment
		{
			get => (LayoutOptions) GetValue(VerticalContentAlignmentProperty);
			set => SetValue(VerticalContentAlignmentProperty, value);
		}

		public bool SortingEnabled
		{
			get => (bool) GetValue(SortingEnabledProperty);
			set => SetValue(SortingEnabledProperty, value);
		}

		public Style HeaderLabelStyle
		{
			get => (Style) GetValue(HeaderLabelStyleProperty);
			set => SetValue(HeaderLabelStyleProperty, value);
		}

		#endregion

		public event EventHandler SizeChanged;

		public DataGridColumn()
		{
			HeaderLabel = new Label();
			SortingIcon = new Image();
		}

		void OnSizeChanged()
		{
			SizeChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
