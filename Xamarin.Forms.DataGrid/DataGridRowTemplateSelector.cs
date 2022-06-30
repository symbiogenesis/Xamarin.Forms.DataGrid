namespace Xamarin.Forms.DataGrid
{
	internal class DataGridRowTemplateSelector : DataTemplateSelector
	{
		private static readonly DataTemplate _dataGridRowTemplate = new(typeof(DataGridViewCell));

		protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
		{
			var collectionView = (CollectionView) container;
			var dataGrid = (DataGrid) collectionView.Parent.Parent;
			var items = dataGrid.InternalItems;

			_dataGridRowTemplate.SetValue(DataGridViewCell.DataGridProperty, dataGrid);
			_dataGridRowTemplate.SetValue(DataGridViewCell.RowContextProperty, item);

			if (items != null)
				_dataGridRowTemplate.SetValue(DataGridViewCell.IndexProperty, items.IndexOf(item));

			return _dataGridRowTemplate;
		}
	}
}