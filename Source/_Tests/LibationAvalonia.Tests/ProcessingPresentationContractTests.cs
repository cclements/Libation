using ApplicationServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DataLayer;
using FileLiberator;
using LibationAvalonia.DesignSystem;
using LibationAvalonia.DesignSystem.Components;
using LibationAvalonia.Features.Processing;
using LibationAvalonia.Shell;
using LibationAvalonia.Views;
using LibationFileManager;
using LibationUiBase.ProcessQueue;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LibationAvalonia.Tests;

[TestClass]
[DoNotParallelize]
public class ProcessingPresentationContractTests
{
	[TestMethod]
	public async Task Shell_ReusesOneProcessingOwnerAcrossRoutesProfilesAndDecanterReparenting()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Overview));
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		ProcessingViewModel? processing = null;
		ProcessQueueViewModel? queueOwner = null;
		DecanterSummary? decanter = null;
		ProcessingView? processingView = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				processing = viewModel.Processing;
				queueOwner = viewModel.Main.ProcessQueue;
				decanter = shell.FindControl<DecanterSummary>("SharedDecanterSurface");
				processingView = shell.GetVisualDescendants().OfType<ProcessingView>().Single();
				Assert.IsNotNull(decanter);

				viewModel.UpdateLayout(new Size(1456, 1060));
				Assert.AreSame(queueOwner, processing.Source);
				Assert.AreSame(processing, processingView.DataContext);
				Assert.AreEqual("QueueDock", (decanter.Parent as Control)?.Name);
				Assert.AreEqual(1, shell.GetLogicalDescendants().OfType<DecanterSummary>().Count());
			});

			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(decanter);
				viewModel.Navigation.Navigate(AppRouteId.Processing);
				Assert.AreSame(processing, viewModel.Processing);
				Assert.AreSame(queueOwner, viewModel.Processing.Source);
				Assert.AreSame(processingView, shell?.GetVisualDescendants().OfType<ProcessingView>().Single());
				Assert.AreSame(decanter, shell?.FindControl<DecanterSummary>("SharedDecanterSurface"));
				Assert.AreEqual("DecanterParkingHost", (decanter.Parent as Control)?.Name);
			});

			await HeadlessTestHost.Dispatch(() =>
				HeadlessTestHost.Configuration.ExperienceStyle = ExperienceStyle.TastingRoom);
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(shell);
				Assert.IsNotNull(decanter);
				viewModel.Navigation.Navigate(AppRouteId.Overview);
				viewModel.UpdateLayout(new Size(1456, 1060));
				Assert.AreSame(processing, viewModel.Processing);
				Assert.AreSame(queueOwner, viewModel.Processing.Source);
				Assert.AreSame(processingView, shell.GetVisualDescendants().OfType<ProcessingView>().Single());
				Assert.AreSame(decanter, shell.FindControl<DecanterSummary>("SharedDecanterSurface"));
				Assert.AreEqual("DecanterSurfaceHost", (decanter.Parent as Control)?.Name);
				Assert.AreEqual(1, shell.GetLogicalDescendants().OfType<DecanterSummary>().Count());
			});
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	[TestMethod]
	public async Task Projection_GroupsTruthfulPartitionsCountsAndAggregateProgress()
	{
		await HeadlessTestHost.Reset();
		ProcessQueueViewModel? source = null;
		ProcessingViewModel? processing = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				source = new ProcessQueueViewModel();
				var completed = CreateQueueItem("S5COMPLETE01", "Completed title");
				var failed = CreateQueueItem("S5FAILED0001", "Failed title");
				var cancelled = CreateQueueItem("S5CANCELLED1", "Cancelled title");
				var active = CreateQueueItem("S5ACTIVE0001", "Active title");
				var waiting = CreateQueueItem("S5WAITING001", "Waiting title");
				source.Queue.Enqueue([completed, failed, cancelled, active, waiting]);

				DequeueExpected(source, completed);
				completed.SetPresentation(ProcessBookStatus.Completed, ProcessBookResult.Success, ProcessBookPresentationStage.Completed, 100);
				source.Queue.MarkCompleted(completed);
				DequeueExpected(source, failed);
				failed.SetPresentation(ProcessBookStatus.Failed, ProcessBookResult.FailedRetry, ProcessBookPresentationStage.None, 0);
				source.Queue.MarkCompleted(failed);
				DequeueExpected(source, cancelled);
				cancelled.SetPresentation(ProcessBookStatus.Cancelled, ProcessBookResult.Cancelled, ProcessBookPresentationStage.None, 0);
				source.Queue.MarkCompleted(cancelled);
				DequeueExpected(source, active);
				active.SetPresentation(ProcessBookStatus.Working, ProcessBookResult.None, ProcessBookPresentationStage.Decrypting, 40);
				processing = new ProcessingViewModel(source);

				Assert.AreEqual(1, processing.ActiveCount);
				Assert.AreEqual(1, processing.WaitingCount);
				Assert.AreEqual(1, processing.CompletedCount);
				Assert.AreEqual(1, processing.FailedCount);
				Assert.AreEqual(1, processing.CancelledCount);
				Assert.AreEqual(3, processing.FinishedCount);
				Assert.AreEqual(2, processing.InQueueCount);
				Assert.AreEqual(5, processing.QueueItemCount);
				Assert.AreEqual(68d, processing.Progress, 0.001d);
				Assert.AreEqual("68%", processing.OverallProgressText);
				Assert.AreEqual("1 active · 1 waiting · 1 completed · 1 failed · 1 cancelled", processing.SummaryText);

				CollectionAssert.AreEqual(
					new[] { "Active", "Waiting", "Completed", "Failed", "Cancelled" },
					processing.QueueRows.Where(row => row.IsSectionHeader).Select(row => row.SectionTitle).ToArray());
				CollectionAssert.AreEqual(
					new ProcessBookViewModel[] { active, waiting, completed, failed, cancelled },
					processing.QueueRows.Where(row => row.IsQueueItem).Select(row => row.Item!.Source).ToArray());
				Assert.AreEqual(5, processing.QueueRows.Where(row => row.IsQueueItem).Select(row => row.Item!.Source).Distinct().Count());
			});
		}
		finally
		{
			await DisposeProjectionAsync(processing, source);
		}
	}

	[TestMethod]
	public async Task Commands_DelegateRetryReorderPerformanceAndLogWorkToExistingOwners()
	{
		await HeadlessTestHost.Reset();
		ProcessQueueViewModel? source = null;
		ProcessingViewModel? processing = null;
		ProcessBookViewModel? retried = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				source = new ProcessQueueViewModel();
				var failed = CreateQueueItem("S5RETRY00001", "Retry title", includeBookDownload: true);
				var first = CreateQueueItem("S5ORDER00001", "First waiting");
				var second = CreateQueueItem("S5ORDER00002", "Second waiting");
				source.Queue.Enqueue([failed]);
				DequeueExpected(source, failed);
				failed.SetPresentation(
					ProcessBookStatus.Failed,
					ProcessBookResult.FailedRetry,
					ProcessBookPresentationStage.None,
					0,
					ProcessBookPresentationStage.Decrypting);
				source.Queue.MarkCompleted(failed);
				source.Queue.Enqueue([first, second]);
				source.LogEntries.Add(new LogEntry(DateTime.Now, "S5 queue contract entry"));
				processing = new ProcessingViewModel(source, item =>
				{
					retried = item;
					return Task.FromResult(true);
				});

				var failedRow = processing.Failed.Single();
				var firstRow = processing.Waiting.Single(item => ReferenceEquals(item.Source, first));
				var secondRow = processing.Waiting.Single(item => ReferenceEquals(item.Source, second));
				Assert.IsNotNull(failedRow.RetryCommand);
				Assert.AreSame(processing.OpenLogCommand, failedRow.OpenLogCommand);
				Assert.IsNull(firstRow.MoveUpCommand);
				Assert.IsNotNull(secondRow.MoveUpCommand);
				Assert.AreSame(processing.CancelAllCommand, processing.RoutePrimaryCommand?.Command);
				Assert.IsTrue(processing.RouteSecondaryCommands.Any(command =>
					ReferenceEquals(command.Command, processing.ClearFinishedCommand)));
				Assert.IsTrue(processing.RouteSecondaryCommands.Any(command =>
					ReferenceEquals(command.Command, processing.PerformanceCommand)));

				secondRow.MoveUpCommand.Execute(null);
				CollectionAssert.AreEqual(
					new ProcessBookViewModel[] { second, first },
					source.Queue.GetAllItems().Where(item => item.Status == ProcessBookStatus.Queued).ToArray());

				Assert.IsFalse(processing.IsPerformanceExpanded);
				Execute(processing.PerformanceCommand);
				Assert.IsTrue(processing.IsPerformanceExpanded);
				Assert.IsFalse(processing.IsLogOpen);
				Execute(processing.OpenLogCommand);
				Assert.IsTrue(processing.IsLogOpen);
				Execute(processing.CloseLogCommand);
				Assert.IsFalse(processing.IsLogOpen);
				Assert.IsTrue(processing.HasLogEntries);
				Execute(processing.ClearLogCommand);
				Assert.AreEqual(0, source.LogEntries.Count);

				failedRow.RetryCommand.Execute(null);
				Assert.AreSame(failed, retried);
				Assert.AreEqual(1, source.Queue.Completed.Count);
				Execute(processing.ClearFinishedCommand);
				Assert.AreEqual(0, source.Queue.Completed.Count);
				Assert.AreEqual(2, source.Queue.GetAllItems().Count());
				Execute(processing.CancelAllCommand);
				Assert.AreEqual(0, source.Queue.GetAllItems().Count());
			});
		}
		finally
		{
			await DisposeProjectionAsync(processing, source);
		}
	}

	[TestMethod]
	public async Task TypedStages_AnnounceOnlyStepBoundariesWhileProgressRemainsNonLive()
	{
		await HeadlessTestHost.Reset();
		ProcessQueueViewModel? source = null;
		ProcessingViewModel? processing = null;

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				source = new ProcessQueueViewModel();
				var active = CreateQueueItem("S5STAGE00001", "Stage title", includeBookDownload: true);
				source.Queue.Enqueue([active]);
				DequeueExpected(source, active);
				AssertProcessableBoundary(active, DownloadPdf.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Downloading);
				AssertProcessableBoundary(active, DownloadDecryptBook.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Decrypting);
				AssertProcessableBoundary(active, ConvertToMp3.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Converting);
				AssertProcessableBoundary(active, DownloadPdf.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Downloading);
				processing = new ProcessingViewModel(source);
				var row = processing.Active.Single();
				var rowChanges = new List<string?>();
				var ownerChanges = new List<string?>();
				row.PropertyChanged += (_, e) => rowChanges.Add(e.PropertyName);
				processing.PropertyChanged += (_, e) => ownerChanges.Add(e.PropertyName);

				active.SetProgress(37);
				Assert.AreEqual(37d, row.Progress);
				Assert.AreEqual("37%", row.ProgressText);
				Assert.AreEqual("Stage title progress 37 percent", row.ProgressAccessibleName);
				CollectionAssert.Contains(rowChanges, nameof(ProcessingQueueItemViewModel.Progress));
				CollectionAssert.Contains(rowChanges, nameof(ProcessingQueueItemViewModel.ProgressAccessibleName));
				CollectionAssert.DoesNotContain(rowChanges, nameof(ProcessingQueueItemViewModel.StageAnnouncement));
				CollectionAssert.Contains(ownerChanges, nameof(ProcessingViewModel.CurrentProgress));
				CollectionAssert.DoesNotContain(ownerChanges, nameof(ProcessingViewModel.CurrentStage));
				CollectionAssert.DoesNotContain(ownerChanges, nameof(ProcessingViewModel.CurrentStageAnnouncement));

				rowChanges.Clear();
				ownerChanges.Clear();
				AssertProcessableBoundary(active, DownloadDecryptBook.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Decrypting);
				Assert.AreEqual("Decrypting", row.Stage);
				Assert.AreEqual("Stage title: Decrypting", row.StageAnnouncement);
				CollectionAssert.Contains(rowChanges, nameof(ProcessingQueueItemViewModel.Stage));
				CollectionAssert.Contains(rowChanges, nameof(ProcessingQueueItemViewModel.StageAnnouncement));
				CollectionAssert.Contains(rowChanges, nameof(ProcessingQueueItemViewModel.RowAccessibleName));
				CollectionAssert.Contains(ownerChanges, nameof(ProcessingViewModel.CurrentStage));

				AssertProcessableBoundary(active, ConvertToMp3.Create(HeadlessTestHost.Configuration), ProcessBookPresentationStage.Converting);
				Assert.AreEqual("Converting", row.Stage);
				active.SetStage(ProcessBookPresentationStage.Completed);
				Assert.AreEqual("Completed", row.Stage);

				var queueItem = new QueueItem
				{
					StageAccessibleName = "Stage boundary live region",
					ProgressAccessibleName = "Progress value without live region",
				};
				var automationHost = new Window { Content = queueItem };
				automationHost.Show();
				try
				{
					var stageText = queueItem.GetVisualDescendants().OfType<TextBlock>()
						.Single(control => AutomationProperties.GetName(control) == queueItem.StageAccessibleName);
					var progressBar = queueItem.GetVisualDescendants().OfType<ProgressBar>()
						.Single(control => AutomationProperties.GetName(control) == queueItem.ProgressAccessibleName);
					Assert.IsTrue(stageText.IsSet(AutomationProperties.LiveSettingProperty));
					Assert.AreEqual(PlatformAutomationPolicy.Polite, AutomationProperties.GetLiveSetting(stageText));
					Assert.IsFalse(progressBar.IsSet(AutomationProperties.LiveSettingProperty));
					Assert.AreEqual(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(progressBar));
				}
				finally
				{
					automationHost.Close();
				}
			});
		}
		finally
		{
			await DisposeProjectionAsync(processing, source);
		}
	}

	[TestMethod]
	public async Task DecanterVariants_ShareOneOwnerBoundCellarRowsAndConditionalCancel()
	{
		await HeadlessTestHost.Reset(ExperienceStyle.Cellar);
		await HeadlessTestHost.Dispatch(() =>
			HeadlessTestHost.Configuration.ContemporaryLastRoute = nameof(AppRouteId.Overview));
		MainWindow? window = null;
		AppShellView? shell = null;
		AppShellViewModel? viewModel = null;
		DecanterSummary? decanter = null;
		Button? cancelButton = null;
		TestProcessBookViewModel[] items = [];

		try
		{
			await HeadlessTestHost.Dispatch(() =>
			{
				window = new MainWindow(HeadlessTestHost.ExperienceManager, null);
				window.Show();
				shell = window.Content as AppShellView;
				Assert.IsNotNull(shell);
				viewModel = shell.DataContext as AppShellViewModel;
				Assert.IsNotNull(viewModel);
				decanter = shell.FindControl<DecanterSummary>("SharedDecanterSurface");
				Assert.IsNotNull(decanter);
				viewModel.UpdateLayout(new Size(1456, 1060));

				items = Enumerable.Range(1, 4)
					.Select(index => CreateQueueItem($"S5ACTIVE{index:0000}", $"Active {index}", includeBookDownload: true))
					.ToArray();
				viewModel.Processing.Source.Queue.Enqueue(items);
				foreach (var item in items)
				{
					DequeueExpected(viewModel.Processing.Source, item);
					item.SetPresentation(ProcessBookStatus.Working, ProcessBookResult.None, ProcessBookPresentationStage.Decrypting, 20 + item.Progress);
				}
			});

			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(decanter);
				RefreshMembershipNow(viewModel.Processing);
				Assert.IsTrue(decanter.IsCellar);
				Assert.IsFalse(decanter.IsTastingRoom);
				Assert.AreEqual("QueueDock", (decanter.Parent as Control)?.Name);
				Assert.AreSame(viewModel.Main.ProcessQueue, viewModel.Processing.Source);
				Assert.AreEqual(4, viewModel.Processing.ActiveCount);
				Assert.AreSame(viewModel.Processing.DecanterActiveItems, decanter.ActiveItems);
				Assert.AreEqual(3, viewModel.Processing.DecanterActiveItems.Count);
				CollectionAssert.AreEqual(
					items.Take(3).Cast<ProcessBookViewModel>().ToArray(),
					viewModel.Processing.DecanterActiveItems.Select(item => item.Source).ToArray());
				Assert.AreEqual(3, decanter.GetVisualDescendants().OfType<ItemsControl>().Single().ItemCount);
				Assert.IsTrue(decanter.CanCancel);
				Assert.AreSame(viewModel.Processing.CurrentCancelCommand, decanter.CancelCommand);
				cancelButton = decanter.GetVisualDescendants().OfType<Button>()
					.Single(button => string.Equals(button.Content?.ToString(), "Cancel", StringComparison.Ordinal));
				Assert.IsFalse(cancelButton.IsEffectivelyVisible);
			});

			await HeadlessTestHost.Dispatch(() =>
				HeadlessTestHost.Configuration.ExperienceStyle = ExperienceStyle.TastingRoom);
			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(shell);
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(decanter);
				Assert.IsNotNull(cancelButton);
				viewModel.UpdateLayout(new Size(1456, 1060));
				Assert.AreSame(decanter, shell.FindControl<DecanterSummary>("SharedDecanterSurface"));
				Assert.IsFalse(decanter.IsCellar);
				Assert.IsTrue(decanter.IsTastingRoom);
				Assert.AreEqual("DecanterSurfaceHost", (decanter.Parent as Control)?.Name);
				Assert.AreSame(viewModel.Processing.DecanterActiveItems, decanter.ActiveItems);
				Assert.AreEqual(3, viewModel.Processing.DecanterActiveItems.Count);
				Assert.IsTrue(decanter.CanCancel);
				Assert.IsNotNull(decanter.CancelCommand);
				Assert.AreEqual(items[0].Title, decanter.CurrentTitle);
				Assert.AreEqual("Decrypting", decanter.CurrentStageText);
				Assert.AreEqual(20d, decanter.Progress);
				Assert.IsTrue(cancelButton.IsEffectivelyVisible);

				foreach (var item in items)
				{
					item.SetPresentation(ProcessBookStatus.Completed, ProcessBookResult.Success, ProcessBookPresentationStage.Completed, 100);
					viewModel.Processing.Source.Queue.MarkCompleted(item);
				}
			});

			await HeadlessTestHost.Dispatch(() =>
			{
				Assert.IsNotNull(viewModel);
				Assert.IsNotNull(decanter);
				Assert.IsNotNull(cancelButton);
				RefreshMembershipNow(viewModel.Processing);
				Assert.AreEqual(0, viewModel.Processing.ActiveCount);
				Assert.IsNull(viewModel.Processing.CurrentCancelCommand);
				Assert.IsFalse(decanter.CanCancel);
				Assert.IsNull(decanter.CancelCommand);
				Assert.IsFalse(cancelButton.IsEffectivelyVisible);
			});
		}
		finally
		{
			if (window is not null)
				await HeadlessTestHost.Dispatch(window.Close);
		}
	}

	private static TestProcessBookViewModel CreateQueueItem(
		string productId,
		string title,
		bool includeBookDownload = false)
	{
		var book = new Book(
			new AudibleProductId(productId),
			title,
			string.Empty,
			"S5 processing presentation contract fixture",
			120,
			ContentType.Product,
			[new Contributor("Author", "AUTHOR0001")],
			[new Contributor("Narrator", "NARRATOR01")],
			"us");
		var item = new TestProcessBookViewModel(
			new LibraryBook(book, DateTime.Today, "test-account"),
			HeadlessTestHost.Configuration);
		return includeBookDownload ? (TestProcessBookViewModel)item.AddDownloadDecryptBook() : item;
	}

	private static void DequeueExpected(ProcessQueueViewModel source, ProcessBookViewModel expected)
	{
		Assert.IsTrue(source.Queue.TryDequeueNext(out var actual));
		Assert.AreSame(expected, actual);
	}

	private static void Execute(ICommand command)
	{
		Assert.IsTrue(command.CanExecute(null));
		command.Execute(null);
	}

	private static void RefreshMembershipNow(ProcessingViewModel processing)
	{
		var refresh = typeof(ProcessingViewModel).GetMethod(
			"RefreshMembership",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(refresh);
		refresh.Invoke(processing, null);
	}

	private static void AssertProcessableBoundary(
		TestProcessBookViewModel item,
		object processable,
		ProcessBookPresentationStage expectedStage)
	{
		item.SetProgressAndTimeRemaining(73, TimeSpan.FromMinutes(2));
		var begin = typeof(ProcessBookViewModel).GetMethod(
			"Processable_Begin",
			BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.IsNotNull(begin);
		begin.Invoke(item, [processable, item.LibraryBook]);
		Assert.AreEqual(expectedStage, item.PresentationStage);
		Assert.AreEqual(expectedStage, item.LastPresentationStage);
		Assert.AreEqual(ProcessBookStatus.Working, item.Status);
		Assert.AreEqual(0, item.Progress);
		Assert.AreEqual(TimeSpan.Zero, item.TimeRemaining);
		Assert.IsNull(item.ETA);
	}

	private static async Task DisposeProjectionAsync(
		ProcessingViewModel? processing,
		ProcessQueueViewModel? source)
	{
		if (processing is null && source is null)
			return;
		await HeadlessTestHost.Dispatch(() =>
		{
			processing?.Dispose();
			if (source is null)
				return;
			foreach (var active in source.Queue.GetActive())
				source.Queue.RemoveActive(active);
			source.Queue.ClearQueue();
			source.Queue.ClearCompleted();
			source.LogEntries.Clear();
		});
	}

	private sealed class TestProcessBookViewModel(LibraryBook book, Configuration configuration)
		: ProcessBookViewModel(book, configuration)
	{
		public void SetPresentation(
			ProcessBookStatus status,
			ProcessBookResult result,
			ProcessBookPresentationStage stage,
			int progress,
			ProcessBookPresentationStage? lastStage = null)
		{
			Result = result;
			Status = status;
			PresentationStage = stage;
			LastPresentationStage = lastStage ?? stage;
			Progress = progress;
		}

		public void SetProgress(int progress) => Progress = progress;

		public void SetProgressAndTimeRemaining(int progress, TimeSpan timeRemaining)
		{
			Progress = progress;
			TimeRemaining = timeRemaining;
		}

		public void SetStage(ProcessBookPresentationStage stage)
		{
			PresentationStage = stage;
			LastPresentationStage = stage;
		}
	}
}
