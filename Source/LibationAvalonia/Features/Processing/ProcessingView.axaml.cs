using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LibationUiBase.ProcessQueue;
using System.Linq;

namespace LibationAvalonia.Features.Processing;

public partial class ProcessingView : UserControl
{
	private ProcessQueueViewModel? subscribedSource;
	private bool isAttached;

	public ProcessingView() => InitializeComponent();

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		base.OnAttachedToVisualTree(e);
		isAttached = true;
		AttachAutoScrollSource();
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		isAttached = false;
		DetachAutoScrollSource();
		base.OnDetachedFromVisualTree(e);
	}

	protected override void OnDataContextBeginUpdate()
	{
		DetachAutoScrollSource();
		base.OnDataContextBeginUpdate();
	}

	protected override void OnDataContextEndUpdate()
	{
		base.OnDataContextEndUpdate();
		if (isAttached)
			AttachAutoScrollSource();
	}

	private void AttachAutoScrollSource()
	{
		var source = (DataContext as ProcessingViewModel)?.Source;
		if (ReferenceEquals(source, subscribedSource))
			return;
		DetachAutoScrollSource();
		subscribedSource = source;
		if (subscribedSource is not null)
			subscribedSource.ProcessStart += Book_ProcessStart;
	}

	private void DetachAutoScrollSource()
	{
		if (subscribedSource is not null)
			subscribedSource.ProcessStart -= Book_ProcessStart;
		subscribedSource = null;
	}

	private void Book_ProcessStart(object? sender, ProcessBookViewModel item)
	{
		if (sender is not ProcessQueueViewModel source || !source.AutoScrollQueue)
			return;

		Dispatcher.UIThread.Post(() =>
		{
			if (DataContext is not ProcessingViewModel viewModel)
				return;

			var rowIndex = viewModel.QueueRows
				.Select((row, index) => (row, index))
				.FirstOrDefault(pair => ReferenceEquals(pair.row.Item?.Source, item)).index;
			if (rowIndex <= 0 || QueueListControl.Presenter?.Panel is not VirtualizingStackPanel panel)
				return;

			var previousItemIndex = rowIndex - 1;
			while (previousItemIndex >= 0 && viewModel.QueueRows[previousItemIndex].IsSectionHeader)
				previousItemIndex--;

			// Preserve the legacy non-interruption rule: follow the newly active item only
			// while the preceding queue item is already visible.
			if (previousItemIndex >= 0
				&& panel.FirstRealizedIndex <= previousItemIndex
				&& panel.LastRealizedIndex >= previousItemIndex)
				QueueListControl.ScrollIntoView(rowIndex);
		// Queue membership is coalesced at Background priority. Run one priority later so
		// this lookup sees the item in Active rather than its stale Waiting position.
		}, DispatcherPriority.ContextIdle);
	}

	internal bool TryScrollItemIntoView(ProcessBookViewModel item)
	{
		if (DataContext is not ProcessingViewModel viewModel)
			return false;

		for (var index = 0; index < viewModel.QueueRows.Count; index++)
		{
			if (!ReferenceEquals(viewModel.QueueRows[index].Item?.Source, item))
				continue;
			QueueListControl.ScrollIntoView(index);
			return true;
		}
		return false;
	}

	public void NumericUpDown_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key is Key.Enter && sender is IInputElement input)
			input.Focus();
	}
}
