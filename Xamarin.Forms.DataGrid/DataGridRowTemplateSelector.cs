namespace Xamarin.Forms.DataGrid
{
	public class DataGridRowTemplateSelector : DataTemplateSelector
	{
		protected static readonly DataTemplate _dataGridRowTemplate = new(typeof(DataGridViewCell));

		protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
		{
			return SetValues(item, container);
		}

		protected static DataTemplate SetValues(object item, BindableObject container)
		{
			var collectionView = (CollectionView)container;
			var dataGrid = (DataGrid)collectionView.Parent.Parent;
			var items = dataGrid.InternalItems;

			_dataGridRowTemplate.SetValue(DataGridViewCell.DataGridProperty, dataGrid);
			_dataGridRowTemplate.SetValue(DataGridViewCell.RowContextProperty, item);

			if (items != null)
				_dataGridRowTemplate.SetValue(DataGridViewCell.IndexProperty, items.IndexOf(item));

			return _dataGridRowTemplate;
		}
	}
}