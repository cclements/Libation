using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.DesignSystem.Components;

public partial class ToastHost : UserControl
{
	public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<ToastHost, IEnumerable?>(nameof(ItemsSource));
	public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<ToastHost, IDataTemplate?>(nameof(ItemTemplate));

	private INotifyCollectionChanged? subscribedCollection;

	public ToastHost()
	{
		Presentations = [];
		InitializeComponent();
	}

	public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
	public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
	public ObservableCollection<ToastPresentation> Presentations { get; }

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property != ItemsSourceProperty)
			return;

		if (subscribedCollection is not null)
			subscribedCollection.CollectionChanged -= ItemsSource_CollectionChanged;
		subscribedCollection = ItemsSource as INotifyCollectionChanged;
		if (subscribedCollection is not null)
			subscribedCollection.CollectionChanged += ItemsSource_CollectionChanged;
		SynchronizePresentations();
	}

	private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		=> SynchronizePresentations();

	private void SynchronizePresentations()
	{
		var desired = ItemsSource?.Cast<object>().OfType<ToastMessage>().ToArray() ?? [];
		foreach (var presentation in Presentations.Where(item => !item.IsExiting && !desired.Any(message => ReferenceEquals(message, item.Message))).ToArray())
			BeginExit(presentation);

		foreach (var message in desired)
		{
			if (Presentations.Any(item => !item.IsExiting && ReferenceEquals(item.Message, message)))
				continue;
			var presentation = new ToastPresentation(message) { IsEntering = true };
			Presentations.Add(presentation);
			Dispatcher.UIThread.Post(() => presentation.IsEntering = false, DispatcherPriority.Render);
		}
	}

	private async void BeginExit(ToastPresentation presentation)
	{
		presentation.IsEntering = false;
		presentation.IsExiting = true;
		var duration = ResolveExitDuration();
		if (duration > TimeSpan.Zero)
			await Task.Delay(duration);
		if (Dispatcher.UIThread.CheckAccess())
			Presentations.Remove(presentation);
		else
			await Dispatcher.UIThread.InvokeAsync(() => Presentations.Remove(presentation));
	}

	private TimeSpan ResolveExitDuration()
		=> Application.Current?.TryGetResource(
			"Libation.Motion.EffectiveDuration.Fast",
			ActualThemeVariant,
			out var duration) == true
			&& duration is TimeSpan value
				? value
				: TimeSpan.Zero;
}

public sealed class ToastPresentation(ToastMessage message) : INotifyPropertyChanged
{
	private bool isEntering;
	private bool isExiting;

	public ToastMessage Message { get; } = message;
	public bool IsEntering
	{
		get => isEntering;
		set
		{
			if (isEntering == value)
				return;
			isEntering = value;
			PropertyChanged?.Invoke(this, new(nameof(IsEntering)));
		}
	}
	public bool IsExiting
	{
		get => isExiting;
		set
		{
			if (isExiting == value)
				return;
			isExiting = value;
			PropertyChanged?.Invoke(this, new(nameof(IsExiting)));
		}
	}
	public event PropertyChangedEventHandler? PropertyChanged;
}
