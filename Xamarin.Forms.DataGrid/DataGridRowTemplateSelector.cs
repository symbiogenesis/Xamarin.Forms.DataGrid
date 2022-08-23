namespace Xamarin.Forms.DataGrid
{
	public class DataGridRowTemplateSelector : DataTemplateSelector
	{
		protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
		{
			return SetValues(item, container);
		}

		protected DataTemplate SetValues(object item, BindableObject container)
		{
			var dataGridRowTemplate = new DataTemplate(typeof(DataGridViewCell));

            var collectionView = (CollectionView)container;
			var dataGrid = (DataGrid)collectionView.Parent.Parent;
			var items = dataGrid.InternalItems;

			dataGridRowTemplate.SetValue(DataGridViewCell.DataGridProperty, dataGrid);
			dataGridRowTemplate.SetValue(DataGridViewCell.RowContextProperty, item);

			if (items != null)
				dataGridRowTemplate.SetValue(DataGridViewCell.IndexProperty, items.IndexOf(item));

			return dataGridRowTemplate;
		}
	}
}