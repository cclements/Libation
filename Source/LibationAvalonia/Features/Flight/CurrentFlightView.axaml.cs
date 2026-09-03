using Avalonia.Controls;
using Avalonia.Input;

namespace LibationAvalonia.Features.Flight;

public partial class CurrentFlightView : UserControl
{
	private static readonly DataFormat<string> FlightItemFormat = DataFormat.CreateStringApplicationFormat(global::LibationAvalonia.Properties.Resources.CurrentFlightViewaxamlLibationCurrentFlightItem);

	public CurrentFlightView() => InitializeComponent();

	private async void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (sender is not Control { DataContext: CurrentFlightItemViewModel item }
			|| !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			return;
		var transfer = new DataTransfer();
		transfer.Add(DataTransferItem.Create(FlightItemFormat, item.Source.Id.ProductId));
		await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
	}

	private void DragHandle_KeyDown(object? sender, KeyEventArgs e)
	{
		if (sender is not Control { DataContext: CurrentFlightItemViewModel item })
			return;
		System.Windows.Input.ICommand? command = e.Key switch
		{
			Key.Up => item.MoveUpCommand,
			Key.Down => item.MoveDownCommand,
			_ => null,
		};
		if (command?.CanExecute(null) != true)
			return;
		command.Execute(null);
		e.Handled = true;
	}

	private void FlightRow_DragOver(object? sender, DragEventArgs e)
	{
		bool canMove = e.DataTransfer.TryGetValue(FlightItemFormat) is not null
			&& sender is Control { DataContext: CurrentFlightItemViewModel };
		e.DragEffects = canMove ? DragDropEffects.Move : DragDropEffects.None;
		e.Handled = true;
	}

	private void FlightRow_Drop(object? sender, DragEventArgs e)
	{
		if (e.DataTransfer.TryGetValue(FlightItemFormat) is not { } productId
			|| sender is not Control { DataContext: CurrentFlightItemViewModel target }
			|| DataContext is not CurrentFlightViewModel viewModel)
			return;
		viewModel.MoveTo(new FlightItemId(productId), target.Source.Id);
		e.DragEffects = DragDropEffects.Move;
		e.Handled = true;
	}
}
